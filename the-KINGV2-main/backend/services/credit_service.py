"""Credit Service - Manages credit calculations and deductions"""
from typing import Dict, Any, Optional, List
from datetime import datetime, timezone
import uuid
import logging

logger = logging.getLogger(__name__)


class CreditService:
    """Service for managing user credits"""
    
    def __init__(self, db):
        self.db = db
    
    async def get_settings(self) -> Dict[str, Any]:
        """Get system credit settings"""
        settings = {}
        cursor = self.db.system_settings.find({}, {"_id": 0})
        async for setting in cursor:
            key = setting.get("setting_key")
            value = setting.get("setting_value")
            setting_type = setting.get("setting_type", "string")
            
            if setting_type == "number":
                settings[key] = float(value)
            elif setting_type == "boolean":
                settings[key] = value.lower() == "true"
            else:
                settings[key] = value
        
        # Defaults
        defaults = {
            "credits_per_1k_tokens_chat": 0.5,
            "credits_per_1k_tokens_project": 1.0
        }
        for key, value in defaults.items():
            if key not in settings:
                settings[key] = value
        
        return settings
    
    async def calculate_credits(self, tokens: int, is_project: bool = False) -> float:
        """Calculate credits for token usage"""
        settings = await self.get_settings()
        rate_key = "credits_per_1k_tokens_project" if is_project else "credits_per_1k_tokens_chat"
        rate = settings.get(rate_key, 1.0 if is_project else 0.5)
        return round((tokens / 1000) * rate, 4)
    
    async def estimate_job_cost(self, tasks: List[Dict], user: Dict) -> Dict[str, Any]:
        """Estimate total cost for a job's tasks"""
        # Check if user uses their own key
        uses_own_key = await self.user_uses_own_key(user["id"])
        if uses_own_key:
            return {
                "total_estimated_credits": 0,
                "breakdown": [],
                "free_usage": True,
                "message": "Using your own API key - no credits charged"
            }
        
        if not user.get("credits_enabled", True):
            return {
                "total_estimated_credits": 0,
                "breakdown": [],
                "free_usage": True,
                "message": "Credits disabled for your account"
            }
        
        settings = await self.get_settings()
        rate = settings.get("credits_per_1k_tokens_project", 1.0)
        
        breakdown = []
        total_tokens = 0
        total_credits = 0
        
        for task in tasks:
            tokens = task.get("estimated_tokens", 500)
            credits = round((tokens / 1000) * rate, 4)
            total_tokens += tokens
            total_credits += credits
            task["estimated_credits"] = credits
            
            breakdown.append({
                "task_id": task.get("id"),
                "title": task.get("title"),
                "agent": task.get("agent_type"),
                "estimated_tokens": tokens,
                "estimated_credits": credits
            })
        
        return {
            "total_estimated_credits": round(total_credits, 2),
            "total_estimated_tokens": total_tokens,
            "breakdown": breakdown,
            "user_credits": user.get("credits", 0),
            "sufficient_credits": user.get("credits", 0) >= total_credits,
            "free_usage": False
        }
    
    async def user_uses_own_key(self, user_id: str) -> bool:
        """Check if user has their own API key configured"""
        provider = await self.db.user_ai_providers.find_one(
            {"user_id": user_id, "is_active": True, "is_default": True},
            {"_id": 0}
        )
        return provider is not None and provider.get("api_key")
    
    async def should_charge(self, user: Dict) -> bool:
        """Determine if credits should be charged for this user"""
        if not user.get("credits_enabled", True):
            return False
        if await self.user_uses_own_key(user["id"]):
            return False
        return True
    
    async def deduct_credits(self, user_id: str, amount: float, reason: str,
                            ref_type: str = None, ref_id: str = None) -> Dict[str, Any]:
        """Deduct credits from user account"""
        user = await self.db.users.find_one({"id": user_id}, {"_id": 0})
        if not user:
            return {"success": False, "error": "User not found"}
        
        # Check if should charge
        if not user.get("credits_enabled", True):
            return {"success": True, "charged": 0, "balance": user["credits"]}
        
        if await self.user_uses_own_key(user_id):
            return {"success": True, "charged": 0, "balance": user["credits"]}
        
        # Check sufficient balance
        if user.get("credits", 0) < amount:
            return {
                "success": False,
                "error": "Insufficient credits",
                "required": amount,
                "available": user.get("credits", 0)
            }
        
        # Deduct
        new_balance = user["credits"] - amount
        await self.db.users.update_one(
            {"id": user_id},
            {"$set": {"credits": new_balance}}
        )
        
        # Log transaction
        await self.db.credit_history.insert_one({
            "id": str(uuid.uuid4()),
            "user_id": user_id,
            "delta": -amount,
            "reason": reason,
            "reference_type": ref_type,
            "reference_id": ref_id,
            "balance_after": new_balance,
            "created_at": datetime.now(timezone.utc).isoformat()
        })
        
        return {
            "success": True,
            "charged": amount,
            "balance": new_balance
        }
    
    async def add_credits(self, user_id: str, amount: float, reason: str,
                         ref_type: str = None, ref_id: str = None) -> Dict[str, Any]:
        """Add credits to user account"""
        result = await self.db.users.update_one(
            {"id": user_id},
            {"$inc": {"credits": amount}}
        )
        
        if result.matched_count == 0:
            return {"success": False, "error": "User not found"}
        
        user = await self.db.users.find_one({"id": user_id}, {"_id": 0, "credits": 1})
        new_balance = user["credits"]
        
        await self.db.credit_history.insert_one({
            "id": str(uuid.uuid4()),
            "user_id": user_id,
            "delta": amount,
            "reason": reason,
            "reference_type": ref_type,
            "reference_id": ref_id,
            "balance_after": new_balance,
            "created_at": datetime.now(timezone.utc).isoformat()
        })
        
        return {
            "success": True,
            "added": amount,
            "balance": new_balance
        }
    
    async def check_credits_for_continuation(self, job: Dict, remaining_tasks: List[Dict]) -> Dict[str, Any]:
        """Check if user has enough credits to continue a job"""
        user = await self.db.users.find_one({"id": job["user_id"]}, {"_id": 0})
        if not user:
            return {"can_continue": False, "error": "User not found"}
        
        if not await self.should_charge(user):
            return {"can_continue": True, "free_usage": True}
        
        # Estimate remaining cost
        remaining_tokens = sum(t.get("estimated_tokens", 500) for t in remaining_tasks)
        remaining_credits = await self.calculate_credits(remaining_tokens, is_project=True)
        
        user_credits = user.get("credits", 0)
        
        if user_credits >= remaining_credits:
            return {
                "can_continue": True,
                "estimated_remaining_cost": remaining_credits,
                "user_credits": user_credits
            }
        else:
            return {
                "can_continue": False,
                "needs_more_credits": True,
                "estimated_remaining_cost": remaining_credits,
                "user_credits": user_credits,
                "shortfall": remaining_credits - user_credits
            }
