// Job Orchestration Service Interface - Updated to match controller
using LittleHelperAI.Data.Models;
using LittleHelperAI.API.Controllers;

namespace LittleHelperAI.API.Services;

public interface IJobOrchestrationService
{
    Task<JobResponse> CreateJobAsync(UserResponse user, CreateJobRequest request);
    Task<JobResponse?> GetJobAsync(string jobId, string userId);
    Task<List<JobResponse>> GetUserJobsAsync(string userId, int limit);
    Task<List<Job>> GetRunningJobsAsync();
    Task<ApproveJobResult> ApproveJobAsync(string jobId, UserResponse user, ApproveJobRequest request);
    Task<object> ContinueJobAsync(string jobId, UserResponse user, bool approved);
    IAsyncEnumerable<object> ExecuteJobAsync(string jobId, UserResponse user);
}

public record ApproveJobResult(bool Success, string? Error, JobResponse? Job);
