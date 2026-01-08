"""Verifier Agent - Validates output against requirements"""
from typing import Dict, Any
from agents.base_agent import BaseAgent, AgentResult
import logging

logger = logging.getLogger(__name__)


class VerifierAgent(BaseAgent):
    """Verifier agent that validates implementation against requirements"""
    
    agent_id = "verifier"
    agent_name = "Verifier"
    agent_color = "#8B5CF6"
    agent_icon = "CheckCircle"
    agent_description = "Validates output against requirements"
    
    def _build_system_prompt(self) -> str:
        return """You are an expert code reviewer and quality assurance specialist. Your role is to:

1. COMPARE implementation against original requirements
2. CHECK for completeness of all requested features
3. VERIFY code quality and best practices
4. IDENTIFY any missing pieces
5. PROVIDE a clear pass/fail verdict

For each verification, provide:

## Requirements Checklist
- [ ] or [x] Requirement 1
- [ ] or [x] Requirement 2
...

## Code Quality Review
- Structure: [Good/Needs Improvement]
- Documentation: [Good/Needs Improvement]
- Error Handling: [Good/Needs Improvement]
- Best Practices: [Good/Needs Improvement]

## Issues Found
[List any issues or missing items]

## Verdict
**PASS** or **FAIL**

[Explanation of verdict]

## Recommendations
[Suggestions for improvement if any]
"""
    
    async def execute(self, task: str, context: Dict[str, Any] = None) -> AgentResult:
        """Verify implementation against requirements"""
        prompt = self._build_prompt(task, context)
        
        # Include original requirements
        if context and context.get('original_requirements'):
            prompt += f"\n\n## Original Requirements\n{context['original_requirements']}\n"
        
        # Include current files
        if context and context.get('existing_files'):
            prompt += "\n\n## Current Implementation\n"
            for f in context['existing_files']:
                prompt += f"\n### {f['path']}\n```\n{f.get('content', '')[:2000]}\n```\n"
        
        prompt += "\n\nVerify the implementation and provide a detailed report."
        
        try:
            response = await self.ai_service.generate(prompt, self.system_prompt)
            content = response.get('content', '')
            tokens = response.get('tokens', 0)
            
            # Determine pass/fail
            passed = '**PASS**' in content.upper() or 'VERDICT: PASS' in content.upper()
            
            return AgentResult(
                success=True,
                content=content,
                tokens_used=tokens,
                metadata={
                    "verification_passed": passed,
                    "verdict": "PASS" if passed else "FAIL"
                }
            )
                
        except Exception as e:
            logger.error(f"Verifier agent error: {e}")
            return AgentResult(
                success=False,
                content=str(e),
                errors=[str(e)]
            )
