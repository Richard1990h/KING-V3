// Job Orchestration Service Implementation - Updated to match interface
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using LittleHelperAI.API.Controllers;
using LittleHelperAI.Agents;
using System.Text.Json;

namespace LittleHelperAI.API.Services;

public class JobOrchestrationService : IJobOrchestrationService
{
    private readonly IDbContext _db;
    private readonly IAgentRegistry _agentRegistry;
    private readonly ICreditService _creditService;
    private readonly ILogger<JobOrchestrationService> _logger;

    public JobOrchestrationService(
        IDbContext db, 
        IAgentRegistry agentRegistry,
        ICreditService creditService,
        ILogger<JobOrchestrationService> logger)
    {
        _db = db;
        _agentRegistry = agentRegistry;
        _creditService = creditService;
        _logger = logger;
    }

    public async Task<JobResponse> CreateJobAsync(UserResponse user, CreateJobRequest request)
    {
        var jobId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        // Create initial tasks based on multi-agent mode
        var tasks = new List<TaskItem>();
        if (request.MultiAgentMode)
        {
            tasks = GenerateMultiAgentTasks(request.Prompt);
        }
        else
        {
            tasks.Add(new TaskItem(
                Guid.NewGuid().ToString(),
                "Single Agent Task",
                request.Prompt,
                "developer",
                0, "pending", 1000, 1.0,
                0, 0, null, new List<string>(), null
            ));
        }

        var totalCredits = tasks.Sum(t => t.EstimatedCredits);

        var job = new Job
        {
            Id = jobId,
            ProjectId = request.ProjectId,
            UserId = user.Id,
            Prompt = request.Prompt,
            Status = "awaiting_approval",
            MultiAgentMode = request.MultiAgentMode,
            Tasks = JsonSerializer.Serialize(tasks),
            TotalEstimatedCredits = (decimal)totalCredits,
            CurrentTaskIndex = -1,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _db.ExecuteAsync(@"
            INSERT INTO jobs (id, project_id, user_id, prompt, status, multi_agent_mode, tasks, 
                total_estimated_credits, current_task_index, created_at, updated_at)
            VALUES (@Id, @ProjectId, @UserId, @Prompt, @Status, @MultiAgentMode, @Tasks,
                @TotalEstimatedCredits, @CurrentTaskIndex, @CreatedAt, @UpdatedAt)",
            job);

        return MapToResponse(job, tasks);
    }

    public async Task<JobResponse?> GetJobAsync(string jobId, string userId)
    {
        var job = await _db.QueryFirstOrDefaultAsync<Job>(
            "SELECT * FROM jobs WHERE id = @JobId AND user_id = @UserId",
            new { JobId = jobId, UserId = userId });
        
        if (job == null) return null;

        var tasks = string.IsNullOrEmpty(job.Tasks) 
            ? new List<TaskItem>() 
            : JsonSerializer.Deserialize<List<TaskItem>>(job.Tasks) ?? new List<TaskItem>();

        return MapToResponse(job, tasks);
    }

    public async Task<List<JobResponse>> GetUserJobsAsync(string userId, int limit)
    {
        var jobs = await _db.QueryAsync<Job>(
            "SELECT * FROM jobs WHERE user_id = @UserId ORDER BY created_at DESC LIMIT @Limit",
            new { UserId = userId, Limit = limit });
        
        return jobs.Select(j => {
            var tasks = string.IsNullOrEmpty(j.Tasks) 
                ? new List<TaskItem>() 
                : JsonSerializer.Deserialize<List<TaskItem>>(j.Tasks) ?? new List<TaskItem>();
            return MapToResponse(j, tasks);
        }).ToList();
    }

    public async Task<List<Job>> GetRunningJobsAsync()
    {
        var jobs = await _db.QueryAsync<Job>(
            "SELECT * FROM jobs WHERE status IN ('in_progress', 'analyzing', 'awaiting_approval') ORDER BY created_at DESC");
        return jobs.ToList();
    }

    public async Task<ApproveJobResult> ApproveJobAsync(string jobId, UserResponse user, ApproveJobRequest request)
    {
        var job = await _db.QueryFirstOrDefaultAsync<Job>(
            "SELECT * FROM jobs WHERE id = @JobId AND user_id = @UserId",
            new { JobId = jobId, UserId = user.Id });

        if (job == null)
            return new ApproveJobResult(false, "Job not found", null);

        if (!request.Approved)
        {
            await _db.ExecuteAsync(
                "UPDATE jobs SET status = 'cancelled', updated_at = @Now, completed_at = @Now WHERE id = @JobId",
                new { Now = DateTime.UtcNow, JobId = jobId });
            return new ApproveJobResult(true, null, await GetJobAsync(jobId, user.Id));
        }

        // Update tasks if modified
        var tasks = request.ModifiedTasks ?? 
            (string.IsNullOrEmpty(job.Tasks) 
                ? new List<TaskItem>() 
                : JsonSerializer.Deserialize<List<TaskItem>>(job.Tasks) ?? new List<TaskItem>());

        var totalCredits = tasks.Sum(t => t.EstimatedCredits);

        await _db.ExecuteAsync(@"
            UPDATE jobs 
            SET status = 'approved', 
                tasks = @Tasks,
                total_estimated_credits = @TotalCredits,
                credits_approved = @TotalCredits,
                updated_at = @Now 
            WHERE id = @JobId",
            new { 
                Tasks = JsonSerializer.Serialize(tasks),
                TotalCredits = totalCredits,
                Now = DateTime.UtcNow,
                JobId = jobId 
            });

        return new ApproveJobResult(true, null, await GetJobAsync(jobId, user.Id));
    }

    public async Task<object> ContinueJobAsync(string jobId, UserResponse user, bool approved)
    {
        if (!approved)
        {
            await _db.ExecuteAsync(
                "UPDATE jobs SET status = 'paused', updated_at = @Now WHERE id = @JobId",
                new { Now = DateTime.UtcNow, JobId = jobId });
            return new { status = "paused", message = "Job paused" };
        }

        await _db.ExecuteAsync(
            "UPDATE jobs SET status = 'in_progress', updated_at = @Now WHERE id = @JobId",
            new { Now = DateTime.UtcNow, JobId = jobId });

        return new { status = "continued", message = "Job continuing" };
    }

    public async IAsyncEnumerable<object> ExecuteJobAsync(string jobId, UserResponse user)
    {
        var job = await _db.QueryFirstOrDefaultAsync<Job>(
            "SELECT * FROM jobs WHERE id = @JobId AND user_id = @UserId",
            new { JobId = jobId, UserId = user.Id });

        if (job == null)
        {
            yield return new { type = "error", message = "Job not found" };
            yield break;
        }

        // Update status to in_progress
        await _db.ExecuteAsync(
            "UPDATE jobs SET status = 'in_progress', started_at = @Now, updated_at = @Now WHERE id = @JobId",
            new { Now = DateTime.UtcNow, JobId = jobId });

        yield return new { type = "status", status = "started", message = "Job execution started" };

        var tasks = string.IsNullOrEmpty(job.Tasks) 
            ? new List<TaskItem>() 
            : JsonSerializer.Deserialize<List<TaskItem>>(job.Tasks) ?? new List<TaskItem>();

        for (int i = 0; i < tasks.Count; i++)
        {
            var task = tasks[i];
            
            yield return new { 
                type = "task_start", 
                task_index = i, 
                task = new { task.Id, task.Title, task.AgentType }
            };

            // Update current task index
            await _db.ExecuteAsync(
                "UPDATE jobs SET current_task_index = @Index, updated_at = @Now WHERE id = @JobId",
                new { Index = i, Now = DateTime.UtcNow, JobId = jobId });

            try
            {
                // Get the appropriate agent
                var agent = _agentRegistry.GetAgent(task.AgentType);
                
                // Execute the agent
                var result = await agent.ExecuteAsync(task.Description);

                // Update task with results
                tasks[i] = task with {
                    Status = result.Success ? "completed" : "failed",
                    ActualTokens = result.TokensUsed,
                    ActualCredits = result.TokensUsed * 0.001, // Simple credit calculation
                    Output = result.Content,
                    FilesCreated = result.FilesCreated.Select(f => f.Path).ToList(),
                    Error = result.Errors.FirstOrDefault()
                };

                yield return new { 
                    type = "task_complete",
                    task_index = i,
                    success = result.Success,
                    output_preview = result.Content.Length > 200 ? result.Content[..200] + "..." : result.Content,
                    files_created = result.FilesCreated.Select(f => f.Path).ToList()
                };

                // Deduct credits
                if (user.CreditsEnabled)
                {
                    await _creditService.DeductCreditsAsync(user.Id, (decimal)tasks[i].ActualCredits, $"Job task: {task.Title}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Task execution failed for task {TaskId}", task.Id);
                
                tasks[i] = task with {
                    Status = "failed",
                    Error = ex.Message
                };

                yield return new { 
                    type = "task_error",
                    task_index = i,
                    error = ex.Message
                };
            }
        }

        // Update job with final results
        var totalActualCredits = tasks.Sum(t => t.ActualCredits);
        var allCompleted = tasks.All(t => t.Status == "completed");

        await _db.ExecuteAsync(@"
            UPDATE jobs 
            SET status = @Status, 
                tasks = @Tasks,
                credits_used = @CreditsUsed,
                updated_at = @Now,
                completed_at = @Now
            WHERE id = @JobId",
            new { 
                Status = allCompleted ? "completed" : "failed",
                Tasks = JsonSerializer.Serialize(tasks),
                CreditsUsed = totalActualCredits,
                Now = DateTime.UtcNow,
                JobId = jobId
            });

        yield return new { 
            type = "complete", 
            status = allCompleted ? "completed" : "completed_with_errors",
            total_credits_used = totalActualCredits
        };
    }

    private static List<TaskItem> GenerateMultiAgentTasks(string prompt)
    {
        // Generate a standard multi-agent pipeline
        return new List<TaskItem>
        {
            new(Guid.NewGuid().ToString(), "Analyze Requirements", 
                $"Analyze and plan implementation for: {prompt}", 
                "planner", 0, "pending", 500, 0.5, 0, 0, null, new List<string>(), null),
            new(Guid.NewGuid().ToString(), "Research Best Practices",
                $"Research best practices and patterns for: {prompt}",
                "researcher", 1, "pending", 800, 0.8, 0, 0, null, new List<string>(), null),
            new(Guid.NewGuid().ToString(), "Implement Solution",
                $"Implement the solution for: {prompt}",
                "developer", 2, "pending", 2000, 2.0, 0, 0, null, new List<string>(), null),
            new(Guid.NewGuid().ToString(), "Design Tests",
                $"Design test cases for: {prompt}",
                "test_designer", 3, "pending", 600, 0.6, 0, 0, null, new List<string>(), null),
            new(Guid.NewGuid().ToString(), "Verify Implementation",
                $"Verify the implementation meets requirements for: {prompt}",
                "verifier", 4, "pending", 400, 0.4, 0, 0, null, new List<string>(), null)
        };
    }

    private static JobResponse MapToResponse(Job job, List<TaskItem> tasks)
    {
        var creditsRemaining = (double)(job.CreditsApproved - job.CreditsUsed);
        return new JobResponse(
            job.Id,
            job.ProjectId,
            job.UserId,
            job.Prompt,
            job.Status,
            tasks,
            (double)job.TotalEstimatedCredits,
            (double)job.CreditsUsed,
            creditsRemaining > 0 ? creditsRemaining : 0,
            job.CurrentTaskIndex,
            job.CreatedAt.ToString("o"),
            job.UpdatedAt.ToString("o")
        );
    }
}
