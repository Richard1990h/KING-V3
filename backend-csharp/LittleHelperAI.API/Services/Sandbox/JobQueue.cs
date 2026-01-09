// Async Job Queue Service
// Background workers for long-running AI jobs
using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LittleHelperAI.API.Services.Sandbox;

public interface IJobQueue
{
    Task<string> EnqueueAsync(PipelineJob job);
    Task<PipelineJob?> GetJobAsync(string jobId);
    Task<JobStatus> GetJobStatusAsync(string jobId);
    Task<List<PipelineJob>> GetUserJobsAsync(string userId, int limit = 20);
    Task CancelJobAsync(string jobId);
    Task<PipelineResult?> GetJobResultAsync(string jobId);
}

public class JobQueue : IJobQueue
{
    private readonly Channel<PipelineJob> _jobChannel;
    private readonly ConcurrentDictionary<string, PipelineJob> _jobs = new();
    private readonly ConcurrentDictionary<string, PipelineResult> _results = new();
    private readonly ILogger<JobQueue> _logger;

    public JobQueue(ILogger<JobQueue> logger)
    {
        _logger = logger;
        _jobChannel = Channel.CreateBounded<PipelineJob>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    public ChannelReader<PipelineJob> Reader => _jobChannel.Reader;

    public async Task<string> EnqueueAsync(PipelineJob job)
    {
        job.Id = Guid.NewGuid().ToString();
        job.Status = JobStatus.Queued;
        job.CreatedAt = DateTime.UtcNow;
        job.QueuePosition = _jobs.Count(j => j.Value.Status == JobStatus.Queued) + 1;

        _jobs[job.Id] = job;

        await _jobChannel.Writer.WriteAsync(job);

        _logger.LogInformation("Job {JobId} enqueued for project {ProjectId}, queue position: {Position}",
            job.Id, job.Request.ProjectId, job.QueuePosition);

        return job.Id;
    }

    public Task<PipelineJob?> GetJobAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<JobStatus> GetJobStatusAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            return Task.FromResult(job.Status);
        }
        return Task.FromResult(JobStatus.NotFound);
    }

    public Task<List<PipelineJob>> GetUserJobsAsync(string userId, int limit = 20)
    {
        var jobs = _jobs.Values
            .Where(j => j.Request.UserId == userId)
            .OrderByDescending(j => j.CreatedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult(jobs);
    }

    public Task CancelJobAsync(string jobId)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            if (job.Status == JobStatus.Queued || job.Status == JobStatus.Running)
            {
                job.Status = JobStatus.Cancelled;
                job.CancellationTokenSource?.Cancel();
                _logger.LogInformation("Job {JobId} cancelled", jobId);
            }
        }
        return Task.CompletedTask;
    }

    public Task<PipelineResult?> GetJobResultAsync(string jobId)
    {
        _results.TryGetValue(jobId, out var result);
        return Task.FromResult(result);
    }

    public void UpdateJobStatus(string jobId, JobStatus status, string? message = null)
    {
        if (_jobs.TryGetValue(jobId, out var job))
        {
            job.Status = status;
            job.StatusMessage = message;

            if (status == JobStatus.Running)
            {
                job.StartedAt = DateTime.UtcNow;
            }
            else if (status == JobStatus.Completed || status == JobStatus.Failed || status == JobStatus.Cancelled)
            {
                job.CompletedAt = DateTime.UtcNow;
            }
        }
    }

    public void StoreResult(string jobId, PipelineResult result)
    {
        _results[jobId] = result;

        // Clean up old results (keep for 24 hours)
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var oldResults = _results.Where(r =>
        {
            return _jobs.TryGetValue(r.Key, out var job) && job.CompletedAt < cutoff;
        }).Select(r => r.Key).ToList();

        foreach (var key in oldResults)
        {
            _results.TryRemove(key, out _);
            _jobs.TryRemove(key, out _);
        }
    }
}

// Background worker that processes jobs
public class JobWorker : BackgroundService
{
    private readonly JobQueue _jobQueue;
    private readonly IAgentPipeline _pipeline;
    private readonly ILogger<JobWorker> _logger;
    private readonly int _workerCount;

    public JobWorker(
        JobQueue jobQueue,
        IAgentPipeline pipeline,
        ILogger<JobWorker> logger,
        int workerCount = 3)
    {
        _jobQueue = jobQueue;
        _pipeline = pipeline;
        _logger = logger;
        _workerCount = workerCount;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job worker starting with {WorkerCount} workers", _workerCount);

        // Start multiple worker tasks
        var workers = Enumerable.Range(0, _workerCount)
            .Select(i => ProcessJobsAsync(i, stoppingToken))
            .ToArray();

        await Task.WhenAll(workers);
    }

    private async Task ProcessJobsAsync(int workerId, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker {WorkerId} started", workerId);

        await foreach (var job in _jobQueue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} failed to process job {JobId}", workerId, job.Id);
            }
        }

        _logger.LogInformation("Worker {WorkerId} stopped", workerId);
    }

    private async Task ProcessJobAsync(PipelineJob job, CancellationToken stoppingToken)
    {
        _logger.LogInformation("Processing job {JobId} for project {ProjectId}", job.Id, job.Request.ProjectId);

        job.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        _jobQueue.UpdateJobStatus(job.Id, JobStatus.Running);

        try
        {
            var result = await _pipeline.ExecutePipelineAsync(job.Request, job.CancellationTokenSource.Token);

            _jobQueue.StoreResult(job.Id, result);

            var finalStatus = result.Status switch
            {
                PipelineStatus.Success => JobStatus.Completed,
                PipelineStatus.Cancelled => JobStatus.Cancelled,
                _ => JobStatus.Failed
            };

            _jobQueue.UpdateJobStatus(job.Id, finalStatus, result.ErrorMessage);

            _logger.LogInformation("Job {JobId} completed with status {Status}", job.Id, finalStatus);

            // Notify via webhook if configured
            if (!string.IsNullOrEmpty(job.WebhookUrl))
            {
                await NotifyWebhookAsync(job, result);
            }
        }
        catch (OperationCanceledException)
        {
            _jobQueue.UpdateJobStatus(job.Id, JobStatus.Cancelled, "Job was cancelled");
            _logger.LogInformation("Job {JobId} was cancelled", job.Id);
        }
        catch (Exception ex)
        {
            _jobQueue.UpdateJobStatus(job.Id, JobStatus.Failed, ex.Message);
            _logger.LogError(ex, "Job {JobId} failed with exception", job.Id);
        }
        finally
        {
            job.CancellationTokenSource?.Dispose();
        }
    }

    private async Task NotifyWebhookAsync(PipelineJob job, PipelineResult result)
    {
        try
        {
            using var client = new HttpClient();
            var payload = new
            {
                job_id = job.Id,
                project_id = job.Request.ProjectId,
                status = result.Status.ToString(),
                success = result.Status == PipelineStatus.Success,
                iterations = result.Iterations,
                duration_ms = result.TotalDuration?.TotalMilliseconds,
                error = result.ErrorMessage
            };

            var json = System.Text.Json.JsonSerializer.Serialize(payload);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            await client.PostAsync(job.WebhookUrl, content);

            _logger.LogInformation("Webhook notified for job {JobId}", job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify webhook for job {JobId}", job.Id);
        }
    }
}

// Models
public class PipelineJob
{
    public string Id { get; set; } = "";
    public PipelineRequest Request { get; set; } = new();
    public JobStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    public int QueuePosition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? WebhookUrl { get; set; }
    public CancellationTokenSource? CancellationTokenSource { get; set; }

    public JobProgress GetProgress()
    {
        return new JobProgress
        {
            JobId = Id,
            Status = Status,
            StatusMessage = StatusMessage,
            QueuePosition = QueuePosition,
            CreatedAt = CreatedAt,
            StartedAt = StartedAt,
            CompletedAt = CompletedAt,
            ElapsedSeconds = StartedAt.HasValue
                ? (int)(DateTime.UtcNow - StartedAt.Value).TotalSeconds
                : 0
        };
    }
}

public class JobProgress
{
    public string JobId { get; set; } = "";
    public JobStatus Status { get; set; }
    public string? StatusMessage { get; set; }
    public int QueuePosition { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int ElapsedSeconds { get; set; }
}

public enum JobStatus
{
    NotFound,
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

// Job Request DTO
public class CreateJobRequest
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "python";
    public string? Prompt { get; set; }
    public List<FileInput>? Files { get; set; }
    public string? EntryPoint { get; set; }
    public bool RunAfterBuild { get; set; }
    public string? WebhookUrl { get; set; }
}

public class FileInput
{
    public string Path { get; set; } = "";
    public string Content { get; set; } = "";
}
