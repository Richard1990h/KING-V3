// Project Collaborators and Credit Sharing Controller
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.Data;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/projects/{projectId}/collaborators")]
[Authorize]
public class CollaboratorsController : ControllerBase
{
    private readonly IDbContext _db;
    private readonly ILogger<CollaboratorsController> _logger;

    public CollaboratorsController(IDbContext db, ILogger<CollaboratorsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // Get project collaborators
    [HttpGet]
    public async Task<ActionResult> GetCollaborators(string projectId)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Verify access
        var hasAccess = await VerifyProjectAccess(projectId, userId);
        if (!hasAccess) return NotFound(new { detail = "Project not found or access denied" });

        var collaborators = await _db.QueryAsync<dynamic>(@"
            SELECT pc.*, u.email, u.display_name, u.role as user_role
            FROM project_collaborators pc
            JOIN users u ON u.id = pc.user_id
            WHERE pc.project_id = @ProjectId
            ORDER BY pc.created_at", new { ProjectId = projectId });

        // Get project owner
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT user_id, credit_mode FROM projects WHERE id = @Id",
            new { Id = projectId });

        var owner = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT id, email, display_name FROM users WHERE id = @Id",
            new { Id = project.user_id });

        return Ok(new {
            owner,
            credit_mode = project.credit_mode ?? "own",
            collaborators
        });
    }

    // Add collaborator (must be a friend)
    [HttpPost]
    public async Task<ActionResult> AddCollaborator(string projectId, [FromBody] AddCollaboratorDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Verify ownership
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id AND user_id = @UserId",
            new { Id = projectId, UserId = userId });

        if (project == null)
            return NotFound(new { detail = "Project not found or you're not the owner" });

        // Verify friendship
        var isFriend = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM friends WHERE user_id = @UserId AND friend_user_id = @FriendId",
            new { UserId = userId, FriendId = dto.UserId });

        if (isFriend == null)
            return BadRequest(new { detail = "You can only add friends as collaborators" });

        // Check if already a collaborator
        var existing = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM project_collaborators WHERE project_id = @ProjectId AND user_id = @UserId",
            new { ProjectId = projectId, UserId = dto.UserId });

        if (existing != null)
            return BadRequest(new { detail = "User is already a collaborator" });

        // Add collaborator
        var collabId = Guid.NewGuid().ToString();
        await _db.ExecuteAsync(@"
            INSERT INTO project_collaborators (id, project_id, user_id, permission_level, invited_by, created_at)
            VALUES (@Id, @ProjectId, @CollabUserId, @Permission, @InvitedBy, NOW())",
            new { 
                Id = collabId, 
                ProjectId = projectId, 
                CollabUserId = dto.UserId, 
                Permission = dto.Permission ?? "edit",
                InvitedBy = userId 
            });

        // Send system DM notification
        await _db.ExecuteAsync(@"
            INSERT INTO direct_messages (id, sender_id, receiver_id, message, message_type, created_at)
            VALUES (@Id, @SenderId, @ReceiverId, @Message, 'system', NOW())",
            new { 
                Id = Guid.NewGuid().ToString(),
                SenderId = userId,
                ReceiverId = dto.UserId,
                Message = $"You've been added as a collaborator to project '{project.name}'"
            });

        _logger.LogInformation("User {0} added to project {1} by {2}", 
            dto.UserId, projectId, userId);

        return Ok(new { message = "Collaborator added", collaborator_id = collabId });
    }

    // Update collaborator permission
    [HttpPut("{collaboratorUserId}")]
    public async Task<ActionResult> UpdateCollaborator(string projectId, string collaboratorUserId, [FromBody] UpdateCollaboratorDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Verify ownership
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id AND user_id = @UserId",
            new { Id = projectId, UserId = userId });

        if (project == null)
            return NotFound(new { detail = "Project not found or you're not the owner" });

        await _db.ExecuteAsync(@"
            UPDATE project_collaborators 
            SET permission_level = @Permission 
            WHERE project_id = @ProjectId AND user_id = @CollabUserId",
            new { Permission = dto.Permission, ProjectId = projectId, CollabUserId = collaboratorUserId });

        return Ok(new { message = "Collaborator updated" });
    }

    // Remove collaborator
    [HttpDelete("{collaboratorUserId}")]
    public async Task<ActionResult> RemoveCollaborator(string projectId, string collaboratorUserId)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Can remove self or owner can remove anyone
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id",
            new { Id = projectId });

        if (project == null)
            return NotFound(new { detail = "Project not found" });

        var isOwner = project.user_id == userId;
        var isSelf = collaboratorUserId == userId;

        if (!isOwner && !isSelf)
            return Forbid();

        await _db.ExecuteAsync(
            "DELETE FROM project_collaborators WHERE project_id = @ProjectId AND user_id = @CollabUserId",
            new { ProjectId = projectId, CollabUserId = collaboratorUserId });

        return Ok(new { message = "Collaborator removed" });
    }

    // Set credit mode for project
    [HttpPut("credit-mode")]
    public async Task<ActionResult> SetCreditMode(string projectId, [FromBody] CreditModeDto dto)
    {
        var userId = User.FindFirst("user_id")?.Value;

        // Verify ownership
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id AND user_id = @UserId",
            new { Id = projectId, UserId = userId });

        if (project == null)
            return NotFound(new { detail = "Project not found or you're not the owner" });

        await _db.ExecuteAsync(
            "UPDATE projects SET credit_mode = @Mode WHERE id = @Id",
            new { Mode = dto.Mode, Id = projectId });

        _logger.LogInformation("Credit mode set to {0} for project {1}", dto.Mode, projectId);

        return Ok(new { message = $"Credit mode set to {dto.Mode}", credit_mode = dto.Mode });
    }

    private async Task<bool> VerifyProjectAccess(string projectId, string userId)
    {
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id AND user_id = @UserId",
            new { Id = projectId, UserId = userId });

        if (project != null) return true;

        var isCollab = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM project_collaborators WHERE project_id = @ProjectId AND user_id = @UserId",
            new { ProjectId = projectId, UserId = userId });

        return isCollab != null;
    }

    public record AddCollaboratorDto(string UserId, string? Permission);
    public record UpdateCollaboratorDto(string Permission);
    public record CreditModeDto(string Mode); // "own" or "shared"
}

// Credit Service Extension for shared credits
public interface ICreditUsageService
{
    Task<bool> DeductCredits(string projectId, string userId, decimal amount, string actionType, string description);
    Task LogCreditUsage(string projectId, string userId, string creditSource, decimal amount, string actionType, string description);
}
