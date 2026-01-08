#!/usr/bin/env python3

import requests
import sys
import json
from datetime import datetime
from typing import Dict, Any, Optional

class Priority1FeaturesTester:
    def __init__(self, base_url: str = "https://codehelper-ai-4.preview.emergentagent.com"):
        self.base_url = base_url
        self.admin_token = None
        self.user_token = None
        self.test_user_id = None
        self.tests_run = 0
        self.tests_passed = 0
        self.failed_tests = []
        self.session = requests.Session()
        self.session.headers.update({'Content-Type': 'application/json'})

    def log(self, message: str, level: str = "INFO"):
        timestamp = datetime.now().strftime("%H:%M:%S")
        print(f"[{timestamp}] {level}: {message}")

    def run_test(self, name: str, method: str, endpoint: str, expected_status: int, 
                 data: Optional[Dict] = None, headers: Optional[Dict] = None, 
                 use_admin_token: bool = False) -> tuple[bool, Dict]:
        """Run a single API test"""
        url = f"{self.base_url}/{endpoint.lstrip('/')}"
        test_headers = self.session.headers.copy()
        if headers:
            test_headers.update(headers)
        
        # Use appropriate token
        token = self.admin_token if use_admin_token else self.user_token
        if token:
            test_headers['Authorization'] = f'Bearer {token}'

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

    def setup_authentication(self):
        """Setup admin and test user authentication"""
        self.log("=== AUTHENTICATION SETUP ===")
        
        # Admin login
        admin_login_data = {
            "email": "admin@littlehelper.ai",
            "password": "admin123"
        }
        success, response = self.run_test("Admin login", "POST", "/api/auth/login", 200, admin_login_data)
        
        if success and 'token' in response:
            self.admin_token = response['token']
            self.log("✅ Admin authentication successful")
        else:
            self.log("❌ Admin authentication failed")
            return False
        
        # Create test user for API key testing
        timestamp = datetime.now().strftime("%H%M%S")
        test_email = f"test_user_priority1_{timestamp}@example.com"
        test_password = "TestPass123!"
        test_name = f"Priority1 Test User {timestamp}"
        
        register_data = {
            "name": test_name,
            "email": test_email,
            "password": test_password
        }
        success, response = self.run_test("Test user registration", "POST", "/api/auth/register", 200, register_data)
        
        if success and 'token' in response:
            self.user_token = response['token']
            self.test_user_id = response['user']['id']
            self.log(f"✅ Test user created: {test_email}")
        else:
            self.log("❌ Test user creation failed")
            return False
        
        return True

    def test_user_profile_endpoints(self):
        """Test user profile and theme customization endpoints"""
        self.log("=== USER PROFILE & THEME TESTS ===")
        
        if not self.user_token:
            self.log("❌ No user token, skipping profile tests")
            return False
        
        # Test GET /api/user/profile
        success, profile_response = self.run_test("Get user profile", "GET", "/api/user/profile", 200)
        
        if success:
            self.log("✅ User profile retrieved successfully")
            # Verify profile includes theme settings
            if 'theme' in profile_response:
                theme = profile_response['theme']
                expected_theme_keys = ['primary_color', 'secondary_color', 'background_color', 'card_color', 'text_color']
                for key in expected_theme_keys:
                    if key in theme:
                        self.log(f"   ✅ Theme has {key}: {theme[key]}")
                    else:
                        self.log(f"   ❌ Theme missing {key}")
            else:
                self.log("   ❌ Profile missing theme settings")
        
        # Test PUT /api/user/profile - Update name, display_name, avatar_url
        profile_update_data = {
            "name": "Updated Test User",
            "display_name": "Updated Display Name",
            "avatar_url": "https://example.com/avatar.jpg"
        }
        success, update_response = self.run_test("Update user profile", "PUT", "/api/user/profile", 200, profile_update_data)
        
        if success:
            self.log("✅ User profile updated successfully")
        
        # Test PUT /api/user/theme - Update theme colors
        theme_update_data = {
            "primary_color": "#ff6b6b",
            "secondary_color": "#4ecdc4",
            "background_color": "#2c3e50",
            "card_color": "#34495e",
            "text_color": "#ecf0f1",
            "hover_color": "#e74c3c",
            "credits_color": "#f39c12"
        }
        success, theme_response = self.run_test("Update user theme", "PUT", "/api/user/theme", 200, theme_update_data)
        
        if success:
            self.log("✅ User theme updated successfully")
            # Verify theme persists by getting profile again
            success, verify_profile = self.run_test("Verify theme persistence", "GET", "/api/user/profile", 200)
            if success and 'theme' in verify_profile:
                updated_theme = verify_profile['theme']
                if updated_theme.get('primary_color') == "#ff6b6b":
                    self.log("   ✅ Theme changes persisted correctly")
                else:
                    self.log("   ❌ Theme changes did not persist")
        
        return True

    def test_password_change(self):
        """Test password change functionality"""
        self.log("=== PASSWORD CHANGE TESTS ===")
        
        if not self.user_token:
            self.log("❌ No user token, skipping password tests")
            return False
        
        # Test PUT /api/user/password with correct current password
        correct_password_data = {
            "current_password": "TestPass123!",
            "new_password": "NewTestPass456!"
        }
        success, response = self.run_test("Change password (correct current)", "PUT", "/api/user/password", 200, correct_password_data)
        
        if success:
            self.log("✅ Password changed successfully with correct current password")
        
        # Test PUT /api/user/password with wrong current password (should fail)
        wrong_password_data = {
            "current_password": "WrongPassword123!",
            "new_password": "AnotherNewPass789!"
        }
        success, response = self.run_test("Change password (wrong current)", "PUT", "/api/user/password", 400, wrong_password_data)
        
        if success:
            self.log("✅ Password change correctly rejected with wrong current password")
        
        return True

    def test_admin_ai_settings(self):
        """Test admin AI settings endpoints"""
        self.log("=== ADMIN AI SETTINGS TESTS ===")
        
        if not self.admin_token:
            self.log("❌ No admin token, skipping AI settings tests")
            return False
        
        # Test GET /api/admin/ai-settings
        success, ai_settings_response = self.run_test("Get AI settings", "GET", "/api/admin/ai-settings", 200, use_admin_token=True)
        
        if success:
            self.log("✅ AI settings retrieved successfully")
            emergent_enabled = ai_settings_response.get('emergent_llm_enabled', False)
            emergent_key_configured = ai_settings_response.get('emergent_key_configured', False)
            self.log(f"   Emergent LLM enabled: {emergent_enabled}")
            self.log(f"   Emergent key configured: {emergent_key_configured}")
        
        # Test PUT /api/admin/ai-settings/emergent-toggle?enabled=false - Toggle off
        success, toggle_off_response = self.run_test("Toggle Emergent LLM off", "PUT", "/api/admin/ai-settings/emergent-toggle?enabled=false", 200, use_admin_token=True)
        
        if success:
            self.log("✅ Emergent LLM toggled off successfully")
            if toggle_off_response.get('enabled') == False:
                self.log("   ✅ Toggle response confirms disabled state")
        
        # Test PUT /api/admin/ai-settings/emergent-toggle?enabled=true - Toggle back on
        success, toggle_on_response = self.run_test("Toggle Emergent LLM on", "PUT", "/api/admin/ai-settings/emergent-toggle?enabled=true", 200, use_admin_token=True)
        
        if success:
            self.log("✅ Emergent LLM toggled on successfully")
            if toggle_on_response.get('enabled') == True:
                self.log("   ✅ Toggle response confirms enabled state")
        
        # Verify final state
        success, final_settings = self.run_test("Verify final AI settings", "GET", "/api/admin/ai-settings", 200, use_admin_token=True)
        
        if success:
            final_enabled = final_settings.get('emergent_llm_enabled', False)
            if final_enabled:
                self.log("   ✅ Emergent LLM is enabled after toggle test")
            else:
                self.log("   ❌ Emergent LLM should be enabled but isn't")
        
        return True

    def test_ip_tracking(self):
        """Test IP tracking endpoints"""
        self.log("=== IP TRACKING TESTS ===")
        
        if not self.admin_token:
            self.log("❌ No admin token, skipping IP tracking tests")
            return False
        
        # Test GET /api/admin/ip-records
        success, ip_records_response = self.run_test("Get IP records", "GET", "/api/admin/ip-records", 200, use_admin_token=True)
        
        if success:
            self.log("✅ IP records retrieved successfully")
            
            if 'records' in ip_records_response:
                records = ip_records_response['records']
                self.log(f"   Found {len(records)} IP records")
                
                # Check for login records
                login_records = [r for r in records if r.get('action') == 'login']
                register_records = [r for r in records if r.get('action') == 'register']
                
                self.log(f"   Login records: {len(login_records)}")
                self.log(f"   Registration records: {len(register_records)}")
                
                if login_records:
                    self.log("   ✅ Login IP tracking is working")
                if register_records:
                    self.log("   ✅ Registration IP tracking is working")
            
            if 'ip_summary' in ip_records_response:
                ip_summary = ip_records_response['ip_summary']
                self.log(f"   IP summary shows {len(ip_summary)} unique IPs")
                
                if ip_summary:
                    total_unique_users = sum(ip.get('unique_users', 0) for ip in ip_summary)
                    self.log(f"   ✅ Total unique users across IPs: {total_unique_users}")
        
        return True

    def test_credit_addons(self):
        """Test credit add-on purchase endpoints"""
        self.log("=== CREDIT ADD-ONS TESTS ===")
        
        if not self.user_token:
            self.log("❌ No user token, skipping credit add-on tests")
            return False
        
        # Test GET /api/credits/packages
        success, packages_response = self.run_test("Get credit packages", "GET", "/api/credits/packages", 200)
        
        if success:
            self.log("✅ Credit packages retrieved successfully")
            
            if isinstance(packages_response, list):
                self.log(f"   Found {len(packages_response)} credit packages")
                
                # Look for add-on packages
                addon_packages = [p for p in packages_response if p.get('is_addon', False)]
                self.log(f"   Add-on packages: {len(addon_packages)}")
                
                for package in packages_response[:3]:  # Show first 3 packages
                    if isinstance(package, dict):
                        name = package.get('name', 'Unknown')
                        price = package.get('price', 0)
                        credits = package.get('credits', 0)
                        self.log(f"   Package: {name} - ${price} for {credits} credits")
        
        # Test POST /api/credits/purchase-addon
        # Use the correct package ID from the API response
        addon_purchase_data = {
            "package_id": "starter",  # Correct package ID
            "origin_url": "https://codehelper-ai-4.preview.emergentagent.com"
        }
        success, purchase_response = self.run_test("Purchase credit add-on", "POST", "/api/credits/purchase-addon", 200, addon_purchase_data)
        
        if success:
            self.log("✅ Credit add-on purchase initiated successfully")
            
            if 'url' in purchase_response:
                self.log("   ✅ Stripe checkout URL returned")
            if 'session_id' in purchase_response:
                self.log("   ✅ Session ID returned")
        else:
            # Try with a different package ID if the first one fails
            addon_purchase_data['package_id'] = "pro"
            success, purchase_response = self.run_test("Purchase credit add-on (alt package)", "POST", "/api/credits/purchase-addon", 200, addon_purchase_data)
            
            if success:
                self.log("✅ Credit add-on purchase with alternative package successful")
        
        return True

    def test_api_keys_plan_lock(self):
        """Test API keys plan lock functionality"""
        self.log("=== API KEYS PLAN LOCK TESTS ===")
        
        if not self.user_token or not self.test_user_id:
            self.log("❌ No user token or user ID, skipping API keys tests")
            return False
        
        # First, verify the test user is on free plan (should not allow API keys)
        success, profile_response = self.run_test("Get user profile for plan check", "GET", "/api/user/profile", 200)
        
        if success:
            user_plan = profile_response.get('plan', 'unknown')
            self.log(f"   Test user plan: {user_plan}")
        
        # Test POST /api/user/api-keys - Should return 403 for free plan users
        api_key_data = {
            "provider": "openai",
            "api_key": "sk-test-fake-api-key-for-testing",
            "model_preference": "gpt-4",
            "is_default": True
        }
        success, api_key_response = self.run_test("Add API key (free plan - should fail)", "POST", "/api/user/api-keys", 403, api_key_data)
        
        if success:
            self.log("✅ API key addition correctly blocked for free plan user")
            
            # Check if error message mentions plan upgrade
            if isinstance(api_key_response, dict):
                detail = api_key_response.get('detail', '')
                if 'plan' in detail.lower() or 'upgrade' in detail.lower():
                    self.log("   ✅ Error message mentions plan upgrade requirement")
                else:
                    self.log(f"   ⚠️  Error message: {detail}")
        
        # Test with admin user (should have a plan that allows API keys)
        if self.admin_token:
            # Switch to admin token temporarily
            original_token = self.user_token
            self.user_token = self.admin_token
            
            success, admin_api_key_response = self.run_test("Add API key (admin - should work)", "POST", "/api/user/api-keys", 200, api_key_data)
            
            if success:
                self.log("✅ API key addition works for admin user (plan allows it)")
            else:
                # Admin might also be on a plan that doesn't allow API keys, check the response
                self.log("   ℹ️  Admin user also blocked from API keys (plan restriction)")
            
            # Restore original token
            self.user_token = original_token
        
        return True

    def run_all_priority1_tests(self):
        """Run all Priority 1 feature tests"""
        self.log("Starting LittleHelper AI Priority 1 Features Tests")
        self.log(f"Base URL: {self.base_url}")
        
        try:
            # Setup authentication
            if not self.setup_authentication():
                self.log("❌ Authentication setup failed, cannot continue")
                return False
            
            # Run all test suites
            self.test_user_profile_endpoints()
            self.test_password_change()
            self.test_admin_ai_settings()
            self.test_ip_tracking()
            self.test_credit_addons()
            self.test_api_keys_plan_lock()
            
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

def main():
    """Main test runner"""
    tester = Priority1FeaturesTester()
    success = tester.run_all_priority1_tests()
    return 0 if success else 1

if __name__ == "__main__":
    sys.exit(main())