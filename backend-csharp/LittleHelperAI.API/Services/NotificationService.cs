// Real-time Notification Service using WebSocket
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using LittleHelperAI.Data;

namespace LittleHelperAI.API.Services;

public class NotificationService
{
    // Track active WebSocket connections per user
    private readonly ConcurrentDictionary<string, ConcurrentBag<WebSocket>> _userConnections = new();
    private readonly ILogger<NotificationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public NotificationService(ILogger<NotificationService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    #region WebSocket Connection Management

    public async Task HandleWebSocketAsync(WebSocket socket, string userId)
    {
        // Add to user connections
        var connections = _userConnections.GetOrAdd(userId, _ => new ConcurrentBag<WebSocket>());
        connections.Add(socket);

        _logger.LogInformation("User {UserId} connected to notifications", userId);

        try
        {
            // Send initial notification counts
            await SendNotificationCounts(userId);

            // Keep connection alive and handle messages
            var buffer = new byte[1024];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                // Handle ping/pong for keep-alive
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    if (message == "ping")
                    {
                        await SendToSocket(socket, new { type = "pong" });
                    }
                    else if (message == "refresh")
                    {
                        await SendNotificationCounts(userId);
                    }
                }
            }
        }
        catch (WebSocketException ex)
        {
            _logger.LogDebug("WebSocket closed for user {UserId}: {Message}", userId, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Notification WebSocket error for user {UserId}", userId);
        }
        finally
        {
            // Remove from connections
            RemoveConnection(userId, socket);
            _logger.LogInformation("User {UserId} disconnected from notifications", userId);
        }
    }

    private void RemoveConnection(string userId, WebSocket socket)
    {
        if (_userConnections.TryGetValue(userId, out var connections))
        {
            // Create new bag without this socket
            var newConnections = new ConcurrentBag<WebSocket>(
                connections.Where(s => s != socket && s.State == WebSocketState.Open)
            );
            _userConnections.TryUpdate(userId, newConnections, connections);
        }
    }

    #endregion

    #region Notification Sending

    /// <summary>
    /// Send a notification to a specific user
    /// </summary>
    public async Task SendToUserAsync(string userId, object notification)
    {
        if (!_userConnections.TryGetValue(userId, out var connections))
            return;

        var deadSockets = new List<WebSocket>();

        foreach (var socket in connections)
        {
            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await SendToSocket(socket, notification);
                }
                catch
                {
                    deadSockets.Add(socket);
                }
            }
            else
            {
                deadSockets.Add(socket);
            }
        }

        // Clean up dead sockets
        foreach (var dead in deadSockets)
        {
            RemoveConnection(userId, dead);
        }
    }

    /// <summary>
    /// Send to multiple users
    /// </summary>
    public async Task SendToUsersAsync(IEnumerable<string> userIds, object notification)
    {
        var tasks = userIds.Select(userId => SendToUserAsync(userId, notification));
        await Task.WhenAll(tasks);
    }

    private async Task SendToSocket(WebSocket socket, object message)
    {
        if (socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        await socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    #endregion

    #region Notification Types

    /// <summary>
    /// Send new DM notification
    /// </summary>
    public async Task NotifyNewDirectMessage(string receiverId, string senderId, string senderName, string messagePreview)
    {
        await SendToUserAsync(receiverId, new
        {
            type = "new_dm",
            senderId,
            senderName,
            preview = messagePreview.Length > 50 ? messagePreview[..50] + "..." : messagePreview,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send friend request notification
    /// </summary>
    public async Task NotifyFriendRequest(string receiverId, string senderId, string senderName)
    {
        await SendToUserAsync(receiverId, new
        {
            type = "friend_request",
            senderId,
            senderName,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send friend request accepted notification
    /// </summary>
    public async Task NotifyFriendRequestAccepted(string originalSenderId, string acceptedByUserId, string acceptedByName)
    {
        await SendToUserAsync(originalSenderId, new
        {
            type = "friend_accepted",
            userId = acceptedByUserId,
            userName = acceptedByName,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send project shared notification
    /// </summary>
    public async Task NotifyProjectShared(string userId, string ownerId, string ownerName, string projectId, string projectName)
    {
        await SendToUserAsync(userId, new
        {
            type = "project_shared",
            ownerId,
            ownerName,
            projectId,
            projectName,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send notification counts update
    /// </summary>
    public async Task SendNotificationCounts(string userId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbContext>();

            // Get unread DM count
            var unreadDMs = await db.QueryAsync<dynamic>(@"
                SELECT sender_id, COUNT(*) as count
                FROM direct_messages
                WHERE receiver_id = @UserId AND is_read = FALSE
                GROUP BY sender_id", new { UserId = userId });

            var unreadList = unreadDMs.ToList();
            var totalUnread = unreadList.Sum(u => (int)(long)u.count);

            // Get pending friend request count
            var pendingRequests = await db.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(*) FROM friend_requests 
                WHERE receiver_id = @UserId AND status = 'pending'", 
                new { UserId = userId });

            await SendToUserAsync(userId, new
            {
                type = "counts",
                unreadDMs = totalUnread,
                unreadByUser = unreadList.ToDictionary(
                    u => (string)u.sender_id, 
                    u => (int)(long)u.count
                ),
                pendingRequests
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification counts to user {UserId}", userId);
        }
    }

    /// <summary>
    /// Send system announcement to all connected users
    /// </summary>
    public async Task BroadcastAnnouncement(string message, string type = "info")
    {
        var notification = new
        {
            type = "announcement",
            message,
            announcementType = type,
            timestamp = DateTime.UtcNow
        };

        var tasks = _userConnections.Keys.Select(userId => SendToUserAsync(userId, notification));
        await Task.WhenAll(tasks);
    }

    #endregion

    #region Connection Status

    public bool IsUserOnline(string userId)
    {
        if (!_userConnections.TryGetValue(userId, out var connections))
            return false;
        return connections.Any(s => s.State == WebSocketState.Open);
    }

    public int GetOnlineUserCount()
    {
        return _userConnections.Count(kvp => kvp.Value.Any(s => s.State == WebSocketState.Open));
    }

    public IEnumerable<string> GetOnlineUserIds()
    {
        return _userConnections
            .Where(kvp => kvp.Value.Any(s => s.State == WebSocketState.Open))
            .Select(kvp => kvp.Key);
    }

    #endregion
}
