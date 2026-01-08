"""Researcher Agent - Gathers knowledge and best practices"""
from typing import Dict, Any
from agents.base_agent import BaseAgent, AgentResult
import logging

logger = logging.getLogger(__name__)


class ResearcherAgent(BaseAgent):
    """Researcher agent that gathers documentation and best practices"""
    
    agent_id = "researcher"
    agent_name = "Researcher"
    agent_color = "#06B6D4"
    agent_icon = "Search"
    agent_description = "Gathers relevant knowledge, documentation, and best practices"
    
    def _build_system_prompt(self) -> str:
        return """You are an expert technical researcher. Your role is to:

1. GATHER relevant information for the task
2. RESEARCH best practices and patterns
3. IDENTIFY appropriate libraries and frameworks
4. DOCUMENT key considerations and trade-offs
5. PROVIDE actionable recommendations

For each research task, provide:
- Overview of the approach
- Recommended libraries/frameworks with versions
- Best practices to follow
- Common pitfalls to avoid
- Code patterns and examples
- File structure recommendations

Be thorough but concise. Focus on practical, actionable information.

Format your response as:
## Overview
[Brief overview]

## Recommended Approach
[Detailed approach]

## Libraries & Dependencies
[List with versions]

## Best Practices
[Key practices]

## File Structure
[Recommended structure]

## Code Patterns
[Examples]
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Research and gather information for the task"""
        prompt = self._build_prompt(task, context)
        
        try:
            response = await self.ai_service.generate(prompt, self.system_prompt)
            content = response.get('content', '')
            tokens = response.get('tokens', 0)
            
            return AgentResult(
                success=True,
                content=content,
                tokens_used=tokens,
                metadata={
                    "research_complete": True,
                    "sections_found": self._count_sections(content)
                }
            )
                
        except Exception as e:
            logger.error(f"Researcher agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
    
    def _count_sections(self, content: str) -> int:
        """Count the number of sections in research output"""
        return content.count('## ')
