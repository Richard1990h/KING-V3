// AI Service Implementation
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using LittleHelperAI.Agents;
using System.Text.Json;

namespace LittleHelperAI.API.Services;

public class AIService : IAIService, Agents.IAIService
{
    private readonly IDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<AIService> _logger;
    private readonly HttpClient _httpClient;

    public AIService(IDbContext db, IConfiguration config, ILogger<AIService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task<object> CheckHealthAsync()
    {
        var emergentEnabled = await _db.QueryFirstOrDefaultAsync<SystemSetting>(
            "SELECT * FROM system_settings WHERE setting_key = 'emergent_llm_enabled'");

        var freeProviders = await GetFreeAIProvidersAsync();
        var enabledProviders = freeProviders.Where(p => p.IsEnabled).ToList();

        return new
        {
            status = "healthy",
            emergent_llm = new
            {
                enabled = emergentEnabled?.SettingValue?.ToLower() == "true",
                configured = !string.IsNullOrEmpty(_config["EmergentLLM:Key"])
            },
            free_providers = enabledProviders.Select(p => new { p.Name, p.Provider, p.IsEnabled }),
            timestamp = DateTime.UtcNow
        };
    }

    // Implementation of Agents.IAIService.CheckHealthAsync
    async Task<HealthStatus> Agents.IAIService.CheckHealthAsync()
    {
        return new HealthStatus(
            "connected",
            "available",
            "configured",
            new List<string> { "gpt-4", "claude-3", "local-llm" }
        );
    }

    public async Task<List<FreeAIProvider>> GetFreeAIProvidersAsync()
    {
        var providers = await _db.QueryAsync<FreeAIProvider>(
            "SELECT * FROM free_ai_providers ORDER BY priority");
        return providers.ToList();
    }

    public async Task UpdateFreeAIProviderAsync(string providerId, bool enabled, string? apiKey = null)
    {
        var updates = new List<string> { "is_enabled = @Enabled", "updated_at = @UpdatedAt" };
        var parameters = new Dictionary<string, object>
        {
            ["Id"] = providerId,
            ["Enabled"] = enabled,
            ["UpdatedAt"] = DateTime.UtcNow
        };

        if (apiKey != null)
        {
            updates.Add("api_key = @ApiKey");
            parameters["ApiKey"] = apiKey;
        }

        await _db.ExecuteAsync(
            $"UPDATE free_ai_providers SET {string.Join(", ", updates)} WHERE id = @Id",
            parameters);
    }

    public async Task<List<KnowledgeBaseEntry>> GetKnowledgeBaseEntriesAsync(int limit)
    {
        var entries = await _db.QueryAsync<KnowledgeBaseEntry>(
            "SELECT * FROM knowledge_base ORDER BY usage_count DESC, created_at DESC LIMIT @Limit",
            new { Limit = limit });
        return entries.ToList();
    }

    public async Task<KnowledgeBaseEntry> AddKnowledgeEntryAsync(string question, string answer, string? provider)
    {
        var entry = new KnowledgeBaseEntry
        {
            Id = Guid.NewGuid().ToString(),
            Question = question,
            Answer = answer,
            Provider = provider,
            HitCount = 1,
            IsValid = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var questionHash = ComputeHash(question);

        await _db.ExecuteAsync(@"
            INSERT INTO knowledge_base (id, question_hash, question, answer, provider, usage_count, is_valid, created_at, updated_at)
            VALUES (@Id, @QuestionHash, @Question, @Answer, @Provider, 1, TRUE, @CreatedAt, @UpdatedAt)
            ON DUPLICATE KEY UPDATE usage_count = usage_count + 1, answer = @Answer, updated_at = @UpdatedAt",
            new {
                entry.Id,
                QuestionHash = questionHash,
                entry.Question,
                entry.Answer,
                entry.Provider,
                entry.CreatedAt,
                entry.UpdatedAt
            });

        return entry;
    }

    public async Task<bool> DeleteKnowledgeEntryAsync(string entryId)
    {
        var result = await _db.ExecuteAsync(
            "DELETE FROM knowledge_base WHERE id = @Id",
            new { Id = entryId });
        return result > 0;
    }

    public async Task ValidateKnowledgeEntryAsync(string entryId, bool isValid)
    {
        await _db.ExecuteAsync(@"
            UPDATE knowledge_base 
            SET is_valid = @IsValid, invalidated_at = @InvalidatedAt, updated_at = @UpdatedAt 
            WHERE id = @Id",
            new {
                Id = entryId,
                IsValid = isValid,
                InvalidatedAt = isValid ? null : (DateTime?)DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
    }

    public async Task<List<AgentActivity>> GetAgentActivityAsync(int limit)
    {
        var activity = await _db.QueryAsync<AgentActivity>(
            "SELECT * FROM agent_activity ORDER BY timestamp DESC LIMIT @Limit",
            new { Limit = limit });
        return activity.ToList();
    }

    // Implementation of Agents.IAIService.GenerateAsync
    public async Task<AIResponse> GenerateAsync(string prompt, string? systemPrompt = null, int maxTokens = 4000)
    {
        // Try Emergent LLM first
        var emergentKey = _config["EmergentLLM:Key"];
        if (!string.IsNullOrEmpty(emergentKey))
        {
            try
            {
                var response = await CallEmergentLLMAsync(prompt, systemPrompt, emergentKey, maxTokens);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Emergent LLM call failed, trying free providers");
            }
        }

        // Try free providers
        var providers = await GetFreeAIProvidersAsync();
        foreach (var provider in providers.Where(p => p.IsEnabled).OrderBy(p => p.Priority))
        {
            try
            {
                return await CallFreeProviderAsync(prompt, systemPrompt, provider, maxTokens);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Free provider {Provider} failed", provider.Provider);
            }
        }

        throw new InvalidOperationException("No AI providers available");
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(string prompt, string? systemPrompt = null)
    {
        // For now, just return the full response in one chunk
        var response = await GenerateAsync(prompt, systemPrompt);
        yield return response.Content;
    }

    private async Task<AIResponse> CallEmergentLLMAsync(string prompt, string? systemPrompt, string apiKey, int maxTokens)
    {
        // Implementation would call the Emergent LLM API
        // For now, return a placeholder
        await Task.CompletedTask;
        var content = $"Response to: {prompt.Substring(0, Math.Min(50, prompt.Length))}...";
        return new AIResponse(content, "emergent", "emergent-default", maxTokens / 2);
    }

    private async Task<AIResponse> CallFreeProviderAsync(string prompt, string? systemPrompt, FreeAIProvider provider, int maxTokens)
    {
        // Implementation would call the respective free provider's API
        await Task.CompletedTask;
        var content = $"Response from {provider.Name}: {prompt.Substring(0, Math.Min(50, prompt.Length))}...";
        return new AIResponse(content, provider.Provider, provider.Model ?? provider.Provider, maxTokens / 2);
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input.ToLower().Trim()));
        return Convert.ToHexString(bytes);
    }
}
