// Notifications Controller - WebSocket endpoint for real-time notifications
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net.WebSockets;
using LittleHelperAI.API.Services;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/notifications")]
public class NotificationsController : ControllerBase
{
    private readonly NotificationService _notificationService;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(
        NotificationService notificationService,
        ILogger<NotificationsController> logger)
    {
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// WebSocket endpoint for real-time notifications
    /// Connect with: ws://host/api/notifications/ws?userId={userId}
    /// </summary>
    [HttpGet("ws")]
    public async Task WebSocketEndpoint([FromQuery] string userId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
            return;
        }

        if (string.IsNullOrEmpty(userId))
        {
            HttpContext.Response.StatusCode = 400;
            await HttpContext.Response.WriteAsync("userId is required");
            return;
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await _notificationService.HandleWebSocketAsync(webSocket, userId);
    }

    /// <summary>
    /// Get online status of a user
    /// </summary>
    [HttpGet("online/{userId}")]
    [Authorize]
    public ActionResult IsUserOnline(string userId)
    {
        var isOnline = _notificationService.IsUserOnline(userId);
        return Ok(new { userId, isOnline });
    }

    /// <summary>
    /// Get count of online users (admin only)
    /// </summary>
    [HttpGet("online-count")]
    [Authorize(Roles = "admin")]
    public ActionResult GetOnlineCount()
    {
        var count = _notificationService.GetOnlineUserCount();
        var userIds = _notificationService.GetOnlineUserIds().ToList();
        return Ok(new { count, userIds });
    }

    /// <summary>
    /// Send a system announcement to all connected users (admin only)
    /// </summary>
    [HttpPost("broadcast")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> BroadcastAnnouncement([FromBody] BroadcastRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { detail = "Message is required" });

        await _notificationService.BroadcastAnnouncement(request.Message, request.Type ?? "info");
        
        _logger.LogInformation("Admin broadcast announcement: {Message}", request.Message);
        
        return Ok(new { message = "Announcement broadcast to all connected users" });
    }

    public record BroadcastRequest(string Message, string? Type);
}
