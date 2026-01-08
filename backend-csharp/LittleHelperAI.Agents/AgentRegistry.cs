// Agent Registry - Provides access to all agents
using Microsoft.Extensions.DependencyInjection;

namespace LittleHelperAI.Agents;

/// <summary>
/// Registry for all available agents
/// </summary>
public interface IAgentRegistry
{
    IAgent GetAgent(string agentId);
    IEnumerable<AgentInfo> GetAllAgents();
}

public record AgentInfo(string Id, string Name, string Color, string Icon, string Description);

public class AgentRegistry : IAgentRegistry
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Dictionary<string, Type> _agentTypes;
    private readonly List<AgentInfo> _agentInfos;

    public AgentRegistry(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        
        _agentTypes = new Dictionary<string, Type>
        {
            ["planner"] = typeof(PlannerAgent),
            ["researcher"] = typeof(ResearcherAgent),
            ["developer"] = typeof(DeveloperAgent),
            ["test_designer"] = typeof(TestDesignerAgent),
            ["executor"] = typeof(ExecutorAgent),
            ["debugger"] = typeof(DebuggerAgent),
            ["verifier"] = typeof(VerifierAgent),
            ["error_analyzer"] = typeof(ErrorAnalyzerAgent)
        };

        _agentInfos = new List<AgentInfo>
        {
            new AgentInfo("planner", "Planner", "#D946EF", "LayoutGrid", "Analyzes requirements and creates detailed execution plans"),
            new AgentInfo("researcher", "Researcher", "#06B6D4", "Search", "Gathers relevant knowledge and best practices"),
            new AgentInfo("developer", "Developer", "#10B981", "Code", "Writes clean, efficient code"),
            new AgentInfo("test_designer", "Test Designer", "#F59E0B", "TestTube", "Creates comprehensive test cases"),
            new AgentInfo("executor", "Executor", "#3B82F6", "Play", "Runs code in isolated sandbox"),
            new AgentInfo("debugger", "Debugger", "#EF4444", "Bug", "Identifies and fixes errors"),
            new AgentInfo("verifier", "Verifier", "#8B5CF6", "CheckCircle", "Validates output against requirements"),
            new AgentInfo("error_analyzer", "Error Analyzer", "#EC4899", "AlertTriangle", "Analyzes errors and dispatches fixes")
        };
    }

    public IAgent GetAgent(string agentId)
    {
        if (!_agentTypes.TryGetValue(agentId, out var agentType))
        {
            // Default to developer agent
            agentType = typeof(DeveloperAgent);
        }

        return (IAgent)_serviceProvider.GetRequiredService(agentType);
    }

    public IEnumerable<AgentInfo> GetAllAgents() => _agentInfos;
}
