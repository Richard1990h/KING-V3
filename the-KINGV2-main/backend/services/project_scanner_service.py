"""Project Scanner Service - Handles project upload and language detection"""
from typing import Dict, Any, List, Optional
import os
import re
import zipfile
import io
import base64
from datetime import datetime, timezone
import uuid
import logging

logger = logging.getLogger(__name__)

# Language detection patterns
LANGUAGE_PATTERNS = {
    "Python": {
        "extensions": [".py", ".pyw", ".pyi"],
        "files": ["requirements.txt", "setup.py", "pyproject.toml", "Pipfile"],
        "markers": ["import ", "def ", "class ", "from ", "if __name__"]
    },
    "JavaScript": {
        "extensions": [".js", ".mjs", ".cjs"],
        "files": ["package.json", ".eslintrc", "webpack.config.js"],
        "markers": ["const ", "let ", "function ", "require(", "module.exports", "import "]
    },
    "TypeScript": {
        "extensions": [".ts", ".tsx"],
        "files": ["tsconfig.json", "package.json"],
        "markers": ["interface ", "type ", ": string", ": number", ": boolean"]
    },
    "Java": {
        "extensions": [".java"],
        "files": ["pom.xml", "build.gradle", "build.gradle.kts"],
        "markers": ["public class", "private ", "protected ", "package ", "import java"]
    },
    "C#": {
        "extensions": [".cs"],
        "files": [".csproj", ".sln", "Program.cs"],
        "markers": ["using System", "namespace ", "public class", "private ", "void "]
    },
    "Go": {
        "extensions": [".go"],
        "files": ["go.mod", "go.sum"],
        "markers": ["package main", "func ", "import (", "type struct"]
    },
    "Rust": {
        "extensions": [".rs"],
        "files": ["Cargo.toml", "Cargo.lock"],
        "markers": ["fn main", "let mut", "impl ", "pub fn", "use "]
    },
    "PHP": {
        "extensions": [".php"],
        "files": ["composer.json", "index.php"],
        "markers": ["<?php", "function ", "class ", "public function", "private function"]
    },
    "Ruby": {
        "extensions": [".rb"],
        "files": ["Gemfile", "Rakefile", ".ruby-version"],
        "markers": ["def ", "class ", "require ", "module ", "end"]
    },
    "C++": {
        "extensions": [".cpp", ".hpp", ".cc", ".h"],
        "files": ["CMakeLists.txt", "Makefile"],
        "markers": ["#include", "int main", "std::", "class ", "void "]
    },
    "React": {
        "extensions": [".jsx", ".tsx"],
        "files": ["package.json"],
        "markers": ["import React", "from 'react'", "useState", "useEffect", "<div"]
    },
    "Vue": {
        "extensions": [".vue"],
        "files": ["vue.config.js", "package.json"],
        "markers": ["<template>", "<script>", "<style>", "export default"]
    },
    "HTML/CSS": {
        "extensions": [".html", ".htm", ".css", ".scss", ".sass"],
        "files": ["index.html", "style.css"],
        "markers": ["<!DOCTYPE", "<html", "<head", "<body", "{\n  "]
    }
}


class ProjectScannerService:
    """Service for scanning uploaded projects"""
    
    def __init__(self, db, ai_service=None, credit_service=None):
        self.db = db
        self.ai_service = ai_service
        self.credit_service = credit_service
    
    def detect_language(self, files: List[Dict[str, str]]) -> Dict[str, Any]:
        """Detect primary language from project files"""
        scores = {lang: 0 for lang in LANGUAGE_PATTERNS.keys()}
        
        file_paths = [f.get("path", "") for f in files]
        file_names = [os.path.basename(p) for p in file_paths]
        
        for lang, patterns in LANGUAGE_PATTERNS.items():
            # Check file extensions
            for f in files:
                path = f.get("path", "")
                ext = os.path.splitext(path)[1].lower()
                if ext in patterns["extensions"]:
                    scores[lang] += 2
            
            # Check config files
            for config_file in patterns["files"]:
                if config_file in file_names or any(p.endswith(config_file) for p in file_paths):
                    scores[lang] += 3
            
            # Check content markers (sample first few files)
            for f in files[:10]:
                content = f.get("content", "")
                for marker in patterns["markers"]:
                    if marker in content:
                        scores[lang] += 1
        
        # Get best match
        best_lang = max(scores.keys(), key=lambda k: scores[k])
        confidence = scores[best_lang] / (len(files) + 10) if files else 0
        
        return {
            "language": best_lang if scores[best_lang] > 0 else "Unknown",
            "confidence": min(confidence, 1.0),
            "scores": {k: v for k, v in scores.items() if v > 0}
        }
    
    async def process_upload(self, project_id: str, files: List[Dict]) -> Dict[str, Any]:
        """Process uploaded files and save to project"""
        now = datetime.now(timezone.utc).isoformat()
        saved_files = []
        
        for file_data in files:
            path = file_data.get("path", "")
            content = file_data.get("content", "")
            
            # Check if file exists
            existing = await self.db.project_files.find_one(
                {"project_id": project_id, "path": path}
            )
            
            file_record = {
                "id": str(uuid.uuid4()),
                "project_id": project_id,
                "path": path,
                "content": content,
                "updated_at": now
            }
            
            if existing:
                await self.db.project_files.update_one(
                    {"project_id": project_id, "path": path},
                    {"$set": {"content": content, "updated_at": now}}
                )
            else:
                await self.db.project_files.insert_one(file_record)
            
            saved_files.append(path)
        
        # Detect language
        detection = self.detect_language(files)
        
        # Update project with detected language
        await self.db.projects.update_one(
            {"id": project_id},
            {"$set": {
                "language": detection["language"],
                "updated_at": now
            }}
        )
        
        return {
            "files_uploaded": len(saved_files),
            "file_paths": saved_files,
            "detected_language": detection["language"],
            "language_confidence": detection["confidence"]
        }
    
    async def process_zip_upload(self, project_id: str, zip_data: bytes) -> Dict[str, Any]:
        """Process a ZIP file upload"""
        files = []
        
        try:
            with zipfile.ZipFile(io.BytesIO(zip_data), 'r') as zf:
                for name in zf.namelist():
                    # Skip directories and hidden files
                    if name.endswith('/') or name.startswith('.') or '/__' in name:
                        continue
                    
                    # Skip binary files and large files
                    info = zf.getinfo(name)
                    if info.file_size > 1024 * 1024:  # Skip files > 1MB
                        continue
                    
                    try:
                        content = zf.read(name).decode('utf-8')
                        files.append({"path": name, "content": content})
                    except UnicodeDecodeError:
                        continue  # Skip binary files
            
            return await self.process_upload(project_id, files)
            
        except zipfile.BadZipFile:
            return {"error": "Invalid ZIP file"}
        except Exception as e:
            logger.error(f"ZIP processing error: {e}")
            return {"error": str(e)}
    
    async def estimate_scan_cost(self, files: List[Dict]) -> Dict[str, Any]:
        """Estimate credit cost to scan project for issues"""
        total_content_length = sum(len(f.get("content", "")) for f in files)
        estimated_tokens = int(total_content_length / 3.5)  # Rough estimate
        
        if self.credit_service:
            estimated_credits = await self.credit_service.calculate_credits(
                estimated_tokens, is_project=True
            )
        else:
            estimated_credits = estimated_tokens / 1000 * 1.0
        
        return {
            "file_count": len(files),
            "total_characters": total_content_length,
            "estimated_tokens": estimated_tokens,
            "estimated_credits": round(estimated_credits, 2)
        }
    
    async def scan_for_issues(self, project_id: str, user: Dict) -> Dict[str, Any]:
        """Scan project files for potential issues using AI"""
        files = await self.db.project_files.find(
            {"project_id": project_id},
            {"_id": 0}
        ).to_list(100)
        
        if not files:
            return {"error": "No files to scan"}
        
        # Check credits
        if self.credit_service and await self.credit_service.should_charge(user):
            cost_estimate = await self.estimate_scan_cost(files)
            if user.get("credits", 0) < cost_estimate["estimated_credits"]:
                return {
                    "error": "Insufficient credits",
                    "estimated_cost": cost_estimate["estimated_credits"],
                    "user_credits": user.get("credits", 0)
                }
        
        # Build scan prompt
        project = await self.db.projects.find_one({"id": project_id}, {"_id": 0})
        language = project.get("language", "Unknown") if project else "Unknown"
        
        scan_prompt = f"""Analyze this {language} project for potential issues.

FILES TO ANALYZE:
"""
        
        for f in files[:20]:  # Limit to 20 files
            scan_prompt += f"\n### {f['path']}\n```\n{f.get('content', '')[:2000]}\n```\n"
        
        scan_prompt += """

Provide a detailed analysis in JSON format:
{
    "issues": [
        {
            "type": "error|warning|suggestion",
            "file": "filename",
            "line": 10,
            "message": "Description of issue",
            "fix_suggestion": "How to fix"
        }
    ],
    "overall_quality": "good|fair|poor",
    "recommendations": ["list of improvements"]
}
"""
        
        if self.ai_service:
            response = await self.ai_service.generate(
                scan_prompt,
                "You are an expert code reviewer. Analyze the code and identify bugs, security issues, performance problems, and best practice violations."
            )
            
            # Deduct credits
            if self.credit_service and await self.credit_service.should_charge(user):
                tokens_used = response.get("tokens", 0)
                credits = await self.credit_service.calculate_credits(tokens_used, True)
                await self.credit_service.deduct_credits(
                    user["id"], credits, "Project scan", "scan", project_id
                )
            
            # Parse response
            content = response.get("content", "")
            
            # Try to extract JSON
            import json
            try:
                # Find JSON in response
                json_match = re.search(r'\{[\s\S]*\}', content)
                if json_match:
                    analysis = json.loads(json_match.group(0))
                    return {
                        "success": True,
                        "analysis": analysis,
                        "tokens_used": response.get("tokens", 0)
                    }
            except:
                pass
            
            return {
                "success": True,
                "raw_analysis": content,
                "tokens_used": response.get("tokens", 0)
            }
        
        return {"error": "AI service not available"}
    
    async def create_project_zip(self, project_id: str) -> Dict[str, Any]:
        """Create a ZIP file from project files"""
        project = await self.db.projects.find_one({"id": project_id}, {"_id": 0})
        if not project:
            return {"error": "Project not found"}
        
        files = await self.db.project_files.find(
            {"project_id": project_id},
            {"_id": 0}
        ).to_list(1000)
        
        zip_buffer = io.BytesIO()
        
        with zipfile.ZipFile(zip_buffer, 'w', zipfile.ZIP_DEFLATED) as zf:
            for f in files:
                zf.writestr(f["path"], f.get("content", ""))
            
            # Add README
            readme = f"""# {project['name']}

{project.get('description', '')}

Language: {project.get('language', 'Unknown')}
Exported from LittleHelper AI

Created: {project.get('created_at', 'Unknown')}
"""
            zf.writestr("README.md", readme)
        
        zip_buffer.seek(0)
        zip_base64 = base64.b64encode(zip_buffer.read()).decode('utf-8')
        
        return {
            "filename": f"{project['name'].replace(' ', '_')}.zip",
            "data": zip_base64,
            "content_type": "application/zip",
            "file_count": len(files)
        }
