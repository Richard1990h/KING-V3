// User Profile Controller - Profile, Theme, Avatar management
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services;
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<UserController> _logger;

    public UserController(IAuthService authService, ILogger<UserController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpGet("profile")]
    public async Task<ActionResult> GetProfile()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var profile = await _authService.GetUserProfileAsync(userId);
        if (profile == null)
            return NotFound();

        return Ok(profile);
    }

    [HttpPut("profile")]
    public async Task<ActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.UpdateProfileAsync(userId, request);
        return Ok(new { message = "Profile updated" });
    }

    [HttpPost("avatar")]
    public async Task<ActionResult> UploadAvatar(IFormFile file)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (file == null || file.Length == 0)
            return BadRequest(new { detail = "No file uploaded" });

        if (file.Length > 5 * 1024 * 1024) // 5MB limit
            return BadRequest(new { detail = "File too large. Maximum 5MB allowed." });

        if (!file.ContentType.StartsWith("image/"))
            return BadRequest(new { detail = "Only image files are allowed" });

        var avatarUrl = await _authService.UploadAvatarAsync(userId, file);
        return Ok(new { avatar_url = avatarUrl });
    }

    [HttpGet("theme")]
    public async Task<ActionResult> GetTheme()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var theme = await _authService.GetUserThemeAsync(userId);
        return Ok(theme);
    }

    [HttpPut("theme")]
    public async Task<ActionResult> UpdateTheme([FromBody] UserTheme theme)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.UpdateUserThemeAsync(userId, theme);
        return Ok(new { message = "Theme updated" });
    }

    [HttpPost("change-password")]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            await _authService.ChangePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
            return Ok(new { message = "Password changed successfully" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpGet("api-keys")]
    public async Task<ActionResult> GetApiKeys()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var providers = await _authService.GetUserAIProvidersAsync(userId);
        return Ok(providers);
    }

    [HttpPost("api-keys")]
    public async Task<ActionResult> AddApiKey([FromBody] AddApiKeyRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check if user's plan allows custom API keys
        var canAddKeys = await _authService.CanUserAddApiKeysAsync(userId);
        if (!canAddKeys)
            return StatusCode(403, new { detail = "Your plan does not allow custom API keys. Please upgrade to Pro or higher." });

        await _authService.AddUserAIProviderAsync(userId, request);
        return Ok(new { message = "API key added" });
    }

    [HttpDelete("api-keys/{provider}")]
    public async Task<ActionResult> DeleteApiKey(string provider)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.DeleteUserAIProviderAsync(userId, provider);
        return Ok(new { message = "API key deleted" });
    }

    // ==================== GOOGLE DRIVE ====================
    
    [HttpGet("google-drive")]
    public async Task<ActionResult> GetGoogleDriveConfig()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var config = await _authService.GetUserGoogleDriveConfigAsync(userId);
        return Ok(config ?? new { is_connected = false });
    }

    [HttpPut("google-drive")]
    public async Task<ActionResult> SaveGoogleDriveConfig([FromBody] UserGoogleDriveConfigRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.SaveUserGoogleDriveConfigAsync(userId, request.IsConnected, request.Email, request.AccessToken, request.RefreshToken);
        return Ok(new { message = "Google Drive configuration saved" });
    }

    // ==================== ADMIN VISIBILITY ====================
    
    [HttpGet("visibility")]
    public async Task<ActionResult> GetVisibilityStatus()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var status = await _authService.GetUserVisibilityAsync(userId);
        return Ok(status);
    }

    [HttpPut("visibility")]
    public async Task<ActionResult> UpdateVisibilityStatus([FromBody] VisibilityRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.UpdateUserVisibilityAsync(userId, request.AppearOffline);
        return Ok(new { message = "Visibility updated", appear_offline = request.AppearOffline });
    }
}

// Request models
public record UpdateProfileRequest(
    string? Name,
    string? DisplayName,
    string? AvatarUrl
);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword
);

public record AddApiKeyRequest(
    string Provider,
    string ApiKey,
    string? ModelPreference,
    bool IsDefault = false
);

public record UserGoogleDriveConfigRequest(
    bool IsConnected,
    string? Email,
    string? AccessToken,
    string? RefreshToken
);
