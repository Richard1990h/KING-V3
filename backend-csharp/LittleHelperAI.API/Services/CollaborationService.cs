// Real-time Collaboration Service using WebSocket
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace LittleHelperAI.API.Services;

public class CollaborationService
{
    // Track active connections per project
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, WebSocketConnection>> _projectConnections = new();
    private readonly ILogger<CollaborationService> _logger;

    public CollaborationService(ILogger<CollaborationService> logger)
    {
        _logger = logger;
    }

    public class WebSocketConnection
    {
        public WebSocket Socket { get; set; } = null!;
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserColor { get; set; } = "";
        public CursorPosition? Cursor { get; set; }
        public string? ActiveFile { get; set; }
    }

    public class CursorPosition
    {
        public int Line { get; set; }
        public int Column { get; set; }
        public int? SelectionStart { get; set; }
        public int? SelectionEnd { get; set; }
    }

    public class CollaborationMessage
    {
        public string Type { get; set; } = ""; // cursor, edit, join, leave, sync
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserColor { get; set; } = "";
        public string? FileId { get; set; }
        public string? FilePath { get; set; }
        public CursorPosition? Cursor { get; set; }
        public EditOperation? Edit { get; set; }
        public List<CollaboratorInfo>? Collaborators { get; set; }
    }

    public class EditOperation
    {
        public string Type { get; set; } = ""; // insert, delete, replace
        public int StartLine { get; set; }
        public int StartColumn { get; set; }
        public int? EndLine { get; set; }
        public int? EndColumn { get; set; }
        public string? Text { get; set; }
        public long Timestamp { get; set; }
    }

    public class CollaboratorInfo
    {
        public string UserId { get; set; } = "";
        public string UserName { get; set; } = "";
        public string UserColor { get; set; } = "";
        public string? ActiveFile { get; set; }
        public CursorPosition? Cursor { get; set; }
    }

    // Generate a random color for the user
    private static readonly string[] Colors = new[]
    {
        "#FF6B6B", "#4ECDC4", "#45B7D1", "#96CEB4", "#FFEAA7",
        "#DDA0DD", "#98D8C8", "#F7DC6F", "#BB8FCE", "#85C1E9"
    };

    private string GenerateUserColor()
    {
        return Colors[Random.Shared.Next(Colors.Length)];
    }

    public async Task HandleWebSocketAsync(WebSocket socket, string projectId, string userId, string userName)
    {
        var connection = new WebSocketConnection
        {
            Socket = socket,
            UserId = userId,
            UserName = userName,
            UserColor = GenerateUserColor()
        };

        // Add to project connections
        var projectConns = _projectConnections.GetOrAdd(projectId, _ => new ConcurrentDictionary<string, WebSocketConnection>());
        projectConns.TryAdd(userId, connection);

        _logger.LogInformation("User {UserId} joined project {ProjectId}", userId, projectId);

        try
        {
            // Notify others that user joined
            await BroadcastToProjectAsync(projectId, new CollaborationMessage
            {
                Type = "join",
                UserId = userId,
                UserName = userName,
                UserColor = connection.UserColor,
                Collaborators = GetCollaborators(projectId)
            }, excludeUserId: null);

            // Send current collaborators to the new user
            await SendToUserAsync(projectId, userId, new CollaborationMessage
            {
                Type = "sync",
                Collaborators = GetCollaborators(projectId)
            });

            // Handle incoming messages
            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    await HandleMessageAsync(projectId, userId, messageJson);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error for user {UserId} in project {ProjectId}", userId, projectId);
        }
        finally
        {
            // Remove from connections
            projectConns.TryRemove(userId, out _);
            
            // Notify others that user left
            await BroadcastToProjectAsync(projectId, new CollaborationMessage
            {
                Type = "leave",
                UserId = userId,
                UserName = userName,
                Collaborators = GetCollaborators(projectId)
            }, excludeUserId: userId);

            _logger.LogInformation("User {UserId} left project {ProjectId}", userId, projectId);
        }
    }

    private async Task HandleMessageAsync(string projectId, string userId, string messageJson)
    {
        try
        {
            var message = JsonSerializer.Deserialize<CollaborationMessage>(messageJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (message == null) return;

            // Get connection
            if (!_projectConnections.TryGetValue(projectId, out var projectConns)) return;
            if (!projectConns.TryGetValue(userId, out var connection)) return;

            switch (message.Type)
            {
                case "cursor":
                    // Update cursor position
                    connection.Cursor = message.Cursor;
                    connection.ActiveFile = message.FileId ?? message.FilePath;
                    
                    // Broadcast to others
                    await BroadcastToProjectAsync(projectId, new CollaborationMessage
                    {
                        Type = "cursor",
                        UserId = userId,
                        UserName = connection.UserName,
                        UserColor = connection.UserColor,
                        FileId = message.FileId,
                        FilePath = message.FilePath,
                        Cursor = message.Cursor
                    }, excludeUserId: userId);
                    break;

                case "edit":
                    // Broadcast edit to others
                    await BroadcastToProjectAsync(projectId, new CollaborationMessage
                    {
                        Type = "edit",
                        UserId = userId,
                        UserName = connection.UserName,
                        FileId = message.FileId,
                        FilePath = message.FilePath,
                        Edit = message.Edit
                    }, excludeUserId: userId);
                    break;

                case "file_open":
                    connection.ActiveFile = message.FileId ?? message.FilePath;
                    await BroadcastToProjectAsync(projectId, new CollaborationMessage
                    {
                        Type = "file_open",
                        UserId = userId,
                        UserName = connection.UserName,
                        UserColor = connection.UserColor,
                        FileId = message.FileId,
                        FilePath = message.FilePath
                    }, excludeUserId: userId);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling message from user {UserId}", userId);
        }
    }

    private List<CollaboratorInfo> GetCollaborators(string projectId)
    {
        if (!_projectConnections.TryGetValue(projectId, out var projectConns))
            return new List<CollaboratorInfo>();

        return projectConns.Values.Select(c => new CollaboratorInfo
        {
            UserId = c.UserId,
            UserName = c.UserName,
            UserColor = c.UserColor,
            ActiveFile = c.ActiveFile,
            Cursor = c.Cursor
        }).ToList();
    }

    private async Task BroadcastToProjectAsync(string projectId, CollaborationMessage message, string? excludeUserId)
    {
        if (!_projectConnections.TryGetValue(projectId, out var projectConns)) return;

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        foreach (var conn in projectConns.Values)
        {
            if (excludeUserId != null && conn.UserId == excludeUserId) continue;
            if (conn.Socket.State != WebSocketState.Open) continue;

            try
            {
                await conn.Socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending to user {UserId}", conn.UserId);
            }
        }
    }

    private async Task SendToUserAsync(string projectId, string userId, CollaborationMessage message)
    {
        if (!_projectConnections.TryGetValue(projectId, out var projectConns)) return;
        if (!projectConns.TryGetValue(userId, out var conn)) return;
        if (conn.Socket.State != WebSocketState.Open) return;

        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        var bytes = Encoding.UTF8.GetBytes(json);

        await conn.Socket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            CancellationToken.None
        );
    }

    public int GetActiveCollaboratorCount(string projectId)
    {
        if (!_projectConnections.TryGetValue(projectId, out var projectConns))
            return 0;
        return projectConns.Count;
    }
}
