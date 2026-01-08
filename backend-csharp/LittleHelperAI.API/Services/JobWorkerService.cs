// Background Job Worker Service - Updated to match interface
using LittleHelperAI.Agents;
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using LittleHelperAI.API.Controllers;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace LittleHelperAI.API.Services;

public class JobWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobWorkerService> _logger;

    public JobWorkerService(IServiceProvider serviceProvider, ILogger<JobWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job Worker Service starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
                var agentRegistry = scope.ServiceProvider.GetRequiredService<IAgentRegistry>();
                var creditService = scope.ServiceProvider.GetRequiredService<ICreditService>();

                // Get approved jobs that need processing
                var pendingJobs = await db.QueryAsync<Job>(
                    "SELECT * FROM jobs WHERE status = 'approved' ORDER BY created_at ASC LIMIT 5");

                foreach (var job in pendingJobs)
                {
                    await ProcessJobAsync(db, agentRegistry, creditService, job, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job worker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessJobAsync(
        IDbContext db,
        IAgentRegistry agentRegistry,
        ICreditService creditService,
        Job job,
        CancellationToken ct)
    {
        _logger.LogInformation("Processing job {JobId}", job.Id);

        try
        {
            // Mark as in progress
            await db.ExecuteAsync(
                "UPDATE jobs SET status = 'in_progress', started_at = @Now, updated_at = @Now WHERE id = @JobId",
                new { Now = DateTime.UtcNow, JobId = job.Id });

            // Get tasks from job
            var tasks = string.IsNullOrEmpty(job.Tasks)
                ? new List<TaskItem>()
                : JsonSerializer.Deserialize<List<TaskItem>>(job.Tasks) ?? new List<TaskItem>();

            // Process each task
            for (var i = Math.Max(0, job.CurrentTaskIndex); i < tasks.Count && !ct.IsCancellationRequested; i++)
            {
                var task = tasks[i];
                
                // Update current task
                await db.ExecuteAsync(
                    "UPDATE jobs SET current_task_index = @Index, updated_at = @Now WHERE id = @JobId",
                    new { Index = i, Now = DateTime.UtcNow, JobId = job.Id });

                var agent = agentRegistry.GetAgent(task.AgentType);

                if (agent != null)
                {
                    try
                    {
                        var result = await agent.ExecuteAsync(task.Description, null);
                        
                        tasks[i] = task with {
                            Status = result.Success ? "completed" : "failed",
                            ActualTokens = result.TokensUsed,
                            ActualCredits = result.TokensUsed * 0.001,
                            Output = result.Content,
                            FilesCreated = result.FilesCreated.Select(f => f.Path).ToList(),
                            Error = result.Errors.FirstOrDefault()
                        };

                        // Deduct credits
                        await creditService.DeductCreditsAsync(
                            job.UserId,
                            (decimal)tasks[i].ActualCredits,
                            $"Job {job.Id}: {task.Title}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Task {TaskId} in job {JobId} failed", task.Id, job.Id);
                        tasks[i] = task with {
                            Status = "failed",
                            Error = ex.Message
                        };
                    }
                }
                else
                {
                    tasks[i] = task with {
                        Status = "skipped",
                        Error = $"Agent type '{task.AgentType}' not found"
                    };
                }

                // Update tasks
                await db.ExecuteAsync(
                    "UPDATE jobs SET tasks = @Tasks, updated_at = @Now WHERE id = @JobId",
                    new { Tasks = JsonSerializer.Serialize(tasks), Now = DateTime.UtcNow, JobId = job.Id });
            }

            // Calculate final status
            var allCompleted = tasks.All(t => t.Status == "completed");
            var totalCreditsUsed = tasks.Sum(t => t.ActualCredits);

            // Mark as completed or failed
            await db.ExecuteAsync(@"
                UPDATE jobs 
                SET status = @Status,
                    tasks = @Tasks,
                    credits_used = @CreditsUsed,
                    updated_at = @Now,
                    completed_at = @Now
                WHERE id = @JobId",
                new { 
                    Status = allCompleted ? "completed" : "completed_with_errors",
                    Tasks = JsonSerializer.Serialize(tasks),
                    CreditsUsed = totalCreditsUsed,
                    Now = DateTime.UtcNow,
                    JobId = job.Id 
                });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed completely", job.Id);
            
            await db.ExecuteAsync(@"
                UPDATE jobs 
                SET status = 'failed',
                    error = @Error,
                    error_count = error_count + 1,
                    updated_at = @Now,
                    completed_at = @Now
                WHERE id = @JobId",
                new { Error = ex.Message, Now = DateTime.UtcNow, JobId = job.Id });
        }
    }
}
