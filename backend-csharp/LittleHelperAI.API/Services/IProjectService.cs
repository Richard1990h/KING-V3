// Project Service Interface
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Services;

public interface IProjectService
{
    Task<List<Project>> GetProjectsAsync(string userId);
    Task<Project?> GetProjectByIdAsync(string projectId, string userId);
    Task<Project> CreateProjectAsync(string userId, string name, string? description, string language);
    Task<bool> UpdateProjectAsync(string projectId, string userId, string? name, string? description);
    Task<bool> DeleteProjectAsync(string projectId, string userId);
    
    // Project Files
    Task<List<ProjectFile>> GetProjectFilesAsync(string projectId);
    Task<ProjectFile?> GetProjectFileAsync(string projectId, string fileId);
    Task<ProjectFile> CreateOrUpdateFileAsync(string projectId, string path, string content);
    Task<bool> DeleteProjectFileAsync(string projectId, string fileId);
    
    // Chat
    Task<List<ChatMessage>> GetChatHistoryAsync(string projectId, string userId, int limit);
    Task<ChatMessage> SaveChatMessageAsync(ChatMessage message);
    
    // Todos
    Task<List<Todo>> GetTodosAsync(string projectId);
    Task<Todo> CreateTodoAsync(string projectId, string text, string? priority, string? agent);
    Task<bool> UpdateTodoAsync(string todoId, bool? completed, string? text);
    Task<bool> DeleteTodoAsync(string todoId);
    
    // Project Runs
    Task<ProjectRun> CreateRunAsync(string projectId, string runType);
    Task UpdateRunAsync(string runId, string status, string? output, List<string>? logs, List<string>? errors);
}
