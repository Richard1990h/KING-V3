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
    private readonly IAIService _aiService;
    private readonly ICreditService _creditService;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectService projectService, 
        IAIService aiService,
        ICreditService creditService,
        ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _aiService = aiService;
        _creditService = creditService;
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
    public async Task<ActionResult> SendChatMessage(string projectId, [FromBody] SendChatRequest request)
    {
        // Save user message
        var userMessage = new ChatMessage
        {
            UserId = GetUserId(),
            ProjectId = projectId,
            ConversationId = request.ConversationId ?? Guid.NewGuid().ToString(),
            Role = "user",
            Content = request.Message,
            MultiAgentMode = request.MultiAgentMode
        };
        await _projectService.SaveChatMessageAsync(userMessage);

        // Generate AI response
        try
        {
            var systemPrompt = @"You are LittleHelper, an AI coding assistant. Help users with their coding questions and tasks.
Be helpful, concise, and provide working code examples when appropriate.
If asked to create files or code, explain what you're creating.";
            
            var aiResponse = await _aiService.GenerateAsync(request.Message, systemPrompt, 2000);
            
            // Save AI message
            var aiMessage = new ChatMessage
            {
                UserId = GetUserId(),
                ProjectId = projectId,
                ConversationId = userMessage.ConversationId,
                Role = "assistant",
                Content = aiResponse.Content,
                Provider = aiResponse.Provider,
                Model = aiResponse.Model,
                TokensUsed = aiResponse.Tokens
            };
            await _projectService.SaveChatMessageAsync(aiMessage);

            // Deduct credits
            await _creditService.DeductCreditsAsync(GetUserId(), (decimal)(aiResponse.Tokens / 1000.0 * 0.5), "Chat message");

            return Ok(new { 
                user_message = userMessage,
                ai_message = new {
                    role = "assistant",
                    content = aiResponse.Content,
                    provider = aiResponse.Provider,
                    model = aiResponse.Model,
                    tokens_used = aiResponse.Tokens,
                    timestamp = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            return Ok(new { 
                user_message = userMessage,
                ai_message = new {
                    role = "assistant",
                    content = $"I apologize, but I encountered an error: {ex.Message}. Please try again or check your AI provider settings.",
                    error = true,
                    timestamp = DateTime.UtcNow
                }
            });
        }
    }

    [HttpDelete("{projectId}/chat")]
    public async Task<ActionResult> ClearChatHistory(string projectId)
    {
        var userId = GetUserId();
        
        // Verify project ownership
        var project = await _projectService.GetProjectAsync(projectId, userId);
        if (project == null)
            return NotFound(new { detail = "Project not found" });

        // Delete chat messages for this project
        var deleted = await _projectService.ClearChatHistoryAsync(projectId, userId);
        
        _logger.LogInformation("Cleared {Count} chat messages for project {ProjectId} by user {UserId}", 
            deleted, projectId, userId);
            
        return Ok(new { message = "Chat history cleared", messages_deleted = deleted });
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
public record SendChatRequest(
    string Message, 
    List<string>? AgentsEnabled = null, 
    string? ConversationId = null, 
    bool MultiAgentMode = false
);
