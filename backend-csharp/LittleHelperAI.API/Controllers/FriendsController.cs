// Friends and Direct Messages Controller
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.Data;
using LittleHelperAI.API.Services;
using System.Text.Json;
using System.Security.Cryptography;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IDbContext _db;
    private readonly ILogger<FriendsController> _logger;
    private readonly NotificationService _notificationService;
    private const int MAX_MESSAGE_LENGTH = 5000;
    private const int MAX_FRIENDS_PER_PAGE = 50;

    public FriendsController(IDbContext db, ILogger<FriendsController> logger, NotificationService notificationService)
    {
        _db = db;
        _logger = logger;
        _notificationService = notificationService;
    }

    // Get friends list with pagination
    [HttpGet]
    public async Task<ActionResult> GetFriends([FromQuery] int page = 1, [FromQuery] int limit = 50)
    {
        var userId = User.FindFirst("user_id")?.Value;
        limit = Math.Min(limit, MAX_FRIENDS_PER_PAGE);
        var offset = (page - 1) * limit;
        
        var friends = await _db.QueryAsync<dynamic>(@"
            SELECT f.id, f.friend_user_id, f.created_at,
                   u.email, 
                   COALESCE(u.display_name, u.name, u.email) as display_name, 
                   u.role
            FROM friends f
            JOIN users u ON u.id = f.friend_user_id
            WHERE f.user_id = @UserId
            ORDER BY COALESCE(u.display_name, u.name, u.email)
            LIMIT @Limit OFFSET @Offset", 
            new { UserId = userId, Limit = limit, Offset = offset });

        var total = await _db.QueryFirstOrDefaultAsync<int>(
            "SELECT COUNT(*) FROM friends WHERE user_id = @UserId",
            new { UserId = userId });

        return Ok(new { 
            friends = friends,
            pagination = new { page, limit, total, pages = (int)Math.Ceiling(total / (double)limit) }
        });
    }

    // Send friend request with rate limiting
    [HttpPost("request")]
    public async Task<ActionResult> SendFriendRequest([FromBody] FriendRequestDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Rate limit: max 10 requests per hour
        var recentRequests = await _db.QueryFirstOrDefaultAsync<int>(@"
            SELECT COUNT(*) FROM friend_requests 
            WHERE sender_id = @UserId AND created_at > DATE_SUB(NOW(), INTERVAL 1 HOUR)",
            new { UserId = userId });

        if (recentRequests >= 10)
            return StatusCode(429, new { detail = "Too many friend requests. Please wait before sending more." });

        // Find user by email
        var targetUser = await _db.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT id, email, COALESCE(display_name, name, email) as display_name 
            FROM users WHERE email = @Email",
            new { Email = dto.Email });

        if (targetUser == null)
            return NotFound(new { detail = "User not found with that email" });

        if (targetUser.id == userId)
            return BadRequest(new { detail = "Cannot send friend request to yourself" });

        // Check if already friends
        var existingFriend = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM friends WHERE user_id = @UserId AND friend_user_id = @FriendId",
            new { UserId = userId, FriendId = targetUser.id });

        if (existingFriend != null)
            return BadRequest(new { detail = "Already friends with this user" });

        // Check for existing request
        var existingRequest = await _db.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT * FROM friend_requests 
            WHERE (sender_id = @UserId AND receiver_id = @ReceiverId)
               OR (sender_id = @ReceiverId AND receiver_id = @UserId)",
            new { UserId = userId, ReceiverId = targetUser.id });

        if (existingRequest != null)
        {
            if (existingRequest.status == "blocked")
                return BadRequest(new { detail = "Cannot send request to this user" });
            if (existingRequest.status == "pending")
                return BadRequest(new { detail = "Friend request already pending" });
        }

        // Get sender name for notification
        var senderUser = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT COALESCE(display_name, name, email) as name FROM users WHERE id = @UserId",
            new { UserId = userId });

        // Create friend request
        var requestId = Guid.NewGuid().ToString();
        await _db.ExecuteAsync(@"
            INSERT INTO friend_requests (id, sender_id, receiver_id, status, created_at)
            VALUES (@Id, @SenderId, @ReceiverId, 'pending', NOW())",
            new { Id = requestId, SenderId = userId, ReceiverId = targetUser.id });

        _logger.LogInformation("Friend request sent from {0} to {1}", userId, (string)targetUser.id);

        // Send real-time notification
        await _notificationService.NotifyFriendRequest(
            (string)targetUser.id, 
            userId!, 
            (string)senderUser.name
        );

        return Ok(new { 
            message = "Friend request sent", 
            request_id = requestId,
            to_user = new { id = targetUser.id, email = targetUser.email, name = targetUser.display_name }
        });
    }

    // Get pending friend requests
    [HttpGet("requests")]
    public async Task<ActionResult> GetFriendRequests()
    {
        var userId = User.FindFirst("user_id")?.Value;

        var incoming = await _db.QueryAsync<dynamic>(@"
            SELECT fr.*, 
                   u.email as sender_email, 
                   COALESCE(u.display_name, u.name, u.email) as sender_name
            FROM friend_requests fr
            JOIN users u ON u.id = fr.sender_id
            WHERE fr.receiver_id = @UserId AND fr.status = 'pending'
            ORDER BY fr.created_at DESC", new { UserId = userId });

        var outgoing = await _db.QueryAsync<dynamic>(@"
            SELECT fr.*, 
                   u.email as receiver_email, 
                   COALESCE(u.display_name, u.name, u.email) as receiver_name
            FROM friend_requests fr
            JOIN users u ON u.id = fr.receiver_id
            WHERE fr.sender_id = @UserId AND fr.status = 'pending'
            ORDER BY fr.created_at DESC", new { UserId = userId });

        return Ok(new { incoming, outgoing });
    }

    // Accept/Deny friend request
    [HttpPut("requests/{requestId}")]
    public async Task<ActionResult> RespondToRequest(string requestId, [FromBody] RespondRequestDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        var request = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM friend_requests WHERE id = @Id AND receiver_id = @ReceiverId AND status = 'pending'",
            new { Id = requestId, ReceiverId = userId });

        if (request == null)
            return NotFound(new { detail = "Friend request not found" });

        if (dto.Action == "accept")
        {
            // Update request status
            await _db.ExecuteAsync(
                "UPDATE friend_requests SET status = 'accepted', updated_at = NOW() WHERE id = @Id",
                new { Id = requestId });

            // Create bidirectional friendship
            var friendId1 = Guid.NewGuid().ToString();
            var friendId2 = Guid.NewGuid().ToString();
            
            await _db.ExecuteAsync(@"
                INSERT INTO friends (id, user_id, friend_user_id, created_at) VALUES 
                (@Id1, @UserId, @FriendId, NOW()),
                (@Id2, @FriendId, @UserId, NOW())",
                new { Id1 = friendId1, Id2 = friendId2, UserId = userId, FriendId = request.sender_id });

            // Get accepter's name for notification
            var accepterUser = await _db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT COALESCE(display_name, name, email) as name FROM users WHERE id = @UserId",
                new { UserId = userId });

            // Send real-time notification to original sender
            await _notificationService.NotifyFriendRequestAccepted(
                (string)request.sender_id,
                userId!,
                (string)accepterUser.name
            );

            // Send system message
            await SendSystemMessage(request.sender_id, userId, "Your friend request was accepted!");

            return Ok(new { message = "Friend request accepted" });
        }
        else if (dto.Action == "deny")
        {
            await _db.ExecuteAsync(
                "UPDATE friend_requests SET status = 'denied', updated_at = NOW() WHERE id = @Id",
                new { Id = requestId });

            return Ok(new { message = "Friend request denied" });
        }
        else if (dto.Action == "block")
        {
            await _db.ExecuteAsync(
                "UPDATE friend_requests SET status = 'blocked', updated_at = NOW() WHERE id = @Id",
                new { Id = requestId });

            return Ok(new { message = "User blocked" });
        }

        return BadRequest(new { detail = "Invalid action. Use: accept, deny, or block" });
    }

    // Remove friend
    [HttpDelete("{friendUserId}")]
    public async Task<ActionResult> RemoveFriend(string friendUserId)
    {
        var userId = User.FindFirst("user_id")?.Value;

        var deleted = await _db.ExecuteAsync(@"
            DELETE FROM friends WHERE 
            (user_id = @UserId AND friend_user_id = @FriendId) OR
            (user_id = @FriendId AND friend_user_id = @UserId)",
            new { UserId = userId, FriendId = friendUserId });

        if (deleted == 0)
            return NotFound(new { detail = "Friendship not found" });

        return Ok(new { message = "Friend removed" });
    }

    // Get DM conversation - fixed query order
    [HttpGet("dm/{friendUserId}")]
    public async Task<ActionResult> GetDirectMessages(string friendUserId, [FromQuery] int limit = 50, [FromQuery] string? before = null)
    {
        var userId = User.FindFirst("user_id")?.Value;
        limit = Math.Min(limit, 100);

        // Verify friendship
        var isFriend = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM friends WHERE user_id = @UserId AND friend_user_id = @FriendId",
            new { UserId = userId, FriendId = friendUserId });

        if (isFriend == null)
            return BadRequest(new { detail = "You can only message friends" });

        // Get messages in correct order (oldest first for display)
        var sql = @"
            SELECT * FROM (
                SELECT dm.id, dm.sender_id, dm.receiver_id, dm.message, dm.message_type, 
                       dm.is_read, dm.created_at,
                       s.email as sender_email,
                       COALESCE(s.display_name, s.name, s.email) as sender_name,
                       r.email as receiver_email,
                       COALESCE(r.display_name, r.name, r.email) as receiver_name
                FROM direct_messages dm
                JOIN users s ON s.id = dm.sender_id
                JOIN users r ON r.id = dm.receiver_id
                WHERE (dm.sender_id = @UserId AND dm.receiver_id = @FriendId)
                   OR (dm.sender_id = @FriendId AND dm.receiver_id = @UserId)
                " + (before != null ? "AND dm.created_at < @Before " : "") + @"
                ORDER BY dm.created_at DESC
                LIMIT @Limit
            ) sub ORDER BY created_at ASC";

        var messages = await _db.QueryAsync<dynamic>(sql, 
            new { UserId = userId, FriendId = friendUserId, Limit = limit, Before = before });

        // Mark messages as read
        await _db.ExecuteAsync(@"
            UPDATE direct_messages SET is_read = TRUE 
            WHERE receiver_id = @UserId AND sender_id = @FriendId AND is_read = FALSE",
            new { UserId = userId, FriendId = friendUserId });

        return Ok(messages);
    }

    // Send DM with validation
    [HttpPost("dm/{friendUserId}")]
    public async Task<ActionResult> SendDirectMessage(string friendUserId, [FromBody] SendMessageDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Validate message
        if (string.IsNullOrWhiteSpace(dto.Message))
            return BadRequest(new { detail = "Message cannot be empty" });

        if (dto.Message.Length > MAX_MESSAGE_LENGTH)
            return BadRequest(new { detail = $"Message too long. Maximum {MAX_MESSAGE_LENGTH} characters." });

        // Verify friendship
        var isFriend = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM friends WHERE user_id = @UserId AND friend_user_id = @FriendId",
            new { UserId = userId, FriendId = friendUserId });

        if (isFriend == null)
            return BadRequest(new { detail = "You can only message friends" });

        var messageId = Guid.NewGuid().ToString();
        var createdAt = DateTime.UtcNow;
        
        await _db.ExecuteAsync(@"
            INSERT INTO direct_messages (id, sender_id, receiver_id, message, message_type, created_at)
            VALUES (@Id, @SenderId, @ReceiverId, @Message, 'text', @CreatedAt)",
            new { Id = messageId, SenderId = userId, ReceiverId = friendUserId, Message = dto.Message.Trim(), CreatedAt = createdAt });

        return Ok(new { 
            id = messageId, 
            message = dto.Message.Trim(), 
            sent_at = createdAt 
        });
    }

    // Get unread message count
    [HttpGet("dm/unread")]
    public async Task<ActionResult> GetUnreadCount()
    {
        var userId = User.FindFirst("user_id")?.Value;

        var unread = await _db.QueryAsync<dynamic>(@"
            SELECT sender_id, COUNT(*) as count
            FROM direct_messages
            WHERE receiver_id = @UserId AND is_read = FALSE
            GROUP BY sender_id", new { UserId = userId });

        var unreadList = unread.ToList();
        var total = unreadList.Sum(u => (int)(long)u.count);

        return Ok(new { total, by_user = unreadList });
    }

    private async Task SendSystemMessage(string senderId, string receiverId, string message)
    {
        await _db.ExecuteAsync(@"
            INSERT INTO direct_messages (id, sender_id, receiver_id, message, message_type, created_at)
            VALUES (@Id, @SenderId, @ReceiverId, @Message, 'system', NOW())",
            new { 
                Id = Guid.NewGuid().ToString(), 
                SenderId = senderId, 
                ReceiverId = receiverId, 
                Message = message 
            });
    }

    public record FriendRequestDto(string Email);
    public record RespondRequestDto(string Action); // accept, deny, block
    public record SendMessageDto(string Message);
}
