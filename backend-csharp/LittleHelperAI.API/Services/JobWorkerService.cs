// Background Job Worker Service
using LittleHelperAI.Agents;
using LittleHelperAI.Data;
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
                var jobService = scope.ServiceProvider.GetRequiredService<IJobOrchestrationService>();
                var db = scope.ServiceProvider.GetRequiredService<IDbContext>();

                // Get approved jobs that need processing
                var pendingJobs = await db.QueryAsync<LittleHelperAI.Data.Models.Job>(
                    "SELECT * FROM jobs WHERE status = 'approved' ORDER BY created_at ASC LIMIT 5");

                foreach (var job in pendingJobs)
                {
                    await ProcessJobAsync(scope.ServiceProvider, job, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in job worker loop");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    private async Task ProcessJobAsync(IServiceProvider services, LittleHelperAI.Data.Models.Job job, CancellationToken ct)
    {
        _logger.LogInformation("Processing job {JobId}", job.Id);

        var jobService = services.GetRequiredService<IJobOrchestrationService>();
        var agentRegistry = services.GetRequiredService<IAgentRegistry>();

        try
        {
            // Mark as in progress
            await jobService.AdvanceJobAsync(job.Id);

            // Get tasks from job
            var tasks = string.IsNullOrEmpty(job.Tasks)
                ? new List<JobTask>()
                : JsonSerializer.Deserialize<List<JobTask>>(job.Tasks) ?? new List<JobTask>();

            // Process each task
            for (var i = job.CurrentTaskIndex; i < tasks.Count && !ct.IsCancellationRequested; i++)
            {
                var task = tasks[i];
                var agent = agentRegistry.GetAgent(task.AgentType);

                if (agent != null)
                {
                    var result = await agent.ExecuteAsync(task.Description, null);
                    task.Status = result.Success ? "completed" : "failed";
                    task.Output = result.Content;
                }

                await jobService.AdvanceJobAsync(job.Id);
            }

            // Mark as completed
            await jobService.CompleteJobAsync(job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Job {JobId} failed", job.Id);
            await jobService.FailJobAsync(job.Id, ex.Message);
        }
    }
}

public class JobTask
{
    public string Id { get; set; } = "";
    public string AgentType { get; set; } = "";
    public string Description { get; set; } = "";
    public string Status { get; set; } = "pending";
    public string? Output { get; set; }
}
