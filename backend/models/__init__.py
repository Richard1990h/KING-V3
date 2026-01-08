"""Pydantic Models for LittleHelper AI"""
from pydantic import BaseModel, Field, EmailStr
from typing import List, Optional, Dict, Any
from datetime import datetime
from enum import Enum


class UserRole(str, Enum):
    USER = "user"
    ADMIN = "admin"


class Plan(str, Enum):
    FREE = "free"
    STARTER = "starter"
    PRO = "pro"
    ENTERPRISE = "enterprise"


class JobStatus(str, Enum):
    PENDING = "pending"
    ANALYZING = "analyzing"
    AWAITING_APPROVAL = "awaiting_approval"
    APPROVED = "approved"
    IN_PROGRESS = "in_progress"
    RUNNING = "running"
    COMPLETED = "completed"
    FAILED = "failed"
    CANCELLED = "cancelled"
    NEEDS_MORE_CREDITS = "needs_more_credits"


class AgentType(str, Enum):
    PLANNER = "planner"
    RESEARCHER = "researcher"
    DEVELOPER = "developer"
    TEST_DESIGNER = "test_designer"
    EXECUTOR = "executor"
    DEBUGGER = "debugger"
    VERIFIER = "verifier"
    ERROR_ANALYZER = "error_analyzer"


# ==================== AUTH MODELS ====================
class UserCreate(BaseModel):
    email: EmailStr
    password: str
    name: str
    tos_accepted: bool = False  # Terms of Service acceptance


class TOSAcceptance(BaseModel):
    """Terms of Service acceptance model"""
    accepted: bool = True


class UserLogin(BaseModel):
    email: EmailStr
    password: str


class UserResponse(BaseModel):
    id: str
    email: str
    name: str
    role: str
    credits: float
    credits_enabled: bool = True
    plan: str = "free"
    created_at: str
    language: str = "en"
    tos_accepted: bool = True


class TokenResponse(BaseModel):
    token: str
    user: UserResponse


# ==================== PROJECT MODELS ====================
class ProjectCreate(BaseModel):
    name: str
    description: Optional[str] = ""
    language: str = "Python"


class ProjectUpdate(BaseModel):
    name: Optional[str] = None
    description: Optional[str] = None


class ProjectResponse(BaseModel):
    id: str
    user_id: str
    name: str
    description: str
    language: str
    created_at: str
    updated_at: str
    status: str


class FileCreate(BaseModel):
    path: str
    content: str = ""


class FileUpdate(BaseModel):
    content: str


class FileResponse(BaseModel):
    id: str
    project_id: str
    path: str
    content: str
    updated_at: str


# ==================== TASK & JOB MODELS ====================
class TaskItem(BaseModel):
    """Individual task in a job list"""
    id: str
    title: str
    description: str
    agent_type: str
    order: int
    status: str = "pending"
    estimated_tokens: int = 0
    estimated_credits: float = 0
    actual_tokens: int = 0
    actual_credits: float = 0
    output: Optional[str] = None
    files_created: List[str] = []
    error: Optional[str] = None


class JobCreate(BaseModel):
    """Create a new job from user prompt"""
    project_id: str
    prompt: str
    multi_agent_mode: bool = True


class JobApproval(BaseModel):
    """User approval for job execution"""
    job_id: str
    approved: bool
    modified_tasks: Optional[List[TaskItem]] = None


class ContinueJobRequest(BaseModel):
    """Request to continue job with more credits"""
    job_id: str
    approved: bool


class JobResponse(BaseModel):
    id: str
    project_id: str
    user_id: str
    prompt: str
    status: str
    tasks: List[TaskItem]
    total_estimated_credits: float
    credits_used: float
    credits_remaining: float
    current_task_index: int
    created_at: str
    updated_at: str


# ==================== CHAT MODELS ====================
class ChatRequest(BaseModel):
    project_id: Optional[str] = None
    message: str
    agents_enabled: List[str] = []
    conversation_id: Optional[str] = None
    multi_agent_mode: bool = False


class GlobalChatRequest(BaseModel):
    message: str
    conversation_id: Optional[str] = None


class ConversationCreate(BaseModel):
    title: Optional[str] = None


# ==================== CREDIT MODELS ====================
class CreditPurchaseRequest(BaseModel):
    package_id: str
    origin_url: str


class CostEstimateRequest(BaseModel):
    message: str
    multi_agent_mode: bool = False


class CostEstimateResponse(BaseModel):
    estimated_credits: float
    user_credits: float
    sufficient_credits: bool
    requires_approval: bool
    breakdown: List[Dict[str, Any]] = []
    message: str


# ==================== ADMIN MODELS ====================
class AdminUserUpdate(BaseModel):
    role: Optional[str] = None
    credits: Optional[float] = None
    credits_enabled: Optional[bool] = None
    plan: Optional[str] = None


class AdminBulkCreditsRequest(BaseModel):
    amount: float
    user_ids: Optional[List[str]] = None


# ==================== TODO MODELS ====================
class TodoCreate(BaseModel):
    project_id: str
    text: str
    priority: str = "medium"


class TodoUpdate(BaseModel):
    text: Optional[str] = None
    completed: Optional[bool] = None
    priority: Optional[str] = None


# ==================== AI PROVIDER MODELS ====================
class AIProviderCreate(BaseModel):
    provider: str
    api_key: str
    model_preference: Optional[str] = None
    is_default: bool = False


class LanguageUpdate(BaseModel):
    language: str


# ==================== PROJECT UPLOAD MODELS ====================
class ProjectScanRequest(BaseModel):
    project_id: str
    scan_for_issues: bool = True


class ProjectScanResponse(BaseModel):
    detected_language: str
    file_count: int
    estimated_scan_credits: float
    issues_found: List[Dict[str, Any]] = []
    recommendations: List[str] = []



# ==================== SUBSCRIPTION PLAN MODELS ====================
class PlanCreate(BaseModel):
    plan_id: str
    name: str
    price_monthly: float
    daily_credits: int
    features: List[str] = []
    max_projects: int = -1
    max_concurrent_workspaces: int = 1
    allows_own_api_keys: bool = False
    api_key_required: Optional[str] = None


class PlanUpdate(BaseModel):
    name: Optional[str] = None
    price_monthly: Optional[float] = None
    daily_credits: Optional[int] = None
    features: Optional[List[str]] = None
    max_projects: Optional[int] = None
    max_concurrent_workspaces: Optional[int] = None
    allows_own_api_keys: Optional[bool] = None
    api_key_required: Optional[str] = None
    is_active: Optional[bool] = None


class SubscriptionCreate(BaseModel):
    plan_id: str
    origin_url: str


class UserAPIKeyInput(BaseModel):
    provider: str
    api_key: str
    model_preference: Optional[str] = None


# ==================== USER PROFILE & THEME MODELS ====================
class ThemeSettings(BaseModel):
    primary_color: str = "#d946ef"  # fuchsia-500
    secondary_color: str = "#06b6d4"  # cyan-500
    background_color: str = "#030712"  # gray-950
    card_color: str = "#0B0F19"
    text_color: str = "#ffffff"
    hover_color: str = "#a855f7"  # purple-500
    credits_color: str = "#d946ef"  # fuchsia-500
    background_image: Optional[str] = None


class UserProfileUpdate(BaseModel):
    name: Optional[str] = None
    display_name: Optional[str] = None
    avatar_url: Optional[str] = None
    theme: Optional[ThemeSettings] = None


class PasswordChange(BaseModel):
    current_password: str
    new_password: str


class AvatarUpload(BaseModel):
    image_data: str  # base64 encoded


# ==================== CREDIT PACKAGE MODELS ====================
class CreditPackageCreate(BaseModel):
    package_id: str
    name: str
    credits: int
    price: float
    is_addon: bool = True  # True for one-time purchases, False for subscription


class CreditPackagePurchase(BaseModel):
    package_id: str
    origin_url: str


# ==================== IP TRACKING MODELS ====================
class IPRecord(BaseModel):
    ip_address: str
    user_id: Optional[str] = None
    action: str  # register, login, etc.
    timestamp: str
    user_agent: Optional[str] = None

