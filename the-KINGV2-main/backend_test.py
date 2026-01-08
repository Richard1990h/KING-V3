#!/usr/bin/env python3

import requests
import sys
import json
from datetime import datetime
from typing import Dict, Any, Optional

class LittleHelperAPITester:
    def __init__(self, base_url: str = "https://codehelper-ai-4.preview.emergentagent.com"):
        self.base_url = base_url
        self.token = None
        self.user_id = None
        self.project_id = None
        self.file_id = None
        self.tests_run = 0
        self.tests_passed = 0
        self.failed_tests = []
        self.session = requests.Session()
        self.session.headers.update({'Content-Type': 'application/json'})

    def log(self, message: str, level: str = "INFO"):
        timestamp = datetime.now().strftime("%H:%M:%S")
        print(f"[{timestamp}] {level}: {message}")

    def run_test(self, name: str, method: str, endpoint: str, expected_status: int, 
                 data: Optional[Dict] = None, headers: Optional[Dict] = None) -> tuple[bool, Dict]:
        """Run a single API test"""
        url = f"{self.base_url}/{endpoint.lstrip('/')}"
        test_headers = self.session.headers.copy()
        if headers:
            test_headers.update(headers)
        if self.token:
            test_headers['Authorization'] = f'Bearer {self.token}'

        self.tests_run += 1
        self.log(f"Testing {name}...")
        
        try:
            if method == 'GET':
                response = self.session.get(url, headers=test_headers)
            elif method == 'POST':
                response = self.session.post(url, json=data, headers=test_headers)
            elif method == 'PUT':
                response = self.session.put(url, json=data, headers=test_headers)
            elif method == 'DELETE':
                response = self.session.delete(url, headers=test_headers)
            else:
                raise ValueError(f"Unsupported method: {method}")

            success = response.status_code == expected_status
            if success:
                self.tests_passed += 1
                self.log(f"✅ {name} - Status: {response.status_code}")
            else:
                self.log(f"❌ {name} - Expected {expected_status}, got {response.status_code}")
                self.log(f"   Response: {response.text[:200]}")
                self.failed_tests.append({
                    'name': name,
                    'expected': expected_status,
                    'actual': response.status_code,
                    'response': response.text[:200]
                })

            try:
                response_data = response.json() if response.text else {}
            except:
                response_data = {'raw_response': response.text}

            return success, response_data

        except Exception as e:
            self.log(f"❌ {name} - Error: {str(e)}", "ERROR")
            self.failed_tests.append({
                'name': name,
                'error': str(e)
            })
            return False, {}

    def test_health_check(self):
        """Test basic health endpoints"""
        self.log("=== HEALTH CHECK TESTS ===")
        
        # Test root endpoint
        self.run_test("Root endpoint", "GET", "/api/", 200)
        
        # Test health endpoint
        self.run_test("Health check", "GET", "/api/health", 200)

    def test_authentication_and_tos(self):
        """Test authentication and Terms of Service endpoints"""
        self.log("=== AUTHENTICATION & TOS TESTS ===")
        
        # Generate unique test user
        timestamp = datetime.now().strftime("%H%M%S")
        test_email = f"test_user_{timestamp}@example.com"
        test_password = "TestPass123!"
        test_name = f"Test User {timestamp}"
        
        # Test 1: Registration without TOS acceptance (should fail)
        register_data_no_tos = {
            "name": test_name,
            "email": test_email,
            "password": test_password,
            "tos_accepted": False
        }
        success, response = self.run_test("Registration without TOS (should fail)", "POST", "/api/auth/register", 400, register_data_no_tos)
        if success:
            self.log("✅ Registration correctly rejected without TOS acceptance")
        
        # Test 2: Registration with TOS acceptance (should succeed)
        register_data_with_tos = {
            "name": test_name,
            "email": test_email,
            "password": test_password,
            "tos_accepted": True
        }
        success, response = self.run_test("Registration with TOS acceptance", "POST", "/api/auth/register", 200, register_data_with_tos)
        
        if success and 'token' in response:
            self.token = response['token']
            self.user_id = response['user']['id']
            self.log(f"✅ Registration successful with TOS, token obtained")
        else:
            self.log("❌ Registration failed, cannot continue with authenticated tests")
            return False
        
        # Test 3: TOS status check
        success, tos_status = self.run_test("TOS status check", "GET", "/api/auth/tos-status", 200)
        if success and isinstance(tos_status, dict):
            tos_accepted = tos_status.get('tos_accepted', False)
            self.log(f"✅ TOS status: accepted={tos_accepted}")
        
        # Test 4: TOS acceptance endpoint
        success, accept_response = self.run_test("Accept TOS", "POST", "/api/auth/accept-tos", 200)
        if success:
            self.log("✅ TOS acceptance endpoint working")
        
        # Test 5: Legal terms endpoint
        success, terms_response = self.run_test("Get legal terms", "GET", "/api/legal/terms", 200)
        if success and isinstance(terms_response, dict):
            version = terms_response.get('version', 'unknown')
            content = terms_response.get('content', {})
            sections = content.get('sections', [])
            self.log(f"✅ Legal terms retrieved: version={version}, sections={len(sections)}")
        
        # Test login
        login_data = {
            "email": test_email,
            "password": test_password
        }
        success, response = self.run_test("User login", "POST", "/api/auth/login", 200, login_data)
        
        if success and 'token' in response:
            self.token = response['token']  # Update token
            self.log("✅ Login successful")
        
        # Test get current user
        self.run_test("Get current user", "GET", "/api/auth/me", 200)
        
        # Test language update
        language_data = {"language": "es"}
        self.run_test("Update language", "PUT", "/api/auth/language", 200, language_data)
        
        return True

    def test_projects(self):
        """Test project management endpoints"""
        self.log("=== PROJECT TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping project tests")
            return False
        
        # Test get projects (empty list initially)
        self.run_test("Get projects (empty)", "GET", "/api/projects", 200)
        
        # Test create project
        project_data = {
            "name": "Test Project",
            "description": "A test project for API testing",
            "language": "Python"
        }
        success, response = self.run_test("Create project", "POST", "/api/projects", 200, project_data)
        
        if success and 'id' in response:
            self.project_id = response['id']
            self.log(f"✅ Project created with ID: {self.project_id}")
        else:
            self.log("❌ Project creation failed")
            return False
        
        # Test get specific project
        self.run_test("Get project by ID", "GET", f"/api/projects/{self.project_id}", 200)
        
        # Test update project
        update_data = {
            "name": "Updated Test Project",
            "description": "Updated description"
        }
        self.run_test("Update project", "PUT", f"/api/projects/{self.project_id}", 200, update_data)
        
        # Test get all projects (should have 1 now)
        self.run_test("Get projects (with data)", "GET", "/api/projects", 200)
        
        return True

    def test_ai_building_system(self):
        """Test AI building system endpoints"""
        self.log("=== AI BUILDING SYSTEM TESTS ===")
        
        if not self.token or not self.project_id:
            self.log("❌ No authentication token or project ID, skipping AI building tests")
            return False
        
        # Test 1: AI planning
        plan_data = {
            "project_id": self.project_id,
            "request": "Create a simple calculator with add, subtract, multiply, and divide functions"
        }
        success, plan_response = self.run_test("AI planning", "POST", "/api/ai/plan", 200, plan_data)
        
        if success and isinstance(plan_response, dict):
            tasks = plan_response.get('tasks', [])
            self.log(f"✅ AI planning created {len(tasks)} tasks")
            for i, task in enumerate(tasks):
                if isinstance(task, dict):
                    description = task.get('description', 'No description')
                    agent = task.get('agent', 'unknown')
                    self.log(f"   Task {i+1}: [{agent}] {description[:50]}...")
        
        # Test 2: AI research
        research_data = {
            "project_id": self.project_id,
            "request": "Research best practices for Python calculator implementation",
            "tasks": [
                {"description": "Research Python math operations", "agent": "researcher"},
                {"description": "Find error handling patterns", "agent": "researcher"}
            ]
        }
        success, research_response = self.run_test("AI research", "POST", "/api/ai/research", 200, research_data)
        
        if success:
            self.log("✅ AI research completed")
        
        # Test 3: Task execution
        execute_data = {
            "project_id": self.project_id,
            "task": "Create a basic calculator class with arithmetic operations",
            "agent": "developer"
        }
        success, execute_response = self.run_test("AI task execution", "POST", "/api/ai/execute-task", 200, execute_data)
        
        if success:
            self.log("✅ AI task execution completed")
        
        return True

    def test_file_management(self):
        """Test file management endpoints"""
        self.log("=== FILE MANAGEMENT TESTS ===")
        
        if not self.project_id:
            self.log("❌ No project ID, skipping file management tests")
            return False
        
        # Test get project files
        success, response = self.run_test("Get project files", "GET", f"/api/projects/{self.project_id}/files", 200)
        
        # Should have default main.py file
        if success and len(response) > 0:
            self.file_id = response[0]['id']
            self.log(f"✅ Found default file with ID: {self.file_id}")
        
        # Test 1: File creation
        file_data = {
            "path": "calculator.py",
            "content": "# Calculator module\n\ndef add(a, b):\n    return a + b\n\ndef subtract(a, b):\n    return a - b"
        }
        success, response = self.run_test("Create file", "POST", f"/api/projects/{self.project_id}/files", 200, file_data)
        
        test_file_id = None
        if success and 'id' in response:
            test_file_id = response['id']
            self.log(f"✅ Calculator file created with ID: {test_file_id}")
            
            # Test update file
            update_data = {
                "content": "# Updated calculator module\n\ndef add(a, b):\n    return a + b\n\ndef subtract(a, b):\n    return a - b\n\ndef multiply(a, b):\n    return a * b\n\ndef divide(a, b):\n    if b != 0:\n        return a / b\n    else:\n        raise ValueError('Cannot divide by zero')"
            }
            self.run_test("Update file", "PUT", f"/api/projects/{self.project_id}/files/{test_file_id}", 200, update_data)
        
        # Test 2: Project export (ZIP download)
        success, export_response = self.run_test("Export project as ZIP", "GET", f"/api/projects/{self.project_id}/export", 200)
        
        if success:
            self.log("✅ Project export successful")
            # Note: In a real test, we'd check the response headers for ZIP content-type
        
        # Test 3: File upload (individual files)
        # Note: This would require multipart/form-data, which is complex to test with simple requests
        # For now, we'll test the ZIP upload endpoint
        
        # Create a simple test file content for upload simulation
        upload_test_data = {
            "files": [
                {"path": "utils.py", "content": "# Utility functions\n\ndef format_result(result):\n    return f'Result: {result}'"}
            ]
        }
        # Note: The actual upload would use multipart form data
        self.log("⚠️  File upload test skipped - requires multipart form data")
        
        # Clean up test file
        if test_file_id:
            self.run_test("Delete test file", "DELETE", f"/api/projects/{self.project_id}/files/{test_file_id}", 200)
        
        return True

    def test_build_and_run(self):
        """Test build and run endpoints"""
        self.log("=== BUILD & RUN TESTS ===")
        
        if not self.project_id:
            self.log("❌ No project ID, skipping build/run tests")
            return False
        
        # Test build project
        success, response = self.run_test("Build project", "POST", f"/api/projects/{self.project_id}/build", 200)
        
        if success:
            self.log(f"✅ Build completed with status: {response.get('status', 'unknown')}")
        
        # Test run project
        success, response = self.run_test("Run project", "POST", f"/api/projects/{self.project_id}/run", 200)
        
        if success:
            self.log(f"✅ Run completed with status: {response.get('status', 'unknown')}")
        
        # Test get project runs
        self.run_test("Get project runs", "GET", f"/api/projects/{self.project_id}/runs", 200)
        
        return True

    def test_chat(self):
        """Test chat endpoints"""
        self.log("=== CHAT TESTS ===")
        
        if not self.project_id:
            self.log("❌ No project ID, skipping chat tests")
            return False
        
        # Test get chat history (empty initially)
        self.run_test("Get chat history (empty)", "GET", f"/api/projects/{self.project_id}/chat", 200)
        
        # Test send chat message
        chat_data = {
            "project_id": self.project_id,
            "message": "Hello, can you help me with this Python project?",
            "agents_enabled": ["developer"]
        }
        success, response = self.run_test("Send chat message", "POST", f"/api/projects/{self.project_id}/chat", 200, chat_data)
        
        if success:
            self.log("✅ Chat message sent successfully")
        
        # Test get chat history (should have messages now)
        self.run_test("Get chat history (with data)", "GET", f"/api/projects/{self.project_id}/chat", 200)
        
        return True

    def test_agents(self):
        """Test agents endpoint"""
        self.log("=== AGENTS TESTS ===")
        
        success, response = self.run_test("Get agents", "GET", "/api/agents", 200)
        
        if success and isinstance(response, list):
            self.log(f"✅ Found {len(response)} agents")
            # Check for expected agent types
            expected_agents = ["planner", "researcher", "developer", "test_designer", "executor", "debugger", "verifier", "error_analyzer"]
            found_agents = [agent.get('id', '') for agent in response if isinstance(agent, dict)]
            
            for expected in expected_agents:
                if expected in found_agents:
                    self.log(f"   ✅ {expected} agent found")
                else:
                    self.log(f"   ⚠️  {expected} agent not found")
        
        return True

    def test_user_profile(self):
        """Test user profile endpoints"""
        self.log("=== USER PROFILE TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping user profile tests")
            return False
        
        # Test 1: Get user profile
        success, profile_response = self.run_test("Get user profile", "GET", "/api/user/profile", 200)
        
        if success and isinstance(profile_response, dict):
            name = profile_response.get('name', 'Unknown')
            email = profile_response.get('email', 'Unknown')
            theme = profile_response.get('theme', {})
            self.log(f"✅ Profile retrieved: name={name}, email={email}, theme_keys={list(theme.keys())}")
        
        # Test 2: Update user profile
        profile_update_data = {
            "name": "Updated Test User",
            "display_name": "Updated Display Name",
            "avatar_url": "https://example.com/avatar.jpg"
        }
        success, update_response = self.run_test("Update user profile", "PUT", "/api/user/profile", 200, profile_update_data)
        
        if success:
            self.log("✅ User profile updated successfully")
        
        # Test 3: Update user theme
        theme_data = {
            "primary_color": "#ff6b6b",
            "secondary_color": "#4ecdc4",
            "background_color": "#2c3e50",
            "card_color": "#34495e",
            "text_color": "#ecf0f1",
            "hover_color": "#e74c3c",
            "credits_color": "#f39c12",
            "background_image": None
        }
        success, theme_response = self.run_test("Update user theme", "PUT", "/api/user/theme", 200, theme_data)
        
        if success:
            self.log("✅ User theme updated successfully")
        
        # Test 4: Change password
        password_change_data = {
            "current_password": "TestPass123!",
            "new_password": "NewTestPass456!",
            "confirm_password": "NewTestPass456!"
        }
        success, password_response = self.run_test("Change password", "PUT", "/api/user/password", 200, password_change_data)
        
        if success:
            self.log("✅ Password changed successfully")
            
            # Test login with new password to verify change
            login_data = {
                "email": profile_response.get('email') if profile_response else f"test_user_{datetime.now().strftime('%H%M%S')}@example.com",
                "password": "NewTestPass456!"
            }
            success, login_response = self.run_test("Login with new password", "POST", "/api/auth/login", 200, login_data)
            
            if success and 'token' in login_response:
                self.token = login_response['token']  # Update token
                self.log("✅ Login with new password successful")
        else:
            self.log("⚠️  Password change failed - continuing with original password")
        
        return True

    def test_admin_login(self):
        """Test admin login with default credentials"""
        self.log("=== ADMIN LOGIN TESTS ===")
        
        # Test admin login with default credentials
        admin_login_data = {
            "email": "admin@littlehelper.ai",
            "password": "admin123"
        }
        success, response = self.run_test("Admin login", "POST", "/api/auth/login", 200, admin_login_data)
        
        if success and 'token' in response:
            admin_token = response['token']
            admin_user = response['user']
            self.log(f"✅ Admin login successful, role: {admin_user.get('role')}")
            
            # Verify admin role
            if admin_user.get('role') == 'admin':
                self.log("✅ Admin role verified")
                return admin_token
            else:
                self.log(f"❌ Expected admin role, got: {admin_user.get('role')}")
                return None
        else:
            self.log("❌ Admin login failed")
            return None

    def test_admin_comprehensive(self):
        """Test comprehensive admin functionality"""
        self.log("=== COMPREHENSIVE ADMIN TESTS ===")
        
        # Test 1: Admin login with specified credentials
        admin_login_data = {
            "email": "admin@littlehelper.ai",
            "password": "adminpassword"
        }
        success, response = self.run_test("Admin login (adminpassword)", "POST", "/api/auth/login", 200, admin_login_data)
        
        admin_token = None
        if success and 'token' in response:
            admin_token = response['token']
            admin_user = response['user']
            self.log(f"✅ Admin login successful with adminpassword, role: {admin_user.get('role')}")
        else:
            # Try alternative admin password
            admin_login_data_alt = {
                "email": "admin@littlehelper.ai",
                "password": "admin123"
            }
            success, response = self.run_test("Admin login (admin123)", "POST", "/api/auth/login", 200, admin_login_data_alt)
            
            if success and 'token' in response:
                admin_token = response['token']
                admin_user = response['user']
                self.log(f"✅ Admin login successful with admin123, role: {admin_user.get('role')}")
        
        if not admin_token:
            self.log("❌ Cannot test admin endpoints without admin token")
            return False
        
        # Store original token and set admin token
        original_token = self.token
        self.token = admin_token
        
        try:
            # Test 2: Admin users list
            success, users_response = self.run_test("Get admin users list", "GET", "/api/admin/users", 200)
            if success and isinstance(users_response, list):
                self.log(f"✅ Found {len(users_response)} users in system")
                for user in users_response[:3]:  # Show first 3 users
                    if isinstance(user, dict):
                        email = user.get('email', 'unknown')
                        role = user.get('role', 'user')
                        credits = user.get('credits', 0)
                        self.log(f"   User: {email} (role: {role}, credits: {credits})")
            
            # Test 3: AI settings
            success, ai_settings_response = self.run_test("Get AI settings", "GET", "/api/admin/ai-settings", 200)
            if success and isinstance(ai_settings_response, dict):
                emergent_enabled = ai_settings_response.get('emergent_llm_enabled', False)
                emergent_configured = ai_settings_response.get('emergent_key_configured', False)
                self.log(f"✅ AI settings: emergent_enabled={emergent_enabled}, key_configured={emergent_configured}")
            
            # Test additional admin endpoints
            self.run_test("Get admin stats", "GET", "/api/admin/stats", 200)
            self.run_test("Get system health", "GET", "/api/admin/system-health", 200)
            self.run_test("Get admin settings", "GET", "/api/admin/settings", 200)
            self.run_test("Get running jobs", "GET", "/api/admin/running-jobs", 200)
            
            # Test IP records (for security monitoring)
            success, ip_records_response = self.run_test("Get IP records", "GET", "/api/admin/ip-records", 200)
            if success and isinstance(ip_records_response, dict):
                records = ip_records_response.get('records', [])
                ip_summary = ip_records_response.get('ip_summary', [])
                self.log(f"✅ IP tracking: {len(records)} recent records, {len(ip_summary)} unique IPs")
            
            return True
            
        finally:
            # Restore original token
            self.token = original_token

    def test_jobs_system(self):
        """Test multi-agent jobs system"""
        self.log("=== JOBS SYSTEM TESTS ===")
        
        if not self.token or not self.project_id:
            self.log("❌ No authentication token or project ID, skipping jobs tests")
            return False
        
        # Test create job with task breakdown
        job_data = {
            "project_id": self.project_id,
            "prompt": "Create a Python calculator with basic arithmetic operations",
            "multi_agent_mode": True
        }
        success, response = self.run_test("Create job", "POST", "/api/jobs/create", 200, job_data)
        
        job_id = None
        if success and 'id' in response:
            job_id = response['id']
            self.log(f"✅ Job created with ID: {job_id}")
            
            # Check if job has task breakdown
            if 'tasks' in response and isinstance(response['tasks'], list):
                self.log(f"✅ Job has {len(response['tasks'])} tasks")
                for i, task in enumerate(response['tasks']):
                    if isinstance(task, dict):
                        agent_type = task.get('agent_type', 'unknown')
                        title = task.get('title', 'No title')
                        self.log(f"   Task {i+1}: [{agent_type}] {title}")
            
            # Check credit estimation
            if 'total_estimated_credits' in response:
                credits = response['total_estimated_credits']
                self.log(f"✅ Job has credit estimate: {credits}")
            
            # Test job approval
            approval_data = {"job_id": job_id, "approved": True}
            success, approval_response = self.run_test("Approve job", "POST", f"/api/jobs/{job_id}/approve", 200, approval_data)
            
            if success:
                self.log("✅ Job approved successfully")
            
            # Test get job details
            success, job_details = self.run_test("Get job details", "GET", f"/api/jobs/{job_id}", 200)
            
            if success:
                status = job_details.get('status', 'unknown')
                self.log(f"✅ Job status: {status}")
        
        # Test get user jobs
        self.run_test("Get user jobs", "GET", "/api/jobs", 200)
        
        return True

    def test_credits(self):
        """Test credits endpoints"""
        self.log("=== CREDITS TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping credits tests")
            return False
        
        # Test get credit packages
        self.run_test("Get credit packages", "GET", "/api/credits/packages", 200)
        
        # Test get credit balance
        success, balance_response = self.run_test("Get credit balance", "GET", "/api/credits/balance", 200)
        
        if success and isinstance(balance_response, dict):
            credits = balance_response.get('credits', 0)
            self.log(f"✅ User has {credits} credits")
            if credits >= 100:
                self.log("✅ User has initial free credits (100+)")
            else:
                self.log(f"⚠️  User has {credits} credits, expected 100+ initial free credits")
        
        return True

    def test_conversations(self):
        """Test conversations endpoints"""
        self.log("=== CONVERSATIONS TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping conversations tests")
            return False
        
        # Test get conversations (initially empty)
        success, conversations = self.run_test("Get conversations", "GET", "/api/conversations", 200)
        
        if success:
            self.log(f"✅ Found {len(conversations) if isinstance(conversations, list) else 0} conversations")
        
        # Test create conversation
        conversation_data = {"title": "Test Conversation"}
        success, response = self.run_test("Create conversation", "POST", "/api/conversations", 200, conversation_data)
        
        conversation_id = None
        if success and 'id' in response:
            conversation_id = response['id']
            self.log(f"✅ Conversation created with ID: {conversation_id}")
            
            # Test get conversation messages (empty initially)
            self.run_test("Get conversation messages", "GET", f"/api/conversations/{conversation_id}/messages", 200)
            
            # Test delete conversation
            self.run_test("Delete conversation", "DELETE", f"/api/conversations/{conversation_id}", 200)
        
        return True
    def test_global_assistant(self):
        """Test global assistant chat endpoints"""
        self.log("=== GLOBAL ASSISTANT TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping global assistant tests")
            return False
        
        # Test get global chat history (empty initially)
        self.run_test("Get global chat history", "GET", "/api/assistant/chat", 200)
        
        # Test send global chat message
        chat_data = {
            "message": "Hello, I need help with coding. Can you assist me?",
        }
        success, response = self.run_test("Send global chat message", "POST", "/api/assistant/chat", 200, chat_data)
        
        if success:
            self.log("✅ Global chat message sent successfully")
            if 'conversation_id' in response:
                conversation_id = response['conversation_id']
                self.log(f"✅ Conversation ID returned: {conversation_id}")
        
        # Test get global chat history again (should have messages now)
        success, history_response = self.run_test("Get global chat history (with data)", "GET", "/api/assistant/chat", 200)
        if success and isinstance(history_response, list):
            self.log(f"✅ Found {len(history_response)} messages in global chat history")
        
        return True

    def test_ai_providers(self):
        """Test AI providers endpoints"""
        self.log("=== AI PROVIDERS TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping AI providers tests")
            return False
        
        # Test get available AI providers
        success, providers_response = self.run_test("Get available AI providers", "GET", "/api/ai-providers", 200)
        if success and isinstance(providers_response, dict):
            self.log(f"✅ Found {len(providers_response)} available AI providers")
            for provider_id, config in providers_response.items():
                available = "available" if config.get('available') else f"requires {config.get('requires_plan', 'unknown')} plan"
                self.log(f"   {config.get('name', provider_id)}: {available}")
        
        # Test get user AI providers (should be empty initially)
        self.run_test("Get user AI providers", "GET", "/api/ai-providers/user", 200)
        
        return True

    def test_knowledge_caching(self):
        """Test knowledge base caching functionality"""
        self.log("=== KNOWLEDGE CACHING TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping knowledge caching tests")
            return False
        
        # Send the same question twice to test caching
        test_question = "What is Python programming language?"
        
        chat_data = {
            "message": test_question,
        }
        
        # First request - should generate fresh response
        success1, response1 = self.run_test("First knowledge request", "POST", "/api/assistant/chat", 200, chat_data)
        
        if success1:
            from_cache1 = response1.get('from_cache', False)
            self.log(f"   First request from cache: {from_cache1}")
        
        # Second request - should come from cache
        success2, response2 = self.run_test("Second knowledge request (cached)", "POST", "/api/assistant/chat", 200, chat_data)
        
        if success2:
            from_cache2 = response2.get('from_cache', False)
            self.log(f"   Second request from cache: {from_cache2}")
            
            if from_cache2:
                self.log("✅ Knowledge caching is working correctly")
            else:
                self.log("⚠️  Knowledge caching may not be working as expected")
        
        return True

    def test_llm(self):
        """Test LLM endpoints"""
        self.log("=== LLM TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping LLM tests")
            return False
        
        # Test LLM generation (might fail if local LLM not available)
        llm_data = {
            "prompt": "Write a simple Python hello world function",
            "model": "local",
            "max_tokens": 100,
            "temperature": 0.7
        }
        success, response = self.run_test("Generate LLM response", "POST", "/api/llm/generate", 200, llm_data)
        
        if not success:
            self.log("⚠️  LLM generation failed - this is expected if local LLM is not available")
        
        return True

    def test_subscription_plans(self):
        """Test subscription plan system endpoints"""
        self.log("=== SUBSCRIPTION PLANS TESTS ===")
        
        # Test get all active plans (public endpoint)
        success, plans_response = self.run_test("Get active plans", "GET", "/api/plans", 200)
        
        if success and isinstance(plans_response, list):
            self.log(f"✅ Found {len(plans_response)} active plans")
            
            # Verify expected default plans exist
            expected_plans = ["free", "starter", "pro", "openai", "enterprise"]
            expected_workspaces = {"free": 1, "starter": 3, "pro": 10, "openai": 5, "enterprise": 50}
            
            found_plans = {}
            for plan in plans_response:
                if isinstance(plan, dict) and 'plan_id' in plan:
                    plan_id = plan['plan_id']
                    found_plans[plan_id] = plan
                    workspaces = plan.get('max_concurrent_workspaces', 0)
                    self.log(f"   Plan '{plan_id}': {workspaces} max workspaces")
            
            # Verify all expected plans exist with correct workspace limits
            for plan_id in expected_plans:
                if plan_id in found_plans:
                    plan = found_plans[plan_id]
                    expected_workspaces_count = expected_workspaces[plan_id]
                    actual_workspaces = plan.get('max_concurrent_workspaces', 0)
                    
                    if actual_workspaces == expected_workspaces_count:
                        self.log(f"   ✅ {plan_id} plan: {actual_workspaces} workspaces (correct)")
                    else:
                        self.log(f"   ❌ {plan_id} plan: expected {expected_workspaces_count}, got {actual_workspaces}")
                else:
                    self.log(f"   ❌ Missing expected plan: {plan_id}")
        
        # Get admin token for admin-only endpoints
        admin_token = self.test_admin_login()
        if not admin_token:
            self.log("❌ Cannot test admin plan endpoints without admin token")
            return False
        
        # Store original token and set admin token
        original_token = self.token
        self.token = admin_token
        
        try:
            # Test get all plans including inactive (admin only)
            success, all_plans_response = self.run_test("Get all plans (admin)", "GET", "/api/plans/all", 200)
            
            if success and isinstance(all_plans_response, list):
                self.log(f"✅ Admin found {len(all_plans_response)} total plans (including inactive)")
            
            # Test create new custom plan
            custom_plan_data = {
                "plan_id": "test_custom_plan",
                "name": "Test Custom Plan",
                "price_monthly": 19.99,
                "daily_credits": 150,
                "features": ["Custom feature 1", "Custom feature 2"],
                "max_projects": 15,
                "max_concurrent_workspaces": 8,
                "allows_own_api_keys": True,
                "api_key_required": None
            }
            success, create_response = self.run_test("Create custom plan", "POST", "/api/admin/plans", 200, custom_plan_data)
            
            custom_plan_created = False
            if success:
                self.log("✅ Custom plan created successfully")
                custom_plan_created = True
                
                # Test update the custom plan
                update_data = {
                    "daily_credits": 200,
                    "price_monthly": 24.99
                }
                success, update_response = self.run_test("Update custom plan", "PUT", "/api/admin/plans/test_custom_plan", 200, update_data)
                
                if success:
                    self.log("✅ Custom plan updated successfully")
                
                # Test delete/deactivate the custom plan
                success, delete_response = self.run_test("Delete custom plan", "DELETE", "/api/admin/plans/test_custom_plan", 200)
                
                if success:
                    self.log("✅ Custom plan deactivated successfully")
            
            # Test that default plans cannot be deleted
            success, delete_default_response = self.run_test("Try delete default plan (should fail)", "DELETE", "/api/admin/plans/free", 400)
            
            if success:
                self.log("✅ Default plan deletion correctly blocked")
            else:
                self.log("❌ Default plan deletion should be blocked but wasn't")
            
            # Test daily credit distribution
            success, distribute_response = self.run_test("Distribute daily credits", "POST", "/api/admin/distribute-daily-credits", 200)
            
            if success:
                self.log("✅ Daily credit distribution completed")
                if isinstance(distribute_response, dict):
                    distributed_count = distribute_response.get('distributed_count', 0)
                    self.log(f"   Distributed credits to {distributed_count} users")
            
        finally:
            # Restore original token
            self.token = original_token
        
        return True

    def test_user_subscription(self):
        """Test user subscription endpoints"""
        self.log("=== USER SUBSCRIPTION TESTS ===")
        
        if not self.token:
            self.log("❌ No authentication token, skipping user subscription tests")
            return False
        
        # Test get user subscription details
        success, subscription_response = self.run_test("Get user subscription", "GET", "/api/user/subscription", 200)
        
        if success and isinstance(subscription_response, dict):
            plan = subscription_response.get('plan', 'unknown')
            active_workspaces = subscription_response.get('active_workspaces', 0)
            can_start_workspace = subscription_response.get('can_start_workspace', False)
            self.log(f"✅ User subscription: plan={plan}, active_workspaces={active_workspaces}, can_start={can_start_workspace}")
        
        # Test workspace limit check
        success, workspace_limit_response = self.run_test("Check workspace limit", "GET", "/api/user/workspace-limit", 200)
        
        if success and isinstance(workspace_limit_response, dict):
            can_start = workspace_limit_response.get('can_start_workspace', False)
            message = workspace_limit_response.get('message', 'No message')
            self.log(f"✅ Workspace limit check: can_start={can_start}, message='{message}'")
        
        # Test subscribe to free plan (should work immediately)
        subscribe_data = {
            "plan_id": "free",
            "origin_url": "https://codehelper-ai-4.preview.emergentagent.com"
        }
        success, subscribe_response = self.run_test("Subscribe to free plan", "POST", "/api/user/subscribe", 200, subscribe_data)
        
        if success:
            self.log("✅ Free plan subscription successful")
            if isinstance(subscribe_response, dict):
                status = subscribe_response.get('status', 'unknown')
                self.log(f"   Subscription status: {status}")
        
        # Test subscribe to paid plan (should return Stripe checkout URL or similar)
        paid_subscribe_data = {
            "plan_id": "starter",
            "origin_url": "https://codehelper-ai-4.preview.emergentagent.com"
        }
        success, paid_subscribe_response = self.run_test("Subscribe to paid plan", "POST", "/api/user/subscribe", 200, paid_subscribe_data)
        
        if success:
            self.log("✅ Paid plan subscription initiated")
            if isinstance(paid_subscribe_response, dict):
                if 'checkout_url' in paid_subscribe_response or 'url' in paid_subscribe_response:
                    self.log("   ✅ Stripe checkout URL returned")
                else:
                    self.log("   ⚠️  No checkout URL in response")
        
        return True

    def cleanup(self):
        """Clean up test data"""
        self.log("=== CLEANUP ===")
        
        if self.project_id:
            success, _ = self.run_test("Delete test project", "DELETE", f"/api/projects/{self.project_id}", 200)
            if success:
                self.log("✅ Test project deleted")

    def run_all_tests(self):
        """Run all test suites"""
        self.log("Starting LittleHelper AI Enhanced API Tests")
        self.log(f"Base URL: {self.base_url}")
        
        try:
            # Run test suites in the order specified in the review request
            self.test_health_check()
            
            # 1. Authentication & TOS Tests
            if self.test_authentication_and_tos():
                # 2. AI Building System Tests (requires project)
                if self.test_projects():
                    self.test_ai_building_system()
                    
                    # 3. File Management Tests
                    self.test_file_management()
                
                # 4. User Profile Tests
                self.test_user_profile()
                
                # Additional comprehensive tests
                self.test_build_and_run()
                self.test_chat()
                self.test_jobs_system()
                self.test_conversations()
                self.test_global_assistant()
                self.test_knowledge_caching()
                self.test_agents()
                self.test_credits()
                self.test_ai_providers()
                self.test_subscription_plans()
                self.test_user_subscription()
                self.test_llm()
                
                # Cleanup
                self.cleanup()
            
            # 5. Admin Tests (separate from user tests)
            self.test_admin_comprehensive()
            
            # Print summary
            self.log("=== TEST SUMMARY ===")
            self.log(f"Tests run: {self.tests_run}")
            self.log(f"Tests passed: {self.tests_passed}")
            self.log(f"Tests failed: {self.tests_run - self.tests_passed}")
            self.log(f"Success rate: {(self.tests_passed/self.tests_run*100):.1f}%")
            
            if self.failed_tests:
                self.log("=== FAILED TESTS ===")
                for test in self.failed_tests:
                    error_msg = test.get('error', f"Expected {test.get('expected')}, got {test.get('actual')}")
                    self.log(f"❌ {test['name']}: {error_msg}")
            
            return self.tests_passed == self.tests_run
            
        except Exception as e:
            self.log(f"❌ Test suite failed with error: {str(e)}", "ERROR")
            return False

    def test_ai_build_flow_fix(self):
        """Test the AI build flow fix - verify no raw JSON in chat responses"""
        self.log("=== AI BUILD FLOW FIX TESTS ===")
        
        # Step 1: Login as admin
        admin_login_data = {
            "email": "admin@littlehelper.ai",
            "password": "admin123"
        }
        success, response = self.run_test("Admin login for AI build flow test", "POST", "/api/auth/login", 200, admin_login_data)
        
        if not success or 'token' not in response:
            self.log("❌ Admin login failed, cannot continue with AI build flow tests")
            return False
        
        # Store admin token
        admin_token = response['token']
        admin_user = response['user']
        self.log(f"✅ Admin login successful, credits: {admin_user.get('credits', 0)}")
        
        # Store original token and set admin token
        original_token = self.token
        self.token = admin_token
        
        try:
            # Step 2: Create a new project
            project_data = {
                "name": "Test AI Build",
                "description": "Testing AI build flow fix",
                "language": "Python"
            }
            success, project_response = self.run_test("Create Test AI Build project", "POST", "/api/projects", 200, project_data)
            
            if not success or 'id' not in project_response:
                self.log("❌ Project creation failed")
                return False
            
            test_project_id = project_response['id']
            self.log(f"✅ Test project created with ID: {test_project_id}")
            
            # Step 3: Test AI Plan endpoint
            plan_data = {
                "project_id": test_project_id,
                "request": "Create a simple hello world script"
            }
            success, plan_response = self.run_test("AI Plan endpoint", "POST", "/api/ai/plan", 200, plan_data)
            
            if success:
                self.log("✅ AI Plan endpoint working")
                
                # Verify response contains tasks array with formatted descriptions
                if 'tasks' in plan_response and isinstance(plan_response['tasks'], list):
                    tasks = plan_response['tasks']
                    self.log(f"✅ Plan contains {len(tasks)} tasks with formatted descriptions")
                    
                    # Check that tasks have proper structure (not raw JSON)
                    for i, task in enumerate(tasks):
                        if isinstance(task, dict):
                            description = task.get('description', '')
                            agent = task.get('agent', '')
                            self.log(f"   Task {i+1}: [{agent}] {description[:60]}...")
                            
                            # Verify no raw JSON indicators in task descriptions
                            raw_json_indicators = ['"estimated_tokens":', '"deliverables":', '"task_breakdown":']
                            has_raw_json = any(indicator in description for indicator in raw_json_indicators)
                            if has_raw_json:
                                self.log(f"❌ Task {i+1} contains raw JSON indicators")
                                return False
                    
                    self.log("✅ All tasks have clean, formatted descriptions (no raw JSON)")
                else:
                    self.log("❌ Plan response missing tasks array")
                    return False
            else:
                self.log("❌ AI Plan endpoint failed")
                return False
            
            # Step 4: Test AI Execute Task endpoint
            execute_data = {
                "project_id": test_project_id,
                "task": "Create main.py with Hello World greeting functionality",
                "agent": "developer"
            }
            success, execute_response = self.run_test("AI Execute Task endpoint", "POST", "/api/ai/execute-task", 200, execute_data)
            
            if success:
                self.log("✅ AI Execute Task endpoint working")
                
                # Verify response contains message field (not raw code/JSON)
                if 'message' in execute_response:
                    message = execute_response['message']
                    self.log(f"✅ Execute response contains formatted message: {message[:100]}...")
                    
                    # Check for raw JSON indicators
                    raw_json_indicators = ['"estimated_tokens":', '"deliverables":', '"code_blocks":']
                    has_raw_json = any(indicator in message for indicator in raw_json_indicators)
                    if has_raw_json:
                        self.log("❌ Execute response contains raw JSON indicators")
                        return False
                    else:
                        self.log("✅ Execute response has clean, user-friendly message")
                
                # Verify response contains files array if files were created
                if 'files' in execute_response and isinstance(execute_response['files'], list):
                    files = execute_response['files']
                    self.log(f"✅ Execute response contains {len(files)} files")
                else:
                    self.log("⚠️  Execute response missing files array (may be expected)")
            else:
                self.log("❌ AI Execute Task endpoint failed")
                return False
            
            # Step 5: Test Chat endpoint with Multi-Agent Mode
            chat_data = {
                "message": "Create a simple hello world script",
                "multi_agent_mode": True,
                "agents_enabled": ["planner", "developer", "verifier"]
            }
            success, chat_response = self.run_test("Chat with Multi-Agent Mode", "POST", f"/api/projects/{test_project_id}/chat", 200, chat_data)
            
            if success:
                self.log("✅ Multi-Agent Chat endpoint working")
                
                # Verify ai_message.content does NOT contain raw JSON
                if 'ai_message' in chat_response and isinstance(chat_response['ai_message'], dict):
                    ai_message = chat_response['ai_message']
                    content = ai_message.get('content', '')
                    
                    self.log(f"✅ AI message content preview: {content[:150]}...")
                    
                    # Check for raw JSON indicators that should NOT be present
                    raw_json_indicators = ['"estimated_tokens":', '"deliverables":', '"task_breakdown":', '"agent_assignments":']
                    has_raw_json = any(indicator in content for indicator in raw_json_indicators)
                    
                    if has_raw_json:
                        self.log("❌ AI message content contains raw JSON indicators - FIX NOT WORKING")
                        self.log(f"   Raw content: {content[:300]}...")
                        return False
                    else:
                        self.log("✅ AI message content is clean and user-friendly - FIX WORKING")
                        
                        # Check if it contains user-friendly task information
                        user_friendly_indicators = ["Task", "Created", "Status:", "tasks"]
                        has_user_friendly = any(indicator in content for indicator in user_friendly_indicators)
                        if has_user_friendly:
                            self.log("✅ AI message contains user-friendly formatted task information")
                        else:
                            self.log("⚠️  AI message may be too brief, but no raw JSON detected")
                else:
                    self.log("❌ Chat response missing ai_message field")
                    return False
            else:
                self.log("❌ Multi-Agent Chat endpoint failed")
                return False
            
            # Clean up test project
            success, _ = self.run_test("Delete test AI build project", "DELETE", f"/api/projects/{test_project_id}", 200)
            if success:
                self.log("✅ Test project cleaned up")
            
            self.log("✅ AI BUILD FLOW FIX TESTS COMPLETED SUCCESSFULLY")
            return True
            
        finally:
            # Restore original token
            self.token = original_token

def main():
    """Main test runner"""
    tester = LittleHelperAPITester()
    
    # Run the specific AI build flow fix test as requested
    success = tester.test_ai_build_flow_fix()
    
    if success:
        tester.log("=== AI BUILD FLOW FIX TEST SUMMARY ===")
        tester.log("✅ All AI build flow fix tests passed")
        tester.log("✅ Raw JSON display issue has been resolved")
        tester.log("✅ Chat now shows user-friendly formatted messages")
    else:
        tester.log("=== AI BUILD FLOW FIX TEST SUMMARY ===")
        tester.log("❌ AI build flow fix tests failed")
        tester.log("❌ Raw JSON may still be displayed in chat")
    
    return 0 if success else 1

if __name__ == "__main__":
    sys.exit(main())