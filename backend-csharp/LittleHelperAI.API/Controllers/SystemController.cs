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
            color = a.Color,
            icon = a.Icon
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
                tokens_used = response.Tokens
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
            var systemPrompt = "You are LittleHelper AI, a helpful coding assistant. Provide clear, concise answers. Be friendly and conversational.";
            var response = await _aiService.GenerateAsync(request.Message, systemPrompt, 2000);
            
            var conversationId = request.ConversationId ?? Guid.NewGuid().ToString();
            var timestamp = DateTime.UtcNow.ToString("o");
            
            return Ok(new
            {
                conversation_id = conversationId,
                user_message = new {
                    id = Guid.NewGuid().ToString(),
                    role = "user",
                    content = request.Message,
                    timestamp = timestamp
                },
                ai_message = new {
                    id = Guid.NewGuid().ToString(),
                    role = "assistant",
                    content = response.Content,
                    provider = response.Provider,
                    model = response.Model,
                    tokens_used = response.Tokens,
                    timestamp = timestamp
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    // AI Plan endpoint - generates a build plan for a project
    [HttpPost("ai/plan")]
    [Authorize]
    public async Task<ActionResult> CreateBuildPlan([FromBody] BuildPlanRequest request)
    {
        try
        {
            var systemPrompt = @"You are a software architect AI. Given a user's request and existing project files, create a detailed build plan.
Return a JSON object with this exact structure:
{
    ""summary"": ""Brief summary of what will be built"",
    ""tasks"": [
        {
            ""id"": ""task-1"",
            ""title"": ""Task title"",
            ""description"": ""What this task does"",
            ""agent"": ""developer"",
            ""estimatedCredits"": 5.0,
            ""dependencies"": []
        }
    ],
    ""totalEstimatedCredits"": 10.0
}
Be concise and practical. Focus on the actual implementation steps.";

            var userPrompt = $"User Request: {request.Prompt}\n\nExisting Files: {string.Join(", ", request.ExistingFiles ?? Array.Empty<string>())}\n\nProject Language: {request.Language ?? "Python"}";
            
            var response = await _aiService.GenerateAsync(userPrompt, systemPrompt, 2000);
            
            // Try to parse as JSON, otherwise wrap in a simple structure
            try
            {
                var json = System.Text.Json.JsonSerializer.Deserialize<object>(response.Content ?? "{}");
                return Ok(json);
            }
            catch
            {
                // If not valid JSON, return a simple plan
                return Ok(new
                {
                    summary = response.Content?.Substring(0, Math.Min(200, response.Content?.Length ?? 0)) ?? "Build plan generated",
                    tasks = new[]
                    {
                        new
                        {
                            id = "task-1",
                            title = "Implement requested feature",
                            description = request.Prompt,
                            agent = "developer",
                            estimatedCredits = 5.0,
                            dependencies = Array.Empty<string>()
                        }
                    },
                    totalEstimatedCredits = 5.0
                });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = $"Failed to create build plan: {ex.Message}" });
        }
    }

    // AI Execute Task endpoint - executes a single task from the plan
    [HttpPost("ai/execute-task")]
    [Authorize]
    public async Task<ActionResult> ExecuteTask([FromBody] ExecuteTaskRequest request)
    {
        try
        {
            var systemPrompt = $@"You are a {request.Agent ?? "developer"} AI agent. 
Execute the following task and provide the code or content needed.
Be practical and provide working code. Use proper formatting.";

            var userPrompt = $"Task: {request.TaskTitle}\nDescription: {request.TaskDescription}\n\nContext:\n{request.Context ?? "No additional context"}";
            
            var response = await _aiService.GenerateAsync(userPrompt, systemPrompt, 4000);
            
            return Ok(new
            {
                taskId = request.TaskId,
                status = "completed",
                output = response.Content,
                agent = request.Agent,
                tokensUsed = response.Tokens,
                creditsUsed = (response.Tokens / 1000.0) * 0.5
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                taskId = request.TaskId,
                status = "failed",
                error = ex.Message,
                agent = request.Agent
            });
        }
    }
}

// Request models
public record EstimateCostRequest(string? Prompt, string? Model);
public record LLMGenerateRequest(string Prompt, string? SystemPrompt, int MaxTokens = 2000);
public record AssistantChatRequest(string Message, string? ConversationId);
public record BuildPlanRequest(string? Prompt, string? Request, string? ProjectId, string? Language, string[]? ExistingFiles, string[]? Agents);
public record ExecuteTaskRequest(string? TaskId, string? Task, string? TaskTitle, string? TaskDescription, string? Agent, string? Context, string? ProjectId);
