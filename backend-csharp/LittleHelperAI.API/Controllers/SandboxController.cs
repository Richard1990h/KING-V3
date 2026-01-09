// Sandbox Execution Controller
// API endpoints for code execution, testing, and job management
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services.Sandbox;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SandboxController : ControllerBase
{
    private readonly IAgentPipeline _pipeline;
    private readonly ISandboxExecutor _sandbox;
    private readonly IStaticAnalyzer _staticAnalyzer;
    private readonly ITestGenerator _testGenerator;
    private readonly IVerificationGate _verificationGate;
    private readonly IRateLimiter _rateLimiter;
    private readonly IJobQueue _jobQueue;
    private readonly ILogger<SandboxController> _logger;

    public SandboxController(
        IAgentPipeline pipeline,
        ISandboxExecutor sandbox,
        IStaticAnalyzer staticAnalyzer,
        ITestGenerator testGenerator,
        IVerificationGate verificationGate,
        IRateLimiter rateLimiter,
        IJobQueue jobQueue,
        ILogger<SandboxController> logger)
    {
        _pipeline = pipeline;
        _sandbox = sandbox;
        _staticAnalyzer = staticAnalyzer;
        _testGenerator = testGenerator;
        _verificationGate = verificationGate;
        _rateLimiter = rateLimiter;
        _jobQueue = jobQueue;
        _logger = logger;
    }

    // ==================== SYNCHRONOUS EXECUTION ====================

    /// <summary>
    /// Execute code synchronously in sandbox (for quick operations)
    /// </summary>
    [HttpPost("execute")]
    public async Task<ActionResult<ExecutionResult>> ExecuteCode([FromBody] ExecuteRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check rate limits
        var limitCheck = await _rateLimiter.CheckLimitAsync(request.ProjectId, userId);
        if (!limitCheck.Allowed)
        {
            return StatusCode(429, new { error = limitCheck.Message, retry_after = limitCheck.RetryAfterSeconds });
        }

        var execRequest = new ExecutionRequest
        {
            ProjectId = request.ProjectId,
            Language = request.Language,
            Files = request.Files?.Select(f => new ProjectFile { Path = f.Path, Content = f.Content }).ToList() ?? new(),
            EntryPoint = request.EntryPoint,
            Phase = Enum.Parse<ExecutionPhase>(request.Phase ?? "Run", ignoreCase: true),
            TimeoutSeconds = Math.Min(request.TimeoutSeconds ?? 60, 120), // Max 2 minutes for sync
            AllowNetwork = request.AllowNetwork ?? false
        };

        var result = await _sandbox.ExecuteWithRetryAsync(execRequest, request.MaxRetries ?? 1);

        return Ok(result);
    }

    /// <summary>
    /// Run static analysis on code
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<StaticAnalysisResult>> AnalyzeCode([FromBody] AnalyzeRequest request)
    {
        var files = request.Files?.Select(f => new ProjectFile { Path = f.Path, Content = f.Content }).ToList() ?? new();
        var result = await _staticAnalyzer.AnalyzeAsync(request.ProjectId, request.Language, files);
        return Ok(result);
    }

    /// <summary>
    /// Generate tests for code
    /// </summary>
    [HttpPost("generate-tests")]
    public async Task<ActionResult<GeneratedTests>> GenerateTests([FromBody] GenerateTestsRequest request)
    {
        var files = request.Files?.Select(f => new ProjectFile { Path = f.Path, Content = f.Content }).ToList() ?? new();
        var result = await _testGenerator.GenerateTestsAsync(request.ProjectId, request.Language, files);
        return Ok(result);
    }

    /// <summary>
    /// Validate code against verification gate
    /// </summary>
    [HttpPost("verify")]
    public async Task<ActionResult<VerificationResult>> VerifyCode([FromBody] VerifyRequest request)
    {
        var files = request.Files?.Select(f => new ProjectFile { Path = f.Path, Content = f.Content }).ToList();
        
        var verificationRequest = new VerificationRequest
        {
            ProjectId = request.ProjectId,
            Files = files,
            BuildOutput = request.BuildOutput
        };

        var result = await _verificationGate.ValidateAsync(verificationRequest);
        return Ok(result);
    }

    // ==================== ASYNC JOB MANAGEMENT ====================

    /// <summary>
    /// Submit a pipeline job for async processing
    /// </summary>
    [HttpPost("jobs")]
    public async Task<ActionResult<JobSubmitResponse>> SubmitJob([FromBody] CreateJobRequest request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check rate limits
        var limitCheck = await _rateLimiter.CheckLimitAsync(request.ProjectId, userId);
        if (!limitCheck.Allowed)
        {
            return StatusCode(429, new { error = limitCheck.Message, retry_after = limitCheck.RetryAfterSeconds });
        }

        var job = new PipelineJob
        {
            Request = new PipelineRequest
            {
                ProjectId = request.ProjectId,
                UserId = userId,
                Language = request.Language,
                Prompt = request.Prompt,
                Files = request.Files?.Select(f => new ProjectFile { Path = f.Path, Content = f.Content }).ToList() ?? new(),
                EntryPoint = request.EntryPoint,
                RunAfterBuild = request.RunAfterBuild
            },
            WebhookUrl = request.WebhookUrl
        };

        var jobId = await _jobQueue.EnqueueAsync(job);

        _logger.LogInformation("Job {JobId} submitted by user {UserId} for project {ProjectId}",
            jobId, userId, request.ProjectId);

        return Accepted(new JobSubmitResponse
        {
            JobId = jobId,
            Status = "queued",
            Message = "Job submitted successfully",
            CheckStatusUrl = $"/api/sandbox/jobs/{jobId}"
        });
    }

    /// <summary>
    /// Get job status
    /// </summary>
    [HttpGet("jobs/{jobId}")]
    public async Task<ActionResult<JobProgress>> GetJobStatus(string jobId)
    {
        var job = await _jobQueue.GetJobAsync(jobId);
        if (job == null)
        {
            return NotFound(new { error = "Job not found" });
        }

        // Verify user owns this job
        var userId = User.FindFirst("user_id")?.Value;
        if (job.Request.UserId != userId)
        {
            return Forbid();
        }

        return Ok(job.GetProgress());
    }

    /// <summary>
    /// Get job result
    /// </summary>
    [HttpGet("jobs/{jobId}/result")]
    public async Task<ActionResult<PipelineResult>> GetJobResult(string jobId)
    {
        var job = await _jobQueue.GetJobAsync(jobId);
        if (job == null)
        {
            return NotFound(new { error = "Job not found" });
        }

        // Verify user owns this job
        var userId = User.FindFirst("user_id")?.Value;
        if (job.Request.UserId != userId)
        {
            return Forbid();
        }

        if (job.Status != JobStatus.Completed && job.Status != JobStatus.Failed)
        {
            return StatusCode(202, new { message = "Job is still processing", status = job.Status.ToString() });
        }

        var result = await _jobQueue.GetJobResultAsync(jobId);
        if (result == null)
        {
            return NotFound(new { error = "Result not found" });
        }

        return Ok(result);
    }

    /// <summary>
    /// Cancel a job
    /// </summary>
    [HttpDelete("jobs/{jobId}")]
    public async Task<ActionResult> CancelJob(string jobId)
    {
        var job = await _jobQueue.GetJobAsync(jobId);
        if (job == null)
        {
            return NotFound(new { error = "Job not found" });
        }

        // Verify user owns this job
        var userId = User.FindFirst("user_id")?.Value;
        if (job.Request.UserId != userId)
        {
            return Forbid();
        }

        await _jobQueue.CancelJobAsync(jobId);
        return Ok(new { message = "Job cancelled" });
    }

    /// <summary>
    /// Get user's jobs
    /// </summary>
    [HttpGet("jobs")]
    public async Task<ActionResult<List<JobProgress>>> GetUserJobs([FromQuery] int limit = 20)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var jobs = await _jobQueue.GetUserJobsAsync(userId, Math.Min(limit, 100));
        return Ok(jobs.Select(j => j.GetProgress()));
    }

    // ==================== FULL PIPELINE ====================

    /// <summary>
    /// Run complete pipeline synchronously (for smaller projects)
    /// </summary>
    [HttpPost("pipeline")]
    public async Task<ActionResult<PipelineResult>> RunPipeline([FromBody] PipelineRequestDto request)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        // Check rate limits
        var limitCheck = await _rateLimiter.CheckLimitAsync(request.ProjectId, userId);
        if (!limitCheck.Allowed)
        {
            return StatusCode(429, new { error = limitCheck.Message, retry_after = limitCheck.RetryAfterSeconds });
        }

        var pipelineRequest = new PipelineRequest
        {
            ProjectId = request.ProjectId,
            UserId = userId,
            Language = request.Language,
            Prompt = request.Prompt,
            Files = request.Files?.Select(f => new ProjectFile { Path = f.Path, Content = f.Content }).ToList() ?? new(),
            EntryPoint = request.EntryPoint,
            RunAfterBuild = request.RunAfterBuild ?? false,
            MaxIterations = Math.Min(request.MaxIterations ?? 10, 10)
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5)); // 5 minute timeout

        var result = await _pipeline.ExecutePipelineAsync(pipelineRequest, cts.Token);

        return Ok(result);
    }

    // ==================== USAGE & LIMITS ====================

    /// <summary>
    /// Get usage statistics
    /// </summary>
    [HttpGet("usage")]
    public async Task<ActionResult<UsageStats>> GetUsage([FromQuery] string? projectId)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var stats = await _rateLimiter.GetUsageStatsAsync(projectId ?? "", userId);
        return Ok(stats);
    }

    /// <summary>
    /// Check if request would be rate limited
    /// </summary>
    [HttpGet("limits/check")]
    public async Task<ActionResult<RateLimitCheck>> CheckLimits([FromQuery] string projectId)
    {
        var userId = User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var check = await _rateLimiter.CheckLimitAsync(projectId, userId);
        return Ok(check);
    }
}

// Request DTOs
public class ExecuteRequest
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "python";
    public List<FileInput>? Files { get; set; }
    public string? EntryPoint { get; set; }
    public string? Phase { get; set; }
    public int? TimeoutSeconds { get; set; }
    public bool? AllowNetwork { get; set; }
    public int? MaxRetries { get; set; }
}

public class AnalyzeRequest
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "python";
    public List<FileInput>? Files { get; set; }
}

public class GenerateTestsRequest
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "python";
    public List<FileInput>? Files { get; set; }
}

public class VerifyRequest
{
    public string ProjectId { get; set; } = "";
    public List<FileInput>? Files { get; set; }
    public string? BuildOutput { get; set; }
}

public class PipelineRequestDto
{
    public string ProjectId { get; set; } = "";
    public string Language { get; set; } = "python";
    public string? Prompt { get; set; }
    public List<FileInput>? Files { get; set; }
    public string? EntryPoint { get; set; }
    public bool? RunAfterBuild { get; set; }
    public int? MaxIterations { get; set; }
}

public class JobSubmitResponse
{
    public string JobId { get; set; } = "";
    public string Status { get; set; } = "";
    public string Message { get; set; } = "";
    public string CheckStatusUrl { get; set; } = "";
}
