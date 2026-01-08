"""Job Orchestration Service - Manages multi-agent job execution"""
from typing import Dict, Any, List, Optional, AsyncGenerator
from datetime import datetime, timezone
import uuid
import asyncio
import logging

from agents import AGENT_REGISTRY, AGENT_INFO
from services.ai_service import AIService
from services.credit_service import CreditService

logger = logging.getLogger(__name__)


class JobOrchestrationService:
    """Orchestrates multi-agent job execution"""
    
    def __init__(self, db, ai_service: AIService, credit_service: CreditService):
        self.db = db
        self.ai_service = ai_service
        self.credit_service = credit_service
    
    async def create_job(self, user: Dict, project: Dict, prompt: str, 
                         multi_agent_mode: bool = True) -> Dict[str, Any]:
        """Create a new job and analyze requirements"""
        job_id = str(uuid.uuid4())
        now = datetime.now(timezone.utc).isoformat()
        
        # Create initial job record
        job = {
            "id": job_id,
            "project_id": project["id"],
            "user_id": user["id"],
            "prompt": prompt,
            "status": "analyzing",
            "multi_agent_mode": multi_agent_mode,
            "tasks": [],
            "total_estimated_credits": 0,
            "credits_used": 0,
            "credits_approved": 0,
            "current_task_index": -1,
            "error_count": 0,
            "max_errors": 5,
            "created_at": now,
            "updated_at": now
        }
        
        await self.db.jobs.insert_one({**job})
        
        # Use Planner agent to analyze and create task breakdown
        planner = AGENT_REGISTRY["planner"](
            self.ai_service,
            project_context={
                "name": project["name"],
                "language": project["language"],
                "description": project.get("description", "")
            }
        )
        
        result = await planner.execute(prompt)
        
        if result.success and result.tasks_generated:
            tasks = result.tasks_generated
            
            # Calculate credit estimates for each task
            cost_estimate = await self.credit_service.estimate_job_cost(tasks, user)
            
            # Update job with tasks
            await self.db.jobs.update_one(
                {"id": job_id},
                {"$set": {
                    "status": "awaiting_approval",
                    "tasks": tasks,
                    "total_estimated_credits": cost_estimate["total_estimated_credits"],
                    "planner_output": result.content,
                    "planner_metadata": result.metadata,
                    "updated_at": datetime.now(timezone.utc).isoformat()
                }}
            )
            
            # Return job with cost estimate
            job["status"] = "awaiting_approval"
            job["tasks"] = tasks
            job["total_estimated_credits"] = cost_estimate["total_estimated_credits"]
            job["cost_estimate"] = cost_estimate
            job["planner_output"] = result.content
            
            return job
        else:
            # Planner failed - use fallback
            await self.db.jobs.update_one(
                {"id": job_id},
                {"$set": {
                    "status": "failed",
                    "error": "Failed to analyze requirements",
                    "updated_at": datetime.now(timezone.utc).isoformat()
                }}
            )
            job["status"] = "failed"
            job["error"] = "Failed to analyze requirements"
            return job
    
    async def approve_job(self, job_id: str, user: Dict, 
                          modified_tasks: List[Dict] = None) -> Dict[str, Any]:
        """Approve a job for execution (with optional task modifications)"""
        job = await self.db.jobs.find_one({"id": job_id, "user_id": user["id"]}, {"_id": 0})
        if not job:
            return {"success": False, "error": "Job not found"}
        
        if job["status"] != "awaiting_approval" and job["status"] != "needs_more_credits":
            return {"success": False, "error": f"Job cannot be approved in status: {job['status']}"}
        
        tasks = modified_tasks if modified_tasks else job["tasks"]
        
        # Recalculate cost if tasks were modified
        cost_estimate = await self.credit_service.estimate_job_cost(tasks, user)
        
        # Check if user has enough credits
        if not cost_estimate.get("free_usage") and not cost_estimate.get("sufficient_credits"):
            return {
                "success": False,
                "error": "Insufficient credits",
                "required": cost_estimate["total_estimated_credits"],
                "available": user.get("credits", 0)
            }
        
        # Update job status
        await self.db.jobs.update_one(
            {"id": job_id},
            {"$set": {
                "status": "approved",
                "tasks": tasks,
                "total_estimated_credits": cost_estimate["total_estimated_credits"],
                "credits_approved": cost_estimate["total_estimated_credits"],
                "current_task_index": 0,
                "updated_at": datetime.now(timezone.utc).isoformat()
            }}
        )
        
        return {
            "success": True,
            "job_id": job_id,
            "status": "approved",
            "total_estimated_credits": cost_estimate["total_estimated_credits"]
        }
    
    async def execute_job(self, job_id: str, user: Dict) -> AsyncGenerator[Dict[str, Any], None]:
        """Execute approved job - yields progress updates"""
        job = await self.db.jobs.find_one({"id": job_id}, {"_id": 0})
        if not job:
            yield {"type": "error", "message": "Job not found"}
            return
        
        project = await self.db.projects.find_one({"id": job["project_id"]}, {"_id": 0})
        if not project:
            yield {"type": "error", "message": "Project not found"}
            return
        
        # Update status to in_progress
        await self.db.jobs.update_one(
            {"id": job_id},
            {"$set": {"status": "in_progress", "started_at": datetime.now(timezone.utc).isoformat()}}
        )
        
        yield {"type": "job_started", "job_id": job_id, "total_tasks": len(job["tasks"])}
        
        tasks = job["tasks"]
        current_index = job.get("current_task_index", 0)
        total_credits_used = job.get("credits_used", 0)
        previous_outputs = []
        existing_files = await self.db.project_files.find({"project_id": project["id"]}, {"_id": 0}).to_list(100)
        
        for i in range(current_index, len(tasks)):
            task = tasks[i]
            
            # Check credits before each task
            should_charge = await self.credit_service.should_charge(user)
            if should_charge:
                remaining_cost = await self._estimate_remaining_cost(tasks[i:])
                user_data = await self.db.users.find_one({"id": user["id"]}, {"_id": 0, "credits": 1})
                
                if user_data["credits"] < task.get("estimated_credits", 0):
                    # Pause job and request more credits
                    await self.db.jobs.update_one(
                        {"id": job_id},
                        {"$set": {
                            "status": "needs_more_credits",
                            "current_task_index": i,
                            "credits_needed": remaining_cost,
                            "updated_at": datetime.now(timezone.utc).isoformat()
                        }}
                    )
                    yield {
                        "type": "needs_credits",
                        "message": f"Need {remaining_cost:.2f} more credits to continue",
                        "credits_needed": remaining_cost,
                        "current_credits": user_data["credits"],
                        "completed_tasks": i,
                        "remaining_tasks": len(tasks) - i
                    }
                    return
            
            # Update task status
            task["status"] = "running"
            await self._update_task(job_id, i, task)
            
            yield {
                "type": "task_started",
                "task_index": i,
                "task_id": task["id"],
                "title": task["title"],
                "agent": task["agent_type"]
            }
            
            # Execute the task with appropriate agent
            try:
                result = await self._execute_task(
                    task, project, previous_outputs, existing_files
                )
                
                # Process result
                task["status"] = "completed" if result.success else "failed"
                task["output"] = result.content
                task["actual_tokens"] = result.tokens_used
                task["actual_credits"] = await self.credit_service.calculate_credits(result.tokens_used, True)
                
                if result.files_created:
                    task["files_created"] = [f["path"] for f in result.files_created]
                    # Save files to project
                    for file_data in result.files_created:
                        await self._save_file(project["id"], file_data)
                        existing_files.append(file_data)
                
                if result.errors:
                    task["error"] = "; ".join(result.errors)
                
                # Deduct credits
                if should_charge and task["actual_credits"] > 0:
                    deduct_result = await self.credit_service.deduct_credits(
                        user["id"], task["actual_credits"],
                        f"Task: {task['title']}", "job_task", task["id"]
                    )
                    total_credits_used += task["actual_credits"]
                
                await self._update_task(job_id, i, task)
                
                # Add to previous outputs for context
                previous_outputs.append({
                    "agent": task["agent_type"],
                    "summary": result.content[:500] if result.content else ""
                })
                
                yield {
                    "type": "task_completed",
                    "task_index": i,
                    "task_id": task["id"],
                    "success": result.success,
                    "files_created": task.get("files_created", []),
                    "credits_used": task["actual_credits"],
                    "output_preview": result.content[:200] if result.content else ""
                }
                
                # Handle errors
                if not result.success and result.errors:
                    error_count = job.get("error_count", 0) + 1
                    
                    if error_count < job.get("max_errors", 5):
                        # Try to auto-fix
                        fix_result = await self._attempt_auto_fix(
                            task, result.errors, project, existing_files
                        )
                        if fix_result:
                            yield {
                                "type": "auto_fix_applied",
                                "task_id": task["id"],
                                "fix_description": fix_result.get("description", "Applied fix")
                            }
                    else:
                        # Too many errors
                        await self.db.jobs.update_one(
                            {"id": job_id},
                            {"$set": {"status": "failed", "error": "Too many errors"}}
                        )
                        yield {"type": "job_failed", "reason": "Too many errors"}
                        return
                
            except Exception as e:
                logger.error(f"Task execution error: {e}")
                task["status"] = "failed"
                task["error"] = str(e)
                await self._update_task(job_id, i, task)
                
                yield {
                    "type": "task_error",
                    "task_index": i,
                    "task_id": task["id"],
                    "error": str(e)
                }
        
        # Job completed
        await self.db.jobs.update_one(
            {"id": job_id},
            {"$set": {
                "status": "completed",
                "credits_used": total_credits_used,
                "completed_at": datetime.now(timezone.utc).isoformat(),
                "updated_at": datetime.now(timezone.utc).isoformat()
            }}
        )
        
        yield {
            "type": "job_completed",
            "job_id": job_id,
            "total_credits_used": total_credits_used,
            "files_created": sum(len(t.get("files_created", [])) for t in tasks)
        }
    
    async def _execute_task(self, task: Dict, project: Dict, 
                           previous_outputs: List, existing_files: List) -> Any:
        """Execute a single task with the appropriate agent"""
        agent_type = task.get("agent_type", "developer")
        agent_class = AGENT_REGISTRY.get(agent_type)
        
        if not agent_class:
            agent_class = AGENT_REGISTRY["developer"]
        
        agent = agent_class(
            self.ai_service,
            project_context={
                "name": project["name"],
                "language": project["language"],
                "description": project.get("description", "")
            }
        )
        
        context = {
            "previous_outputs": previous_outputs,
            "existing_files": existing_files
        }
        
        return await agent.execute(task.get("description", task.get("title", "")), context)
    
    async def _attempt_auto_fix(self, task: Dict, errors: List[str], 
                                project: Dict, existing_files: List) -> Optional[Dict]:
        """Attempt to auto-fix errors using debugger agent"""
        debugger = AGENT_REGISTRY["debugger"](
            self.ai_service,
            project_context={
                "name": project["name"],
                "language": project["language"]
            }
        )
        
        result = await debugger.execute(
            f"Fix the following errors: {'; '.join(errors)}",
            {"errors": errors, "existing_files": existing_files}
        )
        
        if result.success and result.files_created:
            for file_data in result.files_created:
                await self._save_file(project["id"], file_data)
            return {"success": True, "description": "Applied automatic fixes"}
        
        return None
    
    async def _update_task(self, job_id: str, task_index: int, task: Dict):
        """Update a specific task in the job"""
        await self.db.jobs.update_one(
            {"id": job_id},
            {
                "$set": {
                    f"tasks.{task_index}": task,
                    "current_task_index": task_index,
                    "updated_at": datetime.now(timezone.utc).isoformat()
                }
            }
        )
    
    async def _save_file(self, project_id: str, file_data: Dict):
        """Save or update a file in the project"""
        existing = await self.db.project_files.find_one(
            {"project_id": project_id, "path": file_data["path"]}
        )
        
        now = datetime.now(timezone.utc).isoformat()
        
        if existing:
            await self.db.project_files.update_one(
                {"project_id": project_id, "path": file_data["path"]},
                {"$set": {"content": file_data["content"], "updated_at": now}}
            )
        else:
            await self.db.project_files.insert_one({
                "id": str(uuid.uuid4()),
                "project_id": project_id,
                "path": file_data["path"],
                "content": file_data["content"],
                "updated_at": now
            })
    
    async def _estimate_remaining_cost(self, remaining_tasks: List[Dict]) -> float:
        """Estimate cost for remaining tasks"""
        total_tokens = sum(t.get("estimated_tokens", 500) for t in remaining_tasks)
        return await self.credit_service.calculate_credits(total_tokens, is_project=True)
    
    async def get_job(self, job_id: str, user_id: str) -> Optional[Dict]:
        """Get job by ID"""
        return await self.db.jobs.find_one(
            {"id": job_id, "user_id": user_id},
            {"_id": 0}
        )
    
    async def get_user_jobs(self, user_id: str, limit: int = 20) -> List[Dict]:
        """Get user's recent jobs"""
        jobs = await self.db.jobs.find(
            {"user_id": user_id},
            {"_id": 0}
        ).sort("created_at", -1).to_list(limit)
        return jobs
