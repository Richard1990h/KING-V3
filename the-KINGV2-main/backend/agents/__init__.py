"""LittleHelper AI Agents Module"""
from agents.base_agent import BaseAgent, AgentResult
from agents.planner_agent import PlannerAgent
from agents.researcher_agent import ResearcherAgent
from agents.developer_agent import DeveloperAgent
from agents.test_designer_agent import TestDesignerAgent
from agents.executor_agent import ExecutorAgent
from agents.debugger_agent import DebuggerAgent
from agents.verifier_agent import VerifierAgent
from agents.error_analyzer_agent import ErrorAnalyzerAgent

AGENT_REGISTRY = {
    "planner": PlannerAgent,
    "researcher": ResearcherAgent,
    "developer": DeveloperAgent,
    "test_designer": TestDesignerAgent,
    "executor": ExecutorAgent,
    "debugger": DebuggerAgent,
    "verifier": VerifierAgent,
    "error_analyzer": ErrorAnalyzerAgent
}

AGENT_INFO = [
    {"id": "planner", "name": "Planner", "color": "#D946EF", "icon": "LayoutGrid", 
     "description": "Analyzes requirements and creates detailed execution plans with job breakdown"},
    {"id": "researcher", "name": "Researcher", "color": "#06B6D4", "icon": "Search", 
     "description": "Gathers relevant knowledge, documentation, and best practices"},
    {"id": "developer", "name": "Developer", "color": "#10B981", "icon": "Code", 
     "description": "Writes clean, efficient code with best practices"},
    {"id": "test_designer", "name": "Test Designer", "color": "#F59E0B", "icon": "TestTube", 
     "description": "Creates comprehensive test cases and test files"},
    {"id": "executor", "name": "Executor", "color": "#3B82F6", "icon": "Play", 
     "description": "Runs code in isolated sandbox and captures results"},
    {"id": "debugger", "name": "Debugger", "color": "#EF4444", "icon": "Bug", 
     "description": "Identifies and fixes errors systematically"},
    {"id": "verifier", "name": "Verifier", "color": "#8B5CF6", "icon": "CheckCircle", 
     "description": "Validates output against requirements"},
    {"id": "error_analyzer", "name": "Error Analyzer", "color": "#EC4899", "icon": "AlertTriangle", 
     "description": "Analyzes build/runtime errors and dispatches fixes"}
]

__all__ = [
    'BaseAgent', 'AgentResult', 'AGENT_REGISTRY', 'AGENT_INFO',
    'PlannerAgent', 'ResearcherAgent', 'DeveloperAgent', 'TestDesignerAgent',
    'ExecutorAgent', 'DebuggerAgent', 'VerifierAgent', 'ErrorAnalyzerAgent'
]
