"""Test Designer Agent - Creates comprehensive test cases"""
from typing import Dict, Any, List
from agents.base_agent import BaseAgent, AgentResult
import re
import logging

logger = logging.getLogger(__name__)


class TestDesignerAgent(BaseAgent):
    """Test Designer agent that creates test cases and test files"""
    
    agent_id = "test_designer"
    agent_name = "Test Designer"
    agent_color = "#F59E0B"
    agent_icon = "TestTube"
    agent_description = "Creates comprehensive test cases and test files"
    
    def _build_system_prompt(self) -> str:
        language = self.project_context.get('language', 'Python')
        
        test_frameworks = {
            "Python": "pytest",
            "JavaScript": "Jest",
            "TypeScript": "Jest",
            "Java": "JUnit 5",
            "C#": "xUnit",
            "Go": "testing",
            "Rust": "cargo test",
            "PHP": "PHPUnit",
            "Ruby": "RSpec"
        }
        
        framework = test_frameworks.get(language, "appropriate testing framework")
        
        return f"""You are an expert test engineer specializing in {language}. Your role is to:

1. CREATE comprehensive test cases using {framework}
2. COVER happy paths, edge cases, and error conditions
3. WRITE clear, maintainable test code
4. INCLUDE setup and teardown when needed
5. TEST all public interfaces

For each test file, use this format:

### test_filename.ext
```language
test code here
```

TEST CATEGORIES TO INCLUDE:
- Unit tests for individual functions/methods
- Integration tests for component interactions
- Edge case tests (empty inputs, large data, etc.)
- Error handling tests
- Boundary condition tests

FOR EACH TEST:
- Use descriptive test names
- Include arrange/act/assert structure
- Add comments explaining what's being tested
- Mock external dependencies when appropriate
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Generate test cases for the task"""
        prompt = self._build_prompt(task, context)
        
        # Include existing files in context for proper test creation
        if context and context.get('existing_files'):
            prompt += "\n\n## Files to Test\n"
            for f in context['existing_files']:
                prompt += f"\n### {f['path']}\n```\n{f.get('content', '')[:1000]}\n```\n"
        
        prompt += "\n\nCreate comprehensive tests for the above code. Include all test categories."
        
        try:
            response = await self.ai_service.generate(prompt, self.system_prompt)
            content = response.get('content', '')
            tokens = response.get('tokens', 0)
            
            # Extract test files
            files = self._extract_test_files(content)
            
            return AgentResult(
                success=True,
                content=content,
                tokens_used=tokens,
                files_created=files,
                metadata={
                    "test_files_count": len(files),
                    "test_file_names": [f['path'] for f in files]
                }
            )
                
        except Exception as e:
            logger.error(f"Test Designer agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
    
    def _extract_test_files(self, response: str) -> List[Dict[str, str]]:
        """Extract test file contents from response"""
        files = []
        
        # Pattern for test files
        pattern = r'###\s*([\w/.\-]+\.[\w]+)\s*\n```(?:[\w]*)?\n([\s\S]*?)```'
        matches = re.findall(pattern, response)
        
        for path, content in matches:
            files.append({"path": path.strip(), "content": content.strip()})
        
        # Fallback pattern
        if not files:
            pattern2 = r'(?:File:\s*|\*\*)?([\w/.\-]*test[\w/.\-]*\.[\w]+)(?:\*\*)?\s*\n```(?:[\w]*)?\n([\s\S]*?)```'
            matches = re.findall(pattern2, response, re.IGNORECASE)
            for path, content in matches:
                files.append({"path": path.strip(), "content": content.strip()})
        
        return files
