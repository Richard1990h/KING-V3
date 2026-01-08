// Debugger Agent - Identifies and fixes errors
namespace LittleHelperAI.Agents;

public class DebuggerAgent : BaseAgent
{
    public override string AgentId => "debugger";
    public override string AgentName => "Debugger";
    public override string AgentColor => "#EF4444";
    public override string AgentIcon => "Bug";
    public override string AgentDescription => "Identifies and fixes errors systematically";

    public DebuggerAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        return @"You are an expert debugger and code analyst. Your role is to:

1. ANALYZE error messages and stack traces
2. IDENTIFY root causes of bugs
3. PROPOSE specific fixes
4. EXPLAIN why the error occurred
5. PREVENT similar issues in the future

For each bug, provide:

## Error Analysis
[Detailed analysis]

## Root Cause
[Fundamental reason]

## Fix
Provide corrected file(s):

### filename.ext
```language
corrected code
```

## Prevention
[How to prevent this]";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);

        if (execContext?.Errors.Any() == true)
        {
            prompt += "\n\n## Errors to Fix\n";
            foreach (var err in execContext.Errors)
            {
                prompt += $"\n{err}\n";
            }
        }

        if (execContext?.ExistingFiles.Any() == true)
        {
            prompt += "\n\n## Current Code\n";
            foreach (var f in execContext.ExistingFiles)
            {
                prompt += $"\n### {f.Path}\n```\n{f.Content}\n```\n";
            }
        }

        prompt += "\n\nAnalyze the errors and provide COMPLETE fixed file(s).";

        try
        {
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));
            var files = ExtractCodeBlocks(response.Content);

            return new AgentResult
            {
                Success = true,
                Content = response.Content,
                TokensUsed = response.Tokens,
                FilesCreated = files,
                Metadata = new Dictionary<string, object>
                {
                    ["files_fixed"] = files.Count,
                    ["fixed_file_names"] = files.Select(f => f.Path).ToList()
                }
            };
        }
        catch (Exception ex)
        {
            return new AgentResult
            {
                Success = false,
                Content = ex.Message,
                Errors = new List<string> { ex.Message }
            };
        }
    }
}
