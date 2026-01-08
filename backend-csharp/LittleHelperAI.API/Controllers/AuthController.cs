// Authentication Controller - Updated with TOS, Theme, Avatar support
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services;
using LittleHelperAI.API.Models;
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<ActionResult<TokenResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            // Validate TOS acceptance
            if (!request.TosAccepted)
            {
                return BadRequest(new { detail = "You must accept the Terms of Service to register" });
            }

            var clientIp = GetClientIp();
            var result = await _authService.RegisterAsync(request, clientIp);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var clientIp = GetClientIp();
            var userAgent = Request.Headers["User-Agent"].ToString();
            var result = await _authService.LoginAsync(request, clientIp, userAgent);
            return Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { detail = ex.Message });
        }
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var user = await _authService.GetUserByIdAsync(userId);
        if (user == null)
            return NotFound();

        return Ok(user);
    }

    [HttpGet("tos-status")]
    [Authorize]
    public async Task<ActionResult> GetTosStatus()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var status = await _authService.GetTosStatusAsync(userId);
        return Ok(status);
    }

    [HttpPost("accept-tos")]
    [Authorize]
    public async Task<ActionResult> AcceptTos()
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.AcceptTosAsync(userId);
        return Ok(new { message = "Terms of Service accepted", tos_accepted = true });
    }

    [HttpPut("language")]
    [Authorize]
    public async Task<ActionResult> UpdateLanguage([FromBody] UpdateLanguageRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        await _authService.UpdateLanguageAsync(userId, request.Language);
        return Ok(new { message = "Language updated", language = request.Language });
    }

    private string GetClientIp()
    {
        var forwardedFor = Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

// Request/Response Models
public record RegisterRequest(
    string Email, 
    string Password, 
    string Name, 
    bool TosAccepted = false
);

public record LoginRequest(string Email, string Password);

public record UpdateLanguageRequest(string Language);

public record TokenResponse(string Token, UserResponse User);

public record UserResponse(
    string Id, 
    string Email, 
    string Name,
    string? DisplayName,
    string Role, 
    double Credits, 
    bool CreditsEnabled, 
    string Plan, 
    string CreatedAt, 
    string Language,
    string? AvatarUrl,
    bool TosAccepted
);

public record TosStatusResponse(
    bool TosAccepted,
    string? TosAcceptedAt,
    string? TosVersion,
    string CurrentVersion
);
