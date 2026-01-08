"""
LittleHelper AI Backend API Tests
Tests for P0 (Multi-agent build), P1 (Global Assistant), P2 (Admin Plans)
"""
import pytest
import requests
import os

BASE_URL = os.environ.get('REACT_APP_BACKEND_URL', 'https://devbuddy-hub.preview.emergentagent.com')

class TestHealthAndAgents:
    """Basic health and agent endpoint tests"""
    
    def test_health_endpoint(self):
        """Test health check endpoint"""
        response = requests.get(f"{BASE_URL}/api/health")
        assert response.status_code == 200
        data = response.json()
        assert data["status"] == "healthy"
        print(f"Health check passed: {data}")
    
    def test_agents_endpoint(self):
        """Test agents list endpoint"""
        response = requests.get(f"{BASE_URL}/api/agents")
        assert response.status_code == 200
        agents = response.json()
        assert len(agents) >= 7  # Should have at least 7 agents
        agent_ids = [a["id"] for a in agents]
        assert "planner" in agent_ids
        assert "developer" in agent_ids
        assert "verifier" in agent_ids
        print(f"Found {len(agents)} agents: {agent_ids}")


class TestAuthentication:
    """Authentication endpoint tests"""
    
    def test_login_test_user(self):
        """Test login with test user credentials"""
        response = requests.post(f"{BASE_URL}/api/auth/login", json={
            "email": "test@example.com",
            "password": "test123"
        })
        assert response.status_code == 200
        data = response.json()
        assert "token" in data
        assert "user" in data
        assert data["user"]["email"] == "test@example.com"
        print(f"Login successful for: {data['user']['email']}")
    
    def test_login_admin_user(self):
        """Test login with admin user credentials"""
        response = requests.post(f"{BASE_URL}/api/auth/login", json={
            "email": "admin@littlehelper.ai",
            "password": "admin123"
        })
        assert response.status_code == 200
        data = response.json()
        assert "token" in data
        assert data["user"]["role"] == "admin"
        print(f"Admin login successful: {data['user']['email']}")
    
    def test_login_invalid_credentials(self):
        """Test login with invalid credentials"""
        response = requests.post(f"{BASE_URL}/api/auth/login", json={
            "email": "invalid@example.com",
            "password": "wrongpassword"
        })
        assert response.status_code in [401, 400]
        print("Invalid login correctly rejected")


class TestGlobalAssistant:
    """P1 Feature: Global Assistant chat tests"""
    
    @pytest.fixture
    def auth_token(self):
        """Get authentication token"""
        response = requests.post(f"{BASE_URL}/api/auth/login", json={
            "email": "test@example.com",
            "password": "test123"
        })
        return response.json()["token"]
    
    def test_assistant_chat(self, auth_token):
        """Test assistant chat endpoint"""
        response = requests.post(
            f"{BASE_URL}/api/assistant/chat",
            headers={"Authorization": f"Bearer {auth_token}"},
            json={"message": "Hello, can you help me with Python?"}
        )
        assert response.status_code == 200
        data = response.json()
        assert "conversation_id" in data
        assert "user_message" in data
        assert "ai_message" in data
        assert data["ai_message"]["content"]  # Should have content
        print(f"Assistant response: {data['ai_message']['content'][:100]}...")
    
    def test_conversations_list(self, auth_token):
        """Test conversations list endpoint"""
        response = requests.get(
            f"{BASE_URL}/api/conversations",
            headers={"Authorization": f"Bearer {auth_token}"}
        )
        assert response.status_code == 200
        # May be empty list or list of conversations
        data = response.json()
        assert isinstance(data, list)
        print(f"Found {len(data)} conversations")


class TestMultiAgentBuild:
    """P0 Feature: Multi-agent build plan tests"""
    
    @pytest.fixture
    def auth_token(self):
        """Get authentication token"""
        response = requests.post(f"{BASE_URL}/api/auth/login", json={
            "email": "test@example.com",
            "password": "test123"
        })
        return response.json()["token"]
    
    def test_ai_plan_endpoint(self, auth_token):
        """Test AI plan generation endpoint"""
        response = requests.post(
            f"{BASE_URL}/api/ai/plan",
            headers={"Authorization": f"Bearer {auth_token}"},
            json={
                "prompt": "Build a simple calculator in Python",
                "language": "Python"
            }
        )
        assert response.status_code == 200
        data = response.json()
        assert "tasks" in data
        assert len(data["tasks"]) > 0
        # Verify task structure
        task = data["tasks"][0]
        assert "id" in task
        assert "description" in task or "title" in task
        print(f"Generated plan with {len(data['tasks'])} tasks")
    
    def test_ai_execute_task_endpoint(self, auth_token):
        """Test AI task execution endpoint"""
        response = requests.post(
            f"{BASE_URL}/api/ai/execute-task",
            headers={"Authorization": f"Bearer {auth_token}"},
            json={
                "task": "Create a hello.py file that prints Hello World",
                "agent": "developer"
            }
        )
        assert response.status_code == 200
        data = response.json()
        assert "status" in data
        assert data["status"] == "completed"
        assert "files" in data
        assert len(data["files"]) > 0
        # Verify file structure
        file = data["files"][0]
        assert "path" in file
        assert "content" in file
        print(f"Task executed, created {len(data['files'])} file(s)")


class TestAdminPlans:
    """P2 Feature: Admin Plans (Subscription Plans and Credit Packages) tests"""
    
    @pytest.fixture
    def admin_token(self):
        """Get admin authentication token"""
        response = requests.post(f"{BASE_URL}/api/auth/login", json={
            "email": "admin@littlehelper.ai",
            "password": "admin123"
        })
        return response.json()["token"]
    
    def test_get_subscription_plans(self, admin_token):
        """Test getting subscription plans"""
        response = requests.get(
            f"{BASE_URL}/api/admin/plans",
            headers={"Authorization": f"Bearer {admin_token}"}
        )
        assert response.status_code == 200
        plans = response.json()
        assert isinstance(plans, list)
        assert len(plans) >= 4  # Should have at least free, starter, pro, enterprise
        # Verify plan structure
        plan_ids = [p.get("plan_id") or p.get("id") for p in plans]
        assert "free" in plan_ids or any("free" in str(p).lower() for p in plans)
        print(f"Found {len(plans)} subscription plans")
    
    def test_get_credit_packages(self, admin_token):
        """Test getting credit packages"""
        response = requests.get(
            f"{BASE_URL}/api/admin/credit-packages",
            headers={"Authorization": f"Bearer {admin_token}"}
        )
        assert response.status_code == 200
        packages = response.json()
        assert isinstance(packages, list)
        print(f"Found {len(packages)} credit packages")
    
    def test_admin_stats(self, admin_token):
        """Test admin stats endpoint"""
        response = requests.get(
            f"{BASE_URL}/api/admin/stats",
            headers={"Authorization": f"Bearer {admin_token}"}
        )
        assert response.status_code == 200
        stats = response.json()
        assert "total_users" in stats or "totalUsers" in stats
        print(f"Admin stats: {stats}")


class TestProjects:
    """Project management tests"""
    
    @pytest.fixture
    def auth_token(self):
        """Get authentication token"""
        response = requests.post(f"{BASE_URL}/api/auth/login", json={
            "email": "test@example.com",
            "password": "test123"
        })
        return response.json()["token"]
    
    def test_list_projects(self, auth_token):
        """Test listing user projects"""
        response = requests.get(
            f"{BASE_URL}/api/projects",
            headers={"Authorization": f"Bearer {auth_token}"}
        )
        assert response.status_code == 200
        projects = response.json()
        assert isinstance(projects, list)
        print(f"Found {len(projects)} projects")
    
    def test_create_project(self, auth_token):
        """Test creating a new project"""
        response = requests.post(
            f"{BASE_URL}/api/projects",
            headers={"Authorization": f"Bearer {auth_token}"},
            json={
                "name": "TEST_API_Project",
                "description": "Test project created by API tests",
                "language": "python"
            }
        )
        assert response.status_code in [200, 201]
        data = response.json()
        assert "id" in data
        assert data["name"] == "TEST_API_Project"
        print(f"Created project: {data['id']}")
        
        # Cleanup - delete the test project
        project_id = data["id"]
        delete_response = requests.delete(
            f"{BASE_URL}/api/projects/{project_id}",
            headers={"Authorization": f"Bearer {auth_token}"}
        )
        print(f"Cleanup: deleted test project, status: {delete_response.status_code}")


if __name__ == "__main__":
    pytest.main([__file__, "-v", "--tb=short"])
