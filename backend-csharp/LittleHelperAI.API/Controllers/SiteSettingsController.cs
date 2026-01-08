// Site Settings Controller - Admin-configurable site-wide settings
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using Dapper;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController : ControllerBase
{
    private readonly IDbContext _db;
    private readonly ILogger<SiteSettingsController> _logger;

    public SiteSettingsController(IDbContext db, ILogger<SiteSettingsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get all site settings (admin only)
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<SiteSettings>> GetSiteSettings()
    {
        try
        {
            using var conn = _db.CreateConnection();
            
            var settings = await conn.QueryFirstOrDefaultAsync<SiteSettings>(
                @"SELECT id, announcement_enabled, announcement_message, announcement_type, 
                         maintenance_mode, admins_auto_friend, updated_at, updated_by
                  FROM site_settings WHERE id = 'default'");

            if (settings == null)
            {
                // Create default settings if not exists
                settings = new SiteSettings
                {
                    Id = "default",
                    AnnouncementEnabled = false,
                    AnnouncementMessage = null,
                    AnnouncementType = "info",
                    MaintenanceMode = false,
                    AdminsAutoFriend = true,
                    UpdatedAt = DateTime.UtcNow
                };

                await conn.ExecuteAsync(
                    @"INSERT INTO site_settings (id, announcement_enabled, announcement_message, 
                        announcement_type, maintenance_mode, admins_auto_friend, updated_at)
                      VALUES (@Id, @AnnouncementEnabled, @AnnouncementMessage, 
                        @AnnouncementType, @MaintenanceMode, @AdminsAutoFriend, @UpdatedAt)",
                    settings);
            }

            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get site settings");
            return StatusCode(500, new { detail = "Failed to load site settings" });
        }
    }

    /// <summary>
    /// Get public site settings (announcement for login page, etc.)
    /// </summary>
    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<ActionResult<PublicSiteSettings>> GetPublicSiteSettings()
    {
        try
        {
            using var conn = _db.CreateConnection();
            
            var settings = await conn.QueryFirstOrDefaultAsync<SiteSettings>(
                @"SELECT announcement_enabled, announcement_message, announcement_type, maintenance_mode
                  FROM site_settings WHERE id = 'default'");

            var publicSettings = new PublicSiteSettings
            {
                AnnouncementEnabled = settings?.AnnouncementEnabled ?? false,
                AnnouncementMessage = settings?.AnnouncementMessage,
                AnnouncementType = settings?.AnnouncementType ?? "info",
                MaintenanceMode = settings?.MaintenanceMode ?? false
            };

            return Ok(publicSettings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get public site settings");
            // Return empty settings on error - don't break login page
            return Ok(new PublicSiteSettings());
        }
    }

    /// <summary>
    /// Update site settings (admin only)
    /// </summary>
    [HttpPut]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<SiteSettings>> UpdateSiteSettings([FromBody] SiteSettingsRequest request)
    {
        try
        {
            var userId = User.FindFirst("user_id")?.Value;
            using var conn = _db.CreateConnection();

            // Check if settings exist
            var exists = await conn.ExecuteScalarAsync<bool>(
                "SELECT COUNT(1) > 0 FROM site_settings WHERE id = 'default'");

            if (!exists)
            {
                // Create new settings
                await conn.ExecuteAsync(
                    @"INSERT INTO site_settings (id, announcement_enabled, announcement_message, 
                        announcement_type, maintenance_mode, admins_auto_friend, updated_at, updated_by)
                      VALUES ('default', @AnnouncementEnabled, @AnnouncementMessage, 
                        @AnnouncementType, @MaintenanceMode, @AdminsAutoFriend, @UpdatedAt, @UpdatedBy)",
                    new
                    {
                        AnnouncementEnabled = request.AnnouncementEnabled ?? false,
                        AnnouncementMessage = request.AnnouncementMessage,
                        AnnouncementType = request.AnnouncementType ?? "info",
                        MaintenanceMode = request.MaintenanceMode ?? false,
                        AdminsAutoFriend = request.AdminsAutoFriend ?? true,
                        UpdatedAt = DateTime.UtcNow,
                        UpdatedBy = userId
                    });
            }
            else
            {
                // Update existing settings
                var updates = new List<string>();
                var parameters = new DynamicParameters();
                parameters.Add("UpdatedAt", DateTime.UtcNow);
                parameters.Add("UpdatedBy", userId);

                if (request.AnnouncementEnabled.HasValue)
                {
                    updates.Add("announcement_enabled = @AnnouncementEnabled");
                    parameters.Add("AnnouncementEnabled", request.AnnouncementEnabled.Value);
                }
                if (request.AnnouncementMessage != null)
                {
                    updates.Add("announcement_message = @AnnouncementMessage");
                    parameters.Add("AnnouncementMessage", request.AnnouncementMessage);
                }
                if (request.AnnouncementType != null)
                {
                    updates.Add("announcement_type = @AnnouncementType");
                    parameters.Add("AnnouncementType", request.AnnouncementType);
                }
                if (request.MaintenanceMode.HasValue)
                {
                    updates.Add("maintenance_mode = @MaintenanceMode");
                    parameters.Add("MaintenanceMode", request.MaintenanceMode.Value);
                }
                if (request.AdminsAutoFriend.HasValue)
                {
                    updates.Add("admins_auto_friend = @AdminsAutoFriend");
                    parameters.Add("AdminsAutoFriend", request.AdminsAutoFriend.Value);
                }

                updates.Add("updated_at = @UpdatedAt");
                updates.Add("updated_by = @UpdatedBy");

                var sql = $"UPDATE site_settings SET {string.Join(", ", updates)} WHERE id = 'default'";
                await conn.ExecuteAsync(sql, parameters);
            }

            // If admins_auto_friend was enabled, trigger the auto-friend process
            if (request.AdminsAutoFriend == true)
            {
                await AutoFriendAllAdmins(conn);
            }

            // Return updated settings
            var settings = await conn.QueryFirstOrDefaultAsync<SiteSettings>(
                @"SELECT id, announcement_enabled, announcement_message, announcement_type, 
                         maintenance_mode, admins_auto_friend, updated_at, updated_by
                  FROM site_settings WHERE id = 'default'");

            _logger.LogInformation("Site settings updated by user {UserId}", userId);
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update site settings");
            return StatusCode(500, new { detail = "Failed to save site settings" });
        }
    }

    /// <summary>
    /// Auto-friend all admins to all users
    /// </summary>
    private async Task AutoFriendAllAdmins(System.Data.IDbConnection conn)
    {
        try
        {
            // Get all admin user IDs
            var adminIds = await conn.QueryAsync<string>(
                "SELECT id FROM users WHERE role = 'admin'");

            // Get all non-admin user IDs
            var userIds = await conn.QueryAsync<string>(
                "SELECT id FROM users WHERE role != 'admin'");

            foreach (var adminId in adminIds)
            {
                foreach (var userId in userIds)
                {
                    // Check if friendship already exists
                    var exists = await conn.ExecuteScalarAsync<bool>(
                        @"SELECT COUNT(1) > 0 FROM friends 
                          WHERE (user_id = @AdminId AND friend_user_id = @UserId)
                             OR (user_id = @UserId AND friend_user_id = @AdminId)",
                        new { AdminId = adminId, UserId = userId });

                    if (!exists)
                    {
                        // Create bidirectional friendship
                        var friendshipId1 = Guid.NewGuid().ToString();
                        var friendshipId2 = Guid.NewGuid().ToString();
                        var now = DateTime.UtcNow;

                        await conn.ExecuteAsync(
                            @"INSERT INTO friends (id, user_id, friend_user_id, created_at)
                              VALUES (@Id1, @AdminId, @UserId, @Now),
                                     (@Id2, @UserId, @AdminId, @Now)",
                            new { Id1 = friendshipId1, Id2 = friendshipId2, AdminId = adminId, UserId = userId, Now = now });

                        _logger.LogInformation("Auto-friended admin {AdminId} with user {UserId}", adminId, userId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to auto-friend admins");
        }
    }

    /// <summary>
    /// Manually trigger admin auto-friend (admin only)
    /// </summary>
    [HttpPost("auto-friend-admins")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> TriggerAutoFriendAdmins()
    {
        try
        {
            using var conn = _db.CreateConnection();
            await AutoFriendAllAdmins(conn);
            return Ok(new { message = "All admins have been added as friends to all users" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger auto-friend admins");
            return StatusCode(500, new { detail = "Failed to auto-friend admins" });
        }
    }
}
