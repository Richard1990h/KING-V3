"""LittleHelper AI - Main Server
A comprehensive multi-agent AI development platform
"""
from fastapi import FastAPI, APIRouter, HTTPException, Depends, Header, Request, BackgroundTasks, UploadFile, File
from fastapi.responses import StreamingResponse
from dotenv import load_dotenv
from starlette.middleware.cors import CORSMiddleware
from sse_starlette.sse import EventSourceResponse
import os
import logging
from pathlib import Path
from typing import List, Optional, Dict, Any
import uuid
from datetime import datetime, timezone, timedelta
import json
import asyncio
import base64

ROOT_DIR = Path(__file__).parent
load_dotenv(ROOT_DIR / '.env')

# Database type selection
DB_TYPE = os.getenv("DB_TYPE", "mongodb").lower()

if DB_TYPE == "mysql":
    # Use MySQL adapter
    from mysql_adapter import mysql_db, MySQLDatabase
    db = None  # Will be set during startup
    client = None
else:
    # Use MongoDB (default)
    from motor.motor_asyncio import AsyncIOMotorClient
    from config import MONGO_URL, DB_NAME
    client = AsyncIOMotorClient(MONGO_URL)
    db = client[DB_NAME]

# Local imports
from config import (
    JWT_SECRET, STRIPE_SECRET_KEY,
    DEFAULT_ADMIN, DEFAULT_SETTINGS, CREDIT_PACKAGES, AI_PROVIDERS, SUPPORTED_LANGUAGES,
    SUBSCRIPTION_PLANS
)
from models import (
    UserCreate, UserLogin, UserResponse, TokenResponse,
    ProjectCreate, ProjectUpdate, ProjectResponse,
    FileCreate, FileUpdate, FileResponse,
    ChatRequest, GlobalChatRequest, ConversationCreate,
    CreditPurchaseRequest, CostEstimateRequest,
    AdminUserUpdate, AdminBulkCreditsRequest,
    TodoCreate, TodoUpdate, AIProviderCreate, LanguageUpdate,
    JobCreate, JobApproval, ContinueJobRequest, JobResponse,
    ProjectScanRequest, PlanCreate, PlanUpdate, SubscriptionCreate, UserAPIKeyInput,
    ThemeSettings, UserProfileUpdate, PasswordChange, CreditPackageCreate, CreditPackagePurchase
)
from utils import hash_password, verify_password, create_token, decode_token, hash_question
from services import AIService, CreditService, JobOrchestrationService, ProjectScannerService
from agents import AGENT_REGISTRY, AGENT_INFO

# Initialize services (will be set during startup for MySQL)
ai_service = None
credit_service = None
job_service = None
scanner_service = None

# Create the main app
app = FastAPI(title="LittleHelper AI API", version="2.0.0")

# Create a router with the /api prefix
api_router = APIRouter(prefix="/api")

# Configure logging
logging.basicConfig(level=logging.INFO, format='%(asctime)s - %(name)s - %(levelname)s - %(message)s')
logger = logging.getLogger(__name__)


# ==================== AUTH HELPERS ====================

async def get_current_user(authorization: str = Header(None)):
    """Get current authenticated user"""
    if not authorization or not authorization.startswith("Bearer "):
        raise HTTPException(status_code=401, detail="Missing or invalid authorization header")
    token = authorization.split(" ")[1]
    payload = decode_token(token)
    user = await db.users.find_one({"id": payload["user_id"]}, {"_id": 0})
    if not user:
        raise HTTPException(status_code=401, detail="User not found")
    return user


async def get_admin_user(user: dict = Depends(get_current_user)):
    """Verify user is admin"""
    if user.get("role") != "admin":
        raise HTTPException(status_code=403, detail="Admin access required")
    return user


# ==================== STARTUP EVENT ====================

@app.on_event("startup")
async def startup_event():
    """Initialize database with default admin and settings"""
    global db, ai_service, credit_service, job_service, scanner_service
    
    # Connect to database based on DB_TYPE
    if DB_TYPE == "mysql":
        await mysql_db.connect()
        db = mysql_db
        logger.info("Connected to MySQL database")
    else:
        logger.info(f"Connected to MongoDB: {os.getenv('MONGO_URL', 'mongodb://localhost:27017')}")
    
    # Initialize services
    ai_service = AIService(db)
    credit_service = CreditService(db)
    job_service = JobOrchestrationService(db, ai_service, credit_service)
    scanner_service = ProjectScannerService(db, ai_service, credit_service)
    
    # Create default admin
    existing_admin = await db.users.find_one({"id": DEFAULT_ADMIN["id"]})
    if not existing_admin:
        admin_data = {
            "id": DEFAULT_ADMIN["id"],
            "email": DEFAULT_ADMIN["email"],
            "name": DEFAULT_ADMIN["name"],
            "password_hash": hash_password(DEFAULT_ADMIN["password"]),
            "role": DEFAULT_ADMIN["role"],
            "credits": DEFAULT_ADMIN["credits"],
            "credits_enabled": DEFAULT_ADMIN["credits_enabled"],
            "plan": DEFAULT_ADMIN["plan"],
            "language": "en",
            "created_at": datetime.now(timezone.utc).isoformat()
        }
        await db.users.insert_one({**admin_data})
        logger.info(f"Default admin created: {DEFAULT_ADMIN['email']}")
    
    # Initialize settings
    for key, value in DEFAULT_SETTINGS.items():
        existing = await db.system_settings.find_one({"setting_key": key})
        if not existing:
            setting_type = "boolean" if isinstance(value, bool) else \
                          "number" if isinstance(value, (int, float)) else \
                          "json" if isinstance(value, dict) else "string"
            val = str(value).lower() if isinstance(value, bool) else \
                  str(value) if not isinstance(value, dict) else json.dumps(value)
            await db.system_settings.insert_one({
                "setting_key": key,
                "setting_value": val,
                "setting_type": setting_type,
                "created_at": datetime.now(timezone.utc).isoformat()
            })
    
    # Create indexes (only for MongoDB)
    if DB_TYPE != "mysql":
        await db.users.create_index("email", unique=True)
        await db.users.create_index("id", unique=True)
        await db.projects.create_index([("user_id", 1), ("id", 1)])
        await db.project_files.create_index([("project_id", 1), ("path", 1)])
        await db.jobs.create_index([("user_id", 1), ("status", 1)])
        await db.chat_history.create_index([("user_id", 1), ("conversation_id", 1)])
        await db.subscription_plans.create_index("plan_id", unique=True)
    
    # Initialize subscription plans
    for plan_id, plan_data in SUBSCRIPTION_PLANS.items():
        existing_plan = await db.subscription_plans.find_one({"plan_id": plan_id})
        if not existing_plan:
            await db.subscription_plans.insert_one({
                "plan_id": plan_id,
                **plan_data,
                "is_active": True,
                "created_at": datetime.now(timezone.utc).isoformat()
            })
    
    logger.info(f"LittleHelper AI startup complete (DB: {DB_TYPE})")


@app.on_event("shutdown")
async def shutdown_event():
    """Cleanup on shutdown"""
    if DB_TYPE == "mysql":
        await mysql_db.close()
        logger.info("MySQL connection closed")
    elif client:
        client.close()
        logger.info("MongoDB connection closed")


# ==================== HEALTH CHECK ====================

@api_router.get("/health")
async def health_check():
    """Health check endpoint"""
    try:
        if DB_TYPE == "mysql":
            # MySQL health check
            async with mysql_db.pool.acquire() as conn:
                async with conn.cursor() as cur:
                    await cur.execute("SELECT 1")
            return {"status": "healthy", "database": "mysql", "connected": True}
        else:
            # MongoDB health check
            await db.command("ping")
            return {"status": "healthy", "database": "mongodb", "connected": True}
    except Exception as e:
        return {"status": "unhealthy", "database": DB_TYPE, "error": str(e)}


# ==================== AUTH ROUTES ====================

@api_router.post("/auth/register", response_model=TokenResponse)
async def register(data: UserCreate, request: Request):
    """Register a new user"""
    # Validate TOS acceptance
    if not data.tos_accepted:
        raise HTTPException(status_code=400, detail="You must accept the Terms of Service to register")
    
    # Get client IP
    client_ip = request.client.host if request.client else "unknown"
    forwarded_for = request.headers.get("X-Forwarded-For", "").split(",")[0].strip()
    if forwarded_for:
        client_ip = forwarded_for
    user_agent = request.headers.get("User-Agent", "")
    
    # Check if IP has too many recent registrations (anti-abuse)
    recent_registrations = await db.ip_records.count_documents({
        "ip_address": client_ip,
        "action": "register",
        "timestamp": {"$gte": (datetime.now(timezone.utc) - timedelta(hours=24)).isoformat()}
    })
    if recent_registrations >= 3:
        raise HTTPException(status_code=429, detail="Too many registrations from this IP. Please try again later.")
    
    existing = await db.users.find_one({"email": data.email})
    if existing:
        raise HTTPException(status_code=400, detail="Email already registered")
    
    # Get admin default settings
    defaults = await db.settings.find_one({"key": "new_user_defaults"}, {"_id": 0})
    default_values = defaults.get("value", {}) if defaults else {}
    
    settings = await credit_service.get_settings()
    free_credits = default_values.get("free_credits", settings.get("free_credits_on_signup", 100))
    default_theme = default_values.get("theme", {
        "primary_color": "#d946ef",
        "secondary_color": "#06b6d4",
        "background_color": "#030712",
        "card_color": "#0B0F19",
        "text_color": "#ffffff",
        "hover_color": "#a855f7",
        "credits_color": "#d946ef",
        "background_image": None
    })
    default_language = default_values.get("language", "en")
    
    user_id = str(uuid.uuid4())
    now = datetime.now(timezone.utc).isoformat()
    
    user_data = {
        "id": user_id,
        "email": data.email,
        "name": data.name,
        "display_name": data.name,
        "password_hash": hash_password(data.password),
        "role": "user",
        "credits": float(free_credits),
        "credits_enabled": True,
        "plan": "free",
        "created_at": now,
        "language": default_language,
        "avatar_url": None,
        "theme": default_theme,
        "registration_ip": client_ip,
        "tos_accepted": True,
        "tos_accepted_at": now,
        "tos_version": "1.0"
    }
    await db.users.insert_one({**user_data})
    
    # Record IP
    await db.ip_records.insert_one({
        "ip_address": client_ip,
        "user_id": user_id,
        "action": "register",
        "timestamp": now,
        "user_agent": user_agent
    })
    
    token = create_token(user_id, "user")
    
    return TokenResponse(
        token=token,
        user=UserResponse(
            id=user_id, email=data.email, name=data.name,
            role="user", credits=float(free_credits),
            credits_enabled=True, plan="free", created_at=now, language=default_language
        )
    )


@api_router.post("/auth/login", response_model=TokenResponse)
async def login(data: UserLogin, request: Request):
    """Login user"""
    # Get client IP
    client_ip = request.client.host if request.client else "unknown"
    forwarded_for = request.headers.get("X-Forwarded-For", "").split(",")[0].strip()
    if forwarded_for:
        client_ip = forwarded_for
    user_agent = request.headers.get("User-Agent", "")
    
    user = await db.users.find_one({"email": data.email}, {"_id": 0})
    if not user or not verify_password(data.password, user["password_hash"]):
        raise HTTPException(status_code=401, detail="Invalid email or password")
    
    now = datetime.now(timezone.utc).isoformat()
    
    await db.users.update_one(
        {"id": user["id"]},
        {"$set": {"last_login_at": now, "last_login_ip": client_ip}}
    )
    
    # Record IP
    await db.ip_records.insert_one({
        "ip_address": client_ip,
        "user_id": user["id"],
        "action": "login",
        "timestamp": now,
        "user_agent": user_agent
    })
    
    token = create_token(user["id"], user["role"])
    
    # Check if user needs to accept TOS (for existing users who haven't accepted)
    tos_required = not user.get("tos_accepted", False)
    
    return TokenResponse(
        token=token,
        user=UserResponse(
            id=user["id"], email=user["email"], name=user["name"],
            role=user["role"], credits=user["credits"],
            credits_enabled=user.get("credits_enabled", True),
            plan=user.get("plan", "free"), created_at=user["created_at"],
            language=user.get("language", "en"),
            tos_accepted=user.get("tos_accepted", False)
        )
    )


@api_router.get("/auth/tos-status")
async def get_tos_status(user: dict = Depends(get_current_user)):
    """Check if user has accepted Terms of Service"""
    return {
        "tos_accepted": user.get("tos_accepted", False),
        "tos_accepted_at": user.get("tos_accepted_at"),
        "tos_version": user.get("tos_version"),
        "current_version": "1.0"
    }


@api_router.post("/auth/accept-tos")
async def accept_tos(user: dict = Depends(get_current_user)):
    """Accept Terms of Service"""
    now = datetime.now(timezone.utc).isoformat()
    
    await db.users.update_one(
        {"id": user["id"]},
        {"$set": {
            "tos_accepted": True,
            "tos_accepted_at": now,
            "tos_version": "1.0"
        }}
    )
    
    return {
        "message": "Terms of Service accepted",
        "tos_accepted": True,
        "tos_accepted_at": now
    }


@api_router.get("/legal/terms")
async def get_terms_of_service():
    """Get Terms of Service content"""
    return {
        "version": "1.0",
        "last_updated": "2025-01-07",
        "content": {
            "title": "Terms of Service",
            "sections": [
                {
                    "title": "1. Acceptance of Terms",
                    "content": "By accessing or using LittleHelper AI ('the Service'), you agree to be bound by these Terms of Service. If you do not agree to these terms, do not use the Service."
                },
                {
                    "title": "2. Service Description",
                    "content": "LittleHelper AI is an AI-powered code generation and development platform. The Service uses artificial intelligence to generate code, documentation, and other software-related content based on user inputs."
                },
                {
                    "title": "3. No Warranty",
                    "content": "THE SERVICE IS PROVIDED 'AS IS' AND 'AS AVAILABLE' WITHOUT WARRANTIES OF ANY KIND, EITHER EXPRESS OR IMPLIED. WE DO NOT WARRANT THAT THE SERVICE WILL BE UNINTERRUPTED, SECURE, OR ERROR-FREE, OR THAT ANY CODE GENERATED WILL BE ACCURATE, COMPLETE, OR FIT FOR ANY PARTICULAR PURPOSE."
                },
                {
                    "title": "4. Limitation of Liability",
                    "content": "TO THE MAXIMUM EXTENT PERMITTED BY LAW, LITTLEHELPER AI AND ITS OPERATORS SHALL NOT BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES, INCLUDING BUT NOT LIMITED TO LOSS OF PROFITS, DATA, USE, OR OTHER INTANGIBLE LOSSES, RESULTING FROM YOUR USE OF THE SERVICE OR ANY CODE GENERATED BY THE SERVICE."
                },
                {
                    "title": "5. User Responsibility",
                    "content": "You are solely responsible for: (a) reviewing, testing, and validating all code generated by the Service before use; (b) ensuring any generated code complies with applicable laws and regulations; (c) any consequences arising from the use of generated code in production environments; (d) maintaining appropriate backups of your work."
                },
                {
                    "title": "6. Intellectual Property",
                    "content": "Code generated by the Service may be used by you subject to these terms. You acknowledge that AI-generated content may not be eligible for copyright protection in all jurisdictions. We make no claims regarding the originality or uniqueness of generated content."
                },
                {
                    "title": "7. Prohibited Uses",
                    "content": "You may not use the Service to: (a) generate malicious code, malware, or exploits; (b) violate any applicable laws or regulations; (c) infringe on intellectual property rights; (d) generate content that is illegal, harmful, or offensive; (e) attempt to bypass security measures or abuse the Service."
                },
                {
                    "title": "8. Indemnification",
                    "content": "You agree to indemnify, defend, and hold harmless LittleHelper AI and its operators from any claims, damages, losses, or expenses arising from your use of the Service, your violation of these terms, or your violation of any rights of another party."
                },
                {
                    "title": "9. Modifications",
                    "content": "We reserve the right to modify these Terms of Service at any time. Continued use of the Service after modifications constitutes acceptance of the updated terms."
                },
                {
                    "title": "10. Governing Law",
                    "content": "These Terms shall be governed by and construed in accordance with applicable laws, without regard to conflict of law principles."
                }
            ],
            "disclaimer": "BY USING THIS SERVICE, YOU ACKNOWLEDGE THAT YOU HAVE READ, UNDERSTOOD, AND AGREE TO BE BOUND BY THESE TERMS OF SERVICE. YOU UNDERSTAND THAT AI-GENERATED CODE MAY CONTAIN ERRORS, BUGS, OR SECURITY VULNERABILITIES, AND YOU ASSUME ALL RISKS ASSOCIATED WITH ITS USE."
        }
    }


@api_router.get("/auth/me", response_model=UserResponse)
async def get_me(user: dict = Depends(get_current_user)):
    """Get current user info"""
    return UserResponse(
        id=user["id"], email=user["email"], name=user["name"],
        role=user["role"], credits=user["credits"],
        credits_enabled=user.get("credits_enabled", True),
        plan=user.get("plan", "free"), created_at=user["created_at"],
        language=user.get("language", "en"),
        tos_accepted=user.get("tos_accepted", False)
    )


@api_router.put("/auth/language")
async def update_language(data: LanguageUpdate, user: dict = Depends(get_current_user)):
    """Update user language preference"""
    await db.users.update_one({"id": user["id"]}, {"$set": {"language": data.language}})
    return {"message": "Language updated", "language": data.language}


# ==================== USER PROFILE ROUTES ====================

@api_router.get("/user/profile")
async def get_user_profile(user: dict = Depends(get_current_user)):
    """Get full user profile including theme settings"""
    return {
        "id": user["id"],
        "email": user["email"],
        "name": user["name"],
        "display_name": user.get("display_name", user["name"]),
        "avatar_url": user.get("avatar_url"),
        "theme": user.get("theme", {
            "primary_color": "#d946ef",
            "secondary_color": "#06b6d4",
            "background_color": "#030712",
            "card_color": "#0B0F19",
            "text_color": "#ffffff",
            "hover_color": "#a855f7",
            "credits_color": "#d946ef",
            "background_image": None
        }),
        "language": user.get("language", "en"),
        "plan": user.get("plan", "free"),
        "credits": user.get("credits", 0),
        "created_at": user.get("created_at")
    }


@api_router.put("/user/profile")
async def update_user_profile(data: UserProfileUpdate, user: dict = Depends(get_current_user)):
    """Update user profile (name, display_name, avatar)"""
    update_fields = {}
    if data.name is not None:
        update_fields["name"] = data.name
    if data.display_name is not None:
        update_fields["display_name"] = data.display_name
    if data.avatar_url is not None:
        update_fields["avatar_url"] = data.avatar_url
    
    if update_fields:
        await db.users.update_one({"id": user["id"]}, {"$set": update_fields})
    return {"message": "Profile updated", "updated": list(update_fields.keys())}


@api_router.put("/user/theme")
async def update_user_theme(data: ThemeSettings, user: dict = Depends(get_current_user)):
    """Update user theme settings"""
    theme_data = data.model_dump()
    await db.users.update_one({"id": user["id"]}, {"$set": {"theme": theme_data}})
    return {"message": "Theme updated", "theme": theme_data}


@api_router.post("/user/avatar")
async def upload_avatar(file: UploadFile = File(...), user: dict = Depends(get_current_user)):
    """Upload user avatar image"""
    if not file.content_type.startswith("image/"):
        raise HTTPException(status_code=400, detail="File must be an image")
    
    # Read and encode image
    contents = await file.read()
    if len(contents) > 5 * 1024 * 1024:  # 5MB limit
        raise HTTPException(status_code=400, detail="Image too large (max 5MB)")
    
    # Store as base64 data URL
    encoded = base64.b64encode(contents).decode("utf-8")
    avatar_url = f"data:{file.content_type};base64,{encoded}"
    
    await db.users.update_one({"id": user["id"]}, {"$set": {"avatar_url": avatar_url}})
    return {"message": "Avatar uploaded", "avatar_url": avatar_url}


@api_router.put("/user/password")
async def change_password(data: PasswordChange, user: dict = Depends(get_current_user)):
    """Change user password"""
    # Verify current password
    if not verify_password(data.current_password, user["password_hash"]):
        raise HTTPException(status_code=400, detail="Current password is incorrect")
    
    # Update password
    new_hash = hash_password(data.new_password)
    await db.users.update_one({"id": user["id"]}, {"$set": {"password_hash": new_hash}})
    return {"message": "Password changed successfully"}


# ==================== CREDIT ADD-ON ROUTES ====================

@api_router.get("/credits/packages")
async def get_credit_packages(user: dict = Depends(get_current_user)):
    """Get available credit packages (subscription + add-ons)"""
    # Get from database or use defaults
    packages = await db.credit_packages.find({"is_active": True}, {"_id": 0}).to_list(20)
    if not packages:
        # Return default packages from config
        packages = [
            {"package_id": k, **v, "is_addon": True, "is_active": True} 
            for k, v in CREDIT_PACKAGES.items()
        ]
    return packages


@api_router.post("/credits/purchase-addon")
async def purchase_credit_addon(data: CreditPackagePurchase, user: dict = Depends(get_current_user)):
    """Purchase additional credits (one-time add-on)"""
    try:
        from emergentintegrations.payments.stripe.checkout import StripeCheckout, CheckoutSessionRequest
    except ImportError:
        # Fallback for local development without emergentintegrations
        raise HTTPException(status_code=503, detail="Stripe payments not available in local development mode. Please deploy to Emergent platform for payment features.")
    
    # Find package
    package = await db.credit_packages.find_one({"package_id": data.package_id, "is_active": True})
    if not package:
        # Check default packages
        if data.package_id in CREDIT_PACKAGES:
            package = {"package_id": data.package_id, **CREDIT_PACKAGES[data.package_id]}
        else:
            raise HTTPException(status_code=404, detail="Credit package not found")
    
    # Create Stripe checkout
    webhook_url = f"{data.origin_url}/api/webhooks/stripe"
    stripe_checkout = StripeCheckout(api_key=STRIPE_SECRET_KEY, webhook_url=webhook_url)
    
    checkout_request = CheckoutSessionRequest(
        amount=int(package["price"] * 100),
        currency="usd",
        product_name=f"{package['name']} - {package['credits']} Credits",
        success_url=f"{data.origin_url}/credits?session_id={{CHECKOUT_SESSION_ID}}",
        cancel_url=f"{data.origin_url}/credits",
        metadata={
            "user_id": user["id"],
            "package_id": data.package_id,
            "credits": str(package["credits"]),
            "type": "addon"
        }
    )
    
    session = await stripe_checkout.create_checkout_session(checkout_request)
    return {"url": session.url, "session_id": session.session_id}


# ==================== ADMIN SETTINGS FOR EMERGENT LLM ====================

@api_router.get("/admin/ai-settings")
async def get_ai_settings(admin: dict = Depends(get_admin_user)):
    """Get AI settings including Emergent LLM toggle"""
    emergent_enabled = await db.settings.find_one({"key": "emergent_llm_enabled"}, {"_id": 0})
    return {
        "emergent_llm_enabled": emergent_enabled.get("value", True) if emergent_enabled else True,
        "emergent_key_configured": bool(os.environ.get("EMERGENT_LLM_KEY"))
    }


@api_router.put("/admin/ai-settings/emergent-toggle")
async def toggle_emergent_llm(enabled: bool, admin: dict = Depends(get_admin_user)):
    """Toggle Emergent LLM on/off for all users"""
    await db.settings.update_one(
        {"key": "emergent_llm_enabled"},
        {"$set": {"key": "emergent_llm_enabled", "value": enabled}},
        upsert=True
    )
    return {"message": f"Emergent LLM {'enabled' if enabled else 'disabled'}", "enabled": enabled}


@api_router.get("/admin/ip-records")
async def get_ip_records(admin: dict = Depends(get_admin_user), limit: int = 100):
    """Get IP address records for abuse detection"""
    records = await db.ip_records.find({}, {"_id": 0}).sort("timestamp", -1).to_list(limit)
    
    # Group by IP
    ip_stats = {}
    for record in records:
        ip = record["ip_address"]
        if ip not in ip_stats:
            ip_stats[ip] = {"ip": ip, "registrations": 0, "logins": 0, "users": set()}
        if record["action"] == "register":
            ip_stats[ip]["registrations"] += 1
        elif record["action"] == "login":
            ip_stats[ip]["logins"] += 1
        if record.get("user_id"):
            ip_stats[ip]["users"].add(record["user_id"])
    
    # Convert sets to counts
    for ip in ip_stats:
        ip_stats[ip]["unique_users"] = len(ip_stats[ip]["users"])
        del ip_stats[ip]["users"]
    
    return {
        "records": records[:50],
        "ip_summary": list(ip_stats.values())[:20]
    }


# ==================== ADMIN DEFAULT SETTINGS ====================

@api_router.get("/admin/defaults")
async def get_admin_defaults(admin: dict = Depends(get_admin_user)):
    """Get default settings for new users"""
    defaults = await db.settings.find_one({"key": "new_user_defaults"}, {"_id": 0})
    if not defaults:
        defaults = {
            "key": "new_user_defaults",
            "theme": {
                "primary_color": "#d946ef",
                "secondary_color": "#06b6d4",
                "background_color": "#030712",
                "card_color": "#0B0F19",
                "text_color": "#ffffff",
                "hover_color": "#a855f7",
                "credits_color": "#d946ef",
                "background_image": None
            },
            "language": "en",
            "free_credits": 100
        }
    return defaults.get("value", defaults)


@api_router.put("/admin/defaults")
async def update_admin_defaults(admin: dict = Depends(get_admin_user), theme: dict = None, language: str = None, free_credits: int = None):
    """Update default settings for new users"""
    current = await db.settings.find_one({"key": "new_user_defaults"})
    defaults = current.get("value", {}) if current else {}
    
    if theme:
        defaults["theme"] = theme
    if language:
        defaults["language"] = language
    if free_credits is not None:
        defaults["free_credits"] = free_credits
    
    await db.settings.update_one(
        {"key": "new_user_defaults"},
        {"$set": {"key": "new_user_defaults", "value": defaults}},
        upsert=True
    )
    return {"message": "Defaults updated", "defaults": defaults}


# ==================== FREE AI PROVIDERS ====================

FREE_AI_PROVIDERS = {
    "huggingface": {
        "name": "Hugging Face Inference",
        "description": "Free tier with rate limits",
        "models": ["mistralai/Mistral-7B-Instruct-v0.2", "meta-llama/Llama-2-7b-chat-hf"],
        "api_url": "https://api-inference.huggingface.co/models/",
        "requires_key": True,
        "free_tier": True
    },
    "groq": {
        "name": "Groq (Free Tier)",
        "description": "Fast inference, free tier available",
        "models": ["llama-3.1-70b-versatile", "llama-3.1-8b-instant", "mixtral-8x7b-32768"],
        "api_url": "https://api.groq.com/openai/v1/chat/completions",
        "requires_key": True,
        "free_tier": True
    },
    "together": {
        "name": "Together AI (Free Credits)",
        "description": "$25 free credits on signup",
        "models": ["meta-llama/Llama-3-70b-chat-hf", "mistralai/Mixtral-8x7B-Instruct-v0.1"],
        "api_url": "https://api.together.xyz/v1/chat/completions",
        "requires_key": True,
        "free_tier": True
    },
    "openrouter": {
        "name": "OpenRouter (Free Models)",
        "description": "Access to free models from various providers",
        "models": ["meta-llama/llama-3.1-8b-instruct:free", "google/gemma-2-9b-it:free"],
        "api_url": "https://openrouter.ai/api/v1/chat/completions",
        "requires_key": True,
        "free_tier": True
    },
    "cohere": {
        "name": "Cohere (Free Trial)",
        "description": "Free trial with API key",
        "models": ["command", "command-light"],
        "api_url": "https://api.cohere.ai/v1/generate",
        "requires_key": True,
        "free_tier": True
    },
    "local_ollama": {
        "name": "Local Ollama",
        "description": "Free local LLM via Ollama",
        "models": ["llama3", "mistral", "codellama", "qwen2.5-coder"],
        "api_url": "http://localhost:11434/api/generate",
        "requires_key": False,
        "free_tier": True
    }
}


@api_router.get("/admin/free-ai-providers")
async def get_free_ai_providers(admin: dict = Depends(get_admin_user)):
    """Get list of free AI providers with their status"""
    # Get enabled providers from settings
    enabled_settings = await db.settings.find_one({"key": "enabled_free_providers"}, {"_id": 0})
    enabled = enabled_settings.get("value", {}) if enabled_settings else {}
    
    providers = []
    for provider_id, config in FREE_AI_PROVIDERS.items():
        # Get stored API key if any
        stored_key = await db.settings.find_one({"key": f"free_provider_key_{provider_id}"}, {"_id": 0})
        
        providers.append({
            "id": provider_id,
            **config,
            "enabled": enabled.get(provider_id, False),
            "has_key": bool(stored_key and stored_key.get("value"))
        })
    
    return providers


@api_router.put("/admin/free-ai-providers/{provider_id}")
async def toggle_free_ai_provider(provider_id: str, enabled: bool, api_key: str = None, admin: dict = Depends(get_admin_user)):
    """Enable/disable a free AI provider"""
    if provider_id not in FREE_AI_PROVIDERS:
        raise HTTPException(status_code=404, detail="Provider not found")
    
    # Update enabled status
    enabled_settings = await db.settings.find_one({"key": "enabled_free_providers"})
    current_enabled = enabled_settings.get("value", {}) if enabled_settings else {}
    current_enabled[provider_id] = enabled
    
    await db.settings.update_one(
        {"key": "enabled_free_providers"},
        {"$set": {"key": "enabled_free_providers", "value": current_enabled}},
        upsert=True
    )
    
    # Store API key if provided
    if api_key:
        await db.settings.update_one(
            {"key": f"free_provider_key_{provider_id}"},
            {"$set": {"key": f"free_provider_key_{provider_id}", "value": api_key}},
            upsert=True
        )
    
    return {"message": f"Provider {provider_id} {'enabled' if enabled else 'disabled'}", "enabled": enabled}


@api_router.get("/ai/available-providers")
async def get_available_ai_providers(user: dict = Depends(get_current_user)):
    """Get available AI providers for the user (including free ones)"""
    # Get enabled free providers
    enabled_settings = await db.settings.find_one({"key": "enabled_free_providers"}, {"_id": 0})
    enabled = enabled_settings.get("value", {}) if enabled_settings else {}
    
    # Check if Emergent LLM is enabled
    emergent_enabled = await db.settings.find_one({"key": "emergent_llm_enabled"}, {"_id": 0})
    emergent_on = emergent_enabled.get("value", True) if emergent_enabled else True
    
    providers = []
    
    # Add Emergent LLM if enabled
    if emergent_on and os.environ.get("EMERGENT_LLM_KEY"):
        providers.append({
            "id": "emergent",
            "name": "LittleHelper AI (Default)",
            "description": "Powered by Emergent LLM",
            "models": ["gpt-4o", "claude-sonnet-4", "gemini-2.5-flash"],
            "available": True,
            "is_default": True
        })
    
    # Add enabled free providers
    for provider_id, config in FREE_AI_PROVIDERS.items():
        if enabled.get(provider_id):
            providers.append({
                "id": provider_id,
                "name": config["name"],
                "description": config["description"],
                "models": config["models"],
                "available": True,
                "is_free": True
            })
    
    # Add local LLM
    providers.append({
        "id": "local",
        "name": "Local LLM (Ollama)",
        "description": "Requires Ollama running locally",
        "models": ["qwen2.5-coder:1.5b", "llama3", "mistral"],
        "available": True,
        "is_free": True
    })
    
    return providers


# ==================== PROJECT ROUTES ====================

@api_router.get("/projects", response_model=List[ProjectResponse])
async def get_projects(user: dict = Depends(get_current_user)):
    """Get all user projects"""
    projects = await db.projects.find({"user_id": user["id"]}, {"_id": 0}).to_list(100)
    return [ProjectResponse(**p) for p in projects]


@api_router.post("/projects", response_model=ProjectResponse)
async def create_project(data: ProjectCreate, user: dict = Depends(get_current_user)):
    """Create a new project"""
    project_id = str(uuid.uuid4())
    now = datetime.now(timezone.utc).isoformat()
    
    project_data = {
        "id": project_id,
        "user_id": user["id"],
        "name": data.name,
        "description": data.description or "",
        "language": data.language,
        "created_at": now,
        "updated_at": now,
        "status": "active"
    }
    await db.projects.insert_one({**project_data})
    
    # Create default file based on language
    default_files = {
        "Python": {"path": "main.py", "content": "# Welcome to LittleHelper AI\n\nprint('Hello, World!')\n"},
        "JavaScript": {"path": "index.js", "content": "// Welcome to LittleHelper AI\n\nconsole.log('Hello, World!');\n"},
        "TypeScript": {"path": "index.ts", "content": "// Welcome to LittleHelper AI\n\nconsole.log('Hello, World!');\n"},
        "React": {"path": "App.jsx", "content": "// Welcome to LittleHelper AI\n\nimport React from 'react';\n\nexport default function App() {\n  return <div>Hello, World!</div>;\n}\n"},
        "Java": {"path": "Main.java", "content": "// Welcome to LittleHelper AI\n\npublic class Main {\n    public static void main(String[] args) {\n        System.out.println(\"Hello, World!\");\n    }\n}\n"},
        "C#": {"path": "Program.cs", "content": "// Welcome to LittleHelper AI\n\nusing System;\n\nclass Program {\n    static void Main() {\n        Console.WriteLine(\"Hello, World!\");\n    }\n}\n"},
        "Go": {"path": "main.go", "content": "// Welcome to LittleHelper AI\n\npackage main\n\nimport \"fmt\"\n\nfunc main() {\n    fmt.Println(\"Hello, World!\")\n}\n"}
    }
    
    default_file = default_files.get(data.language, default_files["Python"])
    await db.project_files.insert_one({
        "id": str(uuid.uuid4()),
        "project_id": project_id,
        "path": default_file["path"],
        "content": default_file["content"],
        "updated_at": now
    })
    
    return ProjectResponse(**project_data)


@api_router.get("/projects/{project_id}", response_model=ProjectResponse)
async def get_project(project_id: str, user: dict = Depends(get_current_user)):
    """Get project by ID"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]}, {"_id": 0})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    return ProjectResponse(**project)


@api_router.put("/projects/{project_id}", response_model=ProjectResponse)
async def update_project(project_id: str, data: ProjectUpdate, user: dict = Depends(get_current_user)):
    """Update project"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    update_data = {k: v for k, v in data.model_dump().items() if v is not None}
    update_data["updated_at"] = datetime.now(timezone.utc).isoformat()
    
    await db.projects.update_one({"id": project_id}, {"$set": update_data})
    updated = await db.projects.find_one({"id": project_id}, {"_id": 0})
    return ProjectResponse(**updated)


@api_router.delete("/projects/{project_id}")
async def delete_project(project_id: str, user: dict = Depends(get_current_user)):
    """Delete project and all associated data"""
    result = await db.projects.delete_one({"id": project_id, "user_id": user["id"]})
    if result.deleted_count == 0:
        raise HTTPException(status_code=404, detail="Project not found")
    
    await db.project_files.delete_many({"project_id": project_id})
    await db.chat_history.delete_many({"project_id": project_id})
    await db.todos.delete_many({"project_id": project_id})
    await db.jobs.delete_many({"project_id": project_id})
    
    return {"message": "Project deleted"}


@api_router.post("/projects/{project_id}/export")
async def export_project(project_id: str, user: dict = Depends(get_current_user)):
    """Export project as ZIP"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]}, {"_id": 0})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    return await scanner_service.create_project_zip(project_id)


@api_router.post("/projects/{project_id}/upload")
async def upload_files(project_id: str, files: List[UploadFile] = File(...), user: dict = Depends(get_current_user)):
    """Upload files to project"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    file_list = []
    for file in files:
        content = await file.read()
        try:
            content_str = content.decode('utf-8')
        except:
            content_str = content.decode('latin-1')
        file_list.append({"path": file.filename, "content": content_str})
    
    return await scanner_service.process_upload(project_id, file_list)


@api_router.post("/projects/{project_id}/upload-zip")
async def upload_zip(project_id: str, file: UploadFile = File(...), user: dict = Depends(get_current_user)):
    """Upload ZIP file to project"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    content = await file.read()
    return await scanner_service.process_zip_upload(project_id, content)


@api_router.post("/projects/{project_id}/scan")
async def scan_project(project_id: str, user: dict = Depends(get_current_user)):
    """Scan project for issues"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    return await scanner_service.scan_for_issues(project_id, user)


@api_router.get("/projects/{project_id}/scan-estimate")
async def get_scan_estimate(project_id: str, user: dict = Depends(get_current_user)):
    """Get cost estimate for scanning project"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    files = await db.project_files.find({"project_id": project_id}, {"_id": 0}).to_list(100)
    estimate = await scanner_service.estimate_scan_cost(files)
    
    # Check if user uses own key
    uses_own_key = await credit_service.user_uses_own_key(user["id"])
    if uses_own_key or not user.get("credits_enabled", True):
        estimate["free_usage"] = True
        estimate["estimated_credits"] = 0
    
    return estimate


# ==================== FILE ROUTES ====================

@api_router.get("/projects/{project_id}/files", response_model=List[FileResponse])
async def get_project_files(project_id: str, user: dict = Depends(get_current_user)):
    """Get all files in project"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    files = await db.project_files.find({"project_id": project_id}, {"_id": 0}).to_list(1000)
    return [FileResponse(**f) for f in files]


@api_router.post("/projects/{project_id}/files", response_model=FileResponse)
async def create_file(project_id: str, data: FileCreate, user: dict = Depends(get_current_user)):
    """Create a new file"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    existing = await db.project_files.find_one({"project_id": project_id, "path": data.path})
    if existing:
        raise HTTPException(status_code=400, detail="File already exists")
    
    now = datetime.now(timezone.utc).isoformat()
    file_data = {
        "id": str(uuid.uuid4()),
        "project_id": project_id,
        "path": data.path,
        "content": data.content,
        "updated_at": now
    }
    await db.project_files.insert_one({**file_data})
    await db.projects.update_one({"id": project_id}, {"$set": {"updated_at": now}})
    
    return FileResponse(**file_data)


@api_router.put("/projects/{project_id}/files/{file_id}", response_model=FileResponse)
async def update_file(project_id: str, file_id: str, data: FileUpdate, user: dict = Depends(get_current_user)):
    """Update file content"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    now = datetime.now(timezone.utc).isoformat()
    result = await db.project_files.update_one(
        {"id": file_id, "project_id": project_id},
        {"$set": {"content": data.content, "updated_at": now}}
    )
    
    if result.matched_count == 0:
        raise HTTPException(status_code=404, detail="File not found")
    
    await db.projects.update_one({"id": project_id}, {"$set": {"updated_at": now}})
    file_doc = await db.project_files.find_one({"id": file_id}, {"_id": 0})
    return FileResponse(**file_doc)


@api_router.delete("/projects/{project_id}/files/{file_id}")
async def delete_file(project_id: str, file_id: str, user: dict = Depends(get_current_user)):
    """Delete a file"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    result = await db.project_files.delete_one({"id": file_id, "project_id": project_id})
    if result.deleted_count == 0:
        raise HTTPException(status_code=404, detail="File not found")
    
    return {"message": "File deleted"}


@api_router.post("/projects/{project_id}/upload-zip")
async def upload_zip(project_id: str, file: UploadFile = File(...), user: dict = Depends(get_current_user)):
    """Upload a ZIP file and extract files into the project"""
    import zipfile
    import io
    
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    if not file.filename.endswith('.zip'):
        raise HTTPException(status_code=400, detail="Only ZIP files are supported")
    
    content = await file.read()
    files_created = []
    
    try:
        with zipfile.ZipFile(io.BytesIO(content)) as zf:
            for name in zf.namelist():
                # Skip directories and hidden files
                if name.endswith('/') or name.startswith('__') or '/__' in name:
                    continue
                    
                # Skip binary files and common non-code files
                if any(name.endswith(ext) for ext in ['.pyc', '.exe', '.dll', '.so', '.o', '.a', '.bin', '.class', '.jar', '.war']):
                    continue
                
                try:
                    file_content = zf.read(name).decode('utf-8')
                except:
                    continue  # Skip files that can't be decoded as text
                
                # Clean up path
                clean_path = name.lstrip('/')
                if '/' in clean_path:
                    # Remove top-level directory if all files are in one folder
                    parts = clean_path.split('/')
                    if len(parts) > 1:
                        clean_path = '/'.join(parts[1:]) if parts[0] else clean_path
                
                now = datetime.now(timezone.utc).isoformat()
                
                # Check if file exists
                existing = await db.project_files.find_one({"project_id": project_id, "path": clean_path})
                if existing:
                    await db.project_files.update_one(
                        {"project_id": project_id, "path": clean_path},
                        {"$set": {"content": file_content, "updated_at": now}}
                    )
                else:
                    await db.project_files.insert_one({
                        "id": str(uuid.uuid4()),
                        "project_id": project_id,
                        "path": clean_path,
                        "content": file_content,
                        "created_at": now,
                        "updated_at": now
                    })
                
                files_created.append(clean_path)
        
        return {"message": f"Uploaded {len(files_created)} files", "files": files_created}
    
    except zipfile.BadZipFile:
        raise HTTPException(status_code=400, detail="Invalid ZIP file")
    except Exception as e:
        logger.error(f"ZIP upload error: {e}")
        raise HTTPException(status_code=500, detail=f"Failed to process ZIP file: {str(e)}")


@api_router.get("/projects/{project_id}/export")
async def export_project(project_id: str, user: dict = Depends(get_current_user)):
    """Export project as a ZIP file"""
    import zipfile
    import io
    
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    files = await db.project_files.find({"project_id": project_id}, {"_id": 0}).to_list(1000)
    
    # Create ZIP in memory
    zip_buffer = io.BytesIO()
    
    with zipfile.ZipFile(zip_buffer, 'w', zipfile.ZIP_DEFLATED) as zf:
        for file in files:
            zf.writestr(file["path"], file.get("content", ""))
        
        # Add a README with project info
        readme_content = f"""# {project['name']}

{project.get('description', 'No description')}

**Language:** {project.get('language', 'Python')}
**Created:** {project.get('created_at', 'Unknown')}

---
Generated by LittleHelper AI
"""
        zf.writestr("README.md", readme_content)
    
    zip_buffer.seek(0)
    
    return StreamingResponse(
        zip_buffer,
        media_type="application/zip",
        headers={
            "Content-Disposition": f"attachment; filename={project['name'].replace(' ', '_')}.zip"
        }
    )


# ==================== TODO ROUTES ====================

@api_router.get("/projects/{project_id}/todos")
async def get_todos(project_id: str, user: dict = Depends(get_current_user)):
    """Get project todos"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    todos = await db.todos.find({"project_id": project_id}, {"_id": 0}).sort("created_at", 1).to_list(100)
    return todos


@api_router.post("/projects/{project_id}/todos")
async def create_todo(project_id: str, data: TodoCreate, user: dict = Depends(get_current_user)):
    """Create a new todo"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    todo_data = {
        "id": str(uuid.uuid4()),
        "project_id": project_id,
        "text": data.text,
        "completed": False,
        "priority": data.priority,
        "created_at": datetime.now(timezone.utc).isoformat()
    }
    await db.todos.insert_one({**todo_data})
    return todo_data


@api_router.put("/projects/{project_id}/todos/{todo_id}")
async def update_todo(project_id: str, todo_id: str, data: TodoUpdate, user: dict = Depends(get_current_user)):
    """Update a todo"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    update_data = {k: v for k, v in data.model_dump().items() if v is not None}
    if not update_data:
        raise HTTPException(status_code=400, detail="No update data")
    
    result = await db.todos.update_one({"id": todo_id, "project_id": project_id}, {"$set": update_data})
    if result.matched_count == 0:
        raise HTTPException(status_code=404, detail="Todo not found")
    
    updated = await db.todos.find_one({"id": todo_id}, {"_id": 0})
    return updated


@api_router.delete("/projects/{project_id}/todos/{todo_id}")
async def delete_todo(project_id: str, todo_id: str, user: dict = Depends(get_current_user)):
    """Delete a todo"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    result = await db.todos.delete_one({"id": todo_id, "project_id": project_id})
    if result.deleted_count == 0:
        raise HTTPException(status_code=404, detail="Todo not found")
    
    return {"message": "Todo deleted"}


# ==================== JOB ROUTES (Multi-Agent Pipeline) ====================

@api_router.post("/jobs/create")
async def create_job(data: JobCreate, user: dict = Depends(get_current_user)):
    """Create a new job - analyzes prompt and creates task breakdown"""
    project = await db.projects.find_one({"id": data.project_id, "user_id": user["id"]}, {"_id": 0})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    job = await job_service.create_job(user, project, data.prompt, data.multi_agent_mode)
    return job


@api_router.post("/jobs/{job_id}/approve")
async def approve_job(job_id: str, data: JobApproval, user: dict = Depends(get_current_user)):
    """Approve job with optional task modifications"""
    result = await job_service.approve_job(
        job_id, user,
        [t.model_dump() for t in data.modified_tasks] if data.modified_tasks else None
    )
    return result


@api_router.get("/jobs/{job_id}/execute")
async def execute_job_sse(job_id: str, user: dict = Depends(get_current_user)):
    """Execute job with SSE streaming updates"""
    job = await job_service.get_job(job_id, user["id"])
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    
    if job["status"] not in ["approved", "needs_more_credits"]:
        raise HTTPException(status_code=400, detail=f"Job cannot be executed in status: {job['status']}")
    
    async def event_generator():
        try:
            async for update in job_service.execute_job(job_id, user):
                yield {
                    "event": update.get("type", "update"),
                    "data": json.dumps(update)
                }
        except Exception as e:
            yield {
                "event": "error",
                "data": json.dumps({"type": "error", "message": str(e)})
            }
    
    return EventSourceResponse(event_generator())


@api_router.post("/jobs/{job_id}/continue")
async def continue_job(job_id: str, data: ContinueJobRequest, user: dict = Depends(get_current_user)):
    """Continue a job that needs more credits"""
    job = await job_service.get_job(job_id, user["id"])
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    
    if job["status"] != "needs_more_credits":
        raise HTTPException(status_code=400, detail="Job is not waiting for credits")
    
    if not data.approved:
        await db.jobs.update_one({"id": job_id}, {"$set": {"status": "cancelled"}})
        return {"message": "Job cancelled"}
    
    # Update status and continue execution
    await db.jobs.update_one({"id": job_id}, {"$set": {"status": "approved"}})
    return {"message": "Job will continue", "job_id": job_id}


@api_router.get("/jobs/{job_id}")
async def get_job(job_id: str, user: dict = Depends(get_current_user)):
    """Get job details"""
    job = await job_service.get_job(job_id, user["id"])
    if not job:
        raise HTTPException(status_code=404, detail="Job not found")
    return job


@api_router.get("/jobs")
async def get_user_jobs(user: dict = Depends(get_current_user), limit: int = 20):
    """Get user's jobs"""
    jobs = await job_service.get_user_jobs(user["id"], limit)
    return jobs


# ==================== CHAT ROUTES (Simple Chat Mode) ====================

@api_router.get("/projects/{project_id}/chat")
async def get_chat_history(project_id: str, user: dict = Depends(get_current_user)):
    """Get project chat history"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    messages = await db.chat_history.find(
        {"project_id": project_id, "user_id": user["id"]},
        {"_id": 0}
    ).sort("timestamp", 1).to_list(1000)
    return messages


@api_router.post("/projects/{project_id}/chat")
async def send_chat_message(project_id: str, data: ChatRequest, user: dict = Depends(get_current_user)):
    """Send chat message - simple or multi-agent mode"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]}, {"_id": 0})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    # Check credits
    should_charge = await credit_service.should_charge(user)
    estimated_tokens = len(data.message.split()) * 4 * (7 if data.multi_agent_mode else 1) * 2
    estimated_credits = await credit_service.calculate_credits(estimated_tokens, is_project=data.multi_agent_mode) if should_charge else 0
    
    if should_charge and user["credits"] < estimated_credits:
        raise HTTPException(status_code=402, detail=f"Insufficient credits. Estimated cost: {estimated_credits:.2f}")
    
    now = datetime.now(timezone.utc).isoformat()
    conversation_id = data.conversation_id or str(uuid.uuid4())
    
    # Generate AI response
    if data.multi_agent_mode and len(data.agents_enabled) > 1:
        # Multi-agent mode - use job system
        job = await job_service.create_job(user, project, data.message, True)
        ai_content = f"Created job with {len(job.get('tasks', []))} tasks. Job ID: {job['id']}\n\n"
        ai_content += f"Status: {job['status']}\n"
        ai_content += f"Estimated credits: {job.get('total_estimated_credits', 0):.2f}\n\n"
        
        if job.get('tasks'):
            ai_content += "Tasks:\n"
            for i, task in enumerate(job['tasks']):
                ai_content += f"{i+1}. [{task.get('agent_type')}] {task.get('title')}\n"
        
        provider = "multi-agent"
        model = "pipeline"
        tokens_used = estimated_tokens
    else:
        # Simple chat mode - single agent
        agent_type = data.agents_enabled[0] if data.agents_enabled else "developer"
        agent_class = AGENT_REGISTRY.get(agent_type, AGENT_REGISTRY["developer"])
        agent = agent_class(
            ai_service,
            project_context={
                "name": project["name"],
                "language": project["language"],
                "description": project.get("description", "")
            }
        )
        
        existing_files = await db.project_files.find({"project_id": project_id}, {"_id": 0}).to_list(50)
        result = await agent.execute(data.message, {"existing_files": existing_files})
        
        ai_content = result.content
        
        # Clean up AI content - remove raw code blocks for chat display
        if result.files_created and len(result.files_created) > 0:
            # If files were created, show a clean summary instead of raw code
            file_names = [f.get("path", "file") for f in result.files_created]
            clean_summary = f"✅ Created {len(file_names)} file(s): {', '.join(file_names)}\n\nCheck the Files panel to view and edit your code."
            
            # Try to extract any non-code explanation from the response
            import re
            # Remove code blocks and file markers
            explanation = re.sub(r'```[\s\S]*?```', '', ai_content)
            explanation = re.sub(r'FILE:\s*[^\n]+\n?', '', explanation)
            explanation = re.sub(r'###\s*[^\n]+\n?', '', explanation)
            explanation = explanation.strip()
            
            if explanation and len(explanation) > 20:
                ai_content = f"{explanation}\n\n{clean_summary}"
            else:
                ai_content = clean_summary
        
        provider = "local"
        model = ai_service.default_model
        tokens_used = result.tokens_used
        
        # Save any created files
        if result.files_created:
            for file_data in result.files_created:
                existing = await db.project_files.find_one({"project_id": project_id, "path": file_data["path"]})
                if existing:
                    await db.project_files.update_one(
                        {"project_id": project_id, "path": file_data["path"]},
                        {"$set": {"content": file_data["content"], "updated_at": now}}
                    )
                else:
                    await db.project_files.insert_one({
                        "id": str(uuid.uuid4()),
                        "project_id": project_id,
                        "path": file_data["path"],
                        "content": file_data["content"],
                        "updated_at": now
                    })
    
    credits_used = await credit_service.calculate_credits(tokens_used, is_project=data.multi_agent_mode) if should_charge else 0
    
    # Save messages
    user_msg_data = {
        "id": str(uuid.uuid4()),
        "user_id": user["id"],
        "project_id": project_id,
        "conversation_id": conversation_id,
        "role": "user",
        "content": data.message,
        "agent_id": None,
        "provider": None,
        "model": None,
        "tokens_used": 0,
        "credits_deducted": 0,
        "timestamp": now,
        "multi_agent_mode": data.multi_agent_mode
    }
    await db.chat_history.insert_one({**user_msg_data})
    
    ai_msg_data = {
        "id": str(uuid.uuid4()),
        "user_id": user["id"],
        "project_id": project_id,
        "conversation_id": conversation_id,
        "role": "assistant",
        "content": ai_content,
        "agent_id": ",".join(data.agents_enabled) if data.multi_agent_mode else (data.agents_enabled[0] if data.agents_enabled else "developer"),
        "provider": provider,
        "model": model,
        "tokens_used": tokens_used,
        "credits_deducted": credits_used,
        "timestamp": datetime.now(timezone.utc).isoformat(),
        "multi_agent_mode": data.multi_agent_mode
    }
    await db.chat_history.insert_one({**ai_msg_data})
    
    # Deduct credits
    if credits_used > 0:
        await credit_service.deduct_credits(user["id"], credits_used, f"Chat in project {project['name']}", "chat", ai_msg_data["id"])
    
    updated_user = await db.users.find_one({"id": user["id"]}, {"_id": 0, "credits": 1})
    
    return {
        "user_message": user_msg_data,
        "ai_message": ai_msg_data,
        "credits_used": credits_used,
        "remaining_credits": updated_user.get("credits", 0)
    }


@api_router.get("/projects/{project_id}/chat/stream")
async def stream_chat(project_id: str, message: str, agent: str = "developer", user: dict = Depends(get_current_user)):
    """Stream chat response via SSE"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]}, {"_id": 0})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    agent_class = AGENT_REGISTRY.get(agent, AGENT_REGISTRY["developer"])
    agent_instance = agent_class(
        ai_service,
        project_context={
            "name": project["name"],
            "language": project["language"]
        }
    )
    
    async def event_generator():
        full_response = []
        try:
            async for chunk in agent_instance.execute_streaming(message):
                full_response.append(chunk)
                yield {
                    "event": "chunk",
                    "data": json.dumps({"content": chunk})
                }
            
            yield {
                "event": "done",
                "data": json.dumps({"content": "".join(full_response)})
            }
        except Exception as e:
            yield {
                "event": "error",
                "data": json.dumps({"error": str(e)})
            }
    
    return EventSourceResponse(event_generator())


@api_router.delete("/projects/{project_id}/chat")
async def clear_chat_history(project_id: str, user: dict = Depends(get_current_user)):
    """Clear project chat history"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    await db.chat_history.delete_many({"project_id": project_id, "user_id": user["id"]})
    return {"message": "Chat history cleared"}


# ==================== AI BUILDING FLOW ====================

from pydantic import BaseModel as PydanticBaseModel

class PlanRequest(PydanticBaseModel):
    project_id: str
    request: str
    agents: List[str] = ["planner", "researcher", "developer"]


class ResearchRequest(PydanticBaseModel):
    project_id: str
    request: str
    tasks: List[dict]


class ExecuteTaskRequest(PydanticBaseModel):
    project_id: str
    task: str
    agent: str = "developer"


@api_router.post("/ai/plan")
async def create_ai_plan(data: PlanRequest, user: dict = Depends(get_current_user)):
    """Create a build plan from user request using Planner agent"""
    project = await db.projects.find_one({"id": data.project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    # Use Planner agent to create task list
    system_prompt = """You are a software project planner. Analyze the user's request and break it down into specific, actionable tasks.
    For each task, specify which agent should handle it:
    - planner: For architecture and planning decisions
    - researcher: For researching APIs, libraries, best practices
    - developer: For writing code
    - test_designer: For creating tests
    - debugger: For fixing issues
    - verifier: For reviewing and validating
    
    Return a JSON array of tasks with this format:
    [{"description": "Task description", "agent": "agent_type", "priority": 1}]
    
    Keep tasks focused and specific. A typical build might have 5-10 tasks."""
    
    prompt = f"""Project: {project['name']} ({project['language']})
    Description: {project.get('description', 'No description')}
    
    User Request: {data.request}
    
    Create a detailed build plan with specific tasks. Return ONLY valid JSON array."""
    
    try:
        result = await ai_service.generate(prompt, system_prompt, user_id=user["id"])
        
        # Parse tasks from AI response
        import json
        import re
        
        # Try to extract JSON from response
        content = result.get("content", "[]")
        json_match = re.search(r'\[[\s\S]*\]', content)
        if json_match:
            tasks = json.loads(json_match.group())
        else:
            # Fallback: create basic tasks
            tasks = [
                {"description": "Set up project structure", "agent": "developer", "priority": 1},
                {"description": f"Implement: {data.request[:100]}", "agent": "developer", "priority": 2},
                {"description": "Add error handling", "agent": "developer", "priority": 3},
                {"description": "Test the implementation", "agent": "test_designer", "priority": 4}
            ]
        
        return {"tasks": tasks, "message": "Build plan created"}
        
    except Exception as e:
        logger.error(f"AI Plan error: {e}")
        # Return fallback plan
        return {
            "tasks": [
                {"description": f"Analyze request: {data.request[:50]}...", "agent": "researcher", "priority": 1},
                {"description": "Create project files", "agent": "developer", "priority": 2},
                {"description": "Implement main functionality", "agent": "developer", "priority": 3},
                {"description": "Test and verify", "agent": "verifier", "priority": 4}
            ],
            "message": "Created fallback plan"
        }


@api_router.post("/ai/research")
async def research_request(data: ResearchRequest, user: dict = Depends(get_current_user)):
    """Research and refine the build plan"""
    project = await db.projects.find_one({"id": data.project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    # Use Researcher agent to refine tasks
    system_prompt = """You are a software research analyst. Review the build plan and add important details, considerations, and dependencies.
    For each task, add a 'details' field with implementation hints.
    
    Return the refined tasks as a JSON array with this format:
    [{"description": "Task description", "agent": "agent_type", "priority": 1, "details": "Implementation hints"}]"""
    
    tasks_str = "\n".join([f"- {t.get('description', '')}" for t in data.tasks])
    prompt = f"""Project: {project['name']} ({project['language']})
    
    Original Request: {data.request}
    
    Current Plan:
    {tasks_str}
    
    Refine this plan with implementation details. Return ONLY valid JSON array."""
    
    try:
        result = await ai_service.generate(prompt, system_prompt, user_id=user["id"])
        
        import json
        import re
        
        content = result.get("content", "[]")
        json_match = re.search(r'\[[\s\S]*\]', content)
        if json_match:
            refined_tasks = json.loads(json_match.group())
        else:
            # Add basic details to existing tasks
            refined_tasks = [
                {**t, "details": f"Implement using {project['language']} best practices"}
                for t in data.tasks
            ]
        
        return {"refined_tasks": refined_tasks, "message": "Plan refined"}
        
    except Exception as e:
        logger.error(f"AI Research error: {e}")
        return {
            "refined_tasks": [{**t, "details": "Ready to implement"} for t in data.tasks],
            "message": "Research complete (fallback)"
        }


@api_router.post("/ai/execute-task")
async def execute_task(data: ExecuteTaskRequest, user: dict = Depends(get_current_user)):
    """Execute a single task using the appropriate agent"""
    project = await db.projects.find_one({"id": data.project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    # Get existing files for context
    existing_files = await db.project_files.find({"project_id": data.project_id}, {"_id": 0}).to_list(50)
    files_context = "\n".join([f"- {f['path']}" for f in existing_files]) if existing_files else "No files yet"
    
    # Use appropriate agent
    system_prompt = f"""You are a {data.agent} agent. Complete the given task by generating code.
    
    When generating files, use this EXACT format:
    
    FILE: filename.ext
    ```
    code content here
    ```
    
    For example:
    FILE: src/index.js
    ```javascript
    console.log('Hello');
    ```
    
    Generate complete, working code. Include all necessary imports and exports."""
    
    prompt = f"""Project: {project['name']} ({project['language']})
    Existing Files: {files_context}
    
    Task: {data.task}
    
    Generate the code to complete this task. Use the FILE: format for each file you create."""
    
    try:
        result = await ai_service.generate(prompt, system_prompt, max_tokens=4000, user_id=user["id"])
        content = result.get("content", "")
        
        # Parse files from response
        files_created = []
        import re
        
        # Match FILE: path followed by code block
        file_pattern = r'FILE:\s*([^\n]+)\n```[\w]*\n([\s\S]*?)```'
        matches = re.findall(file_pattern, content)
        
        for path, code in matches:
            path = path.strip()
            code = code.strip()
            if path and code:
                files_created.append({"path": path, "content": code})
                
                # Save to database
                existing = await db.project_files.find_one({"project_id": data.project_id, "path": path})
                if existing:
                    await db.project_files.update_one(
                        {"project_id": data.project_id, "path": path},
                        {"$set": {"content": code, "updated_at": datetime.now(timezone.utc).isoformat()}}
                    )
                else:
                    await db.project_files.insert_one({
                        "id": str(uuid.uuid4()),
                        "project_id": data.project_id,
                        "path": path,
                        "content": code,
                        "created_at": datetime.now(timezone.utc).isoformat(),
                        "updated_at": datetime.now(timezone.utc).isoformat()
                    })
        
        # Clean content for display (remove file blocks)
        display_content = re.sub(file_pattern, '', content).strip()
        if not display_content:
            display_content = f"Created {len(files_created)} file(s)"
        
        return {
            "files": files_created,
            "message": display_content or f"Task completed: {data.task[:50]}..."
        }
        
    except Exception as e:
        logger.error(f"AI Execute task error: {e}")
        return {
            "files": [],
            "message": f"Error executing task: {str(e)}"
        }


# ==================== GLOBAL ASSISTANT ====================

@api_router.get("/conversations")
async def get_conversations(user: dict = Depends(get_current_user)):
    """Get all global assistant conversations"""
    pipeline = [
        {"$match": {"user_id": user["id"], "project_id": None, "deleted_by_user": {"$ne": True}}},
        {"$group": {
            "_id": "$conversation_id",
            "title": {"$first": "$conversation_title"},
            "last_message": {"$last": "$content"},
            "last_time": {"$max": "$timestamp"},
            "message_count": {"$sum": 1}
        }},
        {"$sort": {"last_time": -1}},
        {"$limit": 50}
    ]
    conversations = await db.chat_history.aggregate(pipeline).to_list(50)
    
    return [{
        "id": c["_id"],
        "title": c.get("title") or "New Conversation",
        "last_message": c["last_message"][:50] + "..." if len(c["last_message"]) > 50 else c["last_message"],
        "last_time": c["last_time"],
        "message_count": c["message_count"]
    } for c in conversations]


@api_router.post("/conversations")
async def create_conversation(data: ConversationCreate, user: dict = Depends(get_current_user)):
    """Create a new conversation"""
    conversation_id = str(uuid.uuid4())
    return {"id": conversation_id, "title": data.title or "New Conversation"}


@api_router.delete("/conversations/{conversation_id}")
async def delete_conversation(conversation_id: str, user: dict = Depends(get_current_user)):
    """Delete a conversation (soft delete)"""
    await db.chat_history.update_many(
        {"conversation_id": conversation_id, "user_id": user["id"]},
        {"$set": {"deleted_by_user": True}}
    )
    return {"message": "Conversation deleted"}


@api_router.get("/conversations/{conversation_id}/messages")
async def get_conversation_messages(conversation_id: str, user: dict = Depends(get_current_user)):
    """Get messages in a conversation"""
    messages = await db.chat_history.find(
        {"conversation_id": conversation_id, "user_id": user["id"], "deleted_by_user": {"$ne": True}},
        {"_id": 0}
    ).sort("timestamp", 1).to_list(500)
    return messages


@api_router.get("/assistant/chat")
async def get_global_chat_history(user: dict = Depends(get_current_user), conversation_id: str = None):
    """Get global assistant chat history"""
    query = {"user_id": user["id"], "project_id": None, "deleted_by_user": {"$ne": True}}
    if conversation_id:
        query["conversation_id"] = conversation_id
    
    messages = await db.chat_history.find(query, {"_id": 0}).sort("timestamp", 1).to_list(500)
    return messages


@api_router.post("/assistant/chat")
async def send_global_chat(data: GlobalChatRequest, user: dict = Depends(get_current_user)):
    """Send message to global assistant - uses credits"""
    should_charge = await credit_service.should_charge(user)
    estimated_tokens = len(data.message.split()) * 4 * 2
    estimated_credits = await credit_service.calculate_credits(estimated_tokens, is_project=False) if should_charge else 0
    
    if should_charge and user["credits"] < estimated_credits:
        raise HTTPException(status_code=402, detail="Insufficient credits")
    
    now = datetime.now(timezone.utc).isoformat()
    conversation_id = data.conversation_id or str(uuid.uuid4())
    
    # Generate response using developer agent
    agent = AGENT_REGISTRY["developer"](ai_service)
    result = await agent.execute(data.message)
    
    ai_content = result.content
    tokens_used = result.tokens_used
    credits_used = await credit_service.calculate_credits(tokens_used, is_project=False) if should_charge else 0
    
    # Save messages
    user_msg_data = {
        "id": str(uuid.uuid4()),
        "user_id": user["id"],
        "project_id": None,
        "conversation_id": conversation_id,
        "conversation_title": data.message[:50],
        "role": "user",
        "content": data.message,
        "agent_id": None,
        "provider": None,
        "model": None,
        "tokens_used": 0,
        "credits_deducted": 0,
        "timestamp": now
    }
    await db.chat_history.insert_one({**user_msg_data})
    
    ai_msg_data = {
        "id": str(uuid.uuid4()),
        "user_id": user["id"],
        "project_id": None,
        "conversation_id": conversation_id,
        "role": "assistant",
        "content": ai_content,
        "agent_id": "assistant",
        "provider": "local",
        "model": ai_service.default_model,
        "tokens_used": tokens_used,
        "credits_deducted": credits_used,
        "timestamp": datetime.now(timezone.utc).isoformat()
    }
    await db.chat_history.insert_one({**ai_msg_data})
    
    if credits_used > 0:
        await credit_service.deduct_credits(user["id"], credits_used, "Global assistant chat", "chat", ai_msg_data["id"])
    
    updated_user = await db.users.find_one({"id": user["id"]}, {"_id": 0, "credits": 1})
    
    return {
        "user_message": user_msg_data,
        "ai_message": ai_msg_data,
        "conversation_id": conversation_id,
        "credits_used": credits_used,
        "remaining_credits": updated_user.get("credits", 0)
    }


# ==================== AGENTS INFO ====================

@api_router.get("/agents")
async def get_agents():
    """Get available agents"""
    return AGENT_INFO


# ==================== AI PROVIDERS ====================

@api_router.get("/ai-providers")
async def get_ai_providers(user: dict = Depends(get_current_user)):
    """Get available AI providers based on user plan"""
    user_plan = user.get("plan", "free")
    plan_order = {"free": 0, "starter": 1, "pro": 2, "enterprise": 3}
    user_plan_level = plan_order.get(user_plan, 0)
    
    available = {}
    for provider_id, config in AI_PROVIDERS.items():
        required_plan = config.get("requires_plan", "free")
        required_level = plan_order.get(required_plan, 0)
        available[provider_id] = {
            "name": config["name"],
            "models": config["models"],
            "available": user_plan_level >= required_level,
            "requires_plan": required_plan if user_plan_level < required_level else None
        }
    
    return available


@api_router.get("/ai-providers/user")
async def get_user_ai_providers(user: dict = Depends(get_current_user)):
    """Get user's configured AI providers"""
    providers = await db.user_ai_providers.find(
        {"user_id": user["id"]},
        {"_id": 0, "api_key": 0}
    ).to_list(20)
    return providers


@api_router.post("/ai-providers/user")
async def add_user_ai_provider(data: AIProviderCreate, user: dict = Depends(get_current_user)):
    """Add/update user AI provider"""
    if data.provider not in AI_PROVIDERS:
        raise HTTPException(status_code=400, detail="Invalid provider")
    
    if data.is_default:
        await db.user_ai_providers.update_many({"user_id": user["id"]}, {"$set": {"is_default": False}})
    
    now = datetime.now(timezone.utc).isoformat()
    provider_data = {
        "id": str(uuid.uuid4()),
        "user_id": user["id"],
        "provider": data.provider,
        "api_key": data.api_key,
        "model_preference": data.model_preference,
        "is_active": True,
        "is_default": data.is_default,
        "created_at": now,
        "updated_at": now
    }
    
    await db.user_ai_providers.update_one(
        {"user_id": user["id"], "provider": data.provider},
        {"$set": provider_data},
        upsert=True
    )
    
    return {"message": "AI provider configured", "provider": data.provider}


@api_router.delete("/ai-providers/user/{provider}")
async def delete_user_ai_provider(provider: str, user: dict = Depends(get_current_user)):
    """Remove user AI provider"""
    result = await db.user_ai_providers.delete_one({"user_id": user["id"], "provider": provider})
    if result.deleted_count == 0:
        raise HTTPException(status_code=404, detail="Provider not found")
    return {"message": "Provider removed"}


# ==================== CREDITS ====================

@api_router.get("/credits/packages")
async def get_credit_packages():
    """Get available credit packages"""
    return CREDIT_PACKAGES


@api_router.get("/credits/balance")
async def get_credit_balance(user: dict = Depends(get_current_user)):
    """Get user credit balance"""
    uses_own_key = await credit_service.user_uses_own_key(user["id"])
    return {
        "credits": user["credits"],
        "credits_enabled": user.get("credits_enabled", True),
        "uses_own_key": uses_own_key,
        "free_usage": not user.get("credits_enabled", True) or uses_own_key
    }


@api_router.get("/credits/history")
async def get_credit_history(user: dict = Depends(get_current_user), limit: int = 50):
    """Get credit transaction history"""
    history = await db.credit_history.find(
        {"user_id": user["id"]},
        {"_id": 0}
    ).sort("created_at", -1).to_list(limit)
    return history


@api_router.post("/credits/purchase")
async def purchase_credits(data: CreditPurchaseRequest, user: dict = Depends(get_current_user), request: Request = None):
    """Initiate credit purchase via Stripe"""
    try:
        from emergentintegrations.payments.stripe.checkout import StripeCheckout, CheckoutSessionRequest
    except ImportError:
        raise HTTPException(status_code=503, detail="Stripe payments not available in local development mode. Please deploy to Emergent platform for payment features.")
    
    if data.package_id not in CREDIT_PACKAGES:
        raise HTTPException(status_code=400, detail="Invalid package")
    
    package = CREDIT_PACKAGES[data.package_id]
    success_url = f"{data.origin_url}/credits/success?session_id={{CHECKOUT_SESSION_ID}}"
    cancel_url = f"{data.origin_url}/credits"
    host_url = str(request.base_url) if request else data.origin_url
    webhook_url = f"{host_url}api/webhook/stripe"
    
    stripe_checkout = StripeCheckout(api_key=STRIPE_SECRET_KEY, webhook_url=webhook_url)
    checkout_request = CheckoutSessionRequest(
        amount=float(package["price"]),
        currency="usd",
        success_url=success_url,
        cancel_url=cancel_url,
        metadata={
            "user_id": user["id"],
            "package_id": data.package_id,
            "credits": str(package["credits"])
        }
    )
    
    session = await stripe_checkout.create_checkout_session(checkout_request)
    
    transaction = {
        "id": str(uuid.uuid4()),
        "user_id": user["id"],
        "session_id": session.session_id,
        "package_id": data.package_id,
        "amount": package["price"],
        "currency": "usd",
        "credits": package["credits"],
        "status": "pending",
        "payment_status": "initiated",
        "created_at": datetime.now(timezone.utc).isoformat()
    }
    await db.payment_transactions.insert_one({**transaction})
    
    return {"url": session.url, "session_id": session.session_id}


@api_router.get("/credits/status/{session_id}")
async def check_payment_status(session_id: str, user: dict = Depends(get_current_user), request: Request = None):
    """Check payment status"""
    try:
        from emergentintegrations.payments.stripe.checkout import StripeCheckout
    except ImportError:
        raise HTTPException(status_code=503, detail="Stripe payments not available in local development mode.")
    
    transaction = await db.payment_transactions.find_one(
        {"session_id": session_id, "user_id": user["id"]},
        {"_id": 0}
    )
    if not transaction:
        raise HTTPException(status_code=404, detail="Transaction not found")
    
    if transaction["payment_status"] == "paid":
        return {"status": "complete", "payment_status": "paid", "credits_added": transaction["credits"]}
    
    host_url = str(request.base_url) if request else "http://localhost:8001/"
    webhook_url = f"{host_url}api/webhook/stripe"
    stripe_checkout = StripeCheckout(api_key=STRIPE_SECRET_KEY, webhook_url=webhook_url)
    
    status = await stripe_checkout.get_checkout_status(session_id)
    
    if status.payment_status == "paid" and transaction["payment_status"] != "paid":
        await credit_service.add_credits(
            user["id"],
            transaction["credits"],
            f"Purchased {CREDIT_PACKAGES[transaction['package_id']]['name']}",
            "purchase",
            transaction["id"]
        )
        await db.payment_transactions.update_one(
            {"session_id": session_id},
            {"$set": {
                "status": "complete",
                "payment_status": "paid",
                "completed_at": datetime.now(timezone.utc).isoformat()
            }}
        )
        return {"status": "complete", "payment_status": "paid", "credits_added": transaction["credits"]}
    
    return {"status": status.status, "payment_status": status.payment_status}


@api_router.post("/webhook/stripe")
async def stripe_webhook(request: Request):
    """Handle Stripe webhooks"""
    try:
        from emergentintegrations.payments.stripe.checkout import StripeCheckout
    except ImportError:
        raise HTTPException(status_code=503, detail="Stripe payments not available in local development mode.")
    
    body = await request.body()
    signature = request.headers.get("Stripe-Signature")
    host_url = str(request.base_url)
    webhook_url = f"{host_url}api/webhook/stripe"
    
    stripe_checkout = StripeCheckout(api_key=STRIPE_SECRET_KEY, webhook_url=webhook_url)
    
    try:
        event = await stripe_checkout.handle_webhook(body, signature)
        if event.payment_status == "paid":
            transaction = await db.payment_transactions.find_one({"session_id": event.session_id})
            if transaction and transaction["payment_status"] != "paid":
                await credit_service.add_credits(
                    transaction["user_id"],
                    transaction["credits"],
                    f"Purchased credits",
                    "purchase",
                    transaction["id"]
                )
                await db.payment_transactions.update_one(
                    {"session_id": event.session_id},
                    {"$set": {
                        "status": "complete",
                        "payment_status": "paid",
                        "completed_at": datetime.now(timezone.utc).isoformat()
                    }}
                )
        return {"received": True}
    except Exception as e:
        logger.error(f"Webhook error: {e}")
        return {"received": True}


@api_router.post("/estimate-cost")
async def estimate_cost(data: CostEstimateRequest, user: dict = Depends(get_current_user)):
    """Estimate credit cost for a task"""
    uses_own_key = await credit_service.user_uses_own_key(user["id"])
    
    if uses_own_key:
        return {
            "estimated_credits": 0,
            "message": "Using your own API key - no credits will be charged",
            "requires_approval": False
        }
    
    if not user.get("credits_enabled", True):
        return {
            "estimated_credits": 0,
            "message": "Credits disabled for your account",
            "requires_approval": False
        }
    
    base_tokens = len(data.message.split()) * 4
    if data.multi_agent_mode:
        total_tokens = base_tokens * 7 * 2
    else:
        total_tokens = base_tokens * 2
    
    estimated_credits = await credit_service.calculate_credits(total_tokens, is_project=data.multi_agent_mode)
    estimated_credits = round(estimated_credits, 2)
    user_credits = user.get("credits", 0)
    
    return {
        "estimated_credits": estimated_credits,
        "user_credits": user_credits,
        "sufficient_credits": user_credits >= estimated_credits,
        "requires_approval": estimated_credits > 1,
        "message": f"This task will cost approximately {estimated_credits} credits"
    }


# ==================== BUILD/RUN ====================

@api_router.post("/projects/{project_id}/build")
async def build_project(project_id: str, user: dict = Depends(get_current_user)):
    """Build project - check syntax"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]}, {"_id": 0})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    files = await db.project_files.find({"project_id": project_id}, {"_id": 0}).to_list(1000)
    build_id = str(uuid.uuid4())
    now = datetime.now(timezone.utc).isoformat()
    
    build_logs = [
        {"level": "info", "source": "build", "message": f"Starting build for {project['name']}...", "timestamp": now},
        {"level": "info", "source": "build", "message": f"Language: {project['language']}", "timestamp": now},
        {"level": "info", "source": "build", "message": f"Files: {len(files)}", "timestamp": now}
    ]
    
    errors = []
    for f in files:
        if project["language"] == "Python" and f["path"].endswith(".py"):
            try:
                compile(f["content"], f["path"], "exec")
                build_logs.append({"level": "info", "source": "build", "message": f"✓ {f['path']} - syntax OK", "timestamp": now})
            except SyntaxError as e:
                errors.append(f"{f['path']}: {e}")
                build_logs.append({"level": "error", "source": "build", "message": f"✗ {f['path']}: {e}", "timestamp": now})
    
    status = "success" if not errors else "failed"
    build_logs.append({"level": "info" if not errors else "error", "source": "build", "message": f"Build {status}", "timestamp": now})
    
    await db.project_runs.insert_one({
        "id": build_id,
        "project_id": project_id,
        "run_type": "build",
        "status": status,
        "started_at": now,
        "ended_at": datetime.now(timezone.utc).isoformat(),
        "logs": build_logs,
        "errors": errors
    })
    
    return {"build_id": build_id, "status": status, "logs": build_logs, "errors": errors}


@api_router.post("/projects/{project_id}/run")
async def run_project(project_id: str, user: dict = Depends(get_current_user)):
    """Run project - execute main file"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]}, {"_id": 0})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    files = await db.project_files.find({"project_id": project_id}, {"_id": 0}).to_list(1000)
    run_id = str(uuid.uuid4())
    now = datetime.now(timezone.utc).isoformat()
    
    run_logs = [{"level": "info", "source": "runtime", "message": f"Executing {project['name']}...", "timestamp": now}]
    
    main_files = ["main.py", "index.js", "index.ts", "Main.java", "Program.cs", "main.go", "App.jsx"]
    main_file = next((f for f in files if f["path"] in main_files), None)
    
    output = ""
    if main_file and project["language"] == "Python":
        try:
            import io
            import sys
            from contextlib import redirect_stdout, redirect_stderr
            
            stdout_capture, stderr_capture = io.StringIO(), io.StringIO()
            namespace = {"__name__": "__main__"}
            
            with redirect_stdout(stdout_capture), redirect_stderr(stderr_capture):
                exec(main_file["content"], namespace)
            
            output = stdout_capture.getvalue()
            errors = stderr_capture.getvalue()
            
            if output:
                run_logs.append({"level": "info", "source": "runtime", "message": f"Output:\n{output}", "timestamp": now})
            if errors:
                run_logs.append({"level": "error", "source": "runtime", "message": f"Errors:\n{errors}", "timestamp": now})
            
            status = "success" if not errors else "failed"
        except Exception as e:
            run_logs.append({"level": "error", "source": "runtime", "message": f"Runtime error: {e}", "timestamp": now})
            status = "failed"
    else:
        run_logs.append({"level": "info", "source": "runtime", "message": f"Execution simulated for {project['language']}", "timestamp": now})
        status = "success"
    
    run_logs.append({"level": "info", "source": "runtime", "message": f"Run {status}", "timestamp": now})
    
    await db.project_runs.insert_one({
        "id": run_id,
        "project_id": project_id,
        "run_type": "run",
        "status": status,
        "started_at": now,
        "ended_at": datetime.now(timezone.utc).isoformat(),
        "output": output,
        "logs": run_logs
    })
    
    return {"run_id": run_id, "status": status, "logs": run_logs, "output": output}


@api_router.get("/projects/{project_id}/runs")
async def get_project_runs(project_id: str, user: dict = Depends(get_current_user)):
    """Get project run history"""
    project = await db.projects.find_one({"id": project_id, "user_id": user["id"]})
    if not project:
        raise HTTPException(status_code=404, detail="Project not found")
    
    runs = await db.project_runs.find({"project_id": project_id}, {"_id": 0}).sort("started_at", -1).to_list(50)
    return runs


# ==================== ADMIN ROUTES ====================

@api_router.get("/admin/users")
async def admin_get_users(admin: dict = Depends(get_admin_user)):
    """Get all users with comprehensive analytics"""
    users = await db.users.find({}, {"_id": 0, "password_hash": 0}).to_list(1000)
    
    # Enhance each user with analytics
    enhanced_users = []
    for user in users:
        user_id = user["id"]
        
        # Get conversation count
        conversations = await db.chat_history.count_documents({"user_id": user_id})
        global_conversations = await db.global_chat_history.count_documents({"user_id": user_id})
        
        # Get project count
        projects = await db.projects.count_documents({"user_id": user_id})
        
        # Get total credits used (from credit_transactions if exists)
        credits_used = 0
        transactions = await db.credit_transactions.find({"user_id": user_id, "type": "debit"}, {"_id": 0}).to_list(1000)
        for t in transactions:
            credits_used += abs(t.get("amount", 0))
        
        # Get jobs count
        jobs_count = await db.jobs.count_documents({"user_id": user_id})
        completed_jobs = await db.jobs.count_documents({"user_id": user_id, "status": "completed"})
        
        # Get IP records
        ip_records = await db.ip_records.find({"user_id": user_id}, {"_id": 0}).sort("timestamp", -1).to_list(10)
        last_ips = list(set([r.get("ip_address") for r in ip_records if r.get("ip_address")]))[:5]
        
        # Get session time (if tracked)
        session_logs = await db.session_logs.find({"user_id": user_id}, {"_id": 0}).to_list(100)
        total_session_time = sum([s.get("duration_minutes", 0) for s in session_logs])
        avg_session_time = total_session_time / len(session_logs) if session_logs else 0
        
        enhanced_users.append({
            **user,
            "analytics": {
                "total_conversations": conversations + global_conversations,
                "total_projects": projects,
                "total_jobs": jobs_count,
                "completed_jobs": completed_jobs,
                "credits_used": round(credits_used, 2),
                "ip_addresses": last_ips,
                "registration_ip": user.get("registration_ip", "Unknown"),
                "last_login_ip": user.get("last_login_ip", "Unknown"),
                "last_login_at": user.get("last_login_at"),
                "total_session_minutes": total_session_time,
                "avg_session_minutes": round(avg_session_time, 1)
            }
        })
    
    return enhanced_users


@api_router.get("/admin/users/{user_id}/details")
async def admin_get_user_details(user_id: str, admin: dict = Depends(get_admin_user)):
    """Get detailed user information"""
    user = await db.users.find_one({"id": user_id}, {"_id": 0, "password_hash": 0})
    if not user:
        raise HTTPException(status_code=404, detail="User not found")
    
    # Get all IP records
    ip_records = await db.ip_records.find({"user_id": user_id}, {"_id": 0}).sort("timestamp", -1).to_list(100)
    
    # Get credit transactions
    transactions = await db.credit_transactions.find({"user_id": user_id}, {"_id": 0}).sort("timestamp", -1).to_list(50)
    
    # Get projects
    projects = await db.projects.find({"user_id": user_id}, {"_id": 0}).to_list(100)
    
    # Get jobs history
    jobs = await db.jobs.find({"user_id": user_id}, {"_id": 0}).sort("created_at", -1).to_list(50)
    
    return {
        "user": user,
        "ip_history": ip_records,
        "credit_transactions": transactions,
        "projects": projects,
        "jobs": jobs
    }


@api_router.put("/admin/users/{user_id}")
async def admin_update_user(user_id: str, data: AdminUserUpdate, admin: dict = Depends(get_admin_user)):
    """Update user (role, credits, plan)"""
    update_data = {k: v for k, v in data.model_dump().items() if v is not None}
    if not update_data:
        raise HTTPException(status_code=400, detail="No update data provided")
    
    result = await db.users.update_one({"id": user_id}, {"$set": update_data})
    if result.matched_count == 0:
        raise HTTPException(status_code=404, detail="User not found")
    
    return {"message": "User updated"}


@api_router.delete("/admin/users/{user_id}")
async def admin_delete_user(user_id: str, admin: dict = Depends(get_admin_user)):
    """Delete user"""
    if user_id == admin["id"]:
        raise HTTPException(status_code=400, detail="Cannot delete yourself")
    
    result = await db.users.delete_one({"id": user_id})
    if result.deleted_count == 0:
        raise HTTPException(status_code=404, detail="User not found")
    
    # Clean up user data
    projects = await db.projects.find({"user_id": user_id}).to_list(1000)
    for p in projects:
        await db.project_files.delete_many({"project_id": p["id"]})
        await db.chat_history.delete_many({"project_id": p["id"]})
    await db.projects.delete_many({"user_id": user_id})
    await db.user_ai_providers.delete_many({"user_id": user_id})
    await db.jobs.delete_many({"user_id": user_id})
    
    return {"message": "User deleted"}


@api_router.post("/admin/users/bulk-credits")
async def admin_bulk_add_credits(data: AdminBulkCreditsRequest, admin: dict = Depends(get_admin_user)):
    """Add credits to multiple users"""
    if data.user_ids:
        result = await db.users.update_many({"id": {"$in": data.user_ids}}, {"$inc": {"credits": data.amount}})
    else:
        result = await db.users.update_many({}, {"$inc": {"credits": data.amount}})
    
    return {"message": f"Added {data.amount} credits to {result.modified_count} users"}


@api_router.get("/admin/stats")
async def admin_get_stats(admin: dict = Depends(get_admin_user)):
    """Get system statistics"""
    knowledge_hits = await db.chat_history.count_documents({})
    return {
        "total_users": await db.users.count_documents({}),
        "total_projects": await db.projects.count_documents({}),
        "total_jobs": await db.jobs.count_documents({}),
        "active_jobs": await db.jobs.count_documents({"status": {"$in": ["analyzing", "approved", "in_progress"]}}),
        "total_transactions": await db.payment_transactions.count_documents({}),
        "successful_payments": await db.payment_transactions.count_documents({"payment_status": "paid"}),
        "knowledge_hits": knowledge_hits
    }


@api_router.get("/admin/knowledge-base")
async def admin_get_knowledge_base(admin: dict = Depends(get_admin_user), limit: int = 100):
    """Get knowledge base entries (chat history for training/reference)"""
    # Get recent unique conversations with their messages
    pipeline = [
        {"$match": {"role": "assistant"}},
        {"$sort": {"timestamp": -1}},
        {"$limit": limit},
        {"$lookup": {
            "from": "users",
            "localField": "user_id",
            "foreignField": "id",
            "as": "user"
        }},
        {"$unwind": {"path": "$user", "preserveNullAndEmptyArrays": True}},
        {"$project": {
            "_id": 0,
            "id": 1,
            "conversation_id": 1,
            "content": 1,
            "tokens_used": 1,
            "timestamp": 1,
            "project_id": 1,
            "agent_id": 1,
            "user_name": "$user.name",
            "user_email": "$user.email",
            "model": 1
        }}
    ]
    
    entries = await db.chat_history.aggregate(pipeline).to_list(limit)
    
    # Add question context for each answer
    for entry in entries:
        if entry.get("conversation_id"):
            # Find the user message before this assistant message
            user_msg = await db.chat_history.find_one(
                {
                    "conversation_id": entry["conversation_id"],
                    "role": "user",
                    "timestamp": {"$lt": entry["timestamp"]}
                },
                {"_id": 0, "content": 1},
                sort=[("timestamp", -1)]
            )
            if user_msg:
                entry["question"] = user_msg.get("content", "")[:200]
    
    return entries


@api_router.delete("/admin/knowledge-base/{entry_id}")
async def admin_delete_knowledge_entry(entry_id: str, admin: dict = Depends(get_admin_user)):
    """Delete a knowledge base entry"""
    result = await db.chat_history.delete_one({"id": entry_id})
    if result.deleted_count == 0:
        raise HTTPException(status_code=404, detail="Entry not found")
    return {"message": "Entry deleted"}


@api_router.post("/admin/knowledge-base/{entry_id}/invalidate")
async def admin_invalidate_knowledge(entry_id: str, admin: dict = Depends(get_admin_user)):
    """Mark a knowledge entry as invalid/incorrect for training"""
    result = await db.chat_history.update_one(
        {"id": entry_id},
        {"$set": {"is_valid": False, "invalidated_at": datetime.now(timezone.utc).isoformat()}}
    )
    if result.matched_count == 0:
        raise HTTPException(status_code=404, detail="Entry not found")
    return {"message": "Entry marked as invalid"}


@api_router.get("/admin/running-jobs")
async def admin_get_running_jobs(admin: dict = Depends(get_admin_user)):
    """Get currently running jobs"""
    jobs = await db.jobs.find(
        {"status": {"$in": ["analyzing", "approved", "in_progress", "needs_more_credits"]}},
        {"_id": 0}
    ).to_list(100)
    
    for job in jobs:
        user = await db.users.find_one({"id": job["user_id"]}, {"_id": 0, "name": 1, "email": 1})
        if user:
            job["user_name"] = user.get("name")
            job["user_email"] = user.get("email")
        project = await db.projects.find_one({"id": job["project_id"]}, {"_id": 0, "name": 1})
        if project:
            job["project_name"] = project.get("name")
    
    return jobs


@api_router.get("/admin/system-health")
async def admin_get_system_health(admin: dict = Depends(get_admin_user)):
    """Get system health status"""
    health = {
        "database": "healthy",
        "local_llm": "unknown",
        "stripe": "configured" if STRIPE_SECRET_KEY else "not_configured"
    }
    
    # Check database
    try:
        await db.command("ping")
    except:
        health["database"] = "unhealthy"
    
    # Check local LLM
    import httpx
    try:
        async with httpx.AsyncClient(timeout=5.0) as client:
            response = await client.get(f"{ai_service.local_llm_url}/api/tags")
            if response.status_code == 200:
                health["local_llm"] = "connected"
                health["available_models"] = response.json().get("models", [])
            else:
                health["local_llm"] = "error"
    except:
        health["local_llm"] = "not_connected"
    
    return health


@api_router.get("/admin/settings")
async def admin_get_settings(admin: dict = Depends(get_admin_user)):
    """Get all system settings"""
    return await credit_service.get_settings()


@api_router.put("/admin/settings/{key}")
async def admin_update_setting(key: str, value: str, admin: dict = Depends(get_admin_user)):
    """Update a system setting"""
    existing = await db.system_settings.find_one({"setting_key": key})
    if not existing:
        raise HTTPException(status_code=404, detail="Setting not found")
    
    await db.system_settings.update_one(
        {"setting_key": key},
        {"$set": {"setting_value": value, "updated_at": datetime.now(timezone.utc).isoformat()}}
    )
    return {"message": "Setting updated"}


@api_router.put("/admin/credit-config")
async def admin_update_credit_config(chat_rate: float = None, project_rate: float = None, admin: dict = Depends(get_admin_user)):
    """Update credit rates"""
    if chat_rate is not None:
        await db.system_settings.update_one(
            {"setting_key": "credits_per_1k_tokens_chat"},
            {"$set": {"setting_value": str(chat_rate)}}
        )
    if project_rate is not None:
        await db.system_settings.update_one(
            {"setting_key": "credits_per_1k_tokens_project"},
            {"$set": {"setting_value": str(project_rate)}}
        )
    
    return {"message": "Credit config updated"}


# ==================== SUBSCRIPTION PLANS ====================

@api_router.get("/plans")
async def get_plans():
    """Get all active subscription plans"""
    plans = await db.subscription_plans.find({"is_active": True}, {"_id": 0}).to_list(100)
    return plans


@api_router.get("/plans/all")
async def get_all_plans(admin: dict = Depends(get_admin_user)):
    """Get all plans including inactive (admin only)"""
    plans = await db.subscription_plans.find({}, {"_id": 0}).to_list(100)
    return plans


@api_router.post("/admin/plans")
async def create_plan(data: PlanCreate, admin: dict = Depends(get_admin_user)):
    """Create a new subscription plan"""
    existing = await db.subscription_plans.find_one({"plan_id": data.plan_id})
    if existing:
        raise HTTPException(status_code=400, detail="Plan ID already exists")
    
    plan_data = {
        "plan_id": data.plan_id,
        "name": data.name,
        "price_monthly": data.price_monthly,
        "daily_credits": data.daily_credits,
        "features": data.features,
        "max_projects": data.max_projects,
        "max_concurrent_workspaces": data.max_concurrent_workspaces,
        "allows_own_api_keys": data.allows_own_api_keys,
        "api_key_required": data.api_key_required,
        "is_active": True,
        "created_at": datetime.now(timezone.utc).isoformat()
    }
    await db.subscription_plans.insert_one({**plan_data})
    return {"message": "Plan created", "plan": plan_data}


@api_router.put("/admin/plans/{plan_id}")
async def update_plan(plan_id: str, data: PlanUpdate, admin: dict = Depends(get_admin_user)):
    """Update a subscription plan"""
    update_data = {k: v for k, v in data.model_dump().items() if v is not None}
    if not update_data:
        raise HTTPException(status_code=400, detail="No update data provided")
    
    update_data["updated_at"] = datetime.now(timezone.utc).isoformat()
    
    result = await db.subscription_plans.update_one({"plan_id": plan_id}, {"$set": update_data})
    if result.matched_count == 0:
        raise HTTPException(status_code=404, detail="Plan not found")
    
    return {"message": "Plan updated"}


@api_router.delete("/admin/plans/{plan_id}")
async def delete_plan(plan_id: str, admin: dict = Depends(get_admin_user)):
    """Delete (deactivate) a subscription plan"""
    if plan_id in ["free", "starter", "pro", "enterprise"]:
        raise HTTPException(status_code=400, detail="Cannot delete default plans")
    
    result = await db.subscription_plans.update_one(
        {"plan_id": plan_id},
        {"$set": {"is_active": False, "updated_at": datetime.now(timezone.utc).isoformat()}}
    )
    if result.matched_count == 0:
        raise HTTPException(status_code=404, detail="Plan not found")
    
    return {"message": "Plan deactivated"}


@api_router.post("/admin/distribute-daily-credits")
async def distribute_daily_credits(admin: dict = Depends(get_admin_user)):
    """Distribute daily credits to all eligible users based on their subscription start date"""
    now = datetime.now(timezone.utc)
    
    # Get all plans
    plans = await db.subscription_plans.find({"is_active": True}, {"_id": 0}).to_list(100)
    plan_map = {p["plan_id"]: p for p in plans}
    
    # Get all users
    users = await db.users.find(
        {},
        {"_id": 0, "id": 1, "plan": 1, "credits": 1, "subscription_start_date": 1, "last_daily_credit": 1}
    ).to_list(10000)
    
    distributed_count = 0
    for user in users:
        plan = plan_map.get(user.get("plan", "free"), plan_map.get("free"))
        if not plan or plan.get("daily_credits", 0) <= 0:
            continue
            
        # Calculate days since subscription started
        sub_start = user.get("subscription_start_date")
        if not sub_start:
            # For users without subscription start date, use created_at or now
            sub_start = user.get("created_at", now.isoformat())
        
        try:
            if isinstance(sub_start, str):
                sub_start_dt = datetime.fromisoformat(sub_start.replace('Z', '+00:00'))
            else:
                sub_start_dt = sub_start
        except:
            sub_start_dt = now
        
        # Check if user is eligible for daily credits (based on subscription anniversary)
        days_since_sub = (now - sub_start_dt).days
        last_credit_day = user.get("last_daily_credit_day", -1)
        
        # User gets credits every day based on subscription start, not calendar day
        if days_since_sub > last_credit_day:
            await db.users.update_one(
                {"id": user["id"]},
                {
                    "$inc": {"credits": plan["daily_credits"]},
                    "$set": {"last_daily_credit_day": days_since_sub}
                }
            )
            distributed_count += 1
    
    return {"message": f"Distributed daily credits to {distributed_count} users", "timestamp": now.isoformat()}


# ==================== USER SUBSCRIPTION ====================

@api_router.get("/user/subscription")
async def get_user_subscription(user: dict = Depends(get_current_user)):
    """Get user's current subscription details"""
    plan = await db.subscription_plans.find_one({"plan_id": user.get("plan", "free")}, {"_id": 0})
    subscription = await db.user_subscriptions.find_one({"user_id": user["id"], "status": "active"}, {"_id": 0})
    
    # Count active workspaces (jobs in progress)
    active_workspaces = await db.jobs.count_documents({
        "user_id": user["id"],
        "status": {"$in": ["analyzing", "approved", "in_progress", "running"]}
    })
    
    max_workspaces = plan.get("max_concurrent_workspaces", 1) if plan else 1
    
    return {
        "plan": plan,
        "subscription": subscription,
        "credits": user.get("credits", 0),
        "credits_enabled": user.get("credits_enabled", True),
        "subscription_start_date": user.get("subscription_start_date"),
        "active_workspaces": active_workspaces,
        "max_concurrent_workspaces": max_workspaces,
        "can_start_workspace": active_workspaces < max_workspaces
    }


@api_router.get("/user/workspace-limit")
async def check_workspace_limit(user: dict = Depends(get_current_user)):
    """Check if user can start a new workspace based on their plan"""
    plan = await db.subscription_plans.find_one({"plan_id": user.get("plan", "free")}, {"_id": 0})
    max_workspaces = plan.get("max_concurrent_workspaces", 1) if plan else 1
    
    active_workspaces = await db.jobs.count_documents({
        "user_id": user["id"],
        "status": {"$in": ["analyzing", "approved", "in_progress", "running"]}
    })
    
    return {
        "active_workspaces": active_workspaces,
        "max_concurrent_workspaces": max_workspaces,
        "can_start_workspace": active_workspaces < max_workspaces,
        "message": f"You have {active_workspaces} of {max_workspaces} workspaces running" if active_workspaces < max_workspaces 
                  else f"Workspace limit reached ({max_workspaces}). Please wait for a job to complete or upgrade your plan."
    }


@api_router.post("/user/subscribe")
async def subscribe_to_plan(data: SubscriptionCreate, user: dict = Depends(get_current_user), request: Request = None):
    """Subscribe to a plan via Stripe"""
    from emergentintegrations.payments.stripe.checkout import StripeCheckout, CheckoutSessionRequest
    
    plan = await db.subscription_plans.find_one({"plan_id": data.plan_id, "is_active": True}, {"_id": 0})
    if not plan:
        raise HTTPException(status_code=404, detail="Plan not found")
    
    now = datetime.now(timezone.utc).isoformat()
    
    if plan["price_monthly"] == 0:
        # Free plan - just update user
        await db.users.update_one(
            {"id": user["id"]}, 
            {"$set": {
                "plan": data.plan_id,
                "subscription_start_date": now,
                "last_daily_credit_day": 0
            }}
        )
        return {"message": "Subscribed to free plan", "plan": plan}
    
    success_url = f"{data.origin_url}/settings?subscription=success"
    cancel_url = f"{data.origin_url}/settings?subscription=cancelled"
    host_url = str(request.base_url) if request else data.origin_url
    webhook_url = f"{host_url}api/webhook/stripe"
    
    stripe_checkout = StripeCheckout(api_key=STRIPE_SECRET_KEY, webhook_url=webhook_url)
    checkout_request = CheckoutSessionRequest(
        amount=float(plan["price_monthly"]),
        currency="usd",
        success_url=success_url,
        cancel_url=cancel_url,
        metadata={
            "user_id": user["id"],
            "plan_id": data.plan_id,
            "type": "subscription"
        }
    )
    
    session = await stripe_checkout.create_checkout_session(checkout_request)
    
    # Store pending subscription
    await db.user_subscriptions.insert_one({
        "id": str(uuid.uuid4()),
        "user_id": user["id"],
        "plan_id": data.plan_id,
        "session_id": session.session_id,
        "status": "pending",
        "price": plan["price_monthly"],
        "created_at": datetime.now(timezone.utc).isoformat()
    })
    
    return {"url": session.url, "session_id": session.session_id}


# ==================== USER API KEYS ====================

@api_router.get("/user/api-keys")
async def get_user_api_keys(user: dict = Depends(get_current_user)):
    """Get user's configured API keys (masked)"""
    keys = await db.user_ai_providers.find({"user_id": user["id"]}, {"_id": 0}).to_list(20)
    
    # Mask API keys
    for key in keys:
        if key.get("api_key"):
            key["api_key_masked"] = key["api_key"][:8] + "..." + key["api_key"][-4:] if len(key["api_key"]) > 12 else "****"
            del key["api_key"]
    
    return keys


@api_router.post("/user/api-keys")
async def add_user_api_key(data: UserAPIKeyInput, user: dict = Depends(get_current_user)):
    """Add or update an API key"""
    # Check if user's plan allows API keys
    plan = await db.subscription_plans.find_one({"plan_id": user.get("plan", "free")})
    if plan and not plan.get("allows_own_api_keys", False):
        raise HTTPException(status_code=403, detail="Your plan does not allow custom API keys. Please upgrade.")
    
    if data.provider not in AI_PROVIDERS:
        raise HTTPException(status_code=400, detail="Invalid provider")
    
    now = datetime.now(timezone.utc).isoformat()
    
    existing = await db.user_ai_providers.find_one({"user_id": user["id"], "provider": data.provider})
    
    if existing:
        await db.user_ai_providers.update_one(
            {"user_id": user["id"], "provider": data.provider},
            {"$set": {
                "api_key": data.api_key,
                "model_preference": data.model_preference,
                "is_active": True,
                "updated_at": now
            }}
        )
    else:
        await db.user_ai_providers.insert_one({
            "id": str(uuid.uuid4()),
            "user_id": user["id"],
            "provider": data.provider,
            "api_key": data.api_key,
            "model_preference": data.model_preference,
            "is_active": True,
            "is_default": False,
            "created_at": now,
            "updated_at": now
        })
    
    return {"message": f"{data.provider} API key saved"}


@api_router.delete("/user/api-keys/{provider}")
async def delete_user_api_key(provider: str, user: dict = Depends(get_current_user)):
    """Delete an API key"""
    result = await db.user_ai_providers.delete_one({"user_id": user["id"], "provider": provider})
    if result.deleted_count == 0:
        raise HTTPException(status_code=404, detail="API key not found")
    return {"message": f"{provider} API key removed"}


@api_router.put("/user/api-keys/{provider}/default")
async def set_default_api_key(provider: str, user: dict = Depends(get_current_user)):
    """Set an API key as default"""
    key = await db.user_ai_providers.find_one({"user_id": user["id"], "provider": provider})
    if not key:
        raise HTTPException(status_code=404, detail="API key not found")
    
    # Unset all defaults
    await db.user_ai_providers.update_many({"user_id": user["id"]}, {"$set": {"is_default": False}})
    # Set this one as default
    await db.user_ai_providers.update_one({"user_id": user["id"], "provider": provider}, {"$set": {"is_default": True}})
    
    return {"message": f"{provider} set as default"}


@api_router.get("/languages")
async def get_supported_languages():
    """Get supported programming languages"""
    return SUPPORTED_LANGUAGES


# ==================== SETUP CORS AND ROUTER ====================

app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

app.include_router(api_router)


if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8001)
