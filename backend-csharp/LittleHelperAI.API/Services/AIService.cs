// AI Service Implementation - Implements both API and Agent interfaces
using LittleHelperAI.Data;
using LittleHelperAI.Data.Models;
using System.Text.Json;
using System.Net.Http.Headers;

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
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
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
            local_llm = new
            {
                url = _config["LocalLLM:Url"] ?? "http://localhost:11434",
                model = _config["LocalLLM:Model"] ?? "qwen2.5-coder:1.5b"
            },
            timestamp = DateTime.UtcNow
        };
    }

    // Implementation of Agents.IAIService.CheckHealthAsync
    async Task<Agents.HealthStatus> Agents.IAIService.CheckHealthAsync()
    {
        var providers = await GetFreeAIProvidersAsync();
        var availableModels = providers
            .Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.Model))
            .Select(p => $"{p.Provider}:{p.Model}")
            .ToList();

        return new Agents.HealthStatus(
            "connected",
            "available",
            "configured",
            availableModels
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
    public async Task<Agents.AIResponse> GenerateAsync(string prompt, string? systemPrompt = null, int maxTokens = 4000)
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

        // Try free providers in order of priority
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

        // Try local Ollama as fallback
        try
        {
            return await CallOllamaAsync(prompt, systemPrompt, maxTokens);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Local Ollama failed");
        }

        throw new InvalidOperationException("No AI providers available. Please configure at least one provider in the admin panel.");
    }

    public async IAsyncEnumerable<string> GenerateStreamingAsync(string prompt, string? systemPrompt = null)
    {
        // For now, just return the full response in one chunk
        var response = await GenerateAsync(prompt, systemPrompt);
        yield return response.Content;
    }

    private async Task<Agents.AIResponse> CallEmergentLLMAsync(string prompt, string? systemPrompt, string apiKey, int maxTokens)
    {
        // Emergent LLM uses OpenAI-compatible API
        var requestBody = new
        {
            model = "gpt-4o-mini",
            messages = BuildMessages(prompt, systemPrompt),
            max_tokens = maxTokens,
            temperature = 0.7
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        
        var content = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var tokens = result.GetProperty("usage").GetProperty("total_tokens").GetInt32();
        
        return new Agents.AIResponse(content, "emergent", "gpt-4o-mini", tokens);
    }

    private async Task<Agents.AIResponse> CallFreeProviderAsync(string prompt, string? systemPrompt, FreeAIProvider provider, int maxTokens)
    {
        return provider.Provider.ToLower() switch
        {
            "groq" => await CallGroqAsync(prompt, systemPrompt, provider, maxTokens),
            "together" => await CallTogetherAsync(prompt, systemPrompt, provider, maxTokens),
            "huggingface" => await CallHuggingFaceAsync(prompt, systemPrompt, provider, maxTokens),
            "openrouter" => await CallOpenRouterAsync(prompt, systemPrompt, provider, maxTokens),
            "ollama" => await CallOllamaAsync(prompt, systemPrompt, maxTokens),
            _ => throw new NotSupportedException($"Provider {provider.Provider} is not supported")
        };
    }

    private async Task<Agents.AIResponse> CallGroqAsync(string prompt, string? systemPrompt, FreeAIProvider provider, int maxTokens)
    {
        if (string.IsNullOrEmpty(provider.ApiKey))
            throw new InvalidOperationException("Groq API key not configured");

        var requestBody = new
        {
            model = provider.Model ?? "llama-3.1-70b-versatile",
            messages = BuildMessages(prompt, systemPrompt),
            max_tokens = maxTokens,
            temperature = 0.7
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        
        var content = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var tokens = result.TryGetProperty("usage", out var usage) 
            ? usage.GetProperty("total_tokens").GetInt32() 
            : maxTokens / 2;
        
        return new Agents.AIResponse(content, "groq", provider.Model ?? "llama-3.1-70b-versatile", tokens);
    }

    private async Task<Agents.AIResponse> CallTogetherAsync(string prompt, string? systemPrompt, FreeAIProvider provider, int maxTokens)
    {
        if (string.IsNullOrEmpty(provider.ApiKey))
            throw new InvalidOperationException("Together AI API key not configured");

        var requestBody = new
        {
            model = provider.Model ?? "meta-llama/Llama-3.2-11B-Vision-Instruct-Turbo",
            messages = BuildMessages(prompt, systemPrompt),
            max_tokens = maxTokens,
            temperature = 0.7
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.together.xyz/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        
        var content = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var tokens = result.TryGetProperty("usage", out var usage) 
            ? usage.GetProperty("total_tokens").GetInt32() 
            : maxTokens / 2;
        
        return new Agents.AIResponse(content, "together", provider.Model ?? "llama-3.2", tokens);
    }

    private async Task<Agents.AIResponse> CallOpenRouterAsync(string prompt, string? systemPrompt, FreeAIProvider provider, int maxTokens)
    {
        if (string.IsNullOrEmpty(provider.ApiKey))
            throw new InvalidOperationException("OpenRouter API key not configured");

        var requestBody = new
        {
            model = provider.Model ?? "google/gemma-2-9b-it:free",
            messages = BuildMessages(prompt, systemPrompt),
            max_tokens = maxTokens,
            temperature = 0.7
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
        request.Headers.Add("HTTP-Referer", "https://littlehelper.ai");
        request.Headers.Add("X-Title", "LittleHelper AI");

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        
        var content = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        var tokens = result.TryGetProperty("usage", out var usage) 
            ? usage.GetProperty("total_tokens").GetInt32() 
            : maxTokens / 2;
        
        return new Agents.AIResponse(content, "openrouter", provider.Model ?? "gemma-2-9b-it", tokens);
    }

    private async Task<Agents.AIResponse> CallHuggingFaceAsync(string prompt, string? systemPrompt, FreeAIProvider provider, int maxTokens)
    {
        if (string.IsNullOrEmpty(provider.ApiKey))
            throw new InvalidOperationException("HuggingFace API key not configured");

        var model = provider.Model ?? "microsoft/DialoGPT-large";
        var fullPrompt = string.IsNullOrEmpty(systemPrompt) ? prompt : $"{systemPrompt}\n\n{prompt}";
        
        var requestBody = new
        {
            inputs = fullPrompt,
            parameters = new { max_new_tokens = maxTokens, temperature = 0.7 }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"https://api-inference.huggingface.co/models/{model}")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        
        string content;
        if (result.ValueKind == JsonValueKind.Array)
        {
            content = result[0].TryGetProperty("generated_text", out var text) 
                ? text.GetString() ?? ""
                : result[0].GetString() ?? "";
        }
        else
        {
            content = result.GetProperty("generated_text").GetString() ?? "";
        }
        
        return new Agents.AIResponse(content, "huggingface", model, maxTokens / 2);
    }

    private async Task<Agents.AIResponse> CallOllamaAsync(string prompt, string? systemPrompt, int maxTokens)
    {
        var ollamaUrl = _config["LocalLLM:Url"] ?? "http://localhost:11434";
        var model = _config["LocalLLM:Model"] ?? "qwen2.5-coder:1.5b";

        var requestBody = new
        {
            model,
            messages = BuildMessages(prompt, systemPrompt),
            stream = false,
            options = new { num_predict = maxTokens }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{ollamaUrl}/api/chat")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody), System.Text.Encoding.UTF8, "application/json")
        };

        var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<JsonElement>(json);
        
        var content = result.GetProperty("message").GetProperty("content").GetString() ?? "";
        var tokens = result.TryGetProperty("eval_count", out var count) ? count.GetInt32() : maxTokens / 2;
        
        return new Agents.AIResponse(content, "ollama", model, tokens);
    }

    private static object[] BuildMessages(string prompt, string? systemPrompt)
    {
        var messages = new List<object>();
        
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new { role = "system", content = systemPrompt });
        }
        
        messages.Add(new { role = "user", content = prompt });
        
        return messages.ToArray();
    }

    private static string ComputeHash(string input)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input.ToLower().Trim()));
        return Convert.ToHexString(bytes);
    }
}
