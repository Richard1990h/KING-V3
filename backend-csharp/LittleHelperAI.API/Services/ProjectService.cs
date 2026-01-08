// Project Service Implementation - Updated with all methods
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using LittleHelperAI.API.Controllers;
using System.Text.Json;

namespace LittleHelperAI.API.Services;

public class ProjectService : IProjectService
{
    private readonly IDbContext _db;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IDbContext db, ILogger<ProjectService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<List<ProjectResponse>> GetUserProjectsAsync(string userId)
    {
        var projects = await _db.QueryAsync<Project>(
            "SELECT * FROM projects WHERE user_id = @UserId AND status != 'deleted' ORDER BY updated_at DESC",
            new { UserId = userId });
        return projects.Select(MapToResponse).ToList();
    }

    public async Task<ProjectResponse?> GetProjectAsync(string projectId, string userId)
    {
        var project = await _db.QueryFirstOrDefaultAsync<Project>(
            "SELECT * FROM projects WHERE id = @ProjectId AND user_id = @UserId AND status != 'deleted'",
            new { ProjectId = projectId, UserId = userId });
        return project != null ? MapToResponse(project) : null;
    }

    public async Task<ProjectResponse> CreateProjectAsync(string userId, CreateProjectRequest request)
    {
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = request.Name,
            Description = request.Description ?? "",
            Language = request.Language,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(@"
            INSERT INTO projects (id, user_id, name, description, language, status, created_at, updated_at)
            VALUES (@Id, @UserId, @Name, @Description, @Language, @Status, @CreatedAt, @UpdatedAt)",
            project);

        return MapToResponse(project);
    }

    public async Task<ProjectResponse?> UpdateProjectAsync(string projectId, string userId, UpdateProjectRequest request)
    {
        var updates = new List<string> { "updated_at = @UpdatedAt" };
        var parameters = new Dictionary<string, object>
        {
            ["ProjectId"] = projectId,
            ["UserId"] = userId,
            ["UpdatedAt"] = DateTime.UtcNow
        };

        if (request.Name != null) { updates.Add("name = @Name"); parameters["Name"] = request.Name; }
        if (request.Description != null) { updates.Add("description = @Description"); parameters["Description"] = request.Description; }

        var result = await _db.ExecuteAsync(
            $"UPDATE projects SET {string.Join(", ", updates)} WHERE id = @ProjectId AND user_id = @UserId",
            parameters);
        
        if (result == 0) return null;
        return await GetProjectAsync(projectId, userId);
    }

    public async Task<bool> DeleteProjectAsync(string projectId, string userId)
    {
        var result = await _db.ExecuteAsync(
            "UPDATE projects SET status = 'deleted', updated_at = @Now WHERE id = @ProjectId AND user_id = @UserId",
            new { Now = DateTime.UtcNow, ProjectId = projectId, UserId = userId });
        return result > 0;
    }

    public async Task<object?> ExportProjectAsync(string projectId, string userId)
    {
        var project = await _db.QueryFirstOrDefaultAsync<Project>(
            "SELECT * FROM projects WHERE id = @ProjectId AND user_id = @UserId",
            new { ProjectId = projectId, UserId = userId });
        
        if (project == null) return null;

        var files = await _db.QueryAsync<ProjectFile>(
            "SELECT * FROM project_files WHERE project_id = @ProjectId",
            new { ProjectId = projectId });

        return new
        {
            project = MapToResponse(project),
            files = files.Select(f => new { f.Path, f.Content }).ToList()
        };
    }

    public async Task<object> BuildProjectAsync(string projectId, string userId)
    {
        var run = await CreateRunAsync(projectId, "build");
        
        // In a real implementation, this would trigger actual build process
        await UpdateRunAsync(run.Id, "completed", "Build successful", null, null);
        
        return new { run_id = run.Id, status = "completed", message = "Build successful" };
    }

    public async Task<object> RunProjectAsync(string projectId, string userId)
    {
        var run = await CreateRunAsync(projectId, "run");
        
        // In a real implementation, this would trigger actual run process
        await UpdateRunAsync(run.Id, "completed", "Run completed", null, null);
        
        return new { run_id = run.Id, status = "completed", message = "Run completed" };
    }

    public async Task<List<FileResponse>> GetProjectFilesAsync(string projectId, string userId)
    {
        // Verify project ownership
        var project = await _db.QueryFirstOrDefaultAsync<Project>(
            "SELECT id FROM projects WHERE id = @ProjectId AND user_id = @UserId",
            new { ProjectId = projectId, UserId = userId });
        
        if (project == null) return new List<FileResponse>();

        var files = await _db.QueryAsync<ProjectFile>(
            "SELECT * FROM project_files WHERE project_id = @ProjectId ORDER BY path",
            new { ProjectId = projectId });
        return files.Select(MapToFileResponse).ToList();
    }

    public async Task<FileResponse?> GetFileAsync(string projectId, string fileId, string userId)
    {
        var file = await _db.QueryFirstOrDefaultAsync<ProjectFile>(
            @"SELECT pf.* FROM project_files pf 
              JOIN projects p ON pf.project_id = p.id 
              WHERE pf.id = @FileId AND pf.project_id = @ProjectId AND p.user_id = @UserId",
            new { FileId = fileId, ProjectId = projectId, UserId = userId });
        return file != null ? MapToFileResponse(file) : null;
    }

    public async Task<FileResponse> CreateFileAsync(string projectId, string userId, CreateFileRequest request)
    {
        var fileId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await _db.ExecuteAsync(@"
            INSERT INTO project_files (id, project_id, path, content, updated_at)
            VALUES (@Id, @ProjectId, @Path, @Content, @UpdatedAt)
            ON DUPLICATE KEY UPDATE content = @Content, updated_at = @UpdatedAt",
            new { Id = fileId, ProjectId = projectId, Path = request.Path, Content = request.Content, UpdatedAt = now });

        // Update project's updated_at
        await _db.ExecuteAsync(
            "UPDATE projects SET updated_at = @Now WHERE id = @ProjectId",
            new { Now = now, ProjectId = projectId });

        var file = await _db.QueryFirstOrDefaultAsync<ProjectFile>(
            "SELECT * FROM project_files WHERE project_id = @ProjectId AND path = @Path",
            new { ProjectId = projectId, Path = request.Path });

        return MapToFileResponse(file ?? new ProjectFile { Id = fileId, ProjectId = projectId, Path = request.Path, Content = request.Content, UpdatedAt = now });
    }

    public async Task<FileResponse?> UpdateFileAsync(string projectId, string fileId, string userId, UpdateFileRequest request)
    {
        var now = DateTime.UtcNow;
        
        var result = await _db.ExecuteAsync(@"
            UPDATE project_files pf
            JOIN projects p ON pf.project_id = p.id
            SET pf.content = @Content, pf.updated_at = @UpdatedAt
            WHERE pf.id = @FileId AND pf.project_id = @ProjectId AND p.user_id = @UserId",
            new { FileId = fileId, ProjectId = projectId, UserId = userId, Content = request.Content, UpdatedAt = now });
        
        if (result == 0) return null;

        await _db.ExecuteAsync(
            "UPDATE projects SET updated_at = @Now WHERE id = @ProjectId",
            new { Now = now, ProjectId = projectId });

        return await GetFileAsync(projectId, fileId, userId);
    }

    public async Task<bool> DeleteFileAsync(string projectId, string fileId, string userId)
    {
        var result = await _db.ExecuteAsync(@"
            DELETE pf FROM project_files pf
            JOIN projects p ON pf.project_id = p.id
            WHERE pf.id = @FileId AND pf.project_id = @ProjectId AND p.user_id = @UserId",
            new { FileId = fileId, ProjectId = projectId, UserId = userId });
        return result > 0;
    }

    public async Task<List<TodoResponse>> GetTodosAsync(string projectId, string userId)
    {
        // Verify project ownership
        var project = await _db.QueryFirstOrDefaultAsync<Project>(
            "SELECT id FROM projects WHERE id = @ProjectId AND user_id = @UserId",
            new { ProjectId = projectId, UserId = userId });
        
        if (project == null) return new List<TodoResponse>();

        var todos = await _db.QueryAsync<Todo>(
            "SELECT * FROM todos WHERE project_id = @ProjectId ORDER BY created_at DESC",
            new { ProjectId = projectId });
        return todos.Select(MapToTodoResponse).ToList();
    }

    public async Task<TodoResponse> CreateTodoAsync(string projectId, string userId, CreateTodoRequest request)
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            Text = request.Text,
            Priority = request.Priority,
            Completed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(@"
            INSERT INTO todos (id, project_id, text, priority, completed, created_at)
            VALUES (@Id, @ProjectId, @Text, @Priority, @Completed, @CreatedAt)",
            todo);

        return MapToTodoResponse(todo);
    }

    public async Task<TodoResponse?> UpdateTodoAsync(string projectId, string todoId, string userId, UpdateTodoRequest request)
    {
        var updates = new List<string>();
        var parameters = new Dictionary<string, object> 
        { 
            ["TodoId"] = todoId,
            ["ProjectId"] = projectId,
            ["UserId"] = userId
        };

        if (request.Completed.HasValue) { updates.Add("t.completed = @Completed"); parameters["Completed"] = request.Completed.Value; }
        if (request.Text != null) { updates.Add("t.text = @Text"); parameters["Text"] = request.Text; }
        if (request.Priority != null) { updates.Add("t.priority = @Priority"); parameters["Priority"] = request.Priority; }

        if (updates.Count == 0) return null;

        var result = await _db.ExecuteAsync($@"
            UPDATE todos t
            JOIN projects p ON t.project_id = p.id
            SET {string.Join(", ", updates)}
            WHERE t.id = @TodoId AND t.project_id = @ProjectId AND p.user_id = @UserId",
            parameters);
        
        if (result == 0) return null;

        var todo = await _db.QueryFirstOrDefaultAsync<Todo>(
            "SELECT * FROM todos WHERE id = @TodoId",
            new { TodoId = todoId });
        return todo != null ? MapToTodoResponse(todo) : null;
    }

    public async Task<bool> DeleteTodoAsync(string projectId, string todoId, string userId)
    {
        var result = await _db.ExecuteAsync(@"
            DELETE t FROM todos t
            JOIN projects p ON t.project_id = p.id
            WHERE t.id = @TodoId AND t.project_id = @ProjectId AND p.user_id = @UserId",
            new { TodoId = todoId, ProjectId = projectId, UserId = userId });
        return result > 0;
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(string projectId, string userId, int limit)
    {
        var messages = await _db.QueryAsync<ChatMessage>(
            "SELECT * FROM chat_history WHERE project_id = @ProjectId AND user_id = @UserId AND deleted_by_user = FALSE ORDER BY timestamp DESC LIMIT @Limit",
            new { ProjectId = projectId, UserId = userId, Limit = limit });
        return messages.Reverse().ToList();
    }

    public async Task<ChatMessage> SaveChatMessageAsync(ChatMessage message)
    {
        message.Id = Guid.NewGuid().ToString();
        message.Timestamp = DateTime.UtcNow;

        await _db.ExecuteAsync(@"
            INSERT INTO chat_history (id, user_id, project_id, conversation_id, conversation_title, role, content, 
                agent_id, provider, model, tokens_used, credits_deducted, multi_agent_mode, timestamp)
            VALUES (@Id, @UserId, @ProjectId, @ConversationId, @ConversationTitle, @Role, @Content,
                @AgentId, @Provider, @Model, @TokensUsed, @CreditsDeducted, @MultiAgentMode, @Timestamp)",
            message);

        return message;
    }

    public async Task<ProjectRun> CreateRunAsync(string projectId, string runType)
    {
        var run = new ProjectRun
        {
            Id = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            RunType = runType,
            Status = "pending",
            StartedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(@"
            INSERT INTO project_runs (id, project_id, run_type, status, started_at)
            VALUES (@Id, @ProjectId, @RunType, @Status, @StartedAt)",
            run);

        return run;
    }

    public async Task UpdateRunAsync(string runId, string status, string? output, List<string>? logs, List<string>? errors)
    {
        await _db.ExecuteAsync(@"
            UPDATE project_runs 
            SET status = @Status, output = @Output, logs = @Logs, errors = @Errors, ended_at = @EndedAt
            WHERE id = @RunId",
            new {
                RunId = runId,
                Status = status,
                Output = output,
                Logs = logs != null ? JsonSerializer.Serialize(logs) : null,
                Errors = errors != null ? JsonSerializer.Serialize(errors) : null,
                EndedAt = DateTime.UtcNow
            });
    }

    private static ProjectResponse MapToResponse(Project p) => new(
        p.Id, p.UserId, p.Name, p.Description, p.Language, 
        p.CreatedAt.ToString("o"), p.UpdatedAt.ToString("o"), p.Status
    );

    private static FileResponse MapToFileResponse(ProjectFile f) => new(
        f.Id, f.ProjectId, f.Path, f.Content, f.UpdatedAt.ToString("o")
    );

    private static TodoResponse MapToTodoResponse(Todo t) => new(
        t.Id, t.ProjectId, t.Text, t.Completed, t.Priority, t.CreatedAt.ToString("o")
    );
}
