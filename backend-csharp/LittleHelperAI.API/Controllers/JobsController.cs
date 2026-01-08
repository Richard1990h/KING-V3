// Jobs Controller - Multi-Agent Pipeline
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services;
using LittleHelperAI.Data.Models;
using System.Text.Json;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly IJobOrchestrationService _jobService;
    private readonly IAuthService _authService;
    private readonly ILogger<JobsController> _logger;

    public JobsController(
        IJobOrchestrationService jobService, 
        IAuthService authService,
        ILogger<JobsController> logger)
    {
        _jobService = jobService;
        _authService = authService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst("user_id")?.Value ?? throw new UnauthorizedAccessException();

    [HttpPost("create")]
    public async Task<ActionResult<JobResponse>> CreateJob([FromBody] CreateJobRequest request)
    {
        var user = await _authService.GetUserByIdAsync(GetUserId());
        if (user == null)
            return Unauthorized();

        var job = await _jobService.CreateJobAsync(user, request);
        return Ok(job);
    }

    [HttpPost("{jobId}/approve")]
    public async Task<ActionResult> ApproveJob(string jobId, [FromBody] ApproveJobRequest request)
    {
        var user = await _authService.GetUserByIdAsync(GetUserId());
        if (user == null)
            return Unauthorized();

        var result = await _jobService.ApproveJobAsync(jobId, user, request);
        if (!result.Success)
            return BadRequest(new { detail = result.Error });

        return Ok(result);
    }

    [HttpGet("{jobId}/execute")]
    public async Task ExecuteJob(string jobId)
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var user = await _authService.GetUserByIdAsync(GetUserId());
        if (user == null)
        {
            await WriteSSEAsync(new { type = "error", message = "Unauthorized" });
            return;
        }

        await foreach (var update in _jobService.ExecuteJobAsync(jobId, user))
        {
            await WriteSSEAsync(update);
        }
    }

    private async Task WriteSSEAsync(object data)
    {
        var json = JsonSerializer.Serialize(data);
        await Response.WriteAsync($"data: {json}\n\n");
        await Response.Body.FlushAsync();
    }

    [HttpPost("{jobId}/continue")]
    public async Task<ActionResult> ContinueJob(string jobId, [FromBody] ContinueJobRequest request)
    {
        var user = await _authService.GetUserByIdAsync(GetUserId());
        if (user == null)
            return Unauthorized();

        var result = await _jobService.ContinueJobAsync(jobId, user, request.Approved);
        return Ok(result);
    }

    [HttpGet("{jobId}")]
    public async Task<ActionResult<JobResponse>> GetJob(string jobId)
    {
        var job = await _jobService.GetJobAsync(jobId, GetUserId());
        if (job == null)
            return NotFound(new { detail = "Job not found" });
        return Ok(job);
    }

    [HttpGet]
    public async Task<ActionResult<List<JobResponse>>> GetJobs([FromQuery] int limit = 20)
    {
        var jobs = await _jobService.GetUserJobsAsync(GetUserId(), limit);
        return Ok(jobs);
    }
}

// Request/Response Models
public record CreateJobRequest(string ProjectId, string Prompt, bool MultiAgentMode = true);
public record ApproveJobRequest(string JobId, bool Approved, List<TaskItem>? ModifiedTasks = null);
public record ContinueJobRequest(string JobId, bool Approved);
public record JobResponse(
    string Id, string ProjectId, string UserId, string Prompt,
    string Status, List<TaskItem> Tasks, double TotalEstimatedCredits,
    double CreditsUsed, double CreditsRemaining, int CurrentTaskIndex,
    string CreatedAt, string UpdatedAt
);
public record TaskItem(
    string Id, string Title, string Description, string AgentType,
    int Order, string Status, int EstimatedTokens, double EstimatedCredits,
    int ActualTokens, double ActualCredits, string? Output,
    List<string> FilesCreated, string? Error
);
