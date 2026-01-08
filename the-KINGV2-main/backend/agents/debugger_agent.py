"""Debugger Agent - Identifies and fixes errors"""
from typing import Dict, Any, List
from agents.base_agent import BaseAgent, AgentResult
import re
import logging

logger = logging.getLogger(__name__)


class DebuggerAgent(BaseAgent):
    """Debugger agent that identifies and fixes code errors"""
    
    agent_id = "debugger"
    agent_name = "Debugger"
    agent_color = "#EF4444"
    agent_icon = "Bug"
    agent_description = "Identifies and fixes errors systematically"
    
    def _build_system_prompt(self) -> str:
        return """You are an expert debugger and code analyst. Your role is to:

1. ANALYZE error messages and stack traces
2. IDENTIFY root causes of bugs
3. PROPOSE specific fixes
4. EXPLAIN why the error occurred
5. PREVENT similar issues in the future

For each bug, provide:

## Error Analysis
[Detailed analysis of what went wrong]

## Root Cause
[The fundamental reason for the error]

## Fix
Provide the corrected file(s) in this format:

### filename.ext
```language
corrected code
```

## Prevention
[How to prevent this type of error]

IMPORTANT:
- Always provide COMPLETE fixed files, not just snippets
- Ensure fixes don't introduce new bugs
- Add error handling where appropriate
- Include comments explaining the fix
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Debug and fix errors"""
        prompt = self._build_prompt(task, context)
        
        # Include error details
        if context and context.get('errors'):
            prompt += "\n\n## Errors to Fix\n"
            for err in context['errors']:
                prompt += f"\n{err}\n"
        
        if context and context.get('existing_files'):
            prompt += "\n\n## Current Code\n"
            for f in context['existing_files']:
                prompt += f"\n### {f['path']}\n```\n{f.get('content', '')}\n```\n"
        
        prompt += "\n\nAnalyze the errors and provide COMPLETE fixed file(s)."
        
        try:
            response = await self.ai_service.generate(prompt, self.system_prompt)
            content = response.get('content', '')
            tokens = response.get('tokens', 0)
            
            # Extract fixed files
            files = self._extract_fixed_files(content)
            
            return AgentResult(
                success=True,
                content=content,
                tokens_used=tokens,
                files_created=files,
                metadata={
                    "files_fixed": len(files),
                    "fixed_file_names": [f['path'] for f in files]
                }
            )
                
        except Exception as e:
            logger.error(f"Debugger agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
    
    def _extract_fixed_files(self, response: str) -> List[Dict[str, str]]:
        """Extract fixed file contents from response"""
        files = []
        
        pattern = r'###\s*([\w/.\-]+\.[\w]+)\s*\n```(?:[\w]*)?\n([\s\S]*?)```'
        matches = re.findall(pattern, response)
        
        for path, content in matches:
            files.append({"path": path.strip(), "content": content.strip()})
        
        return files
