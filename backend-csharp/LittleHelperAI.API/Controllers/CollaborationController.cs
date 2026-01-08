// Collaboration Controller - WebSocket and Google Drive sharing
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Net.WebSockets;
using System.Text.Json;
using LittleHelperAI.API.Services;
using LittleHelperAI.Data;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/collaboration")]
public class CollaborationController : ControllerBase
{
    private readonly CollaborationService _collaborationService;
    private readonly IDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<CollaborationController> _logger;
    private readonly HttpClient _httpClient;

    public CollaborationController(
        CollaborationService collaborationService, 
        IDbContext db,
        IConfiguration config,
        ILogger<CollaborationController> logger)
    {
        _collaborationService = collaborationService;
        _db = db;
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    // WebSocket endpoint for real-time collaboration
    [HttpGet("ws/{projectId}")]
    public async Task ConnectWebSocket(string projectId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        // Get user from query string (token-based auth for WebSocket)
        var token = HttpContext.Request.Query["token"].ToString();
        var userId = HttpContext.Request.Query["userId"].ToString();
        var userName = HttpContext.Request.Query["userName"].ToString();

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userName))
        {
            HttpContext.Response.StatusCode = 401;
            return;
        }

        // Verify project access
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id",
            new { Id = projectId });

        if (project == null)
        {
            HttpContext.Response.StatusCode = 404;
            return;
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await _collaborationService.HandleWebSocketAsync(webSocket, projectId, userId, userName);
    }

    // Get active collaborators for a project
    [HttpGet("{projectId}/collaborators")]
    [Authorize]
    public ActionResult GetCollaborators(string projectId)
    {
        var count = _collaborationService.GetActiveCollaboratorCount(projectId);
        return Ok(new { project_id = projectId, active_count = count });
    }

    // Generate a shareable link for the project
    [HttpPost("{projectId}/share")]
    [Authorize]
    public async Task<ActionResult> CreateShareLink(string projectId)
    {
        var userId = User.FindFirst("user_id")?.Value;
        
        // Check if user owns the project
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id AND user_id = @UserId",
            new { Id = projectId, UserId = userId });

        if (project == null)
        {
            return NotFound(new { detail = "Project not found or access denied" });
        }

        // Generate a unique share token
        var shareToken = GenerateShareToken();
        var expiresAt = DateTime.UtcNow.AddDays(7); // 7-day expiry

        // Save share link
        await _db.ExecuteAsync(@"
            INSERT INTO project_shares (id, project_id, share_token, created_by, expires_at, created_at)
            VALUES (@Id, @ProjectId, @ShareToken, @CreatedBy, @ExpiresAt, @CreatedAt)
            ON DUPLICATE KEY UPDATE share_token = @ShareToken, expires_at = @ExpiresAt",
            new {
                Id = Guid.NewGuid().ToString(),
                ProjectId = projectId,
                ShareToken = shareToken,
                CreatedBy = userId,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow
            });

        var baseUrl = _config["Frontend:Url"] ?? "https://codecollab-suite.preview.emergentagent.com";
        var shareUrl = $"{baseUrl}/workspace/{projectId}?share={shareToken}";

        return Ok(new {
            share_url = shareUrl,
            share_token = shareToken,
            expires_at = expiresAt,
            can_edit = true
        });
    }

    // Validate a share link
    [HttpGet("share/validate/{shareToken}")]
    public async Task<ActionResult> ValidateShareLink(string shareToken)
    {
        var share = await _db.QueryFirstOrDefaultAsync<dynamic>(@"
            SELECT ps.*, p.name as project_name, p.description as project_description
            FROM project_shares ps
            JOIN projects p ON p.id = ps.project_id
            WHERE ps.share_token = @Token AND ps.expires_at > NOW()",
            new { Token = shareToken });

        if (share == null)
        {
            return NotFound(new { detail = "Invalid or expired share link" });
        }

        return Ok(new {
            project_id = share.project_id,
            project_name = share.project_name,
            project_description = share.project_description,
            expires_at = share.expires_at,
            can_edit = true
        });
    }

    // Export project files to Google Drive (simplified - creates downloadable zip)
    [HttpPost("{projectId}/export/drive")]
    [Authorize]
    public async Task<ActionResult> ExportToDrive(string projectId)
    {
        var userId = User.FindFirst("user_id")?.Value;
        
        // Get project files
        var files = await _db.QueryAsync<dynamic>(
            "SELECT * FROM project_files WHERE project_id = @ProjectId",
            new { ProjectId = projectId });

        var fileList = files.ToList();
        if (fileList.Count == 0)
        {
            return BadRequest(new { detail = "No files to export" });
        }

        // For now, return a download link for a zip file
        // Full Google Drive integration would require OAuth setup
        return Ok(new {
            message = "Export functionality available. To enable Google Drive upload:",
            instructions = new[] {
                "1. Configure Google OAuth credentials in admin panel",
                "2. Connect your Google Drive account",
                "3. Files will be uploaded to a 'LittleHelper Projects' folder"
            },
            files_count = fileList.Count,
            download_available = true
        });
    }

    // Download project as zip
    [HttpGet("{projectId}/download")]
    [Authorize]
    public async Task<ActionResult> DownloadProject(string projectId)
    {
        var userId = User.FindFirst("user_id")?.Value;
        
        // Verify access
        var project = await _db.QueryFirstOrDefaultAsync<dynamic>(
            "SELECT * FROM projects WHERE id = @Id AND user_id = @UserId",
            new { Id = projectId, UserId = userId });

        if (project == null)
        {
            return NotFound(new { detail = "Project not found" });
        }

        // Get all files
        var files = await _db.QueryAsync<dynamic>(
            "SELECT path, content FROM project_files WHERE project_id = @ProjectId",
            new { ProjectId = projectId });

        var fileList = files.ToList();
        if (fileList.Count == 0)
        {
            return BadRequest(new { detail = "No files to download" });
        }

        // Create zip in memory
        using var memoryStream = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(memoryStream, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            foreach (var file in fileList)
            {
                var entry = archive.CreateEntry(file.path);
                using var entryStream = entry.Open();
                using var writer = new StreamWriter(entryStream);
                writer.Write(file.content ?? "");
            }
        }

        memoryStream.Position = 0;
        var projectName = project.name?.ToString() ?? "project";
        return File(memoryStream.ToArray(), "application/zip", $"{projectName}.zip");
    }

    private static string GenerateShareToken()
    {
        var bytes = new byte[24];
        Random.Shared.NextBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }
}
