"""LittleHelper AI Configuration"""
import os
from pathlib import Path
from dotenv import load_dotenv

ROOT_DIR = Path(__file__).parent
load_dotenv(ROOT_DIR / '.env')

# Database
MONGO_URL = os.environ.get('MONGO_URL', 'mongodb://localhost:27017')
DB_NAME = os.environ.get('DB_NAME', 'littlehelper_ai')

# JWT
JWT_SECRET = os.environ.get('JWT_SECRET', 'littlehelper-ai-secret-key-2024')
JWT_ALGORITHM = "HS256"
JWT_EXPIRATION_HOURS = 24

# Stripe
STRIPE_SECRET_KEY = os.environ.get('STRIPE_API_KEY', 'sk_test_emergent')

# Local LLM
LOCAL_LLM_URL = os.environ.get('LOCAL_LLM_URL', 'http://localhost:11434')
LOCAL_LLM_MODEL = os.environ.get('LOCAL_LLM_MODEL', 'qwen2.5-coder:1.5b')

# Default Admin
DEFAULT_ADMIN = {
    "id": "00000000-0000-0000-0000-000000000001",
    "email": "admin@littlehelper.ai",
    "name": "System Admin",
    "password": "admin123",
    "role": "admin",
    "credits": 999999.0,
    "credits_enabled": True,
    "plan": "enterprise"
}

# Default Settings
DEFAULT_SETTINGS = {
    "credits_per_1k_tokens_chat": 0.5,
    "credits_per_1k_tokens_project": 1.0,
    "knowledge_cache_hours": 168,
    "default_ai_provider": "local",
    "default_ai_model": "qwen2.5-coder:1.5b",
    "max_tokens_per_request": 4000,
    "free_credits_on_signup": 100,
    "enable_knowledge_sharing": True,
    "pipeline_timeout_seconds": 300,
    "max_concurrent_jobs_per_user": 3,
    "error_retry_limit": 5,
    "auto_fix_enabled": True
}

# Credit Packages (one-time purchase)
CREDIT_PACKAGES = {
    "starter": {"credits": 100, "price": 9.99, "name": "Starter Pack"},
    "pro": {"credits": 500, "price": 39.99, "name": "Pro Pack"},
    "enterprise": {"credits": 2000, "price": 149.99, "name": "Enterprise Pack"}
}

# Subscription Plans (monthly)
# max_concurrent_workspaces: How many AI workspaces can run simultaneously
# daily_credits: Credits added based on subscription start date
SUBSCRIPTION_PLANS = {
    "free": {
        "name": "Free",
        "price_monthly": 0,
        "daily_credits": 10,
        "features": ["Basic chat", "Local LLM only", "1 project", "1 concurrent workspace"],
        "max_projects": 1,
        "max_concurrent_workspaces": 1,
        "allows_own_api_keys": False
    },
    "starter": {
        "name": "Starter",
        "price_monthly": 9.99,
        "daily_credits": 50,
        "features": ["Unlimited chat", "Local + OpenAI", "5 projects", "3 concurrent workspaces", "Email support"],
        "max_projects": 5,
        "max_concurrent_workspaces": 3,
        "allows_own_api_keys": False
    },
    "pro": {
        "name": "Pro",
        "price_monthly": 29.99,
        "daily_credits": 200,
        "features": ["Unlimited chat", "All AI providers", "Unlimited projects", "10 concurrent workspaces", "Priority support", "API access"],
        "max_projects": -1,
        "max_concurrent_workspaces": 10,
        "allows_own_api_keys": True
    },
    "openai": {
        "name": "OpenAI Plan",
        "price_monthly": 19.99,
        "daily_credits": 100,
        "features": ["Use your own OpenAI key", "No credit charges with own key", "5 projects", "5 concurrent workspaces", "GPT-4 access"],
        "max_projects": 5,
        "max_concurrent_workspaces": 5,
        "allows_own_api_keys": True,
        "api_key_required": "openai"
    },
    "enterprise": {
        "name": "Enterprise",
        "price_monthly": 99.99,
        "daily_credits": 1000,
        "features": ["Everything in Pro", "50 concurrent workspaces", "Custom AI models", "Dedicated support", "SLA guarantee", "Custom integrations"],
        "max_projects": -1,
        "max_concurrent_workspaces": 50,
        "allows_own_api_keys": True
    }
}

# AI Providers
AI_PROVIDERS = {
    "openai": {"name": "OpenAI", "models": ["gpt-4o", "gpt-4o-mini", "gpt-4-turbo", "gpt-3.5-turbo"], "base_url": "https://api.openai.com/v1", "requires_plan": "starter"},
    "anthropic": {"name": "Anthropic", "models": ["claude-3-opus", "claude-3-sonnet", "claude-3-haiku"], "base_url": "https://api.anthropic.com/v1", "requires_plan": "pro"},
    "google": {"name": "Google AI", "models": ["gemini-pro", "gemini-pro-vision"], "base_url": "https://generativelanguage.googleapis.com/v1", "requires_plan": "starter"},
    "azure": {"name": "Azure OpenAI", "models": ["gpt-4", "gpt-35-turbo"], "requires_plan": "enterprise"},
    "local": {"name": "Local (Ollama/LM Studio)", "models": ["qwen2.5-coder:1.5b", "codellama", "mistral", "llama2", "deepseek-coder"], "base_url": "http://localhost:11434", "requires_plan": "free"}
}

# Supported Languages
SUPPORTED_LANGUAGES = {
    "Python": {"extension": ".py", "main_file": "main.py", "runtime": "python3"},
    "JavaScript": {"extension": ".js", "main_file": "index.js", "runtime": "node"},
    "TypeScript": {"extension": ".ts", "main_file": "index.ts", "runtime": "ts-node"},
    "Java": {"extension": ".java", "main_file": "Main.java", "runtime": "java"},
    "C#": {"extension": ".cs", "main_file": "Program.cs", "runtime": "dotnet"},
    "Go": {"extension": ".go", "main_file": "main.go", "runtime": "go"},
    "Rust": {"extension": ".rs", "main_file": "main.rs", "runtime": "cargo"},
    "C++": {"extension": ".cpp", "main_file": "main.cpp", "runtime": "g++"},
    "PHP": {"extension": ".php", "main_file": "index.php", "runtime": "php"},
    "Ruby": {"extension": ".rb", "main_file": "main.rb", "runtime": "ruby"},
    "React": {"extension": ".jsx", "main_file": "App.jsx", "runtime": "npm"},
    "Vue": {"extension": ".vue", "main_file": "App.vue", "runtime": "npm"},
    "HTML/CSS": {"extension": ".html", "main_file": "index.html", "runtime": "browser"}
}
