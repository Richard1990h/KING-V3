// Base Agent Interface and Implementation
using System.Text.Json;
using System.Text.RegularExpressions;

namespace LittleHelperAI.Agents;

/// <summary>
/// Result from an agent execution
/// </summary>
public class AgentResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = "";
    public int TokensUsed { get; set; }
    public List<FileOutput> FilesCreated { get; set; } = new();
    public List<TaskOutput> TasksGenerated { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public Dictionary<string, object> Metadata { get; set; } = new();
}

public record FileOutput(string Path, string Content);
public record TaskOutput(
    string Id, string Title, string Description, string AgentType,
    int Order, int EstimatedTokens, List<string> Dependencies
);

/// <summary>
/// Interface for all agents
/// </summary>
public interface IAgent
{
    string AgentId { get; }
    string AgentName { get; }
    string AgentColor { get; }
    string AgentIcon { get; }
    string AgentDescription { get; }

    Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null);
    IAsyncEnumerable<string> ExecuteStreamingAsync(string task, ProjectContext? context = null);
}

/// <summary>
/// Project context for agent execution
/// </summary>
public class ProjectContext
{
    public string Name { get; set; } = "";
    public string Language { get; set; } = "Python";
    public string Description { get; set; } = "";
}

/// <summary>
/// Execution context with previous outputs and files
/// </summary>
public class ExecutionContext
{
    public List<PreviousOutput> PreviousOutputs { get; set; } = new();
    public List<ExistingFile> ExistingFiles { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public string? OriginalRequirements { get; set; }
}

public record PreviousOutput(string Agent, string Summary);
public record ExistingFile(string Path, string Content);

/// <summary>
/// Base implementation for all agents
/// </summary>
public abstract class BaseAgent : IAgent
{
    protected readonly IAIService _aiService;
    
    public abstract string AgentId { get; }
    public abstract string AgentName { get; }
    public abstract string AgentColor { get; }
    public abstract string AgentIcon { get; }
    public abstract string AgentDescription { get; }

    protected BaseAgent(IAIService aiService)
    {
        _aiService = aiService;
    }

    protected abstract string BuildSystemPrompt(ProjectContext? context);

    public abstract Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null);

    public virtual async IAsyncEnumerable<string> ExecuteStreamingAsync(string task, ProjectContext? context = null)
    {
        var prompt = BuildPrompt(task, context, null);
        var systemPrompt = BuildSystemPrompt(context);
        
        await foreach (var chunk in _aiService.GenerateStreamingAsync(prompt, systemPrompt))
        {
            yield return chunk;
        }
    }

    protected string BuildPrompt(string task, ProjectContext? context, ExecutionContext? execContext)
    {
        var parts = new List<string>();

        if (context != null)
        {
            parts.Add("## Project Context");
            parts.Add($"Language: {context.Language}");
            parts.Add($"Project: {context.Name}");
            if (!string.IsNullOrEmpty(context.Description))
                parts.Add($"Description: {context.Description}");
            parts.Add("");
        }

        if (execContext != null)
        {
            if (execContext.PreviousOutputs.Any())
            {
                parts.Add("## Previous Agent Outputs");
                foreach (var output in execContext.PreviousOutputs.TakeLast(3))
                {
                    var summary = output.Summary.Length > 500 ? output.Summary[..500] : output.Summary;
                    parts.Add($"[{output.Agent}]: {summary}");
                }
                parts.Add("");
            }

            if (execContext.ExistingFiles.Any())
            {
                parts.Add("## Existing Files");
                foreach (var file in execContext.ExistingFiles.Take(10))
                {
                    parts.Add($"- {file.Path}");
                }
                parts.Add("");
            }

            if (execContext.Errors.Any())
            {
                parts.Add("## Errors to Address");
                foreach (var error in execContext.Errors)
                {
                    parts.Add($"- {error}");
                }
                parts.Add("");
            }
        }

        parts.Add("## Task");
        parts.Add(task);

        return string.Join("\n", parts);
    }

    protected Dictionary<string, object>? ParseJsonFromResponse(string response)
    {
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(response);
        }
        catch { }

        // Try to find JSON in code blocks
        var jsonMatch = Regex.Match(response, @"```(?:json)?\s*([\s\S]*?)```");
        if (jsonMatch.Success)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonMatch.Groups[1].Value.Trim());
            }
            catch { }
        }

        // Try to find raw JSON object
        jsonMatch = Regex.Match(response, @"\{[\s\S]*\}");
        if (jsonMatch.Success)
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, object>>(jsonMatch.Value);
            }
            catch { }
        }

        return null;
    }

    protected List<FileOutput> ExtractCodeBlocks(string response)
    {
        var files = new List<FileOutput>();
        var pattern = @"###\s*([\w/.\-]+\.[\w]+)\s*\n```(?:[\w]*)?\n([\s\S]*?)```";
        var matches = Regex.Matches(response, pattern);

        foreach (Match match in matches)
        {
            files.Add(new FileOutput(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
        }

        return files;
    }
}

/// <summary>
/// AI Service interface for agents
/// </summary>
public interface IAIService
{
    Task<AIResponse> GenerateAsync(string prompt, string? systemPrompt = null, int maxTokens = 4000);
    IAsyncEnumerable<string> GenerateStreamingAsync(string prompt, string? systemPrompt = null);
    Task<HealthStatus> CheckHealthAsync();
}

public record AIResponse(string Content, string Provider, string Model, int Tokens);
public record HealthStatus(string Database, string LocalLlm, string Stripe, List<string>? AvailableModels);
