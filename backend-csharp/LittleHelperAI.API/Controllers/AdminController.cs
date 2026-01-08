// Admin Controller - Full Admin functionality with IP tracking, AI settings, Knowledge Base
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.API.Services;
using LittleHelperAI.Data.Models;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin")]
public class AdminController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ICreditService _creditService;
    private readonly IJobOrchestrationService _jobService;
    private readonly IAIService _aiService;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IAuthService authService,
        ICreditService creditService,
        IJobOrchestrationService jobService,
        IAIService aiService,
        IConfiguration config,
        ILogger<AdminController> logger)
    {
        _authService = authService;
        _creditService = creditService;
        _jobService = jobService;
        _aiService = aiService;
        _config = config;
        _logger = logger;
    }

    // ==================== USER MANAGEMENT ====================

    [HttpGet("users")]
    public async Task<ActionResult> GetUsers()
    {
        var users = await _authService.GetAllUsersAsync();
        return Ok(users);
    }

    [HttpPut("users/{userId}")]
    public async Task<ActionResult> UpdateUser(string userId, [FromBody] AdminUpdateUserRequest request)
    {
        var success = await _authService.UpdateUserAsync(userId, request);
        if (!success)
            return NotFound(new { detail = "User not found" });
        return Ok(new { message = "User updated" });
    }

    [HttpDelete("users/{userId}")]
    public async Task<ActionResult> DeleteUser(string userId)
    {
        var currentUserId = User.FindFirst("user_id")?.Value;
        if (userId == currentUserId)
            return BadRequest(new { detail = "Cannot delete yourself" });

        var success = await _authService.DeleteUserAsync(userId);
        if (!success)
            return NotFound(new { detail = "User not found" });
        return Ok(new { message = "User deleted" });
    }

    [HttpPost("users/bulk-credits")]
    public async Task<ActionResult> BulkAddCredits([FromBody] BulkCreditsRequest request)
    {
        var count = await _creditService.BulkAddCreditsAsync(request.Amount, request.UserIds);
        return Ok(new { message = $"Added {request.Amount} credits to {count} users" });
    }

    // ==================== SYSTEM STATS & HEALTH ====================

    [HttpGet("stats")]
    public async Task<ActionResult> GetStats()
    {
        var stats = await _authService.GetSystemStatsAsync();
        return Ok(stats);
    }

    [HttpGet("running-jobs")]
    public async Task<ActionResult> GetRunningJobs()
    {
        var jobs = await _jobService.GetRunningJobsAsync();
        return Ok(jobs);
    }

    [HttpGet("system-health")]
    public async Task<ActionResult> GetSystemHealth()
    {
        // Get system information
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var computerInfo = new Dictionary<string, object>();
        
        // Memory info
        var memoryUsed = process.WorkingSet64 / (1024.0 * 1024.0); // MB
        var gcMemory = GC.GetTotalMemory(false) / (1024.0 * 1024.0); // MB
        
        // CPU info
        var processorCount = Environment.ProcessorCount;
        
        // Disk info  
        var drives = new List<object>();
        try 
        {
            foreach (var drive in System.IO.DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    drives.Add(new 
                    {
                        name = drive.Name,
                        type = drive.DriveType.ToString(),
                        totalSize = $"{drive.TotalSize / (1024.0 * 1024.0 * 1024.0):F2} GB",
                        freeSpace = $"{drive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0):F2} GB",
                        usedSpace = $"{(drive.TotalSize - drive.TotalFreeSpace) / (1024.0 * 1024.0 * 1024.0):F2} GB",
                        percentUsed = ((drive.TotalSize - drive.TotalFreeSpace) * 100.0 / drive.TotalSize).ToString("F1") + "%",
                        format = drive.DriveFormat
                    });
                }
            }
        }
        catch { /* Ignore drive errors */ }

        // Database health
        var dbHealth = "Unknown";
        try
        {
            var count = await _creditService.GetSettingsAsync();
            dbHealth = "Connected";
        }
        catch
        {
            dbHealth = "Disconnected";
        }

        // AI providers status
        var aiHealth = await _aiService.CheckHealthAsync();

        return Ok(new
        {
            system = new
            {
                status = "healthy",
                os = Environment.OSVersion.ToString(),
                machineName = Environment.MachineName,
                userName = Environment.UserName,
                is64Bit = Environment.Is64BitOperatingSystem,
                dotnetVersion = Environment.Version.ToString(),
                processors = processorCount
            },
            memory = new
            {
                status = "healthy",
                processMemory = $"{memoryUsed:F2} MB",
                gcMemory = $"{gcMemory:F2} MB",
                gcCollections = new {
                    gen0 = GC.CollectionCount(0),
                    gen1 = GC.CollectionCount(1),
                    gen2 = GC.CollectionCount(2)
                }
            },
            storage = new
            {
                status = "healthy",
                drives = drives
            },
            database = new
            {
                status = dbHealth == "Connected" ? "healthy" : "error",
                connection = dbHealth,
                type = "MySQL/MariaDB"
            },
            api = new
            {
                status = "healthy",
                uptime = (DateTime.UtcNow - process.StartTime.ToUniversalTime()).ToString(@"dd\.hh\:mm\:ss"),
                startTime = process.StartTime.ToUniversalTime().ToString("o"),
                port = 8001
            },
            ai_services = aiHealth,
            timestamp = DateTime.UtcNow
        });
    }

    // ==================== SETTINGS ====================

    [HttpGet("settings")]
    public async Task<ActionResult> GetSettings()
    {
        var settings = await _creditService.GetSettingsAsync();
        return Ok(settings);
    }

    [HttpPut("settings/{key}")]
    public async Task<ActionResult> UpdateSetting(string key, [FromQuery] string value)
    {
        var success = await _creditService.UpdateSettingAsync(key, value);
        if (!success)
            return NotFound(new { detail = "Setting not found" });
        return Ok(new { message = "Setting updated" });
    }

    [HttpPut("credit-config")]
    public async Task<ActionResult> UpdateCreditConfig([FromQuery] double? chatRate, [FromQuery] double? projectRate)
    {
        if (chatRate.HasValue)
            await _creditService.UpdateSettingAsync("credits_per_1k_tokens_chat", chatRate.Value.ToString());
        if (projectRate.HasValue)
            await _creditService.UpdateSettingAsync("credits_per_1k_tokens_project", projectRate.Value.ToString());
        return Ok(new { message = "Credit config updated" });
    }

    // ==================== DEFAULT SETTINGS (for new users) ====================

    [HttpGet("defaults")]
    public async Task<ActionResult> GetDefaults()
    {
        var defaults = await _authService.GetDefaultSettingsAsync();
        return Ok(defaults);
    }

    [HttpPut("defaults")]
    public async Task<ActionResult> UpdateDefaults([FromBody] UpdateDefaultsRequest request)
    {
        await _authService.UpdateDefaultSettingsAsync(request);
        return Ok(new { message = "Defaults updated" });
    }

    // ==================== IP TRACKING ====================

    [HttpGet("ip-records")]
    public async Task<ActionResult> GetIpRecords([FromQuery] int limit = 100)
    {
        var records = await _authService.GetIpRecordsAsync(limit);
        return Ok(records);
    }

    // ==================== AI SETTINGS ====================

    [HttpGet("ai-settings")]
    public async Task<ActionResult> GetAISettings()
    {
        var emergentEnabled = await _creditService.GetSettingValueAsync("emergent_llm_enabled");
        var emergentKeyConfigured = !string.IsNullOrEmpty(_config["EmergentLLM:Key"]);

        return Ok(new
        {
            emergent_llm_enabled = emergentEnabled?.ToLower() == "true" || emergentEnabled == "1",
            emergent_key_configured = emergentKeyConfigured
        });
    }

    [HttpPut("ai-settings/emergent-toggle")]
    public async Task<ActionResult> ToggleEmergentLLM([FromQuery] bool enabled)
    {
        await _creditService.UpdateSettingAsync("emergent_llm_enabled", enabled.ToString().ToLower());
        return Ok(new { message = $"Emergent LLM {(enabled ? "enabled" : "disabled")}", enabled });
    }

    // ==================== FREE AI PROVIDERS ====================

    [HttpGet("free-ai-providers")]
    public async Task<ActionResult> GetFreeAIProviders()
    {
        var providers = await _aiService.GetFreeAIProvidersAsync();
        
        // Map to frontend-expected format
        var response = providers.Select(p => new
        {
            id = p.Id,
            name = p.Name,
            provider = p.Provider,
            api_key = p.ApiKey,
            model = p.Model,
            enabled = p.IsEnabled,
            is_enabled = p.IsEnabled,
            priority = p.Priority,
            requires_key = !string.Equals(p.Provider, "ollama", StringComparison.OrdinalIgnoreCase),
            has_key = !string.IsNullOrEmpty(p.ApiKey),
            models = GetModelsForProvider(p.Provider),
            created_at = p.CreatedAt.ToString("o"),
            updated_at = p.UpdatedAt.ToString("o")
        });
        
        return Ok(response);
    }
    
    private static List<string> GetModelsForProvider(string provider)
    {
        return provider.ToLower() switch
        {
            "groq" => new List<string> { "llama-3.1-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768" },
            "together" => new List<string> { "meta-llama/Llama-3.2-11B-Vision-Instruct-Turbo", "mistralai/Mixtral-8x7B-Instruct-v0.1" },
            "huggingface" => new List<string> { "microsoft/DialoGPT-large", "google/flan-t5-large", "facebook/blenderbot-400M-distill" },
            "openrouter" => new List<string> { "google/gemma-2-9b-it:free", "mistralai/mistral-7b-instruct:free", "meta-llama/llama-3-8b-instruct:free" },
            "ollama" => new List<string> { "qwen2.5-coder:1.5b", "llama2", "mistral", "codellama" },
            _ => new List<string>()
        };
    }

    [HttpPut("free-ai-providers/{providerId}")]
    public async Task<ActionResult> ToggleFreeAIProvider(
        string providerId, 
        [FromQuery] bool enabled, 
        [FromQuery] string? apiKey = null)
    {
        await _aiService.UpdateFreeAIProviderAsync(providerId, enabled, apiKey);
        return Ok(new { message = $"Provider {providerId} {(enabled ? "enabled" : "disabled")}", enabled });
    }

    // ==================== KNOWLEDGE BASE ====================

    [HttpGet("knowledge-base")]
    public async Task<ActionResult> GetKnowledgeBase([FromQuery] int limit = 50)
    {
        var entries = await _aiService.GetKnowledgeBaseEntriesAsync(limit);
        
        // Map to frontend-expected format
        var response = entries.Select(e => new
        {
            id = e.Id,
            question = e.Question,
            question_text = e.Question, // Alias for frontend compatibility
            answer = e.Answer,
            answer_text = e.Answer, // Alias for frontend compatibility
            provider = e.Provider,
            hitCount = e.HitCount,
            hit_count = e.HitCount, // Alias for frontend compatibility
            usage_count = e.HitCount, // Another alias
            isValid = e.IsValid,
            is_valid = e.IsValid, // Alias for frontend compatibility
            invalidated_at = e.InvalidatedAt?.ToString("o"),
            created_at = e.CreatedAt.ToString("o"),
            updated_at = e.UpdatedAt.ToString("o")
        });
        
        return Ok(response);
    }

    [HttpPost("knowledge-base")]
    public async Task<ActionResult> AddKnowledgeEntry([FromBody] AddKnowledgeEntryRequest request)
    {
        var entry = await _aiService.AddKnowledgeEntryAsync(request.Question, request.Answer, request.Provider);
        return Ok(entry);
    }

    [HttpDelete("knowledge-base/{entryId}")]
    public async Task<ActionResult> DeleteKnowledgeEntry(string entryId)
    {
        var success = await _aiService.DeleteKnowledgeEntryAsync(entryId);
        if (!success)
            return NotFound(new { detail = "Entry not found" });
        return Ok(new { message = "Entry deleted" });
    }

    [HttpPut("knowledge-base/{entryId}/validate")]
    public async Task<ActionResult> ValidateKnowledgeEntry(string entryId, [FromQuery] bool isValid)
    {
        await _aiService.ValidateKnowledgeEntryAsync(entryId, isValid);
        return Ok(new { message = $"Entry marked as {(isValid ? "valid" : "invalid")}" });
    }

    [HttpPost("knowledge-base/invalidate-expired")]
    public async Task<ActionResult> InvalidateExpiredEntries()
    {
        // Invalidate entries older than 30 days with low usage
        var entries = await _aiService.GetKnowledgeBaseEntriesAsync(1000);
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var invalidated = 0;
        
        foreach (var entry in entries.Where(e => e.IsValid && e.HitCount < 5 && e.CreatedAt < thirtyDaysAgo))
        {
            await _aiService.ValidateKnowledgeEntryAsync(entry.Id, false);
            invalidated++;
        }
        
        return Ok(new { message = $"Invalidated {invalidated} expired entries" });
    }

    // ==================== CREDIT PACKAGES ====================

    [HttpGet("credit-packages")]
    public async Task<ActionResult> GetCreditPackages()
    {
        var packages = await _creditService.GetCreditPackagesAsync();
        return Ok(packages);
    }

    [HttpPost("credit-packages")]
    public async Task<ActionResult> CreateCreditPackage([FromBody] CreateCreditPackageRequest request)
    {
        var package = await _creditService.CreateCreditPackageAsync(request);
        return Ok(package);
    }

    [HttpPut("credit-packages/{packageId}")]
    public async Task<ActionResult> UpdateCreditPackage(string packageId, [FromBody] UpdateCreditPackageRequest request)
    {
        var success = await _creditService.UpdateCreditPackageAsync(packageId, request);
        if (!success)
            return NotFound(new { detail = "Package not found" });
        return Ok(new { message = "Package updated" });
    }

    [HttpDelete("credit-packages/{packageId}")]
    public async Task<ActionResult> DeleteCreditPackage(string packageId)
    {
        var success = await _creditService.DeleteCreditPackageAsync(packageId);
        if (!success)
            return NotFound(new { detail = "Package not found" });
        return Ok(new { message = "Package deleted" });
    }

    // ==================== AGENT ACTIVITY ====================

    [HttpGet("agent-activity")]
    public async Task<ActionResult> GetAgentActivity([FromQuery] int limit = 100)
    {
        var activity = await _aiService.GetAgentActivityAsync(limit);
        
        // Map to frontend-expected format
        var response = activity.Select(a => new
        {
            id = a.Id,
            userId = a.UserId,
            user_id = a.UserId, // Alias
            projectId = a.ProjectId,
            project_id = a.ProjectId, // Alias
            jobId = a.JobId,
            job_id = a.JobId, // Alias
            agentId = a.AgentId,
            agent_id = a.AgentId, // Alias
            action = a.Action,
            tokensUsed = a.TokensUsed,
            tokens_used = a.TokensUsed, // Alias
            tokens_input = a.TokensUsed / 2,
            tokens_output = a.TokensUsed / 2,
            creditsUsed = a.CreditsUsed,
            credits_used = a.CreditsUsed, // Alias
            success = a.Success,
            status = a.Success ? "completed" : (a.Error != null ? "failed" : "pending"),
            error = a.Error,
            timestamp = a.Timestamp.ToString("o"),
            created_at = a.Timestamp.ToString("o") // Alias
        });
        
        return Ok(response);
    }

    // ==================== SUBSCRIPTION PLANS ====================

    [HttpGet("subscription-plans")]
    public async Task<ActionResult> GetSubscriptionPlans()
    {
        var plans = await _creditService.GetSubscriptionPlansAsync();
        return Ok(plans);
    }

    [HttpPost("subscription-plans")]
    public async Task<ActionResult> CreateSubscriptionPlan([FromBody] CreateSubscriptionPlanRequest request)
    {
        var plan = await _creditService.CreateSubscriptionPlanAsync(request);
        return Ok(plan);
    }

    [HttpPut("subscription-plans/{planId}")]
    public async Task<ActionResult> UpdateSubscriptionPlan(string planId, [FromBody] UpdateSubscriptionPlanRequest request)
    {
        var success = await _creditService.UpdateSubscriptionPlanAsync(planId, request);
        if (!success)
            return NotFound(new { detail = "Plan not found" });
        return Ok(new { message = "Plan updated" });
    }

    [HttpDelete("subscription-plans/{planId}")]
    public async Task<ActionResult> DeleteSubscriptionPlan(string planId)
    {
        var success = await _creditService.DeleteSubscriptionPlanAsync(planId);
        if (!success)
            return NotFound(new { detail = "Plan not found" });
        return Ok(new { message = "Plan deleted" });
    }
}

// Request/Response Models
public record AdminUpdateUserRequest(string? Role, double? Credits, bool? CreditsEnabled, string? Plan);
public record BulkCreditsRequest(double Amount, List<string>? UserIds);

public record UpdateDefaultsRequest(
    UserTheme? Theme,
    string? Language,
    int? FreeCredits
);

public record AddKnowledgeEntryRequest(
    string Question,
    string Answer,
    string? Provider
);

public record CreateCreditPackageRequest(
    string PackageId,
    string Name,
    int Credits,
    decimal Price,
    int SortOrder = 0
);

public record UpdateCreditPackageRequest(
    string? Name,
    int? Credits,
    decimal? Price,
    bool? IsActive,
    int? SortOrder
);
