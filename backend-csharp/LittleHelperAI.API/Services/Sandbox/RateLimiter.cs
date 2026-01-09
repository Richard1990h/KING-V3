// Rate Limiter and Cost Tracking Service
// Per-project and per-user rate/cost limits
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace LittleHelperAI.API.Services.Sandbox;

public interface IRateLimiter
{
    Task<RateLimitCheck> CheckLimitAsync(string projectId, string userId);
    Task<decimal> RecordUsageAsync(string projectId, string userId, PipelineResult result);
    Task<UsageStats> GetUsageStatsAsync(string projectId, string userId);
    Task ResetLimitsAsync(string projectId);
}

public class RateLimiter : IRateLimiter
{
    private readonly ILogger<RateLimiter> _logger;
    private readonly RateLimitConfig _config;
    private readonly ConcurrentDictionary<string, UserUsage> _userUsage = new();
    private readonly ConcurrentDictionary<string, ProjectUsage> _projectUsage = new();

    public RateLimiter(ILogger<RateLimiter> logger, RateLimitConfig? config = null)
    {
        _logger = logger;
        _config = config ?? new RateLimitConfig();
    }

    public Task<RateLimitCheck> CheckLimitAsync(string projectId, string userId)
    {
        var result = new RateLimitCheck { Allowed = true };

        // Check user rate limit
        var userKey = $"user:{userId}";
        var userUsage = _userUsage.GetOrAdd(userKey, _ => new UserUsage());

        CleanOldEntries(userUsage);

        // Requests per minute
        var recentRequests = userUsage.Requests.Count(r => r > DateTime.UtcNow.AddMinutes(-1));
        if (recentRequests >= _config.MaxRequestsPerMinute)
        {
            result.Allowed = false;
            result.Message = $"Rate limit exceeded. Max {_config.MaxRequestsPerMinute} requests per minute.";
            result.RetryAfterSeconds = 60;
            _logger.LogWarning("User {UserId} exceeded rate limit: {Requests}/min", userId, recentRequests);
            return Task.FromResult(result);
        }

        // Requests per hour
        var hourlyRequests = userUsage.Requests.Count(r => r > DateTime.UtcNow.AddHours(-1));
        if (hourlyRequests >= _config.MaxRequestsPerHour)
        {
            result.Allowed = false;
            result.Message = $"Hourly limit exceeded. Max {_config.MaxRequestsPerHour} requests per hour.";
            result.RetryAfterSeconds = 3600;
            _logger.LogWarning("User {UserId} exceeded hourly limit: {Requests}/hour", userId, hourlyRequests);
            return Task.FromResult(result);
        }

        // Check daily cost limit
        var todayStart = DateTime.UtcNow.Date;
        var dailyCost = userUsage.CostEntries
            .Where(c => c.Timestamp >= todayStart)
            .Sum(c => c.Amount);

        if (dailyCost >= _config.MaxDailyCostPerUser)
        {
            result.Allowed = false;
            result.Message = $"Daily cost limit exceeded. Spent ${dailyCost:F2} of ${_config.MaxDailyCostPerUser:F2} limit.";
            result.RetryAfterSeconds = (int)(todayStart.AddDays(1) - DateTime.UtcNow).TotalSeconds;
            _logger.LogWarning("User {UserId} exceeded daily cost limit: ${DailyCost}", userId, dailyCost);
            return Task.FromResult(result);
        }

        // Check project limits
        var projectKey = $"project:{projectId}";
        var projectUsage = _projectUsage.GetOrAdd(projectKey, _ => new ProjectUsage());

        CleanOldProjectEntries(projectUsage);

        var projectDailyCost = projectUsage.CostEntries
            .Where(c => c.Timestamp >= todayStart)
            .Sum(c => c.Amount);

        if (projectDailyCost >= _config.MaxDailyCostPerProject)
        {
            result.Allowed = false;
            result.Message = $"Project daily cost limit exceeded. Project spent ${projectDailyCost:F2} of ${_config.MaxDailyCostPerProject:F2} limit.";
            _logger.LogWarning("Project {ProjectId} exceeded daily cost limit: ${DailyCost}", projectId, projectDailyCost);
            return Task.FromResult(result);
        }

        // Check concurrent executions
        var activeExecutions = projectUsage.ActiveExecutions;
        if (activeExecutions >= _config.MaxConcurrentExecutionsPerProject)
        {
            result.Allowed = false;
            result.Message = $"Too many concurrent executions. Max {_config.MaxConcurrentExecutionsPerProject} per project.";
            result.RetryAfterSeconds = 10;
            return Task.FromResult(result);
        }

        // Record request
        userUsage.Requests.Add(DateTime.UtcNow);
        projectUsage.ActiveExecutions++;

        result.RemainingRequests = _config.MaxRequestsPerMinute - recentRequests - 1;
        result.RemainingDailyCost = _config.MaxDailyCostPerUser - dailyCost;

        return Task.FromResult(result);
    }

    public Task<decimal> RecordUsageAsync(string projectId, string userId, PipelineResult result)
    {
        var cost = CalculateCost(result);

        // Record user cost
        var userKey = $"user:{userId}";
        var userUsage = _userUsage.GetOrAdd(userKey, _ => new UserUsage());
        userUsage.CostEntries.Add(new CostEntry
        {
            Timestamp = DateTime.UtcNow,
            Amount = cost,
            ProjectId = projectId,
            RequestId = result.RequestId
        });

        // Record project cost
        var projectKey = $"project:{projectId}";
        var projectUsage = _projectUsage.GetOrAdd(projectKey, _ => new ProjectUsage());
        projectUsage.CostEntries.Add(new CostEntry
        {
            Timestamp = DateTime.UtcNow,
            Amount = cost,
            UserId = userId,
            RequestId = result.RequestId
        });

        // Decrement active executions
        if (projectUsage.ActiveExecutions > 0)
            projectUsage.ActiveExecutions--;

        _logger.LogInformation("Recorded usage: Project={ProjectId}, User={UserId}, Cost=${Cost:F4}", 
            projectId, userId, cost);

        return Task.FromResult(cost);
    }

    public Task<UsageStats> GetUsageStatsAsync(string projectId, string userId)
    {
        var stats = new UsageStats
        {
            ProjectId = projectId,
            UserId = userId,
            AsOf = DateTime.UtcNow
        };

        var todayStart = DateTime.UtcNow.Date;
        var weekStart = todayStart.AddDays(-(int)todayStart.DayOfWeek);
        var monthStart = new DateTime(todayStart.Year, todayStart.Month, 1);

        // User stats
        var userKey = $"user:{userId}";
        if (_userUsage.TryGetValue(userKey, out var userUsage))
        {
            stats.UserDailyCost = userUsage.CostEntries.Where(c => c.Timestamp >= todayStart).Sum(c => c.Amount);
            stats.UserWeeklyCost = userUsage.CostEntries.Where(c => c.Timestamp >= weekStart).Sum(c => c.Amount);
            stats.UserMonthlyCost = userUsage.CostEntries.Where(c => c.Timestamp >= monthStart).Sum(c => c.Amount);
            stats.UserTotalRequests = userUsage.Requests.Count;
            stats.UserRequestsToday = userUsage.Requests.Count(r => r >= todayStart);
        }

        // Project stats
        var projectKey = $"project:{projectId}";
        if (_projectUsage.TryGetValue(projectKey, out var projectUsage))
        {
            stats.ProjectDailyCost = projectUsage.CostEntries.Where(c => c.Timestamp >= todayStart).Sum(c => c.Amount);
            stats.ProjectWeeklyCost = projectUsage.CostEntries.Where(c => c.Timestamp >= weekStart).Sum(c => c.Amount);
            stats.ProjectMonthlyCost = projectUsage.CostEntries.Where(c => c.Timestamp >= monthStart).Sum(c => c.Amount);
            stats.ProjectActiveExecutions = projectUsage.ActiveExecutions;
        }

        // Limits
        stats.UserDailyLimit = _config.MaxDailyCostPerUser;
        stats.ProjectDailyLimit = _config.MaxDailyCostPerProject;
        stats.RequestsPerMinuteLimit = _config.MaxRequestsPerMinute;

        return Task.FromResult(stats);
    }

    public Task ResetLimitsAsync(string projectId)
    {
        var projectKey = $"project:{projectId}";
        _projectUsage.TryRemove(projectKey, out _);
        _logger.LogInformation("Reset limits for project {ProjectId}", projectId);
        return Task.CompletedTask;
    }

    private decimal CalculateCost(PipelineResult result)
    {
        decimal cost = 0;

        // Base cost per iteration
        cost += result.Iterations * _config.CostPerIteration;

        // Token-based cost
        foreach (var phase in result.Phases)
        {
            cost += phase.TokensUsed * _config.CostPerToken;
        }

        // Execution time cost (per second)
        if (result.TotalDuration.HasValue)
        {
            cost += (decimal)result.TotalDuration.Value.TotalSeconds * _config.CostPerExecutionSecond;
        }

        // Sandbox execution cost
        var sandboxPhases = result.Phases.Count(p => 
            p.Phase is PipelinePhase.Build or PipelinePhase.TestExecution or PipelinePhase.Execution);
        cost += sandboxPhases * _config.CostPerSandboxExecution;

        return Math.Round(cost, 4);
    }

    private void CleanOldEntries(UserUsage usage)
    {
        var cutoff = DateTime.UtcNow.AddHours(-24);
        usage.Requests.RemoveAll(r => r < cutoff);
        usage.CostEntries.RemoveAll(c => c.Timestamp < cutoff.AddDays(-30)); // Keep 30 days for billing
    }

    private void CleanOldProjectEntries(ProjectUsage usage)
    {
        var cutoff = DateTime.UtcNow.AddDays(-30);
        usage.CostEntries.RemoveAll(c => c.Timestamp < cutoff);
    }
}

// Internal tracking classes
internal class UserUsage
{
    public List<DateTime> Requests { get; } = new();
    public List<CostEntry> CostEntries { get; } = new();
}

internal class ProjectUsage
{
    public List<CostEntry> CostEntries { get; } = new();
    public int ActiveExecutions { get; set; }
}

internal class CostEntry
{
    public DateTime Timestamp { get; set; }
    public decimal Amount { get; set; }
    public string? ProjectId { get; set; }
    public string? UserId { get; set; }
    public string? RequestId { get; set; }
}

// Public models
public class RateLimitCheck
{
    public bool Allowed { get; set; }
    public string? Message { get; set; }
    public int? RetryAfterSeconds { get; set; }
    public int RemainingRequests { get; set; }
    public decimal RemainingDailyCost { get; set; }
}

public class UsageStats
{
    public string ProjectId { get; set; } = "";
    public string UserId { get; set; } = "";
    public DateTime AsOf { get; set; }

    // User stats
    public decimal UserDailyCost { get; set; }
    public decimal UserWeeklyCost { get; set; }
    public decimal UserMonthlyCost { get; set; }
    public int UserTotalRequests { get; set; }
    public int UserRequestsToday { get; set; }

    // Project stats
    public decimal ProjectDailyCost { get; set; }
    public decimal ProjectWeeklyCost { get; set; }
    public decimal ProjectMonthlyCost { get; set; }
    public int ProjectActiveExecutions { get; set; }

    // Limits
    public decimal UserDailyLimit { get; set; }
    public decimal ProjectDailyLimit { get; set; }
    public int RequestsPerMinuteLimit { get; set; }
}

public class RateLimitConfig
{
    // Request limits
    public int MaxRequestsPerMinute { get; set; } = 10;
    public int MaxRequestsPerHour { get; set; } = 100;
    public int MaxConcurrentExecutionsPerProject { get; set; } = 3;

    // Cost limits (in USD)
    public decimal MaxDailyCostPerUser { get; set; } = 10.00m;
    public decimal MaxDailyCostPerProject { get; set; } = 50.00m;
    public decimal MaxMonthlyCostPerUser { get; set; } = 100.00m;

    // Cost calculation
    public decimal CostPerToken { get; set; } = 0.00001m; // $0.01 per 1000 tokens
    public decimal CostPerIteration { get; set; } = 0.01m;
    public decimal CostPerSandboxExecution { get; set; } = 0.02m;
    public decimal CostPerExecutionSecond { get; set; } = 0.001m;
}
