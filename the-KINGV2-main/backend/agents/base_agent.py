"""Base Agent Class for LittleHelper AI"""
from abc import ABC, abstractmethod
from typing import Dict, Any, List, Optional, AsyncGenerator
from dataclasses import dataclass, field
import json
import logging

logger = logging.getLogger(__name__)


@dataclass
class AgentResult:
    """Result from an agent execution"""
    success: bool
    content: str
    tokens_used: int = 0
    files_created: List[Dict[str, str]] = field(default_factory=list)  # [{"path": "...", "content": "..."}]
    tasks_generated: List[Dict[str, Any]] = field(default_factory=list)
    errors: List[str] = field(default_factory=list)
    metadata: Dict[str, Any] = field(default_factory=dict)


class BaseAgent(ABC):
    """Abstract base class for all agents"""
    
    agent_id: str = "base"
    agent_name: str = "Base Agent"
    agent_color: str = "#6B7280"
    agent_icon: str = "Bot"
    agent_description: str = "Base agent class"
    
    def __init__(self, ai_service, project_context: Dict[str, Any] = None):
        self.ai_service = ai_service
        self.project_context = project_context or {}
        self.system_prompt = self._build_system_prompt()
    
    @abstractmethod
    def _build_system_prompt(self) -> str:
        """Build the system prompt for this agent"""
        pass
    
    @abstractmethod
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Execute the agent's task"""
        pass
    
    async def execute_streaming(self, task: str, context: Dict[str, Any] = None) -> AsyncGenerator[str, None]:
        """Execute with streaming output"""
        prompt = self._build_prompt(task, context)
        async for chunk in self.ai_service.generate_streaming(prompt, self.system_prompt):
            yield chunk
    
    def _build_prompt(self, task: str, context: Dict[str, Any] = None) -> str:
        """Build the full prompt with context"""
        prompt_parts = []
        
        if self.project_context:
            prompt_parts.append(f"## Project Context")
            prompt_parts.append(f"Language: {self.project_context.get('language', 'Unknown')}")
            prompt_parts.append(f"Project: {self.project_context.get('name', 'Unknown')}")
            if self.project_context.get('description'):
                prompt_parts.append(f"Description: {self.project_context['description']}")
            prompt_parts.append("")
        
        if context:
            if context.get('previous_outputs'):
                prompt_parts.append("## Previous Agent Outputs")
                for output in context['previous_outputs'][-3:]:
                    prompt_parts.append(f"[{output['agent']}]: {output['summary'][:500]}")
                prompt_parts.append("")
            
            if context.get('existing_files'):
                prompt_parts.append("## Existing Files")
                for f in context['existing_files'][:10]:
                    prompt_parts.append(f"- {f['path']}")
                prompt_parts.append("")
            
            if context.get('errors'):
                prompt_parts.append("## Errors to Address")
                for err in context['errors']:
                    prompt_parts.append(f"- {err}")
                prompt_parts.append("")
        
        prompt_parts.append("## Task")
        prompt_parts.append(task)
        
        return "\n".join(prompt_parts)
    
    def _parse_json_from_response(self, response: str) -> Optional[Dict]:
        """Extract JSON from response text"""
        try:
            # Try direct parse first
            return json.loads(response)
        except:
            pass
        
        # Try to find JSON in code blocks
        import re
        json_match = re.search(r'```(?:json)?\s*([\s\S]*?)```', response)
        if json_match:
            try:
                return json.loads(json_match.group(1).strip())
            except:
                pass
        
        # Try to find raw JSON object
        json_match = re.search(r'\{[\s\S]*\}', response)
        if json_match:
            try:
                return json.loads(json_match.group(0))
            except:
                pass
        
        return None
    
    def _extract_code_blocks(self, response: str) -> List[Dict[str, str]]:
        """Extract code blocks from response"""
        import re
        files = []
        
        # Pattern for file path followed by code block
        pattern = r'(?:File:\s*|###\s*|`)?([\w/.-]+\.[\w]+)`?\s*\n```(?:\w+)?\n([\s\S]*?)```'
        matches = re.findall(pattern, response, re.MULTILINE)
        
        for path, content in matches:
            files.append({"path": path.strip(), "content": content.strip()})
        
        return files
