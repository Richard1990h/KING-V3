// Project Service Implementation
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;

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

    public async Task<List<Project>> GetProjectsAsync(string userId)
    {
        var projects = await _db.QueryAsync<Project>(
            "SELECT * FROM projects WHERE user_id = @UserId AND status != 'deleted' ORDER BY updated_at DESC",
            new { UserId = userId });
        return projects.ToList();
    }

    public async Task<Project?> GetProjectByIdAsync(string projectId, string userId)
    {
        return await _db.QueryFirstOrDefaultAsync<Project>(
            "SELECT * FROM projects WHERE id = @ProjectId AND user_id = @UserId AND status != 'deleted'",
            new { ProjectId = projectId, UserId = userId });
    }

    public async Task<Project> CreateProjectAsync(string userId, string name, string? description, string language)
    {
        var project = new Project
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            Name = name,
            Description = description ?? "",
            Language = language,
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(@"
            INSERT INTO projects (id, user_id, name, description, language, status, created_at, updated_at)
            VALUES (@Id, @UserId, @Name, @Description, @Language, @Status, @CreatedAt, @UpdatedAt)",
            project);

        return project;
    }

    public async Task<bool> UpdateProjectAsync(string projectId, string userId, string? name, string? description)
    {
        var updates = new List<string> { "updated_at = @UpdatedAt" };
        var parameters = new Dictionary<string, object>
        {
            ["ProjectId"] = projectId,
            ["UserId"] = userId,
            ["UpdatedAt"] = DateTime.UtcNow
        };

        if (name != null) { updates.Add("name = @Name"); parameters["Name"] = name; }
        if (description != null) { updates.Add("description = @Description"); parameters["Description"] = description; }

        var result = await _db.ExecuteAsync(
            $"UPDATE projects SET {string.Join(", ", updates)} WHERE id = @ProjectId AND user_id = @UserId",
            parameters);
        return result > 0;
    }

    public async Task<bool> DeleteProjectAsync(string projectId, string userId)
    {
        var result = await _db.ExecuteAsync(
            "UPDATE projects SET status = 'deleted', updated_at = @Now WHERE id = @ProjectId AND user_id = @UserId",
            new { Now = DateTime.UtcNow, ProjectId = projectId, UserId = userId });
        return result > 0;
    }

    public async Task<List<ProjectFile>> GetProjectFilesAsync(string projectId)
    {
        var files = await _db.QueryAsync<ProjectFile>(
            "SELECT * FROM project_files WHERE project_id = @ProjectId ORDER BY path",
            new { ProjectId = projectId });
        return files.ToList();
    }

    public async Task<ProjectFile?> GetProjectFileAsync(string projectId, string fileId)
    {
        return await _db.QueryFirstOrDefaultAsync<ProjectFile>(
            "SELECT * FROM project_files WHERE id = @FileId AND project_id = @ProjectId",
            new { FileId = fileId, ProjectId = projectId });
    }

    public async Task<ProjectFile> CreateOrUpdateFileAsync(string projectId, string path, string content)
    {
        var fileId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        await _db.ExecuteAsync(@"
            INSERT INTO project_files (id, project_id, path, content, updated_at)
            VALUES (@Id, @ProjectId, @Path, @Content, @UpdatedAt)
            ON DUPLICATE KEY UPDATE content = @Content, updated_at = @UpdatedAt",
            new { Id = fileId, ProjectId = projectId, Path = path, Content = content, UpdatedAt = now });

        // Update project's updated_at
        await _db.ExecuteAsync(
            "UPDATE projects SET updated_at = @Now WHERE id = @ProjectId",
            new { Now = now, ProjectId = projectId });

        return await _db.QueryFirstOrDefaultAsync<ProjectFile>(
            "SELECT * FROM project_files WHERE project_id = @ProjectId AND path = @Path",
            new { ProjectId = projectId, Path = path }) ?? new ProjectFile { Id = fileId, ProjectId = projectId, Path = path, Content = content };
    }

    public async Task<bool> DeleteProjectFileAsync(string projectId, string fileId)
    {
        var result = await _db.ExecuteAsync(
            "DELETE FROM project_files WHERE id = @FileId AND project_id = @ProjectId",
            new { FileId = fileId, ProjectId = projectId });
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

    public async Task<List<Todo>> GetTodosAsync(string projectId)
    {
        var todos = await _db.QueryAsync<Todo>(
            "SELECT * FROM todos WHERE project_id = @ProjectId ORDER BY created_at DESC",
            new { ProjectId = projectId });
        return todos.ToList();
    }

    public async Task<Todo> CreateTodoAsync(string projectId, string text, string? priority, string? agent)
    {
        var todo = new Todo
        {
            Id = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            Text = text,
            Priority = priority ?? "medium",
            Agent = agent,
            Completed = false,
            CreatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(@"
            INSERT INTO todos (id, project_id, text, priority, completed, created_at)
            VALUES (@Id, @ProjectId, @Text, @Priority, @Completed, @CreatedAt)",
            todo);

        return todo;
    }

    public async Task<bool> UpdateTodoAsync(string todoId, bool? completed, string? text)
    {
        var updates = new List<string>();
        var parameters = new Dictionary<string, object> { ["TodoId"] = todoId };

        if (completed.HasValue) { updates.Add("completed = @Completed"); parameters["Completed"] = completed.Value; }
        if (text != null) { updates.Add("text = @Text"); parameters["Text"] = text; }

        if (updates.Count == 0) return true;

        var result = await _db.ExecuteAsync(
            $"UPDATE todos SET {string.Join(", ", updates)} WHERE id = @TodoId",
            parameters);
        return result > 0;
    }

    public async Task<bool> DeleteTodoAsync(string todoId)
    {
        var result = await _db.ExecuteAsync(
            "DELETE FROM todos WHERE id = @TodoId",
            new { TodoId = todoId });
        return result > 0;
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
                Logs = logs != null ? System.Text.Json.JsonSerializer.Serialize(logs) : null,
                Errors = errors != null ? System.Text.Json.JsonSerializer.Serialize(errors) : null,
                EndedAt = DateTime.UtcNow
            });
    }
}
