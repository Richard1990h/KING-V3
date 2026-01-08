// Job Orchestration Service Implementation
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using System.Text.Json;

namespace LittleHelperAI.API.Services;

public class JobOrchestrationService : IJobOrchestrationService
{
    private readonly IDbContext _db;
    private readonly ILogger<JobOrchestrationService> _logger;

    public JobOrchestrationService(IDbContext db, ILogger<JobOrchestrationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<Job> CreateJobAsync(string projectId, string userId, string prompt, bool multiAgentMode)
    {
        var job = new Job
        {
            Id = Guid.NewGuid().ToString(),
            ProjectId = projectId,
            UserId = userId,
            Prompt = prompt,
            Status = "pending",
            MultiAgentMode = multiAgentMode,
            CurrentTaskIndex = -1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.ExecuteAsync(@"
            INSERT INTO jobs (id, project_id, user_id, prompt, status, multi_agent_mode, current_task_index, created_at, updated_at)
            VALUES (@Id, @ProjectId, @UserId, @Prompt, @Status, @MultiAgentMode, @CurrentTaskIndex, @CreatedAt, @UpdatedAt)",
            job);

        return job;
    }

    public async Task<Job?> GetJobAsync(string jobId)
    {
        return await _db.QueryFirstOrDefaultAsync<Job>(
            "SELECT * FROM jobs WHERE id = @JobId",
            new { JobId = jobId });
    }

    public async Task<List<Job>> GetProjectJobsAsync(string projectId)
    {
        var jobs = await _db.QueryAsync<Job>(
            "SELECT * FROM jobs WHERE project_id = @ProjectId ORDER BY created_at DESC",
            new { ProjectId = projectId });
        return jobs.ToList();
    }

    public async Task<List<Job>> GetRunningJobsAsync()
    {
        var jobs = await _db.QueryAsync<Job>(
            "SELECT * FROM jobs WHERE status IN ('in_progress', 'analyzing', 'awaiting_approval') ORDER BY created_at DESC");
        return jobs.ToList();
    }

    public async Task<Job?> ApproveJobAsync(string jobId, string userId, decimal approvedCredits)
    {
        var job = await GetJobAsync(jobId);
        if (job == null || job.UserId != userId) return null;

        await _db.ExecuteAsync(@"
            UPDATE jobs SET status = 'approved', credits_approved = @Credits, updated_at = @Now WHERE id = @JobId",
            new { Credits = approvedCredits, Now = DateTime.UtcNow, JobId = jobId });

        return await GetJobAsync(jobId);
    }

    public async Task<Job?> CancelJobAsync(string jobId, string userId)
    {
        var job = await GetJobAsync(jobId);
        if (job == null || job.UserId != userId) return null;

        await _db.ExecuteAsync(@"
            UPDATE jobs SET status = 'cancelled', updated_at = @Now, completed_at = @Now WHERE id = @JobId",
            new { Now = DateTime.UtcNow, JobId = jobId });

        return await GetJobAsync(jobId);
    }

    public async Task<Job?> AdvanceJobAsync(string jobId)
    {
        var job = await GetJobAsync(jobId);
        if (job == null) return null;

        var newIndex = job.CurrentTaskIndex + 1;

        await _db.ExecuteAsync(@"
            UPDATE jobs SET current_task_index = @Index, status = 'in_progress', updated_at = @Now WHERE id = @JobId",
            new { Index = newIndex, Now = DateTime.UtcNow, JobId = jobId });

        return await GetJobAsync(jobId);
    }

    public async Task<Job?> CompleteJobAsync(string jobId)
    {
        await _db.ExecuteAsync(@"
            UPDATE jobs SET status = 'completed', updated_at = @Now, completed_at = @Now WHERE id = @JobId",
            new { Now = DateTime.UtcNow, JobId = jobId });

        return await GetJobAsync(jobId);
    }

    public async Task<Job?> FailJobAsync(string jobId, string error)
    {
        var job = await GetJobAsync(jobId);
        if (job == null) return null;

        await _db.ExecuteAsync(@"
            UPDATE jobs SET status = 'failed', error = @Error, error_count = error_count + 1, updated_at = @Now, completed_at = @Now WHERE id = @JobId",
            new { Error = error, Now = DateTime.UtcNow, JobId = jobId });

        return await GetJobAsync(jobId);
    }
}
