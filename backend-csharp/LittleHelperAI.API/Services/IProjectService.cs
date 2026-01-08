// Project Service Interface - Updated with all methods
using LittleHelperAI.Data.Models;
using LittleHelperAI.API.Controllers;

namespace LittleHelperAI.API.Services;

public interface IProjectService
{
    // Projects
    Task<List<ProjectResponse>> GetUserProjectsAsync(string userId);
    Task<ProjectResponse?> GetProjectAsync(string projectId, string userId);
    Task<ProjectResponse> CreateProjectAsync(string userId, CreateProjectRequest request);
    Task<ProjectResponse?> UpdateProjectAsync(string projectId, string userId, UpdateProjectRequest request);
    Task<bool> DeleteProjectAsync(string projectId, string userId);
    Task<object?> ExportProjectAsync(string projectId, string userId);
    Task<object> BuildProjectAsync(string projectId, string userId);
    Task<object> RunProjectAsync(string projectId, string userId);
    
    // Project Files
    Task<List<FileResponse>> GetProjectFilesAsync(string projectId, string userId);
    Task<FileResponse?> GetFileAsync(string projectId, string fileId, string userId);
    Task<FileResponse> CreateFileAsync(string projectId, string userId, CreateFileRequest request);
    Task<FileResponse?> UpdateFileAsync(string projectId, string fileId, string userId, UpdateFileRequest request);
    Task<bool> DeleteFileAsync(string projectId, string fileId, string userId);
    
    // Todos
    Task<List<TodoResponse>> GetTodosAsync(string projectId, string userId);
    Task<TodoResponse> CreateTodoAsync(string projectId, string userId, CreateTodoRequest request);
    Task<TodoResponse?> UpdateTodoAsync(string projectId, string todoId, string userId, UpdateTodoRequest request);
    Task<bool> DeleteTodoAsync(string projectId, string todoId, string userId);
    
    // Chat
    Task<List<ChatMessage>> GetChatHistoryAsync(string projectId, string userId, int limit);
    Task<ChatMessage> SaveChatMessageAsync(ChatMessage message);
    
    // Project Runs
    Task<ProjectRun> CreateRunAsync(string projectId, string runType);
    Task UpdateRunAsync(string runId, string status, string? output, List<string>? logs, List<string>? errors);
}
