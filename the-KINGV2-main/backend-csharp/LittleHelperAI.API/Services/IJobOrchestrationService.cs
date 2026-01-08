// Job Orchestration Service Interface
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Services;

public interface IJobOrchestrationService
{
    Task<Job> CreateJobAsync(string projectId, string userId, string prompt, bool multiAgentMode);
    Task<Job?> GetJobAsync(string jobId);
    Task<List<Job>> GetProjectJobsAsync(string projectId);
    Task<List<Job>> GetRunningJobsAsync();
    Task<Job?> ApproveJobAsync(string jobId, string userId, decimal approvedCredits);
    Task<Job?> CancelJobAsync(string jobId, string userId);
    Task<Job?> AdvanceJobAsync(string jobId);
    Task<Job?> CompleteJobAsync(string jobId);
    Task<Job?> FailJobAsync(string jobId, string error);
}
