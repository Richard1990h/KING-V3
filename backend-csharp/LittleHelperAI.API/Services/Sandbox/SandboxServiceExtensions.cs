// Sandbox Services Registration
// Extension methods for registering sandbox services in DI
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace LittleHelperAI.API.Services.Sandbox;

public static class SandboxServiceExtensions
{
    public static IServiceCollection AddSandboxServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        var sandboxConfig = configuration.GetSection("Sandbox").Get<SandboxConfig>() ?? new SandboxConfig();
        var rateLimitConfig = configuration.GetSection("RateLimits").Get<RateLimitConfig>() ?? new RateLimitConfig();
        var verificationConfig = configuration.GetSection("Verification").Get<VerificationConfig>() ?? new VerificationConfig();

        services.AddSingleton(sandboxConfig);
        services.AddSingleton(rateLimitConfig);
        services.AddSingleton(verificationConfig);

        // Core services
        services.AddSingleton<ISandboxExecutor, SandboxExecutor>();
        services.AddSingleton<IStaticAnalyzer, StaticAnalyzer>();
        services.AddSingleton<ITestGenerator, TestGenerator>();
        services.AddSingleton<IVerificationGate, VerificationGate>();
        services.AddSingleton<IRateLimiter, RateLimiter>();

        // Job queue (singleton to maintain state)
        services.AddSingleton<JobQueue>();
        services.AddSingleton<IJobQueue>(sp => sp.GetRequiredService<JobQueue>());

        // Pipeline (scoped for per-request instances)
        services.AddScoped<IAgentPipeline, AgentPipeline>();

        // Background worker
        services.AddHostedService<JobWorker>();

        return services;
    }

    public static IServiceCollection AddCodeGeneratorService<TImplementation>(this IServiceCollection services)
        where TImplementation : class, ICodeGeneratorService
    {
        services.AddScoped<ICodeGeneratorService, TImplementation>();
        return services;
    }
}

// Default Code Generator (placeholder - should be replaced with actual AI implementation)
public class DefaultCodeGenerator : ICodeGeneratorService
{
    private readonly ILogger<DefaultCodeGenerator> _logger;

    public DefaultCodeGenerator(ILogger<DefaultCodeGenerator> logger)
    {
        _logger = logger;
    }

    public Task<CodeGenerationResult> GenerateCodeAsync(CodeGenerationRequest request, CancellationToken ct = default)
    {
        _logger.LogWarning("Using placeholder code generator. Implement ICodeGeneratorService with actual AI service.");

        // This is a placeholder - actual implementation would call AI service (GPT, Claude, etc.)
        return Task.FromResult(new CodeGenerationResult
        {
            Success = false,
            ErrorMessage = "Code generation service not configured. Please implement ICodeGeneratorService."
        });
    }
}

// AI-powered Code Generator (example implementation structure)
public class AICodeGenerator : ICodeGeneratorService
{
    private readonly ILogger<AICodeGenerator> _logger;
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public AICodeGenerator(
        ILogger<AICodeGenerator> logger,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiKey = configuration["AI:ApiKey"] ?? "";
        _model = configuration["AI:Model"] ?? "gpt-4";
    }

    public async Task<CodeGenerationResult> GenerateCodeAsync(CodeGenerationRequest request, CancellationToken ct = default)
    {
        var result = new CodeGenerationResult();

        try
        {
            // Build the prompt
            var systemPrompt = BuildSystemPrompt(request.Language);
            var userPrompt = BuildUserPrompt(request);

            // Call AI API (structure depends on which AI service you use)
            var response = await CallAIServiceAsync(systemPrompt, userPrompt, ct);

            if (response.Success)
            {
                result.Success = true;
                result.Files = ParseCodeFromResponse(response.Content, request.Language);
                result.Explanation = ExtractExplanation(response.Content);
                result.TokensUsed = response.TokensUsed;
            }
            else
            {
                result.Success = false;
                result.ErrorMessage = response.Error;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Code generation failed for project {ProjectId}", request.ProjectId);
            result.Success = false;
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    private string BuildSystemPrompt(string language)
    {
        return $@"You are an expert {language} developer. Generate clean, well-structured, production-ready code.

Rules:
1. Follow {language} best practices and conventions
2. Include proper error handling
3. Add docstrings/comments for public APIs
4. Make code testable
5. Avoid hardcoded values - use configuration
6. Include type hints/annotations where applicable

Output format:
- Return code in markdown code blocks with filenames
- Example: ```python:main.py ... ```
- Include a brief explanation of the implementation";
    }

    private string BuildUserPrompt(CodeGenerationRequest request)
    {
        var prompt = new System.Text.StringBuilder();
        prompt.AppendLine(request.Prompt);

        if (request.ExistingFiles.Any())
        {
            prompt.AppendLine("\n--- EXISTING CODE ---");
            foreach (var file in request.ExistingFiles)
            {
                prompt.AppendLine($"\n### {file.Path}");
                prompt.AppendLine($"```{request.Language}");
                prompt.AppendLine(file.Content);
                prompt.AppendLine("```");
            }
        }

        if (request.Context.Any())
        {
            prompt.AppendLine("\n--- CONTEXT ---");
            foreach (var kv in request.Context)
            {
                prompt.AppendLine($"{kv.Key}: {kv.Value}");
            }
        }

        return prompt.ToString();
    }

    private async Task<AIResponse> CallAIServiceAsync(string systemPrompt, string userPrompt, CancellationToken ct)
    {
        // This is a placeholder structure - implement based on your AI provider
        // Example for OpenAI-compatible APIs:

        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt }
            },
            temperature = 0.2,
            max_tokens = 4000
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        _httpClient.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (response.IsSuccessStatusCode)
        {
            // Parse response - structure depends on API
            using var doc = System.Text.Json.JsonDocument.Parse(responseJson);
            var messageContent = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            var tokensUsed = doc.RootElement
                .GetProperty("usage")
                .GetProperty("total_tokens")
                .GetInt32();

            return new AIResponse
            {
                Success = true,
                Content = messageContent ?? "",
                TokensUsed = tokensUsed
            };
        }
        else
        {
            return new AIResponse
            {
                Success = false,
                Error = $"AI API error: {response.StatusCode} - {responseJson}"
            };
        }
    }

    private List<ProjectFile> ParseCodeFromResponse(string content, string language)
    {
        var files = new List<ProjectFile>();
        
        // Parse code blocks with filenames
        // Format: ```language:filename.ext
        var pattern = @"```(?:(\w+):)?([^\n`]+)?\n([\s\S]*?)```";
        var matches = System.Text.RegularExpressions.Regex.Matches(content, pattern);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var filename = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;
            var code = match.Groups[3].Value.Trim();

            if (!string.IsNullOrEmpty(code))
            {
                // Generate default filename if not provided
                if (string.IsNullOrEmpty(filename))
                {
                    filename = language.ToLower() switch
                    {
                        "python" => "main.py",
                        "javascript" => "index.js",
                        "typescript" => "index.ts",
                        "csharp" => "Program.cs",
                        "go" => "main.go",
                        "java" => "Main.java",
                        _ => $"code.{language}"
                    };
                }

                files.Add(new ProjectFile
                {
                    Path = filename,
                    Content = code
                });
            }
        }

        return files;
    }

    private string? ExtractExplanation(string content)
    {
        // Extract text outside of code blocks
        var withoutCode = System.Text.RegularExpressions.Regex.Replace(content, @"```[\s\S]*?```", "");
        var trimmed = withoutCode.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private class AIResponse
    {
        public bool Success { get; set; }
        public string Content { get; set; } = "";
        public string? Error { get; set; }
        public int TokensUsed { get; set; }
    }
}
