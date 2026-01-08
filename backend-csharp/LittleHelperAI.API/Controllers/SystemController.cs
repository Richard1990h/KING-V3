// System Controller - General endpoints for agents, health, etc.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using LittleHelperAI.Agents;
using LittleHelperAI.API.Services;

namespace LittleHelperAI.API.Controllers;

[ApiController]
[Route("api")]
public class SystemController : ControllerBase
{
    private readonly IAgentRegistry _agentRegistry;
    private readonly AIService _aiService;
    private readonly IConfiguration _config;

    public SystemController(IAgentRegistry agentRegistry, AIService aiService, IConfiguration config)
    {
        _agentRegistry = agentRegistry;
        _aiService = aiService;
        _config = config;
    }

    // Health check
    [HttpGet("health")]
    public ActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    // Get all agents
    [HttpGet("agents")]
    public ActionResult GetAgents()
    {
        var agents = _agentRegistry.GetAllAgents();
        return Ok(agents.Select(a => new
        {
            id = a.Id,
            name = a.Name,
            description = a.Description,
            capabilities = a.Capabilities
        }));
    }

    // Get available languages
    [HttpGet("languages")]
    public ActionResult GetLanguages()
    {
        return Ok(new[]
        {
            new { code = "en", name = "English" },
            new { code = "es", name = "Español" },
            new { code = "fr", name = "Français" },
            new { code = "de", name = "Deutsch" },
            new { code = "it", name = "Italiano" },
            new { code = "pt", name = "Português" },
            new { code = "zh", name = "中文" },
            new { code = "ja", name = "日本語" },
            new { code = "ko", name = "한국어" },
            new { code = "ru", name = "Русский" }
        });
    }

    // Estimate cost for a prompt
    [HttpPost("estimate-cost")]
    [Authorize]
    public ActionResult EstimateCost([FromBody] EstimateCostRequest request)
    {
        // Simple estimation: ~4 chars = 1 token, 1000 tokens = 0.5 credits
        var estimatedTokens = (request.Prompt?.Length ?? 0) / 4;
        var estimatedCredits = (estimatedTokens / 1000.0) * 0.5;
        
        return Ok(new
        {
            estimated_tokens = estimatedTokens,
            estimated_credits = Math.Max(0.1, estimatedCredits),
            model = request.Model ?? "default"
        });
    }

    // AI Providers endpoints
    [HttpGet("ai-providers")]
    public async Task<ActionResult> GetAIProviders()
    {
        var providers = await _aiService.GetFreeAIProvidersAsync();
        return Ok(providers.Where(p => p.IsEnabled).Select(p => new
        {
            id = p.Id,
            name = p.Name,
            provider = p.Provider,
            model = p.Model,
            enabled = p.IsEnabled
        }));
    }

    [HttpGet("ai-providers/user")]
    [Authorize]
    public ActionResult GetUserAIProviders()
    {
        // Return user's configured providers (placeholder)
        return Ok(new List<object>());
    }

    // LLM endpoints
    [HttpGet("llm/models")]
    public async Task<ActionResult> GetLLMModels()
    {
        var providers = await _aiService.GetFreeAIProvidersAsync();
        var models = providers
            .Where(p => p.IsEnabled && !string.IsNullOrEmpty(p.Model))
            .Select(p => new
            {
                id = $"{p.Provider}:{p.Model}",
                provider = p.Provider,
                model = p.Model,
                name = p.Name
            });
        return Ok(models);
    }

    [HttpPost("llm/generate")]
    [Authorize]
    public async Task<ActionResult> GenerateLLM([FromBody] LLMGenerateRequest request)
    {
        try
        {
            var response = await _aiService.GenerateAsync(request.Prompt, request.SystemPrompt, request.MaxTokens);
            return Ok(new
            {
                content = response.Content,
                provider = response.Provider,
                model = response.Model,
                tokens_used = response.TokensUsed
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    // Assistant chat endpoint
    [HttpPost("assistant/chat")]
    [Authorize]
    public async Task<ActionResult> AssistantChat([FromBody] AssistantChatRequest request)
    {
        try
        {
            var systemPrompt = "You are LittleHelper AI, a helpful coding assistant. Provide clear, concise answers.";
            var response = await _aiService.GenerateAsync(request.Message, systemPrompt, 2000);
            
            return Ok(new
            {
                id = Guid.NewGuid().ToString(),
                role = "assistant",
                content = response.Content,
                provider = response.Provider,
                model = response.Model,
                tokens_used = response.TokensUsed
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }
}

// Request models
public record EstimateCostRequest(string? Prompt, string? Model);
public record LLMGenerateRequest(string Prompt, string? SystemPrompt, int MaxTokens = 2000);
public record AssistantChatRequest(string Message, string? ConversationId);
