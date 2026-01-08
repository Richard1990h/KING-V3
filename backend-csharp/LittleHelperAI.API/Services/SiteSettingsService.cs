// Site Settings Service with Caching
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Services;

public interface ISiteSettingsService
{
    Task<SiteSettings> GetSettingsAsync();
    Task<PublicSiteSettings> GetPublicSettingsAsync();
    Task<SiteSettings> UpdateSettingsAsync(SiteSettingsRequest request, string updatedBy);
    Task InvalidateCacheAsync();
}

public class SiteSettingsService : ISiteSettingsService
{
    private readonly IDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<SiteSettingsService> _logger;
    
    private const string CACHE_KEY = "site_settings";
    private const string PUBLIC_CACHE_KEY = "site_settings_public";
    private static readonly TimeSpan CACHE_DURATION = TimeSpan.FromMinutes(5);

    public SiteSettingsService(IDbContext db, ICacheService cache, ILogger<SiteSettingsService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<SiteSettings> GetSettingsAsync()
    {
        // Try cache first
        var cached = await _cache.GetAsync<SiteSettings>(CACHE_KEY);
        if (cached != null)
        {
            return cached;
        }

        // Load from database
        var settings = await _db.QueryFirstOrDefaultAsync<SiteSettings>(@"
            SELECT id, announcement_enabled, announcement_message, announcement_type, 
                   maintenance_mode, admins_auto_friend, updated_at, updated_by
            FROM site_settings WHERE id = 'default'");

        if (settings == null)
        {
            // Create default settings
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

            await _db.ExecuteAsync(@"
                INSERT INTO site_settings (id, announcement_enabled, announcement_message, 
                    announcement_type, maintenance_mode, admins_auto_friend, updated_at)
                VALUES (@Id, @AnnouncementEnabled, @AnnouncementMessage, 
                    @AnnouncementType, @MaintenanceMode, @AdminsAutoFriend, @UpdatedAt)",
                settings);
        }

        // Cache the result
        await _cache.SetAsync(CACHE_KEY, settings, CACHE_DURATION);
        
        return settings;
    }

    public async Task<PublicSiteSettings> GetPublicSettingsAsync()
    {
        // Try cache first
        var cached = await _cache.GetAsync<PublicSiteSettings>(PUBLIC_CACHE_KEY);
        if (cached != null)
        {
            return cached;
        }

        // Load from database
        var settings = await _db.QueryFirstOrDefaultAsync<SiteSettings>(@"
            SELECT announcement_enabled, announcement_message, announcement_type, maintenance_mode
            FROM site_settings WHERE id = 'default'");

        var publicSettings = new PublicSiteSettings
        {
            AnnouncementEnabled = settings?.AnnouncementEnabled ?? false,
            AnnouncementMessage = settings?.AnnouncementMessage,
            AnnouncementType = settings?.AnnouncementType ?? "info",
            MaintenanceMode = settings?.MaintenanceMode ?? false
        };

        // Cache with shorter duration for public settings (more frequently accessed)
        await _cache.SetAsync(PUBLIC_CACHE_KEY, publicSettings, TimeSpan.FromMinutes(1));
        
        return publicSettings;
    }

    public async Task<SiteSettings> UpdateSettingsAsync(SiteSettingsRequest request, string updatedBy)
    {
        // Check if settings exist
        var exists = await _db.ExecuteScalarAsync<bool>(
            "SELECT COUNT(1) > 0 FROM site_settings WHERE id = 'default'");

        if (!exists)
        {
            await _db.ExecuteAsync(@"
                INSERT INTO site_settings (id, announcement_enabled, announcement_message, 
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
                    UpdatedBy = updatedBy
                });
        }
        else
        {
            // Build dynamic update
            var updates = new List<string>();
            var parameters = new Dapper.DynamicParameters();
            parameters.Add("UpdatedAt", DateTime.UtcNow);
            parameters.Add("UpdatedBy", updatedBy);

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
            await _db.ExecuteAsync(sql, parameters);
        }

        // Invalidate cache
        await InvalidateCacheAsync();

        // Return updated settings
        return await GetSettingsAsync();
    }

    public async Task InvalidateCacheAsync()
    {
        await _cache.RemoveAsync(CACHE_KEY);
        await _cache.RemoveAsync(PUBLIC_CACHE_KEY);
        _logger.LogInformation("Site settings cache invalidated");
    }
}
