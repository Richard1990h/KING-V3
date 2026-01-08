"""Error Analyzer Agent - Analyzes errors and dispatches fixes"""
from typing import Dict, Any, List
from agents.base_agent import BaseAgent, AgentResult
import re
import logging

logger = logging.getLogger(__name__)


class ErrorAnalyzerAgent(BaseAgent):
    """Error Analyzer agent that parses errors and creates fix tasks"""
    
    agent_id = "error_analyzer"
    agent_name = "Error Analyzer"
    agent_color = "#EC4899"
    agent_icon = "AlertTriangle"
    agent_description = "Analyzes build/runtime errors and dispatches fixes"
    
    def _build_system_prompt(self) -> str:
        return """You are an expert error analyst. Your role is to:

1. PARSE error messages and logs
2. CATEGORIZE errors by type and severity
3. IDENTIFY root causes
4. CREATE fix tasks for other agents
5. PRIORITIZE fixes by impact

ERROR CATEGORIES:
- SYNTAX: Syntax errors, typos
- IMPORT: Missing imports, wrong paths
- RUNTIME: Runtime exceptions
- LOGIC: Logical errors, wrong output
- DEPENDENCY: Missing packages
- CONFIG: Configuration issues

For error analysis, respond with JSON:
```json
{
    "errors_found": [
        {
            "category": "SYNTAX",
            "severity": "high",
            "file": "main.py",
            "line": 42,
            "message": "Original error message",
            "root_cause": "Why this error occurred",
            "fix_description": "How to fix it"
        }
    ],
    "fix_tasks": [
        {
            "agent": "debugger",
            "priority": 1,
            "description": "Fix syntax error in main.py",
            "files_affected": ["main.py"]
        }
    ],
    "can_auto_fix": true,
    "requires_user_input": false
}
```
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Analyze errors and create fix tasks"""
        prompt = self._build_prompt(task, context)
        
        # Include error details
        if context and context.get('errors'):
            prompt += "\n\n## Errors to Analyze\n"
            for err in context['errors']:
                prompt += f"\n```\n{err}\n```\n"
        
        if context and context.get('build_logs'):
            prompt += "\n\n## Build Logs\n"
            prompt += f"```\n{context['build_logs']}\n```\n"
        
        prompt += "\n\nAnalyze all errors and respond with JSON containing error analysis and fix tasks."
        
        try:
            response = await self.ai_service.generate(prompt, self.system_prompt)
            content = response.get('content', '')
            tokens = response.get('tokens', 0)
            
            # Parse analysis
            analysis = self._parse_json_from_response(content)
            
            if analysis:
                fix_tasks = []
                for task in analysis.get('fix_tasks', []):
                    fix_tasks.append({
                        "agent_type": task.get('agent', 'debugger'),
                        "title": task.get('description', 'Fix error'),
                        "description": task.get('description', ''),
                        "priority": task.get('priority', 1),
                        "files_affected": task.get('files_affected', [])
                    })
                
                return AgentResult(
                    success=True,
                    content=content,
                    tokens_used=tokens,
                    tasks_generated=fix_tasks,
                    metadata={
                        "errors_found": len(analysis.get('errors_found', [])),
                        "can_auto_fix": analysis.get('can_auto_fix', False),
                        "requires_user_input": analysis.get('requires_user_input', False),
                        "error_categories": list(set(e.get('category', 'UNKNOWN') for e in analysis.get('errors_found', [])))
                    }
                )
            
            return AgentResult(
                success=True,
                content=content,
                tokens_used=tokens,
                metadata={"analysis_complete": True}
            )
                
        except Exception as e:
            logger.error(f"Error Analyzer agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
