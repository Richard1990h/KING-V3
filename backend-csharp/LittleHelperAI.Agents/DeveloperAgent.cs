// Developer Agent - Writes code and creates files
using System.Text.RegularExpressions;

namespace LittleHelperAI.Agents;

public class DeveloperAgent : BaseAgent
{
    public override string AgentId => "developer";
    public override string AgentName => "Developer";
    public override string AgentColor => "#10B981";
    public override string AgentIcon => "Code";
    public override string AgentDescription => "Writes clean, efficient code with best practices";

    public DeveloperAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        var language = context?.Language ?? "Python";
        return $@"You are an expert {language} developer. Your role is to:

1. WRITE clean, efficient, well-documented code
2. FOLLOW best practices and design patterns
3. HANDLE edge cases and errors properly
4. CREATE modular, maintainable code structure
5. INCLUDE helpful comments

When creating files, use this EXACT format for each file:

### filename.ext
```language
code here
```

IMPORTANT RULES:
- Create ALL necessary files for the task
- Include proper imports and dependencies
- Add error handling
- Follow {language} conventions and style guides
- Create a complete, working implementation
- Include configuration files if needed";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);
        prompt += "\n\nCreate all necessary files for this task. Use the exact format: ### filename.ext followed by code block.";

        try
        {
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));
            var files = ExtractFilesFromResponse(response.Content);

            return new AgentResult
            {
                Success = true,
                Content = response.Content,
                TokensUsed = response.Tokens,
                FilesCreated = files,
                Metadata = new Dictionary<string, object>
                {
                    ["files_count"] = files.Count,
                    ["file_names"] = files.Select(f => f.Path).ToList()
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

    private List<FileOutput> ExtractFilesFromResponse(string response)
    {
        var files = new List<FileOutput>();

        // Pattern 1: ### filename.ext followed by code block
        var pattern1 = @"###\s*([\w/.\-]+\.[\w]+)\s*\n```(?:[\w]*)?\n([\s\S]*?)```";
        var matches = Regex.Matches(response, pattern1);
        foreach (Match match in matches)
        {
            files.Add(new FileOutput(match.Groups[1].Value.Trim(), match.Groups[2].Value.Trim()));
        }

        if (files.Any()) return files;

        // Pattern 2: File: filename.ext or **filename.ext**
        var pattern2 = @"(?:File:\s*|\*\*)?([\w/.\-]+\.[\w]+)(?:\*\*)?\s*\n```(?:[\w]*)?\n([\s\S]*?)```";
        matches = Regex.Matches(response, pattern2);
        foreach (Match match in matches)
        {
            var path = match.Groups[1].Value.Trim();
            if (!files.Any(f => f.Path == path))
            {
                files.Add(new FileOutput(path, match.Groups[2].Value.Trim()));
            }
        }

        return files;
    }
}
