// Executor Agent - Runs code in isolated environment
namespace LittleHelperAI.Agents;

public class ExecutorAgent : BaseAgent
{
    public override string AgentId => "executor";
    public override string AgentName => "Executor";
    public override string AgentColor => "#3B82F6";
    public override string AgentIcon => "Play";
    public override string AgentDescription => "Runs code in isolated sandbox and captures results";

    public ExecutorAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        return @"You are a code execution specialist. Your role is to:

1. ANALYZE the code to determine how to run it
2. IDENTIFY required dependencies
3. PREPARE the execution environment
4. EXECUTE the code safely
5. CAPTURE and analyze output

Respond with execution plan in JSON format:
```json
{
    ""language"": ""python"",
    ""main_file"": ""main.py"",
    ""dependencies"": [""package1""],
    ""setup_commands"": [""pip install -r requirements.txt""],
    ""run_command"": ""python main.py"",
    ""expected_behavior"": ""Description""
}
```";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);

        try
        {
            // For production, this would use Docker/sandboxing
            // For now, we analyze and provide execution plan
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));

            return new AgentResult
            {
                Success = true,
                Content = response.Content,
                TokensUsed = response.Tokens,
                Metadata = new Dictionary<string, object>
                {
                    ["analysis_only"] = true,
                    ["requires_sandbox"] = true
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
