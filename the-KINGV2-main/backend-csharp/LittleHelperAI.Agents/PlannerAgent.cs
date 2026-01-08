// Planner Agent - Analyzes requirements and creates job breakdown
using System.Text.Json;

namespace LittleHelperAI.Agents;

public class PlannerAgent : BaseAgent
{
    public override string AgentId => "planner";
    public override string AgentName => "Planner";
    public override string AgentColor => "#D946EF";
    public override string AgentIcon => "LayoutGrid";
    public override string AgentDescription => "Analyzes requirements and creates detailed execution plans with job breakdown";

    public PlannerAgent(IAIService aiService) : base(aiService) { }

    protected override string BuildSystemPrompt(ProjectContext? context)
    {
        return @"You are an expert project planner and software architect. Your role is to:

1. ANALYZE user requirements thoroughly
2. BREAK DOWN the project into clear, actionable tasks
3. CREATE a step-by-step execution plan
4. ESTIMATE complexity and effort for each task
5. IDENTIFY dependencies between tasks
6. ASSIGN appropriate agents to each task

AVAILABLE AGENTS:
- researcher: Gathers documentation and best practices
- developer: Writes code and creates files
- test_designer: Creates test cases and test files
- executor: Runs and tests code
- debugger: Fixes errors and bugs
- verifier: Validates against requirements

You MUST respond with a valid JSON object in this exact format:
{
    ""project_summary"": ""Brief description of what will be built"",
    ""total_files_estimated"": 5,
    ""complexity"": ""medium"",
    ""tasks"": [
        {
            ""id"": ""task-1"",
            ""title"": ""Research best practices"",
            ""description"": ""Detailed description"",
            ""agent_type"": ""researcher"",
            ""order"": 1,
            ""estimated_tokens"": 1000,
            ""dependencies"": [],
            ""deliverables"": [""list of outputs""]
        }
    ],
    ""estimated_total_tokens"": 10000
}";
    }

    public override async Task<AgentResult> ExecuteAsync(string task, ProjectContext? context = null, ExecutionContext? execContext = null)
    {
        var prompt = BuildPrompt(task, context, execContext);
        prompt += "\n\nCreate a detailed task breakdown for this project. Respond with ONLY valid JSON.";

        try
        {
            var response = await _aiService.GenerateAsync(prompt, BuildSystemPrompt(context));
            var plan = ParseJsonFromResponse(response.Content);

            if (plan != null && plan.ContainsKey("tasks"))
            {
                var tasks = new List<TaskOutput>();
                var tasksJson = plan["tasks"].ToString();
                var taskList = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(tasksJson ?? "[]");

                if (taskList != null)
                {
                    for (int i = 0; i < taskList.Count; i++)
                    {
                        var t = taskList[i];
                        var taskId = t.ContainsKey("id") ? t["id"]?.ToString() ?? $"task-{i + 1}" : $"task-{i + 1}";
                        var title = t.ContainsKey("title") ? t["title"]?.ToString() ?? $"Task {i + 1}" : $"Task {i + 1}";
                        var description = t.ContainsKey("description") ? t["description"]?.ToString() ?? "" : "";
                        var agentType = t.ContainsKey("agent_type") ? t["agent_type"]?.ToString() ?? "developer" : "developer";
                        var estimatedTokens = 500;
                        if (t.ContainsKey("estimated_tokens"))
                        {
                            int.TryParse(t["estimated_tokens"]?.ToString(), out estimatedTokens);
                        }
                        
                        tasks.Add(new TaskOutput(taskId, title, description, agentType, i + 1, estimatedTokens, new List<string>()));
                    }
                }

                return new AgentResult
                {
                    Success = true,
                    Content = response.Content,
                    TokensUsed = response.Tokens,
                    TasksGenerated = tasks,
                    Metadata = new Dictionary<string, object>
                    {
                        ["project_summary"] = plan.GetValueOrDefault("project_summary")?.ToString() ?? "",
                        ["complexity"] = plan.GetValueOrDefault("complexity")?.ToString() ?? "medium",
                        ["estimated_total_tokens"] = tasks.Sum(t => t.EstimatedTokens)
                    }
                };
            }

            // Fallback plan
            return CreateFallbackPlan(task, response.Content, response.Tokens, context?.Language ?? "Python");
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

    private AgentResult CreateFallbackPlan(string task, string aiResponse, int tokens, string language)
    {
        var taskPreview = task.Length > 100 ? task.Substring(0, 100) : task;
        var taskSummary = task.Length > 200 ? task.Substring(0, 200) : task;
        
        var tasks = new List<TaskOutput>
        {
            new TaskOutput("task-1", "Research Requirements", $"Research best practices for: {taskPreview}", "researcher", 1, 800, new List<string>()),
            new TaskOutput("task-2", "Create Project Structure", $"Create the initial {language} project structure", "developer", 2, 1500, new List<string>()),
            new TaskOutput("task-3", "Implement Core Logic", "Implement the main functionality", "developer", 3, 2000, new List<string>()),
            new TaskOutput("task-4", "Create Tests", "Create test cases for implementation", "test_designer", 4, 1000, new List<string>()),
            new TaskOutput("task-5", "Verify Implementation", "Verify implementation meets requirements", "verifier", 5, 500, new List<string>())
        };

        return new AgentResult
        {
            Success = true,
            Content = aiResponse,
            TokensUsed = tokens,
            TasksGenerated = tasks,
            Metadata = new Dictionary<string, object>
            {
                ["project_summary"] = taskSummary,
                ["complexity"] = "medium",
                ["fallback_used"] = true,
                ["estimated_total_tokens"] = tasks.Sum(t => t.EstimatedTokens)
            }
        };
    }
}
