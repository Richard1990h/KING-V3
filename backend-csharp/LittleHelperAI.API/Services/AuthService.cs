// Authentication Service Implementation
using LittleHelperAI.API.Controllers;
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace LittleHelperAI.API.Services;

public class AuthService : IAuthService
{
    private readonly IDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IDbContext db, IConfiguration config, ILogger<AuthService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<TokenResponse> RegisterAsync(RegisterRequest request, string clientIp)
    {
        // Check if email exists
        var existing = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email", 
            new { request.Email });
        
        if (existing != null)
            throw new InvalidOperationException("Email already registered");

        var userId = Guid.NewGuid().ToString();
        var passwordHash = HashPassword(request.Password);
        var now = DateTime.UtcNow;

        await _db.ExecuteAsync(@"
            INSERT INTO users (id, email, name, password_hash, registration_ip, tos_accepted, tos_accepted_at, tos_version, created_at)
            VALUES (@Id, @Email, @Name, @PasswordHash, @Ip, @TosAccepted, @TosAcceptedAt, @TosVersion, @CreatedAt)",
            new { 
                Id = userId, 
                request.Email, 
                request.Name, 
                PasswordHash = passwordHash,
                Ip = clientIp,
                TosAccepted = request.TosAccepted,
                TosAcceptedAt = request.TosAccepted ? now : (DateTime?)null,
                TosVersion = request.TosAccepted ? "1.0" : null,
                CreatedAt = now
            });

        await RecordIpAsync(userId, clientIp, "register", null);

        // Auto-friend admins if enabled in site settings
        await AutoFriendAdminsForNewUser(userId);

        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id", 
            new { Id = userId });
        
        return new TokenResponse(
            GenerateToken(user!),
            MapToUserResponse(user!)
        );
    }

    private async Task AutoFriendAdminsForNewUser(string newUserId)
    {
        try
        {
            // Check if admins_auto_friend is enabled
            var settings = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT admins_auto_friend FROM site_settings WHERE id = 'default'");
            
            if (settings == null || settings.admins_auto_friend != true)
                return;

            // Get all admin IDs
            var adminIds = await _db.QueryAsync<string>(
                "SELECT id FROM users WHERE role = 'admin'");

            foreach (var adminId in adminIds)
            {
                // Create bidirectional friendship
                var friendshipId1 = Guid.NewGuid().ToString();
                var friendshipId2 = Guid.NewGuid().ToString();
                var now = DateTime.UtcNow;

                await _db.ExecuteAsync(
                    @"INSERT IGNORE INTO friends (id, user_id, friend_user_id, created_at)
                      VALUES (@Id1, @AdminId, @UserId, @Now),
                             (@Id2, @UserId, @AdminId, @Now)",
                    new { Id1 = friendshipId1, Id2 = friendshipId2, AdminId = adminId, UserId = newUserId, Now = now });

                _logger.LogInformation("Auto-friended admin {AdminId} with new user {UserId}", adminId, newUserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to auto-friend admins for new user {UserId}", newUserId);
            // Don't throw - this shouldn't block registration
        }
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, string clientIp, string? userAgent = null)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE email = @Email",
            new { request.Email });

        if (user == null || !VerifyPassword(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        // Check maintenance mode (only block non-admins)
        if (user.Role != "admin")
        {
            var settings = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT maintenance_mode FROM site_settings WHERE id = 'default'");
            
            if (settings?.maintenance_mode == true)
                throw new UnauthorizedAccessException("System is currently under maintenance. Please try again later.");
        }

        // Update last login
        await _db.ExecuteAsync(
            "UPDATE users SET last_login_at = @Now, last_login_ip = @Ip WHERE id = @Id",
            new { Now = DateTime.UtcNow, Ip = clientIp, user.Id });

        await RecordIpAsync(user.Id, clientIp, "login", userAgent);

        return new TokenResponse(
            GenerateToken(user),
            MapToUserResponse(user)
        );
    }

    public async Task<UserResponse?> GetUserByIdAsync(string userId)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id",
            new { Id = userId });
        return user != null ? MapToUserResponse(user) : null;
    }

    public async Task<object?> GetUserProfileAsync(string userId)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id",
            new { Id = userId });
        
        if (user == null) return null;

        var theme = await GetUserThemeAsync(userId);
        
        return new {
            user.Id,
            user.Email,
            user.Name,
            user.DisplayName,
            user.Role,
            user.Credits,
            user.CreditsEnabled,
            user.Plan,
            user.Language,
            user.AvatarUrl,
            user.TosAccepted,
            user.CreatedAt,
            Theme = theme
        };
    }

    public async Task UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var updates = new List<string>();
        var parameters = new Dictionary<string, object> { ["Id"] = userId };

        if (request.Name != null) { updates.Add("name = @Name"); parameters["Name"] = request.Name; }
        if (request.DisplayName != null) { updates.Add("display_name = @DisplayName"); parameters["DisplayName"] = request.DisplayName; }
        if (request.AvatarUrl != null) { updates.Add("avatar_url = @AvatarUrl"); parameters["AvatarUrl"] = request.AvatarUrl; }

        if (updates.Count > 0)
        {
            var sql = $"UPDATE users SET {string.Join(", ", updates)} WHERE id = @Id";
            await _db.ExecuteAsync(sql, parameters);
        }
    }

    public async Task<string> UploadAvatarAsync(string userId, IFormFile file)
    {
        // In production, upload to cloud storage. For now, store as base64
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        var avatarUrl = $"data:{file.ContentType};base64,{base64}";
        
        await _db.ExecuteAsync(
            "UPDATE users SET avatar_url = @AvatarUrl WHERE id = @Id",
            new { AvatarUrl = avatarUrl, Id = userId });
        
        return avatarUrl;
    }

    public async Task<UserTheme?> GetUserThemeAsync(string userId)
    {
        return await _db.QueryFirstOrDefaultAsync<UserTheme>(
            "SELECT * FROM user_themes WHERE user_id = @UserId",
            new { UserId = userId });
    }

    public async Task UpdateUserThemeAsync(string userId, UserTheme theme)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO user_themes (user_id, primary_color, secondary_color, background_color, card_color, text_color, hover_color, credits_color, background_image)
            VALUES (@UserId, @PrimaryColor, @SecondaryColor, @BackgroundColor, @CardColor, @TextColor, @HoverColor, @CreditsColor, @BackgroundImage)
            ON DUPLICATE KEY UPDATE
            primary_color = @PrimaryColor, secondary_color = @SecondaryColor, background_color = @BackgroundColor,
            card_color = @CardColor, text_color = @TextColor, hover_color = @HoverColor, credits_color = @CreditsColor, background_image = @BackgroundImage",
            new { 
                UserId = userId,
                theme.PrimaryColor,
                theme.SecondaryColor,
                theme.BackgroundColor,
                theme.CardColor,
                theme.TextColor,
                theme.HoverColor,
                theme.CreditsColor,
                theme.BackgroundImage
            });
    }

    public async Task ChangePasswordAsync(string userId, string currentPassword, string newPassword)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT * FROM users WHERE id = @Id",
            new { Id = userId });
        
        if (user == null || !VerifyPassword(currentPassword, user.PasswordHash))
            throw new UnauthorizedAccessException("Current password is incorrect");

        await _db.ExecuteAsync(
            "UPDATE users SET password_hash = @PasswordHash WHERE id = @Id",
            new { PasswordHash = HashPassword(newPassword), Id = userId });
    }

    public async Task<List<UserAIProvider>> GetUserAIProvidersAsync(string userId)
    {
        var providers = await _db.QueryAsync<UserAIProvider>(
            "SELECT * FROM user_ai_providers WHERE user_id = @UserId",
            new { UserId = userId });
        return providers.ToList();
    }

    public async Task<bool> CanUserAddApiKeysAsync(string userId)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT plan FROM users WHERE id = @Id",
            new { Id = userId });
        return user?.Plan is "pro" or "enterprise";
    }

    public async Task AddUserAIProviderAsync(string userId, AddApiKeyRequest request)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO user_ai_providers (id, user_id, provider, api_key, model_preference, is_default, created_at, updated_at)
            VALUES (@Id, @UserId, @Provider, @ApiKey, @ModelPreference, @IsDefault, @Now, @Now)
            ON DUPLICATE KEY UPDATE api_key = @ApiKey, model_preference = @ModelPreference, is_default = @IsDefault, updated_at = @Now",
            new {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                request.Provider,
                request.ApiKey,
                request.ModelPreference,
                request.IsDefault,
                Now = DateTime.UtcNow
            });
    }

    public async Task DeleteUserAIProviderAsync(string userId, string provider)
    {
        await _db.ExecuteAsync(
            "DELETE FROM user_ai_providers WHERE user_id = @UserId AND provider = @Provider",
            new { UserId = userId, Provider = provider });
    }

    public async Task<object> GetTosStatusAsync(string userId)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT tos_accepted, tos_accepted_at, tos_version FROM users WHERE id = @Id",
            new { Id = userId });
        
        return new TosStatusResponse(
            user?.TosAccepted ?? false,
            user?.TosAcceptedAt?.ToString("o"),
            user?.TosVersion,
            "1.0"
        );
    }

    public async Task AcceptTosAsync(string userId)
    {
        await _db.ExecuteAsync(@"
            UPDATE users SET tos_accepted = TRUE, tos_accepted_at = @Now, tos_version = @Version WHERE id = @Id",
            new { Now = DateTime.UtcNow, Version = "1.0", Id = userId });
    }

    public async Task UpdateLanguageAsync(string userId, string language)
    {
        await _db.ExecuteAsync(
            "UPDATE users SET language = @Language WHERE id = @Id",
            new { Language = language, Id = userId });
    }

    public async Task<List<object>> GetAllUsersAsync()
    {
        var users = await _db.QueryAsync<User>("SELECT * FROM users ORDER BY created_at DESC");
        return users.Select(u => (object)MapToUserResponse(u)).ToList();
    }

    public async Task<bool> UpdateUserAsync(string userId, AdminUpdateUserRequest request)
    {
        var updates = new List<string>();
        var parameters = new Dictionary<string, object> { ["Id"] = userId };

        if (request.Role != null) { updates.Add("role = @Role"); parameters["Role"] = request.Role; }
        if (request.Credits.HasValue) { updates.Add("credits = @Credits"); parameters["Credits"] = request.Credits.Value; }
        if (request.CreditsEnabled.HasValue) { updates.Add("credits_enabled = @CreditsEnabled"); parameters["CreditsEnabled"] = request.CreditsEnabled.Value; }
        if (request.Plan != null) { updates.Add("plan = @Plan"); parameters["Plan"] = request.Plan; }

        if (updates.Count == 0) return true;

        var result = await _db.ExecuteAsync(
            $"UPDATE users SET {string.Join(", ", updates)} WHERE id = @Id",
            parameters);
        return result > 0;
    }

    public async Task<bool> DeleteUserAsync(string userId)
    {
        var result = await _db.ExecuteAsync(
            "DELETE FROM users WHERE id = @Id",
            new { Id = userId });
        return result > 0;
    }

    public async Task<object> GetSystemStatsAsync()
    {
        var totalUsers = await _db.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM users");
        var totalProjects = await _db.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM projects");
        var totalJobs = await _db.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM jobs");
        var activeJobs = await _db.QueryFirstOrDefaultAsync<int>("SELECT COUNT(*) FROM jobs WHERE status IN ('in_progress', 'analyzing')");

        return new {
            total_users = totalUsers,
            total_projects = totalProjects,
            total_jobs = totalJobs,
            active_jobs = activeJobs,
            timestamp = DateTime.UtcNow
        };
    }

    public async Task<DefaultSettings?> GetDefaultSettingsAsync()
    {
        return await _db.QueryFirstOrDefaultAsync<DefaultSettings>(
            "SELECT * FROM default_settings WHERE setting_key = 'new_user_defaults'");
    }

    public async Task UpdateDefaultSettingsAsync(UpdateDefaultsRequest request)
    {
        var updates = new List<string>();
        var parameters = new Dictionary<string, object>();

        if (request.FreeCredits.HasValue) { updates.Add("free_credits = @FreeCredits"); parameters["FreeCredits"] = request.FreeCredits.Value; }
        if (request.Language != null) { updates.Add("language = @Language"); parameters["Language"] = request.Language; }
        if (request.Theme != null) { updates.Add("theme_json = @ThemeJson"); parameters["ThemeJson"] = System.Text.Json.JsonSerializer.Serialize(request.Theme); }

        if (updates.Count > 0)
        {
            await _db.ExecuteAsync(
                $"UPDATE default_settings SET {string.Join(", ", updates)} WHERE setting_key = 'new_user_defaults'",
                parameters);
        }
    }

    public async Task<List<IpRecord>> GetIpRecordsAsync(int limit)
    {
        var records = await _db.QueryAsync<IpRecord>(
            "SELECT * FROM ip_records ORDER BY timestamp DESC LIMIT @Limit",
            new { Limit = limit });
        return records.ToList();
    }

    private async Task RecordIpAsync(string userId, string ip, string action, string? userAgent)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO ip_records (id, user_id, ip_address, action, user_agent, timestamp)
            VALUES (@Id, @UserId, @IpAddress, @Action, @UserAgent, @Timestamp)",
            new {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                IpAddress = ip,
                Action = action,
                UserAgent = userAgent,
                Timestamp = DateTime.UtcNow
            });
    }

    private string GenerateToken(User user)
    {
        var key = Encoding.ASCII.GetBytes(_config["JWT:Secret"] ?? "littlehelper-ai-secret-key-2024");
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim("user_id", user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            }),
            Expires = DateTime.UtcNow.AddDays(7),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "littlehelper-salt"));
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    // ==================== GOOGLE DRIVE USER CONFIG ====================
    
    public async Task<object?> GetUserGoogleDriveConfigAsync(string userId)
    {
        var config = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT is_connected, email, access_token, refresh_token, updated_at FROM user_google_drive_config WHERE user_id = @UserId",
            new { UserId = userId });
        
        if (config == null)
            return null;
        
        return new {
            is_connected = config.is_connected,
            email = config.email,
            // Don't expose full tokens in response, just indicate they exist
            has_access_token = !string.IsNullOrEmpty(config.access_token),
            has_refresh_token = !string.IsNullOrEmpty(config.refresh_token),
            updated_at = config.updated_at
        };
    }

    public async Task SaveUserGoogleDriveConfigAsync(string userId, bool isConnected, string? email, string? accessToken, string? refreshToken)
    {
        var now = DateTime.UtcNow;
        
        await _db.ExecuteAsync(@"
            INSERT INTO user_google_drive_config (id, user_id, is_connected, email, access_token, refresh_token, created_at, updated_at)
            VALUES (@Id, @UserId, @IsConnected, @Email, @AccessToken, @RefreshToken, @Now, @Now)
            ON DUPLICATE KEY UPDATE
            is_connected = @IsConnected,
            email = COALESCE(@Email, email),
            access_token = COALESCE(@AccessToken, access_token),
            refresh_token = COALESCE(@RefreshToken, refresh_token),
            updated_at = @Now",
            new {
                Id = Guid.NewGuid().ToString(),
                UserId = userId,
                IsConnected = isConnected,
                Email = email,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                Now = now
            });
        
        _logger.LogInformation("User {UserId} Google Drive config updated. Connected: {IsConnected}", userId, isConnected);
    }

    // ==================== USER VISIBILITY (ADMIN APPEAR OFFLINE) ====================
    
    public async Task<object> GetUserVisibilityAsync(string userId)
    {
        var user = await _db.QueryFirstOrDefaultAsync<User>(
            "SELECT appear_offline, last_login_at FROM users WHERE id = @Id",
            new { Id = userId });
        
        return new {
            appear_offline = user?.AppearOffline ?? false,
            last_login_at = user?.LastLoginAt?.ToString("o")
        };
    }

    public async Task UpdateUserVisibilityAsync(string userId, bool appearOffline)
    {
        await _db.ExecuteAsync(
            "UPDATE users SET appear_offline = @AppearOffline WHERE id = @Id",
            new { AppearOffline = appearOffline, Id = userId });
        
        _logger.LogInformation("User {UserId} visibility updated. Appear Offline: {AppearOffline}", userId, appearOffline);
    }

    private static UserResponse MapToUserResponse(User user) => new(
        user.Id,
        user.Email,
        user.Name,
        user.DisplayName,
        user.Role,
        (double)user.Credits,
        user.CreditsEnabled,
        user.Plan,
        user.CreatedAt.ToString("o"),
        user.Language,
        user.AvatarUrl,
        user.TosAccepted
    );
}
