// Site Settings Controller - Admin-configurable site-wide settings
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using LittleHelperAI.API.Services;
using Dapper;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/site-settings")]
public class SiteSettingsController : ControllerBase
{
    private readonly ISiteSettingsService _siteSettingsService;
    private readonly IDbContext _db;
    private readonly ILogger<SiteSettingsController> _logger;
    private readonly NotificationService _notificationService;

    public SiteSettingsController(
        ISiteSettingsService siteSettingsService,
        IDbContext db, 
        ILogger<SiteSettingsController> logger,
        NotificationService notificationService)
    {
        _siteSettingsService = siteSettingsService;
        _db = db;
        _logger = logger;
        _notificationService = notificationService;
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
            var settings = await _siteSettingsService.GetSettingsAsync();
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
            var settings = await _siteSettingsService.GetPublicSettingsAsync();
            return Ok(settings);
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
            
            var settings = await _siteSettingsService.UpdateSettingsAsync(request, userId!);

            // If admins_auto_friend was enabled, trigger the auto-friend process
            if (request.AdminsAutoFriend == true)
            {
                using var conn = _db.CreateConnection();
                conn.Open();
                await AutoFriendAllAdmins(conn);
            }

            // If announcement changed, broadcast to all connected users
            if (request.AnnouncementEnabled == true && !string.IsNullOrEmpty(request.AnnouncementMessage))
            {
                await _notificationService.BroadcastAnnouncement(
                    request.AnnouncementMessage,
                    request.AnnouncementType ?? "info"
                );
            }

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
            conn.Open();
            await AutoFriendAllAdmins(conn);
            return Ok(new { message = "All admins have been added as friends to all users" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger auto-friend admins");
            return StatusCode(500, new { detail = "Failed to auto-friend admins" });
        }
    }

    /// <summary>
    /// Invalidate site settings cache (admin only)
    /// </summary>
    [HttpPost("invalidate-cache")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> InvalidateCache()
    {
        await _siteSettingsService.InvalidateCacheAsync();
        return Ok(new { message = "Site settings cache invalidated" });
    }
}
