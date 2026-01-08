"""AI Service - Unified interface for all AI providers including Emergent LLM"""
from typing import Dict, Any, Optional, AsyncGenerator
import httpx
import json
import logging
import os
from dotenv import load_dotenv

load_dotenv()

logger = logging.getLogger(__name__)


class AIService:
    """Unified AI service supporting multiple providers including Emergent LLM"""
    
    def __init__(self, db=None):
        self.db = db
        self.local_llm_url = os.environ.get('LOCAL_LLM_URL', 'http://localhost:11434')
        self.default_model = os.environ.get('LOCAL_LLM_MODEL', 'qwen2.5-coder:1.5b')
        self.emergent_key = os.environ.get('EMERGENT_LLM_KEY')
    
    async def get_user_provider(self, user_id: str) -> Optional[Dict]:
        """Get user's configured AI provider"""
        if not self.db:
            return None
        provider = await self.db.user_ai_providers.find_one(
            {"user_id": user_id, "is_default": True, "is_active": True},
            {"_id": 0}
        )
        return provider
    
    async def is_emergent_enabled(self) -> bool:
        """Check if Emergent LLM is enabled globally"""
        if self.db is None:
            return bool(self.emergent_key)
        setting = await self.db.settings.find_one({"key": "emergent_llm_enabled"}, {"_id": 0})
        if setting:
            return setting.get("value", True)
        return True  # Default enabled
    
    async def generate(self, prompt: str, system_prompt: str = None, 
                       provider: str = "auto", model: str = None, 
                       api_key: str = None, max_tokens: int = 4000,
                       user_id: str = None) -> Dict[str, Any]:
        """Generate AI response with smart provider selection"""
        
        # Check if user has their own API key
        user_provider = None
        if user_id and self.db is not None:
            user_provider = await self.get_user_provider(user_id)
        
        # Provider priority:
        # 1. User's own API key (if configured)
        # 2. Emergent LLM (if enabled)
        # 3. Local LLM (fallback)
        
        try:
            if user_provider and user_provider.get("api_key"):
                # Use user's own key
                return await self._call_with_provider(
                    prompt, system_prompt,
                    user_provider.get("provider", "openai"),
                    user_provider.get("model_preference") or model or "gpt-4o",
                    user_provider["api_key"],
                    max_tokens
                )
            elif self.emergent_key and await self.is_emergent_enabled():
                # Use Emergent LLM
                return await self._call_emergent_llm(prompt, system_prompt, model or "gpt-4o", max_tokens)
            else:
                # Fall back to local LLM
                return await self._call_local_llm(prompt, system_prompt, model or self.default_model, max_tokens)
                
        except Exception as e:
            logger.error(f"AI generation error: {e}")
            # Try fallbacks
            if self.emergent_key and await self.is_emergent_enabled():
                try:
                    return await self._call_emergent_llm(prompt, system_prompt, "gpt-4o", max_tokens)
                except:
                    pass
            return await self._call_local_llm(prompt, system_prompt, self.default_model, max_tokens)
    
    async def _call_emergent_llm(self, prompt: str, system_prompt: str, model: str, max_tokens: int) -> Dict:
        """Call Emergent LLM using emergentintegrations library"""
        try:
            # Try to import emergentintegrations (only available on Emergent platform)
            try:
                from emergentintegrations.llm.chat import LlmChat, UserMessage
            except ImportError:
                raise Exception("emergentintegrations not available - using local LLM")
            
            chat = LlmChat(
                api_key=self.emergent_key,
                session_id=f"littlehelper-{id(prompt)}",
                system_message=system_prompt or "You are a helpful AI coding assistant."
            )
            
            # Configure model (default to OpenAI gpt-4o)
            provider_name = "openai"
            model_name = model or "gpt-4o"
            
            # Map common model names
            if "claude" in model.lower():
                provider_name = "anthropic"
                model_name = model if model.startswith("claude") else "claude-sonnet-4-5-20250929"
            elif "gemini" in model.lower():
                provider_name = "gemini"
                model_name = model if model.startswith("gemini") else "gemini-2.5-flash"
            
            chat.with_model(provider_name, model_name)
            
            user_message = UserMessage(text=prompt)
            response = await chat.send_message(user_message)
            
            # Estimate tokens (roughly)
            tokens = int((len(prompt.split()) + len(response.split())) * 1.3)
            
            return {
                "content": response,
                "provider": f"emergent-{provider_name}",
                "model": model_name,
                "tokens": tokens
            }
            
        except Exception as e:
            logger.error(f"Emergent LLM error: {e}")
            raise e
    
    async def _call_with_provider(self, prompt: str, system_prompt: str, 
                                   provider: str, model: str, api_key: str, max_tokens: int) -> Dict:
        """Call a specific provider with user's API key"""
        if provider == "openai":
            return await self._call_openai(prompt, system_prompt, model, api_key, max_tokens)
        elif provider == "anthropic":
            return await self._call_anthropic(prompt, system_prompt, model, api_key, max_tokens)
        else:
            # Try Emergent LLM format for other providers
            return await self._call_openai(prompt, system_prompt, model, api_key, max_tokens)
    
    async def generate_streaming(self, prompt: str, system_prompt: str = None,
                                  provider: str = "auto", model: str = None,
                                  api_key: str = None, user_id: str = None) -> AsyncGenerator[str, None]:
        """Generate AI response with streaming"""
        
        # Check if user has their own API key
        user_provider = None
        if user_id and self.db is not None:
            user_provider = await self.get_user_provider(user_id)
        
        try:
            if user_provider and user_provider.get("api_key"):
                if user_provider.get("provider") == "openai":
                    async for chunk in self._stream_openai(
                        prompt, system_prompt,
                        user_provider.get("model_preference") or model or "gpt-4o",
                        user_provider["api_key"]
                    ):
                        yield chunk
                    return
            
            # For Emergent LLM, use non-streaming and yield all at once
            if self.emergent_key and await self.is_emergent_enabled():
                result = await self._call_emergent_llm(prompt, system_prompt, model or "gpt-4o", 4000)
                yield result["content"]
                return
                
            # Fall back to local LLM streaming
            async for chunk in self._stream_local_llm(prompt, system_prompt, model or self.default_model):
                yield chunk
                
        except Exception as e:
            logger.error(f"AI streaming error: {e}")
            yield f"Error: {str(e)}"
    
    async def _call_local_llm(self, prompt: str, system_prompt: str, model: str, max_tokens: int) -> Dict:
        """Call local Ollama/LM Studio"""
        full_prompt = f"{system_prompt}\n\n{prompt}" if system_prompt else prompt
        
        try:
            async with httpx.AsyncClient(timeout=180.0) as client:
                response = await client.post(
                    f"{self.local_llm_url}/api/generate",
                    json={
                        "model": model,
                        "prompt": full_prompt,
                        "stream": False,
                        "options": {
                            "temperature": 0.7,
                            "num_predict": max_tokens
                        }
                    }
                )
                
                if response.status_code == 200:
                    data = response.json()
                    return {
                        "content": data.get("response", ""),
                        "provider": "local",
                        "model": model,
                        "tokens": data.get("eval_count", len(data.get("response", "").split()) * 1.3)
                    }
                else:
                    logger.warning(f"Local LLM returned status {response.status_code}")
                    return self._fallback_response(f"Local LLM error: {response.status_code}")
                    
        except httpx.ConnectError:
            logger.warning("Local LLM not available, using fallback")
            return self._fallback_response("Local LLM not connected")
        except Exception as e:
            logger.error(f"Local LLM error: {e}")
            return self._fallback_response(str(e))
    
    async def _stream_local_llm(self, prompt: str, system_prompt: str, model: str) -> AsyncGenerator[str, None]:
        """Stream from local Ollama/LM Studio"""
        full_prompt = f"{system_prompt}\n\n{prompt}" if system_prompt else prompt
        
        try:
            async with httpx.AsyncClient(timeout=180.0) as client:
                async with client.stream(
                    "POST",
                    f"{self.local_llm_url}/api/generate",
                    json={
                        "model": model,
                        "prompt": full_prompt,
                        "stream": True,
                        "options": {"temperature": 0.7, "num_predict": 4000}
                    }
                ) as response:
                    async for line in response.aiter_lines():
                        if line:
                            try:
                                data = json.loads(line)
                                if "response" in data:
                                    yield data["response"]
                            except json.JSONDecodeError:
                                continue
        except Exception as e:
            logger.error(f"Local LLM stream error: {e}")
            yield f"\n[Stream error: {str(e)}]"
    
    async def _call_openai(self, prompt: str, system_prompt: str, model: str, api_key: str, max_tokens: int) -> Dict:
        """Call OpenAI API"""
        messages = []
        if system_prompt:
            messages.append({"role": "system", "content": system_prompt})
        messages.append({"role": "user", "content": prompt})
        
        try:
            async with httpx.AsyncClient(timeout=120.0) as client:
                response = await client.post(
                    "https://api.openai.com/v1/chat/completions",
                    headers={"Authorization": f"Bearer {api_key}"},
                    json={
                        "model": model,
                        "messages": messages,
                        "max_tokens": max_tokens,
                        "temperature": 0.7
                    }
                )
                
                if response.status_code == 200:
                    data = response.json()
                    return {
                        "content": data["choices"][0]["message"]["content"],
                        "provider": "openai",
                        "model": model,
                        "tokens": data["usage"]["total_tokens"]
                    }
                else:
                    raise Exception(f"OpenAI error: {response.status_code}")
        except Exception as e:
            raise e
    
    async def _stream_openai(self, prompt: str, system_prompt: str, model: str, api_key: str) -> AsyncGenerator[str, None]:
        """Stream from OpenAI API"""
        messages = []
        if system_prompt:
            messages.append({"role": "system", "content": system_prompt})
        messages.append({"role": "user", "content": prompt})
        
        try:
            async with httpx.AsyncClient(timeout=120.0) as client:
                async with client.stream(
                    "POST",
                    "https://api.openai.com/v1/chat/completions",
                    headers={"Authorization": f"Bearer {api_key}"},
                    json={
                        "model": model,
                        "messages": messages,
                        "max_tokens": 4000,
                        "temperature": 0.7,
                        "stream": True
                    }
                ) as response:
                    async for line in response.aiter_lines():
                        if line.startswith("data: ") and line != "data: [DONE]":
                            try:
                                data = json.loads(line[6:])
                                if data["choices"][0]["delta"].get("content"):
                                    yield data["choices"][0]["delta"]["content"]
                            except:
                                continue
        except Exception as e:
            yield f"\n[Stream error: {str(e)}]"
    
    async def _call_anthropic(self, prompt: str, system_prompt: str, model: str, api_key: str, max_tokens: int) -> Dict:
        """Call Anthropic API"""
        try:
            async with httpx.AsyncClient(timeout=120.0) as client:
                body = {
                    "model": model,
                    "max_tokens": max_tokens,
                    "messages": [{"role": "user", "content": prompt}]
                }
                if system_prompt:
                    body["system"] = system_prompt
                
                response = await client.post(
                    "https://api.anthropic.com/v1/messages",
                    headers={
                        "x-api-key": api_key,
                        "anthropic-version": "2023-06-01"
                    },
                    json=body
                )
                
                if response.status_code == 200:
                    data = response.json()
                    return {
                        "content": data["content"][0]["text"],
                        "provider": "anthropic",
                        "model": model,
                        "tokens": data["usage"]["input_tokens"] + data["usage"]["output_tokens"]
                    }
                else:
                    raise Exception(f"Anthropic error: {response.status_code}")
        except Exception as e:
            raise e
    
    def _fallback_response(self, error_msg: str = "") -> Dict:
        """Return a helpful fallback response when AI is unavailable"""
        return {
            "content": f"""I'm currently operating in fallback mode as the AI service is not available.

Error: {error_msg}

To use the full AI capabilities:
1. The platform uses Emergent LLM by default (admin controlled)
2. You can configure your own API key in Settings
3. Or ensure Ollama is running locally (http://localhost:11434)

I can still help with basic code templates and project structure. What would you like to create?""",
            "provider": "fallback",
            "model": "none",
            "tokens": 50
        }
    
    async def estimate_tokens(self, text: str) -> int:
        """Estimate token count for text"""
        # Rough estimation: ~1.3 tokens per word for code
        return int(len(text.split()) * 1.3)
