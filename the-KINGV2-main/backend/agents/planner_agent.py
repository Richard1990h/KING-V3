"""Planner Agent - Analyzes requirements and creates job breakdown"""
from typing import Dict, Any, List
from agents.base_agent import BaseAgent, AgentResult
import json
import uuid
import logging

logger = logging.getLogger(__name__)


class PlannerAgent(BaseAgent):
    """Planner agent that analyzes user requirements and creates a task breakdown"""
    
    agent_id = "planner"
    agent_name = "Planner"
    agent_color = "#D946EF"
    agent_icon = "LayoutGrid"
    agent_description = "Analyzes requirements and creates detailed execution plans with job breakdown"
    
    def _build_system_prompt(self) -> str:
        return """You are an expert project planner and software architect. Your role is to:

1. ANALYZE user requirements thoroughly
2. BREAK DOWN the project into clear, actionable tasks
3. CREATE a step-by-step execution plan
4. ESTIMATE complexity and effort for each task
5. IDENTIFY dependencies between tasks
6. ASSIGN appropriate agents to each task

When creating tasks, consider:
- Each task should be completable by a single agent
- Tasks should be in logical execution order
- Include file creation, testing, and verification tasks
- Estimate token usage based on complexity

AVAILABLE AGENTS:
- researcher: Gathers documentation and best practices
- developer: Writes code and creates files
- test_designer: Creates test cases and test files
- executor: Runs and tests code
- debugger: Fixes errors and bugs
- verifier: Validates against requirements

You MUST respond with a valid JSON object in this exact format:
{
    "project_summary": "Brief description of what will be built",
    "total_files_estimated": 5,
    "complexity": "medium",
    "tasks": [
        {
            "id": "task-1",
            "title": "Research best practices",
            "description": "Detailed description of what this task involves",
            "agent_type": "researcher",
            "order": 1,
            "estimated_tokens": 1000,
            "dependencies": [],
            "deliverables": ["list of files or outputs"]
        }
    ],
    "estimated_total_tokens": 10000
}
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Analyze requirements and create task breakdown"""
        prompt = self._build_prompt(task, context)
        
        # Add specific instruction for output format
        prompt += "\n\nCreate a detailed task breakdown for this project. Respond with ONLY valid JSON."
        
        try:
            response = await self.ai_service.generate(prompt, self.system_prompt)
            content = response.get('content', '')
            tokens = response.get('tokens', 0)
            
            # Parse the JSON response
            plan = self._parse_json_from_response(content)
            
            if plan and 'tasks' in plan:
                # Ensure each task has required fields and unique IDs
                tasks = []
                for i, t in enumerate(plan['tasks']):
                    task_obj = {
                        "id": t.get('id', f"task-{uuid.uuid4().hex[:8]}"),
                        "title": t.get('title', f"Task {i+1}"),
                        "description": t.get('description', ''),
                        "agent_type": t.get('agent_type', 'developer'),
                        "order": t.get('order', i + 1),
                        "status": "pending",
                        "estimated_tokens": t.get('estimated_tokens', 500),
                        "estimated_credits": 0,  # Will be calculated by service
                        "actual_tokens": 0,
                        "actual_credits": 0,
                        "dependencies": t.get('dependencies', []),
                        "deliverables": t.get('deliverables', []),
                        "output": None,
                        "files_created": [],
                        "error": None
                    }
                    tasks.append(task_obj)
                
                return AgentResult(
                    success=True,
                    content=content,
                    tokens_used=tokens,
                    tasks_generated=tasks,
                    metadata={
                        "project_summary": plan.get('project_summary', ''),
                        "total_files_estimated": plan.get('total_files_estimated', 0),
                        "complexity": plan.get('complexity', 'medium'),
                        "estimated_total_tokens": plan.get('estimated_total_tokens', sum(t['estimated_tokens'] for t in tasks))
                    }
                )
            else:
                # Fallback: Create a basic task list
                return self._create_fallback_plan(task, content, tokens)
                
        except Exception as e:
            logger.error(f"Planner agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
    
    def _create_fallback_plan(self, original_task: str, ai_response: str, tokens: int) -> AgentResult:
        """Create a fallback plan when AI doesn't return proper JSON"""
        language = self.project_context.get('language', 'Python')
        
        tasks = [
            {
                "id": f"task-{uuid.uuid4().hex[:8]}",
                "title": "Research Requirements",
                "description": f"Research best practices and patterns for: {original_task[:100]}",
                "agent_type": "researcher",
                "order": 1,
                "status": "pending",
                "estimated_tokens": 800,
                "estimated_credits": 0,
                "actual_tokens": 0,
                "actual_credits": 0,
                "dependencies": [],
                "deliverables": ["Research notes"],
                "output": None,
                "files_created": [],
                "error": None
            },
            {
                "id": f"task-{uuid.uuid4().hex[:8]}",
                "title": "Create Project Structure",
                "description": f"Create the initial {language} project structure and main files",
                "agent_type": "developer",
                "order": 2,
                "status": "pending",
                "estimated_tokens": 1500,
                "estimated_credits": 0,
                "actual_tokens": 0,
                "actual_credits": 0,
                "dependencies": ["task-1"],
                "deliverables": ["Project files"],
                "output": None,
                "files_created": [],
                "error": None
            },
            {
                "id": f"task-{uuid.uuid4().hex[:8]}",
                "title": "Implement Core Logic",
                "description": "Implement the main functionality as requested",
                "agent_type": "developer",
                "order": 3,
                "status": "pending",
                "estimated_tokens": 2000,
                "estimated_credits": 0,
                "actual_tokens": 0,
                "actual_credits": 0,
                "dependencies": ["task-2"],
                "deliverables": ["Implementation code"],
                "output": None,
                "files_created": [],
                "error": None
            },
            {
                "id": f"task-{uuid.uuid4().hex[:8]}",
                "title": "Create Tests",
                "description": "Create test cases for the implementation",
                "agent_type": "test_designer",
                "order": 4,
                "status": "pending",
                "estimated_tokens": 1000,
                "estimated_credits": 0,
                "actual_tokens": 0,
                "actual_credits": 0,
                "dependencies": ["task-3"],
                "deliverables": ["Test files"],
                "output": None,
                "files_created": [],
                "error": None
            },
            {
                "id": f"task-{uuid.uuid4().hex[:8]}",
                "title": "Verify Implementation",
                "description": "Verify the implementation meets all requirements",
                "agent_type": "verifier",
                "order": 5,
                "status": "pending",
                "estimated_tokens": 500,
                "estimated_credits": 0,
                "actual_tokens": 0,
                "actual_credits": 0,
                "dependencies": ["task-4"],
                "deliverables": ["Verification report"],
                "output": None,
                "files_created": [],
                "error": None
            }
        ]
        
        return AgentResult(
            success=True,
            content=ai_response or "Created default task breakdown",
            tokens_used=tokens,
            tasks_generated=tasks,
            metadata={
                "project_summary": original_task[:200],
                "total_files_estimated": 5,
                "complexity": "medium",
                "estimated_total_tokens": sum(t['estimated_tokens'] for t in tasks),
                "fallback_used": True
            }
        )
