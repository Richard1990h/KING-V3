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
            
            // Save conversation to database
            var userId = User.FindFirst("user_id")?.Value ?? "anonymous";
            await SaveChatMessage(userId, conversationId, "user", request.Message, null);
            await SaveChatMessage(userId, conversationId, "assistant", response.Content ?? "", response.Provider);
            
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

    // Get user conversations list
    [HttpGet("conversations")]
    [Authorize]
    public async Task<ActionResult> GetConversations()
    {
        try
        {
            var userId = User.FindFirst("user_id")?.Value ?? "";
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { detail = "User not authenticated" });
            }

            var connectionString = _config.GetConnectionString("DefaultConnection");
            using var conn = new MySqlConnector.MySqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT DISTINCT conversation_id, 
                       MAX(conversation_title) as title,
                       MIN(timestamp) as created_at,
                       MAX(timestamp) as last_message_at,
                       COUNT(*) as message_count
                FROM chat_history 
                WHERE user_id = @UserId 
                  AND conversation_id IS NOT NULL 
                  AND deleted_by_user = 0
                  AND project_id IS NULL
                GROUP BY conversation_id
                ORDER BY MAX(timestamp) DESC
                LIMIT 50";

            using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);

            var conversations = new List<object>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                conversations.Add(new
                {
                    id = reader["conversation_id"]?.ToString(),
                    title = reader["title"]?.ToString() ?? "New Conversation",
                    created_at = reader["created_at"],
                    last_message_at = reader["last_message_at"],
                    message_count = Convert.ToInt32(reader["message_count"])
                });
            }

            return Ok(conversations);
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    // Get messages for a specific conversation
    [HttpGet("conversations/{conversationId}")]
    [Authorize]
    public async Task<ActionResult> GetConversationMessages(string conversationId)
    {
        try
        {
            var userId = User.FindFirst("user_id")?.Value ?? "";
            
            var connectionString = _config.GetConnectionString("DefaultConnection");
            using var conn = new MySqlConnector.MySqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT id, role, content, agent_id, provider, model, tokens_used, timestamp
                FROM chat_history 
                WHERE user_id = @UserId 
                  AND conversation_id = @ConversationId 
                  AND deleted_by_user = 0
                ORDER BY timestamp ASC";

            using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ConversationId", conversationId);

            var messages = new List<object>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                messages.Add(new
                {
                    id = reader["id"]?.ToString(),
                    role = reader["role"]?.ToString(),
                    content = reader["content"]?.ToString(),
                    agent = reader["agent_id"]?.ToString(),
                    provider = reader["provider"]?.ToString(),
                    model = reader["model"]?.ToString(),
                    tokens_used = reader["tokens_used"] != DBNull.Value ? Convert.ToInt32(reader["tokens_used"]) : 0,
                    timestamp = reader["timestamp"]
                });
            }

            return Ok(messages);
        }
        catch (Exception ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    // Helper method to save chat messages
    private async Task SaveChatMessage(string userId, string conversationId, string role, string content, string? provider)
    {
        try
        {
            var connectionString = _config.GetConnectionString("DefaultConnection");
            using var conn = new MySqlConnector.MySqlConnection(connectionString);
            await conn.OpenAsync();

            var sql = @"INSERT INTO chat_history (id, user_id, conversation_id, role, content, provider, timestamp) 
                        VALUES (@Id, @UserId, @ConversationId, @Role, @Content, @Provider, @Timestamp)";

            using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@Id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("@UserId", userId);
            cmd.Parameters.AddWithValue("@ConversationId", conversationId);
            cmd.Parameters.AddWithValue("@Role", role);
            cmd.Parameters.AddWithValue("@Content", content);
            cmd.Parameters.AddWithValue("@Provider", provider ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@Timestamp", DateTime.UtcNow);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving chat message: {ex.Message}");
        }
    }

    // AI Plan endpoint - generates a build plan for a project
    [HttpPost("ai/plan")]
    [Authorize]
    public async Task<ActionResult> CreateBuildPlan([FromBody] BuildPlanRequest request)
    {
        try
        {
            // Support both 'prompt' and 'request' field names from frontend
            var userRequest = request.Prompt ?? request.Request ?? "";
            if (string.IsNullOrWhiteSpace(userRequest))
            {
                return BadRequest(new { detail = "No prompt or request provided" });
            }

            var systemPrompt = @"You are a software architect AI. Given a user's request and existing project files, create a detailed build plan.
Return ONLY a valid JSON object with this exact structure (no markdown, no code blocks, just raw JSON):
{
    ""summary"": ""Brief summary of what will be built"",
    ""tasks"": [
        {
            ""id"": ""task-1"",
            ""title"": ""Task title"",
            ""description"": ""Detailed description of what code to write and in which file"",
            ""agent"": ""developer"",
            ""estimatedCredits"": 5.0,
            ""dependencies"": []
        }
    ],
    ""totalEstimatedCredits"": 10.0
}
Rules:
1. Each task should describe ONE file to create or modify
2. Include the filename in the description (e.g., 'Create main.py with...')
3. Be specific about what code goes in each file
4. Return ONLY the JSON, no explanations before or after";

            var userPrompt = $"User Request: {userRequest}\n\nProject Language: {request.Language ?? "Python"}";
            
            var response = await _aiService.GenerateAsync(userPrompt, systemPrompt, 2000);
            
            // Try to extract JSON from response (in case LLM wrapped it)
            var content = response.Content ?? "{}";
            
            // Remove markdown code blocks if present
            if (content.Contains("```json"))
            {
                var start = content.IndexOf("```json") + 7;
                var end = content.LastIndexOf("```");
                if (end > start) content = content.Substring(start, end - start);
            }
            else if (content.Contains("```"))
            {
                var start = content.IndexOf("```") + 3;
                var end = content.LastIndexOf("```");
                if (end > start) content = content.Substring(start, end - start);
            }
            content = content.Trim();
            
            // Try to parse as JSON
            try
            {
                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                return Ok(json);
            }
            catch
            {
                // If not valid JSON, return a simple plan based on the user request
                return Ok(new
                {
                    summary = $"Build: {userRequest.Substring(0, Math.Min(100, userRequest.Length))}",
                    tasks = new[]
                    {
                        new
                        {
                            id = "task-1",
                            title = "Implement requested feature",
                            description = userRequest,
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

    // AI Execute Task endpoint - executes a single task from the plan and creates files
    [HttpPost("ai/execute-task")]
    [Authorize]
    public async Task<ActionResult> ExecuteTask([FromBody] ExecuteTaskRequest request)
    {
        try
        {
            // Support both 'task' and 'taskDescription' field names
            var taskDescription = request.Task ?? request.TaskDescription ?? "";
            var taskTitle = request.TaskTitle ?? taskDescription.Substring(0, Math.Min(50, taskDescription.Length));
            var projectId = request.ProjectId;
            
            var systemPrompt = $@"You are a {request.Agent ?? "developer"} AI agent.
Your job is to generate working code for the given task.

IMPORTANT: You MUST return a JSON response with this exact structure:
{{
    ""files"": [
        {{
            ""path"": ""filename.py"",
            ""content"": ""# actual code here...""
        }}
    ],
    ""message"": ""Brief description of what was created""
}}

Rules:
1. Extract the filename from the task description, or generate an appropriate one
2. Generate complete, working code - not placeholders
3. Return ONLY the JSON, no markdown code blocks
4. Use proper indentation in the code content (use \\n for newlines, \\t for tabs)
5. If creating a Python file, include proper imports and a main block if needed
6. If creating a web file (HTML/CSS/JS), make it complete and functional";

            var userPrompt = $"Task: {taskTitle}\nDescription: {taskDescription}\n\nGenerate the code and return as JSON with files array.";
            
            var response = await _aiService.GenerateAsync(userPrompt, systemPrompt, 4000);
            
            var content = response.Content ?? "{}";
            
            // Remove markdown code blocks if present
            if (content.Contains("```json"))
            {
                var start = content.IndexOf("```json") + 7;
                var end = content.LastIndexOf("```");
                if (end > start) content = content.Substring(start, end - start);
            }
            else if (content.Contains("```"))
            {
                var start = content.IndexOf("```") + 3;
                var end = content.LastIndexOf("```");
                if (end > start) content = content.Substring(start, end - start);
            }
            content = content.Trim();
            
            var createdFiles = new List<object>();
            string message = "Task completed";
            
            try
            {
                var json = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(content);
                
                if (json.TryGetProperty("files", out var filesArray) && filesArray.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    foreach (var fileObj in filesArray.EnumerateArray())
                    {
                        var path = fileObj.GetProperty("path").GetString() ?? "untitled.txt";
                        var fileContent = fileObj.GetProperty("content").GetString() ?? "";
                        
                        // Unescape the content (convert \n to actual newlines, etc.)
                        fileContent = System.Text.RegularExpressions.Regex.Unescape(fileContent);
                        
                        createdFiles.Add(new { path, content = fileContent });
                    }
                }
                
                if (json.TryGetProperty("message", out var msgProp))
                {
                    message = msgProp.GetString() ?? message;
                }
            }
            catch
            {
                // If JSON parsing fails, try to extract code blocks and create a file
                var codeMatch = System.Text.RegularExpressions.Regex.Match(content, @"```(\w+)?\s*([\s\S]*?)```");
                if (codeMatch.Success)
                {
                    var lang = codeMatch.Groups[1].Value;
                    var code = codeMatch.Groups[2].Value.Trim();
                    var extension = lang switch
                    {
                        "python" or "py" => ".py",
                        "javascript" or "js" => ".js",
                        "typescript" or "ts" => ".ts",
                        "html" => ".html",
                        "css" => ".css",
                        "json" => ".json",
                        "java" => ".java",
                        "csharp" or "cs" => ".cs",
                        _ => ".txt"
                    };
                    
                    // Try to extract filename from task
                    var fileNameMatch = System.Text.RegularExpressions.Regex.Match(taskDescription, @"(\w+\.\w+)");
                    var path = fileNameMatch.Success ? fileNameMatch.Groups[1].Value : $"generated{extension}";
                    
                    createdFiles.Add(new { path, content = code });
                    message = $"Generated {path}";
                }
                else
                {
                    // Just use the raw content as code
                    var path = "generated.txt";
                    var fileNameMatch = System.Text.RegularExpressions.Regex.Match(taskDescription, @"(\w+\.\w+)");
                    if (fileNameMatch.Success) path = fileNameMatch.Groups[1].Value;
                    
                    createdFiles.Add(new { path, content = content });
                    message = $"Generated {path}";
                }
            }
            
            return Ok(new
            {
                taskId = request.TaskId ?? Guid.NewGuid().ToString(),
                status = "completed",
                files = createdFiles,
                output = message,
                agent = request.Agent,
                tokensUsed = response.Tokens,
                creditsUsed = (response.Tokens / 1000.0) * 0.5
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                taskId = request.TaskId ?? Guid.NewGuid().ToString(),
                status = "failed",
                error = ex.Message,
                files = Array.Empty<object>(),
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
