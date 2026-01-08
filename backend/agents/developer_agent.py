"""Developer Agent - Writes code and creates files"""
from typing import Dict, Any, List
from agents.base_agent import BaseAgent, AgentResult
import re
import logging

logger = logging.getLogger(__name__)


class DeveloperAgent(BaseAgent):
    """Developer agent that writes code and creates project files"""
    
    agent_id = "developer"
    agent_name = "Developer"
    agent_color = "#10B981"
    agent_icon = "Code"
    agent_description = "Writes clean, efficient code with best practices"
    
    def _build_system_prompt(self) -> str:
        language = self.project_context.get('language', 'Python')
        
        return f"""You are an expert {language} developer. Your role is to:

1. WRITE clean, efficient, well-documented code
2. FOLLOW best practices and design patterns
3. HANDLE edge cases and errors properly
4. CREATE modular, maintainable code structure
5. INCLUDE helpful comments

When creating files, use this EXACT format for each file:

### filename.ext
```language
code here
```

IMPORTANT RULES:
- Create ALL necessary files for the task
- Include proper imports and dependencies
- Add error handling
- Follow {language} conventions and style guides
- Create a complete, working implementation
- Include configuration files if needed (package.json, requirements.txt, etc.)

For each file, include:
- File header comment explaining purpose
- Proper imports
- Well-named functions/classes
- Docstrings/comments
- Error handling
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Generate code for the task"""
        prompt = self._build_prompt(task, context)
        
        # Add instruction to create complete files
        prompt += "\n\nCreate all necessary files for this task. Use the exact format: ### filename.ext followed by code block."
        
        try:
            response = await self.ai_service.generate(prompt, self.system_prompt)
            content = response.get('content', '')
            tokens = response.get('tokens', 0)
            
            # Extract files from response
            files = self._extract_files_from_response(content)
            
            return AgentResult(
                success=True,
                content=content,
                tokens_used=tokens,
                files_created=files,
                metadata={
                    "files_count": len(files),
                    "file_names": [f['path'] for f in files]
                }
            )
                
        except Exception as e:
            logger.error(f"Developer agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
    
    def _extract_files_from_response(self, response: str) -> List[Dict[str, str]]:
        """Extract file contents from developer response"""
        files = []
        
        # Pattern 1: ### filename.ext followed by code block
        pattern1 = r'###\s*([\w/.\-]+\.[\w]+)\s*\n```(?:[\w]*)?\n([\s\S]*?)```'
        matches = re.findall(pattern1, response)
        for path, content in matches:
            files.append({"path": path.strip(), "content": content.strip()})
        
        if files:
            return files
        
        # Pattern 2: File: filename.ext or **filename.ext**
        pattern2 = r'(?:File:\s*|\*\*)?([\w/.\-]+\.[\w]+)(?:\*\*)?\s*\n```(?:[\w]*)?\n([\s\S]*?)```'
        matches = re.findall(pattern2, response)
        for path, content in matches:
            if not any(f['path'] == path.strip() for f in files):
                files.append({"path": path.strip(), "content": content.strip()})
        
        if files:
            return files
        
        # Pattern 3: Just code blocks with filename in first line comment
        pattern3 = r'```(?:[\w]*)?\n(?:#|//|<!--|/\*)\s*([\w/.\-]+\.[\w]+).*?\n([\s\S]*?)```'
        matches = re.findall(pattern3, response)
        for path, content in matches:
            if not any(f['path'] == path.strip() for f in files):
                files.append({"path": path.strip(), "content": content.strip()})
        
        return files
