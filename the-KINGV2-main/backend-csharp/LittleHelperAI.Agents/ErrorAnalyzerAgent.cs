// Error Analyzer Agent - Analyzes errors and dispatches fixes
namespace LittleHelperAI.Agents;

public class ErrorAnalyzerAgent : BaseAgent
{
    public override string AgentId => "error_analyzer";
    public override string AgentName => "Error Analyzer";
    public override string AgentColor => "#EC4899";
    public override string AgentIcon => "AlertTriangle";
    public override string AgentDescription => "Analyzes build/runtime errors and dispatches fixes";

    public ErrorAnalyzerAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        return @"You are an expert error analyst. Your role is to:

1. PARSE error messages and logs
2. CATEGORIZE errors by type and severity
3. IDENTIFY root causes
4. CREATE fix tasks for other agents
5. PRIORITIZE fixes by impact

ERROR CATEGORIES:
- SYNTAX: Syntax errors, typos
- IMPORT: Missing imports, wrong paths
- RUNTIME: Runtime exceptions
- LOGIC: Logical errors
- DEPENDENCY: Missing packages
- CONFIG: Configuration issues

Respond with JSON:
```json
{
    ""errors_found"": [
        {
            ""category"": ""SYNTAX"",
            ""severity"": ""high"",
            ""file"": ""main.py"",
            ""line"": 42,
            ""message"": ""Error message"",
            ""root_cause"": ""Why this occurred"",
            ""fix_description"": ""How to fix""
        }
    ],
    ""fix_tasks"": [
        {
            ""agent"": ""debugger"",
            ""priority"": 1,
            ""description"": ""Fix syntax error"",
            ""files_affected"": [""main.py""]
        }
    ],
    ""can_auto_fix"": true
}
```";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);

        if (execContext?.Errors.Any() == true)
        {
            prompt += "\n\n## Errors to Analyze\n";
            foreach (var err in execContext.Errors)
            {
                prompt += $"\n```\n{err}\n```\n";
            }
        }

        prompt += "\n\nAnalyze all errors and respond with JSON.";

        try
        {
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));
            var analysis = ParseJsonFromResponse(response.Content);

            var tasks = new List<TaskOutput>();
            // Parse fix tasks from analysis if available

            return new AgentResult
            {
                Success = true,
                Content = response.Content,
                TokensUsed = response.Tokens,
                TasksGenerated = tasks,
                Metadata = new Dictionary<string, object>
                {
                    ["analysis_complete"] = true
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
