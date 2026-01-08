// Projects Controller
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services;
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(IProjectService projectService, ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst("user_id")?.Value ?? throw new UnauthorizedAccessException();

    [HttpGet]
    public async Task<ActionResult<List<ProjectResponse>>> GetProjects()
    {
        var projects = await _projectService.GetUserProjectsAsync(GetUserId());
        return Ok(projects);
    }

    [HttpGet("{projectId}")]
    public async Task<ActionResult<ProjectResponse>> GetProject(string projectId)
    {
        var project = await _projectService.GetProjectAsync(projectId, GetUserId());
        if (project == null)
            return NotFound(new { detail = "Project not found" });
        return Ok(project);
    }

    [HttpPost]
    public async Task<ActionResult<ProjectResponse>> CreateProject([FromBody] CreateProjectRequest request)
    {
        var project = await _projectService.CreateProjectAsync(GetUserId(), request);
        return Ok(project);
    }

    [HttpPut("{projectId}")]
    public async Task<ActionResult<ProjectResponse>> UpdateProject(string projectId, [FromBody] UpdateProjectRequest request)
    {
        var project = await _projectService.UpdateProjectAsync(projectId, GetUserId(), request);
        if (project == null)
            return NotFound(new { detail = "Project not found" });
        return Ok(project);
    }

    [HttpDelete("{projectId}")]
    public async Task<ActionResult> DeleteProject(string projectId)
    {
        var success = await _projectService.DeleteProjectAsync(projectId, GetUserId());
        if (!success)
            return NotFound(new { detail = "Project not found" });
        return Ok(new { message = "Project deleted" });
    }

    [HttpPost("{projectId}/export")]
    public async Task<ActionResult> ExportProject(string projectId)
    {
        var result = await _projectService.ExportProjectAsync(projectId, GetUserId());
        if (result == null)
            return NotFound(new { detail = "Project not found" });
        return Ok(result);
    }

    [HttpPost("{projectId}/build")]
    public async Task<ActionResult> BuildProject(string projectId)
    {
        var result = await _projectService.BuildProjectAsync(projectId, GetUserId());
        return Ok(result);
    }

    [HttpPost("{projectId}/run")]
    public async Task<ActionResult> RunProject(string projectId)
    {
        var result = await _projectService.RunProjectAsync(projectId, GetUserId());
        return Ok(result);
    }

    // File endpoints
    [HttpGet("{projectId}/files")]
    public async Task<ActionResult<List<FileResponse>>> GetFiles(string projectId)
    {
        var files = await _projectService.GetProjectFilesAsync(projectId, GetUserId());
        return Ok(files);
    }

    [HttpPost("{projectId}/files")]
    public async Task<ActionResult<FileResponse>> CreateFile(string projectId, [FromBody] CreateFileRequest request)
    {
        var file = await _projectService.CreateFileAsync(projectId, GetUserId(), request);
        return Ok(file);
    }

    [HttpPut("{projectId}/files/{fileId}")]
    public async Task<ActionResult<FileResponse>> UpdateFile(string projectId, string fileId, [FromBody] UpdateFileRequest request)
    {
        var file = await _projectService.UpdateFileAsync(projectId, fileId, GetUserId(), request);
        if (file == null)
            return NotFound(new { detail = "File not found" });
        return Ok(file);
    }

    [HttpDelete("{projectId}/files/{fileId}")]
    public async Task<ActionResult> DeleteFile(string projectId, string fileId)
    {
        var success = await _projectService.DeleteFileAsync(projectId, fileId, GetUserId());
        if (!success)
            return NotFound(new { detail = "File not found" });
        return Ok(new { message = "File deleted" });
    }

    // Todo endpoints
    [HttpGet("{projectId}/todos")]
    public async Task<ActionResult<List<TodoResponse>>> GetTodos(string projectId)
    {
        var todos = await _projectService.GetTodosAsync(projectId, GetUserId());
        return Ok(todos);
    }

    [HttpPost("{projectId}/todos")]
    public async Task<ActionResult<TodoResponse>> CreateTodo(string projectId, [FromBody] CreateTodoRequest request)
    {
        var todo = await _projectService.CreateTodoAsync(projectId, GetUserId(), request);
        return Ok(todo);
    }

    [HttpPut("{projectId}/todos/{todoId}")]
    public async Task<ActionResult<TodoResponse>> UpdateTodo(string projectId, string todoId, [FromBody] UpdateTodoRequest request)
    {
        var todo = await _projectService.UpdateTodoAsync(projectId, todoId, GetUserId(), request);
        if (todo == null)
            return NotFound(new { detail = "Todo not found" });
        return Ok(todo);
    }

    [HttpDelete("{projectId}/todos/{todoId}")]
    public async Task<ActionResult> DeleteTodo(string projectId, string todoId)
    {
        var success = await _projectService.DeleteTodoAsync(projectId, todoId, GetUserId());
        if (!success)
            return NotFound(new { detail = "Todo not found" });
        return Ok(new { message = "Todo deleted" });
    }

    // Chat Endpoints
    [HttpGet("{projectId}/chat")]
    public async Task<ActionResult<List<ChatMessage>>> GetChatHistory(string projectId, [FromQuery] int limit = 50)
    {
        var messages = await _projectService.GetChatHistoryAsync(projectId, GetUserId(), limit);
        return Ok(messages);
    }

    [HttpPost("{projectId}/chat")]
    public async Task<ActionResult<ChatMessage>> SendChatMessage(string projectId, [FromBody] SendChatRequest request)
    {
        var message = new ChatMessage
        {
            UserId = GetUserId(),
            ProjectId = projectId,
            ConversationId = request.ConversationId,
            Role = "user",
            Content = request.Message,
            MultiAgentMode = request.MultiAgentMode
        };

        var savedMessage = await _projectService.SaveChatMessageAsync(message);
        return Ok(savedMessage);
    }

    [HttpDelete("{projectId}/chat")]
    public async Task<ActionResult> ClearChatHistory(string projectId)
    {
        // Mark messages as deleted
        var userId = GetUserId();
        // For now just return success - in production would update database
        return Ok(new { message = "Chat history cleared" });
    }
}

// Request/Response Models
public record CreateProjectRequest(string Name, string? Description, string Language = "Python");
public record UpdateProjectRequest(string? Name, string? Description);
public record ProjectResponse(string Id, string UserId, string Name, string Description, string Language, string CreatedAt, string UpdatedAt, string Status);
public record CreateFileRequest(string Path, string Content = "");
public record UpdateFileRequest(string Content);
public record FileResponse(string Id, string ProjectId, string Path, string Content, string UpdatedAt);
public record CreateTodoRequest(string Text, string Priority = "medium");
public record UpdateTodoRequest(string? Text, bool? Completed, string? Priority);
public record TodoResponse(string Id, string ProjectId, string Text, bool Completed, string Priority, string CreatedAt);
