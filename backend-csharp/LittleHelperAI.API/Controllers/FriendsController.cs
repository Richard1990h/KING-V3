// Friends and Direct Messages Controller
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.Data;
using System.Text.Json;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IDbContext _db;
    private readonly ILogger<FriendsController> _logger;

    public FriendsController(IDbContext db, ILogger<FriendsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Get friends list
    [HttpGet]
    public async Task<ActionResult> GetFriends()
    {
        var userId = User.FindFirst("user_id")?.Value;
        
        var friends = await _db.QueryAsync<dynamic>(@"
            SELECT f.*, u.email, u.display_name, u.role
            FROM friends f
            JOIN users u ON u.id = f.friend_user_id
            WHERE f.user_id = @UserId
            ORDER BY u.display_name", new { UserId = userId });

        return Ok(friends);
    }

    // Send friend request
    [HttpPost("request")]
    public async Task<ActionResult> SendFriendRequest([FromBody] FriendRequestDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Find user by email
        var targetUser = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT id, email, display_name FROM users WHERE email = @Email",
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

        // Create friend request
        var requestId = Guid.NewGuid().ToString();
        await _db.ExecuteAsync(@"
            INSERT INTO friend_requests (id, sender_id, receiver_id, status, created_at)
            VALUES (@Id, @SenderId, @ReceiverId, 'pending', NOW())",
            new { Id = requestId, SenderId = userId, ReceiverId = targetUser.id });

        _logger.LogInformation("Friend request sent from {SenderId} to {ReceiverId}", userId, targetUser.id);

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
            SELECT fr.*, u.email as sender_email, u.display_name as sender_name
            FROM friend_requests fr
            JOIN users u ON u.id = fr.sender_id
            WHERE fr.receiver_id = @UserId AND fr.status = 'pending'
            ORDER BY fr.created_at DESC", new { UserId = userId });

        var outgoing = await _db.QueryAsync<dynamic>(@"
            SELECT fr.*, u.email as receiver_email, u.display_name as receiver_name
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

        return BadRequest(new { detail = "Invalid action" });
    }

    // Remove friend
    [HttpDelete("{friendUserId}")]
    public async Task<ActionResult> RemoveFriend(string friendUserId)
    {
        var userId = User.FindFirst("user_id")?.Value;

        await _db.ExecuteAsync(@"
            DELETE FROM friends WHERE 
            (user_id = @UserId AND friend_user_id = @FriendId) OR
            (user_id = @FriendId AND friend_user_id = @UserId)",
            new { UserId = userId, FriendId = friendUserId });

        // Send system message
        await SendSystemMessage(userId, friendUserId, "You are no longer friends.");

        return Ok(new { message = "Friend removed" });
    }

    // Get DM conversation
    [HttpGet("dm/{friendUserId}")]
    public async Task<ActionResult> GetDirectMessages(string friendUserId, [FromQuery] int limit = 50)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Verify friendship
        var isFriend = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM friends WHERE user_id = @UserId AND friend_user_id = @FriendId",
            new { UserId = userId, FriendId = friendUserId });

        if (isFriend == null)
            return BadRequest(new { detail = "You can only message friends" });

        var messages = await _db.QueryAsync<dynamic>(@"
            SELECT dm.*, 
                   s.display_name as sender_name, s.email as sender_email,
                   r.display_name as receiver_name, r.email as receiver_email
            FROM direct_messages dm
            JOIN users s ON s.id = dm.sender_id
            JOIN users r ON r.id = dm.receiver_id
            WHERE (dm.sender_id = @UserId AND dm.receiver_id = @FriendId)
               OR (dm.sender_id = @FriendId AND dm.receiver_id = @UserId)
            ORDER BY dm.created_at DESC
            LIMIT @Limit", new { UserId = userId, FriendId = friendUserId, Limit = limit });

        // Mark messages as read
        await _db.ExecuteAsync(@"
            UPDATE direct_messages SET is_read = TRUE 
            WHERE receiver_id = @UserId AND sender_id = @FriendId AND is_read = FALSE",
            new { UserId = userId, FriendId = friendUserId });

        return Ok(messages.Reverse());
    }

    // Send DM
    [HttpPost("dm/{friendUserId}")]
    public async Task<ActionResult> SendDirectMessage(string friendUserId, [FromBody] SendMessageDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Verify friendship
        var isFriend = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM friends WHERE user_id = @UserId AND friend_user_id = @FriendId",
            new { UserId = userId, FriendId = friendUserId });

        if (isFriend == null)
            return BadRequest(new { detail = "You can only message friends" });

        var messageId = Guid.NewGuid().ToString();
        await _db.ExecuteAsync(@"
            INSERT INTO direct_messages (id, sender_id, receiver_id, message, message_type, created_at)
            VALUES (@Id, @SenderId, @ReceiverId, @Message, 'text', NOW())",
            new { Id = messageId, SenderId = userId, ReceiverId = friendUserId, Message = dto.Message });

        return Ok(new { 
            id = messageId, 
            message = dto.Message, 
            sent_at = DateTime.UtcNow 
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

        var total = unread.Sum(u => (int)u.count);

        return Ok(new { total, by_user = unread });
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
