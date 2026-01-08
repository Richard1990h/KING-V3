#!/usr/bin/env python3

import requests
import json
import sys
from datetime import datetime

class AIBuildFlowTester:
    def __init__(self, base_url: str = "https://codehelper-ai-4.preview.emergentagent.com"):
        self.base_url = base_url
        self.session = requests.Session()
        self.session.headers.update({'Content-Type': 'application/json'})
        self.token = None
        self.project_id = None

    def log(self, message: str, level: str = "INFO"):
        timestamp = datetime.now().strftime("%H:%M:%S")
        print(f"[{timestamp}] {level}: {message}")

    def login_admin(self):
        """Login as admin"""
        self.log("=== ADMIN LOGIN ===")
        login_data = {
            "email": "admin@littlehelper.ai",
            "password": "admin123"
        }
        
        response = self.session.post(f"{self.base_url}/api/auth/login", json=login_data)
        if response.status_code == 200:
            data = response.json()
            self.token = data['token']
            self.session.headers['Authorization'] = f'Bearer {self.token}'
            self.log(f"✅ Admin login successful, credits: {data['user'].get('credits', 0)}")
            return True
        else:
            self.log(f"❌ Admin login failed: {response.status_code} - {response.text}")
            return False

    def create_project(self):
        """Create test project"""
        self.log("=== CREATE PROJECT ===")
        project_data = {
            "name": "Test AI Build",
            "description": "Testing AI build flow fix for raw JSON issue",
            "language": "Python"
        }
        
        response = self.session.post(f"{self.base_url}/api/projects", json=project_data)
        if response.status_code == 200:
            data = response.json()
            self.project_id = data['id']
            self.log(f"✅ Project created: {data['name']} (ID: {self.project_id})")
            return True
        else:
            self.log(f"❌ Project creation failed: {response.status_code} - {response.text}")
            return False

    def test_ai_plan(self):
        """Test AI Plan endpoint for formatted responses"""
        self.log("=== TEST AI PLAN ENDPOINT ===")
        plan_data = {
            "project_id": self.project_id,
            "request": "Create a simple hello world script"
        }
        
        response = self.session.post(f"{self.base_url}/api/ai/plan", json=plan_data)
        if response.status_code == 200:
            data = response.json()
            self.log("✅ AI Plan endpoint successful")
            
            # Check for tasks array
            if 'tasks' in data and isinstance(data['tasks'], list):
                tasks = data['tasks']
                self.log(f"✅ Response contains {len(tasks)} tasks")
                
                # Verify each task has proper formatting
                for i, task in enumerate(tasks):
                    if isinstance(task, dict):
                        description = task.get('description', '')
                        agent = task.get('agent', '')
                        
                        # Check for raw JSON indicators
                        raw_json_indicators = [
                            '"estimated_tokens":',
                            '"deliverables":',
                            '"task_breakdown":',
                            '"agent_assignments":',
                            '"priority":',
                            '"dependencies":'
                        ]
                        
                        has_raw_json = any(indicator in description for indicator in raw_json_indicators)
                        if has_raw_json:
                            self.log(f"❌ Task {i+1} contains raw JSON: {description[:100]}...")
                            return False
                        else:
                            self.log(f"✅ Task {i+1}: [{agent}] {description[:60]}...")
                
                self.log("✅ All tasks have clean, formatted descriptions")
                return True
            else:
                self.log("❌ Response missing tasks array")
                return False
        else:
            self.log(f"❌ AI Plan failed: {response.status_code} - {response.text}")
            return False

    def test_ai_execute_task(self):
        """Test AI Execute Task endpoint"""
        self.log("=== TEST AI EXECUTE TASK ENDPOINT ===")
        execute_data = {
            "project_id": self.project_id,
            "task": "Create main.py with Hello World greeting functionality",
            "agent": "developer"
        }
        
        response = self.session.post(f"{self.base_url}/api/ai/execute-task", json=execute_data)
        if response.status_code == 200:
            data = response.json()
            self.log("✅ AI Execute Task endpoint successful")
            
            # Check for message field
            if 'message' in data:
                message = data['message']
                self.log(f"✅ Response contains message: {message[:100]}...")
                
                # Check for raw JSON indicators
                raw_json_indicators = [
                    '"estimated_tokens":',
                    '"deliverables":',
                    '"code_blocks":',
                    '"file_operations":',
                    '"execution_steps":'
                ]
                
                has_raw_json = any(indicator in message for indicator in raw_json_indicators)
                if has_raw_json:
                    self.log(f"❌ Execute response contains raw JSON: {message[:200]}...")
                    return False
                else:
                    self.log("✅ Execute response has clean, user-friendly message")
            
            # Check for files array
            if 'files' in data:
                files = data['files']
                self.log(f"✅ Response contains files array with {len(files)} files")
            
            return True
        else:
            self.log(f"❌ AI Execute Task failed: {response.status_code} - {response.text}")
            return False

    def test_chat_multi_agent(self):
        """Test Chat endpoint with Multi-Agent Mode"""
        self.log("=== TEST CHAT WITH MULTI-AGENT MODE ===")
        chat_data = {
            "message": "Create a simple hello world script",
            "multi_agent_mode": True,
            "agents_enabled": ["planner", "developer", "verifier"]
        }
        
        response = self.session.post(f"{self.base_url}/api/projects/{self.project_id}/chat", json=chat_data)
        if response.status_code == 200:
            data = response.json()
            self.log("✅ Multi-Agent Chat endpoint successful")
            
            # Check ai_message content
            if 'ai_message' in data and isinstance(data['ai_message'], dict):
                ai_message = data['ai_message']
                content = ai_message.get('content', '')
                
                self.log(f"✅ AI message content preview: {content[:150]}...")
                
                # Check for raw JSON indicators that should NOT be present
                raw_json_indicators = [
                    '"estimated_tokens":',
                    '"deliverables":',
                    '"task_breakdown":',
                    '"agent_assignments":',
                    '"priority":',
                    '"dependencies":',
                    '"execution_plan":',
                    '"resource_requirements":'
                ]
                
                has_raw_json = any(indicator in content for indicator in raw_json_indicators)
                if has_raw_json:
                    self.log("❌ AI message content contains raw JSON - FIX NOT WORKING")
                    self.log(f"   Raw content sample: {content[:300]}...")
                    return False
                else:
                    self.log("✅ AI message content is clean and user-friendly - FIX WORKING")
                    
                    # Check for user-friendly indicators
                    user_friendly_indicators = [
                        "Created job",
                        "Task",
                        "Status:",
                        "Estimated credits:",
                        "tasks"
                    ]
                    
                    has_user_friendly = any(indicator in content for indicator in user_friendly_indicators)
                    if has_user_friendly:
                        self.log("✅ AI message contains user-friendly formatted information")
                    else:
                        self.log("⚠️  AI message may be brief, but no raw JSON detected")
                
                return True
            else:
                self.log("❌ Chat response missing ai_message field")
                return False
        else:
            self.log(f"❌ Multi-Agent Chat failed: {response.status_code} - {response.text}")
            return False

    def test_additional_chat_scenarios(self):
        """Test additional chat scenarios to ensure fix is comprehensive"""
        self.log("=== TEST ADDITIONAL CHAT SCENARIOS ===")
        
        # Test 1: Complex request that might generate more detailed responses
        complex_chat_data = {
            "message": "Create a complete web application with user authentication, database integration, and REST API endpoints",
            "multi_agent_mode": True,
            "agents_enabled": ["planner", "researcher", "developer", "test_designer", "verifier"]
        }
        
        response = self.session.post(f"{self.base_url}/api/projects/{self.project_id}/chat", json=complex_chat_data)
        if response.status_code == 200:
            data = response.json()
            if 'ai_message' in data:
                content = data['ai_message'].get('content', '')
                
                # Check for raw JSON in complex response
                raw_json_indicators = ['"estimated_tokens":', '"deliverables":', '"task_breakdown":']
                has_raw_json = any(indicator in content for indicator in raw_json_indicators)
                
                if has_raw_json:
                    self.log("❌ Complex request still generates raw JSON")
                    return False
                else:
                    self.log("✅ Complex request generates clean response")
        
        # Test 2: Single agent mode (should also be clean)
        single_agent_data = {
            "message": "Add error handling to the hello world script",
            "multi_agent_mode": False,
            "agents_enabled": ["developer"]
        }
        
        response = self.session.post(f"{self.base_url}/api/projects/{self.project_id}/chat", json=single_agent_data)
        if response.status_code == 200:
            data = response.json()
            if 'ai_message' in data:
                content = data['ai_message'].get('content', '')
                
                # Check for raw JSON in single agent response
                raw_json_indicators = ['"estimated_tokens":', '"deliverables":', '"code_blocks":']
                has_raw_json = any(indicator in content for indicator in raw_json_indicators)
                
                if has_raw_json:
                    self.log("❌ Single agent mode still generates raw JSON")
                    return False
                else:
                    self.log("✅ Single agent mode generates clean response")
        
        return True

    def cleanup(self):
        """Clean up test project"""
        if self.project_id:
            self.log("=== CLEANUP ===")
            response = self.session.delete(f"{self.base_url}/api/projects/{self.project_id}")
            if response.status_code == 200:
                self.log("✅ Test project deleted")
            else:
                self.log(f"⚠️  Failed to delete test project: {response.status_code}")

    def run_comprehensive_test(self):
        """Run comprehensive AI build flow fix test"""
        self.log("Starting Comprehensive AI Build Flow Fix Test")
        self.log(f"Backend URL: {self.base_url}")
        
        try:
            # Step 1: Login as admin
            if not self.login_admin():
                return False
            
            # Step 2: Create test project
            if not self.create_project():
                return False
            
            # Step 3: Test AI Plan endpoint
            if not self.test_ai_plan():
                return False
            
            # Step 4: Test AI Execute Task endpoint
            if not self.test_ai_execute_task():
                return False
            
            # Step 5: Test Chat with Multi-Agent Mode
            if not self.test_chat_multi_agent():
                return False
            
            # Step 6: Test additional scenarios
            if not self.test_additional_chat_scenarios():
                return False
            
            self.log("=== COMPREHENSIVE TEST RESULTS ===")
            self.log("✅ ALL TESTS PASSED")
            self.log("✅ AI Build Flow Fix is working correctly")
            self.log("✅ No raw JSON detected in any responses")
            self.log("✅ All responses are user-friendly and formatted")
            
            return True
            
        except Exception as e:
            self.log(f"❌ Test failed with exception: {str(e)}", "ERROR")
            return False
        
        finally:
            self.cleanup()

def main():
    tester = AIBuildFlowTester()
    success = tester.run_comprehensive_test()
    return 0 if success else 1

if __name__ == "__main__":
    sys.exit(main())