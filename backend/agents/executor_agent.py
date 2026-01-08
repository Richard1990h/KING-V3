"""Executor Agent - Runs code in isolated environment"""
from typing import Dict, Any
from agents.base_agent import BaseAgent, AgentResult
import asyncio
import subprocess
import tempfile
import os
import logging

logger = logging.getLogger(__name__)


class ExecutorAgent(BaseAgent):
    """Executor agent that runs code in an isolated environment"""
    
    agent_id = "executor"
    agent_name = "Executor"
    agent_color = "#3B82F6"
    agent_icon = "Play"
    agent_description = "Runs code in isolated sandbox and captures results"
    
    def _build_system_prompt(self) -> str:
        return """You are a code execution specialist. Your role is to:

1. ANALYZE the code to determine how to run it
2. IDENTIFY required dependencies
3. PREPARE the execution environment
4. EXECUTE the code safely
5. CAPTURE and analyze output

When asked to run code, provide:
- Command to execute
- Expected output description
- Any setup steps needed
- Error handling approach

Respond with execution plan in this format:
```json
{
    "language": "python",
    "main_file": "main.py",
    "dependencies": ["package1", "package2"],
    "setup_commands": ["pip install -r requirements.txt"],
    "run_command": "python main.py",
    "expected_behavior": "Description of expected output"
}
```
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Execute code and return results"""
        files = context.get('existing_files', []) if context else []
        language = self.project_context.get('language', 'Python')
        
        try:
            # For now, we'll do Python execution inline
            # In production, this would use Docker/sandboxing
            if language == 'Python':
                result = await self._execute_python(files)
            else:
                result = await self._analyze_execution(task, files)
            
            return result
                
        except Exception as e:
            logger.error(f"Executor agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
    
    async def _execute_python(self, files: list) -> AgentResult:
        """Execute Python code in a safe manner"""
        import io
        import sys
        from contextlib import redirect_stdout, redirect_stderr
        
        # Find main file
        main_file = None
        for f in files:
            if f['path'] in ['main.py', 'app.py', 'index.py'] or f['path'].endswith('/main.py'):
                main_file = f
                break
        
        if not main_file:
            main_file = next((f for f in files if f['path'].endswith('.py')), None)
        
        if not main_file:
            return AgentResult(
                success=False,
                content="No Python file found to execute",
                errors=["No Python file found"]
            )
        
        stdout_capture = io.StringIO()
        stderr_capture = io.StringIO()
        
        try:
            # Create a safe namespace
            namespace = {"__name__": "__main__", "__builtins__": __builtins__}
            
            # Execute with output capture
            with redirect_stdout(stdout_capture), redirect_stderr(stderr_capture):
                exec(main_file['content'], namespace)
            
            stdout_val = stdout_capture.getvalue()
            stderr_val = stderr_capture.getvalue()
            
            output = []
            if stdout_val:
                output.append(f"Output:\n{stdout_val}")
            if stderr_val:
                output.append(f"Warnings/Errors:\n{stderr_val}")
            
            return AgentResult(
                success=True,
                content="\n".join(output) if output else "Code executed successfully with no output",
                metadata={
                    "executed_file": main_file['path'],
                    "has_output": bool(stdout_val),
                    "has_errors": bool(stderr_val)
                }
            )
            
        except SyntaxError as e:
            return AgentResult(
                success=False,
                content=f"Syntax Error in {main_file['path']}:\n{e}",
                errors=[f"SyntaxError: {e}"]
            )
        except Exception as e:
            return AgentResult(
                success=False,
                content=f"Runtime Error:\n{e}",
                errors=[f"RuntimeError: {e}"]
            )
    
    async def _analyze_execution(self, task: str, files: list) -> AgentResult:
        """Analyze how to execute non-Python code"""
        prompt = self._build_prompt(task, {"existing_files": files})
        
        response = await self.ai_service.generate(prompt, self.system_prompt)
        content = response.get('content', '')
        tokens = response.get('tokens', 0)
        
        return AgentResult(
            success=True,
            content=content,
            tokens_used=tokens,
            metadata={"analysis_only": True}
        )
