#!/usr/bin/env python3

import requests
import sys
import json
from datetime import datetime
from typing import Dict, Any, Optional

class LittleHelperWorkflowTester:
    def __init__(self, base_url: str = "https://codehelper-ai-4.preview.emergentagent.com"):
        self.base_url = base_url
        self.token = None
        self.project_id = None
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
                self.log(f"   Response: {response.text[:300]}")
                self.failed_tests.append({
                    'name': name,
                    'expected': expected_status,
                    'actual': response.status_code,
                    'response': response.text[:300]
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

    def test_complete_workflow(self):
        """Test the complete LittleHelper AI workflow as specified in review request"""
        self.log("=== LITTLEHELPER AI COMPLETE WORKFLOW TEST ===")
        
        # Step 1: AUTHENTICATION - Login as admin
        self.log("Step 1: AUTHENTICATION")
        admin_login_data = {
            "email": "admin@littlehelper.ai",
            "password": "admin123"
        }
        success, response = self.run_test("Admin login", "POST", "/api/auth/login", 200, admin_login_data)
        
        if not success or 'token' not in response:
            self.log("❌ Admin login failed - cannot continue workflow")
            return False
        
        self.token = response['token']
        admin_user = response['user']
        self.log(f"✅ Admin login successful, role: {admin_user.get('role')}, credits: {admin_user.get('credits')}")
        
        # Step 2: KNOWLEDGE BASE (Critical test)
        self.log("Step 2: KNOWLEDGE BASE")
        success, kb_response = self.run_test("Get knowledge base", "GET", "/api/admin/knowledge-base?limit=50", 200)
        
        if success:
            if isinstance(kb_response, list):
                self.log(f"✅ Knowledge base returned {len(kb_response)} entries")
                # Check if entries contain conversation data
                if kb_response and isinstance(kb_response[0], dict):
                    first_entry = kb_response[0]
                    if 'conversation_id' in first_entry or 'content' in first_entry or 'role' in first_entry:
                        self.log("✅ Knowledge base contains conversation data")
                    else:
                        self.log("⚠️  Knowledge base entries may not contain expected conversation data")
            else:
                self.log("⚠️  Knowledge base response is not a list")
        else:
            self.log("❌ Knowledge base endpoint failed - this is critical")
        
        # Step 3: CREATE & BUILD PROJECT
        self.log("Step 3: CREATE & BUILD PROJECT")
        project_data = {
            "name": "Hello Python",
            "description": "A simple Python project for testing",
            "language": "Python"
        }
        success, project_response = self.run_test("Create project", "POST", "/api/projects", 200, project_data)
        
        if not success or 'id' not in project_response:
            self.log("❌ Project creation failed - cannot continue workflow")
            return False
        
        self.project_id = project_response['id']
        self.log(f"✅ Project 'Hello Python' created with ID: {self.project_id}")
        
        # Step 4: AI BUILD FLOW
        self.log("Step 4: AI BUILD FLOW")
        plan_data = {
            "project_id": self.project_id,
            "request": "Create a simple Python script that prints 'Hello World' and asks for user name, then greets them",
            "agents": ["planner", "researcher", "developer"]
        }
        success, plan_response = self.run_test("AI plan creation", "POST", "/api/ai/plan", 200, plan_data)
        
        if success:
            if isinstance(plan_response, dict) and 'tasks' in plan_response:
                tasks = plan_response['tasks']
                self.log(f"✅ AI plan created with {len(tasks)} tasks")
                for i, task in enumerate(tasks):
                    if isinstance(task, dict):
                        description = task.get('description', 'No description')
                        agent = task.get('agent', 'unknown')
                        self.log(f"   Task {i+1}: [{agent}] {description[:60]}...")
            else:
                self.log("⚠️  AI plan response doesn't contain expected task list")
        else:
            self.log("❌ AI plan creation failed")
        
        # Step 5: EXECUTE TASK
        self.log("Step 5: EXECUTE TASK")
        execute_data = {
            "project_id": self.project_id,
            "task": "Create main.py with Hello World greeting functionality",
            "agent": "developer"
        }
        success, execute_response = self.run_test("Execute task", "POST", "/api/ai/execute-task", 200, execute_data)
        
        if success:
            self.log("✅ Task execution completed")
            if isinstance(execute_response, dict):
                if 'files_created' in execute_response:
                    files_created = execute_response['files_created']
                    self.log(f"   Files created: {len(files_created) if isinstance(files_created, list) else 0}")
                if 'content' in execute_response:
                    content_preview = execute_response['content'][:100] + "..." if len(execute_response['content']) > 100 else execute_response['content']
                    self.log(f"   Response preview: {content_preview}")
        else:
            self.log("❌ Task execution failed")
        
        # Step 6: GET PROJECT FILES
        self.log("Step 6: GET PROJECT FILES")
        success, files_response = self.run_test("Get project files", "GET", f"/api/projects/{self.project_id}/files", 200)
        
        if success:
            if isinstance(files_response, list):
                self.log(f"✅ Project has {len(files_response)} files")
                for file_info in files_response:
                    if isinstance(file_info, dict):
                        path = file_info.get('path', 'unknown')
                        content_length = len(file_info.get('content', ''))
                        self.log(f"   File: {path} ({content_length} characters)")
                        
                        # Check if main.py was created/updated
                        if path == 'main.py':
                            content = file_info.get('content', '')
                            if 'Hello World' in content or 'hello' in content.lower():
                                self.log("   ✅ main.py contains Hello World functionality")
                            else:
                                self.log("   ⚠️  main.py may not contain expected Hello World functionality")
            else:
                self.log("⚠️  Files response is not a list")
        else:
            self.log("❌ Get project files failed")
        
        # Step 7: EXPORT PROJECT
        self.log("Step 7: EXPORT PROJECT")
        success, export_response = self.run_test("Export project", "GET", f"/api/projects/{self.project_id}/export", 200)
        
        if success:
            # Check if response is a ZIP file by checking headers or content
            if hasattr(export_response, 'headers'):
                content_type = export_response.headers.get('content-type', '')
                if 'zip' in content_type.lower():
                    self.log("✅ Project export returned ZIP file")
                else:
                    self.log(f"⚠️  Project export content-type: {content_type}")
            else:
                # For our test, we'll assume success if the endpoint responds
                self.log("✅ Project export completed successfully")
        else:
            self.log("❌ Project export failed")
        
        # Step 8: ADMIN STATS
        self.log("Step 8: ADMIN STATS")
        success, stats_response = self.run_test("Get admin stats", "GET", "/api/admin/stats", 200)
        
        if success:
            if isinstance(stats_response, dict):
                self.log("✅ Admin stats retrieved successfully")
                
                # Check for knowledge_hits field
                if 'knowledge_hits' in stats_response:
                    knowledge_hits = stats_response['knowledge_hits']
                    self.log(f"   ✅ knowledge_hits field present: {knowledge_hits}")
                    if knowledge_hits > 0:
                        self.log("   ✅ knowledge_hits > 0 as expected")
                    else:
                        self.log("   ⚠️  knowledge_hits is 0, may indicate no knowledge base usage")
                else:
                    self.log("   ❌ knowledge_hits field is missing from admin stats")
                
                # Log other interesting stats
                for key, value in stats_response.items():
                    if key != 'knowledge_hits':
                        self.log(f"   {key}: {value}")
            else:
                self.log("⚠️  Admin stats response is not a dictionary")
        else:
            self.log("❌ Admin stats retrieval failed")
        
        return True

    def cleanup(self):
        """Clean up test data"""
        self.log("=== CLEANUP ===")
        
        if self.project_id:
            success, _ = self.run_test("Delete test project", "DELETE", f"/api/projects/{self.project_id}", 200)
            if success:
                self.log("✅ Test project deleted")

    def run_workflow_test(self):
        """Run the complete workflow test"""
        self.log("Starting LittleHelper AI Complete Workflow Test")
        self.log(f"Base URL: {self.base_url}")
        
        try:
            # Run the complete workflow
            workflow_success = self.test_complete_workflow()
            
            # Cleanup
            self.cleanup()
            
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
                    if 'response' in test:
                        self.log(f"   Response: {test['response']}")
            
            # Determine overall success
            critical_failures = []
            for test in self.failed_tests:
                if any(keyword in test['name'].lower() for keyword in ['knowledge base', 'admin stats', 'login']):
                    critical_failures.append(test['name'])
            
            if critical_failures:
                self.log("=== CRITICAL FAILURES ===")
                for failure in critical_failures:
                    self.log(f"❌ CRITICAL: {failure}")
                return False
            
            return workflow_success and len(self.failed_tests) == 0
            
        except Exception as e:
            self.log(f"❌ Workflow test failed with error: {str(e)}", "ERROR")
            return False

def main():
    """Main test runner"""
    tester = LittleHelperWorkflowTester()
    success = tester.run_workflow_test()
    return 0 if success else 1

if __name__ == "__main__":
    sys.exit(main())