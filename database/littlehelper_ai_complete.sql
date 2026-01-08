-- =====================================================
-- LittleHelper AI - Complete Database Setup
-- Load this file into phpMyAdmin to setup/reset database
-- User: root | Password: (none)
-- =====================================================

-- Drop and recreate database for fresh start
DROP DATABASE IF EXISTS littlehelper_ai;
CREATE DATABASE littlehelper_ai CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE littlehelper_ai;

-- =====================================================
-- USERS TABLE
-- =====================================================
CREATE TABLE users (
    id VARCHAR(36) PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    display_name VARCHAR(255),
    password_hash VARCHAR(255) NOT NULL,
    role VARCHAR(20) DEFAULT 'user',
    credits DECIMAL(10,4) DEFAULT 100.0000,
    credits_enabled BOOLEAN DEFAULT TRUE,
    plan VARCHAR(50) DEFAULT 'free',
    language VARCHAR(10) DEFAULT 'en',
    avatar_url TEXT,
    registration_ip VARCHAR(45),
    last_login_ip VARCHAR(45),
    tos_accepted BOOLEAN DEFAULT FALSE,
    tos_accepted_at DATETIME,
    tos_version VARCHAR(20),
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    last_login_at DATETIME,
    INDEX idx_email (email),
    INDEX idx_role (role)
);

-- =====================================================
-- USER THEMES TABLE
-- =====================================================
CREATE TABLE user_themes (
    user_id VARCHAR(36) PRIMARY KEY,
    primary_color VARCHAR(20) DEFAULT '#d946ef',
    secondary_color VARCHAR(20) DEFAULT '#06b6d4',
    background_color VARCHAR(20) DEFAULT '#030712',
    card_color VARCHAR(20) DEFAULT '#0B0F19',
    text_color VARCHAR(20) DEFAULT '#ffffff',
    hover_color VARCHAR(20) DEFAULT '#a855f7',
    credits_color VARCHAR(20) DEFAULT '#d946ef',
    background_image TEXT,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

-- =====================================================
-- PROJECTS TABLE
-- =====================================================
CREATE TABLE projects (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    language VARCHAR(50) DEFAULT 'Python',
    status VARCHAR(20) DEFAULT 'active',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id)
);

-- =====================================================
-- PROJECT FILES TABLE
-- =====================================================
CREATE TABLE project_files (
    id VARCHAR(36) PRIMARY KEY,
    project_id VARCHAR(36) NOT NULL,
    path VARCHAR(500) NOT NULL,
    content LONGTEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    UNIQUE KEY unique_project_path (project_id, path(255)),
    INDEX idx_project_id (project_id)
);

-- =====================================================
-- TODOS TABLE
-- =====================================================
CREATE TABLE todos (
    id VARCHAR(36) PRIMARY KEY,
    project_id VARCHAR(36) NOT NULL,
    text TEXT NOT NULL,
    completed BOOLEAN DEFAULT FALSE,
    priority VARCHAR(20) DEFAULT 'medium',
    agent VARCHAR(50),
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    INDEX idx_project_id (project_id)
);

-- =====================================================
-- JOBS TABLE (Multi-Agent Pipeline)
-- =====================================================
CREATE TABLE jobs (
    id VARCHAR(36) PRIMARY KEY,
    project_id VARCHAR(36) NOT NULL,
    user_id VARCHAR(36) NOT NULL,
    prompt TEXT NOT NULL,
    status VARCHAR(30) DEFAULT 'pending',
    multi_agent_mode BOOLEAN DEFAULT TRUE,
    tasks JSON,
    total_estimated_credits DECIMAL(10,4) DEFAULT 0,
    credits_used DECIMAL(10,4) DEFAULT 0,
    credits_approved DECIMAL(10,4) DEFAULT 0,
    current_task_index INT DEFAULT -1,
    error_count INT DEFAULT 0,
    max_errors INT DEFAULT 5,
    planner_output TEXT,
    planner_metadata JSON,
    error TEXT,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    started_at DATETIME,
    completed_at DATETIME,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_project_id (project_id),
    INDEX idx_user_id (user_id),
    INDEX idx_status (status)
);

-- =====================================================
-- CHAT HISTORY TABLE
-- =====================================================
CREATE TABLE chat_history (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    project_id VARCHAR(36),
    conversation_id VARCHAR(36),
    conversation_title VARCHAR(255),
    role VARCHAR(20) NOT NULL,
    content TEXT NOT NULL,
    agent_id VARCHAR(50),
    provider VARCHAR(50),
    model VARCHAR(100),
    tokens_used INT DEFAULT 0,
    credits_deducted DECIMAL(10,4) DEFAULT 0,
    multi_agent_mode BOOLEAN DEFAULT FALSE,
    deleted_by_user BOOLEAN DEFAULT FALSE,
    is_valid BOOLEAN DEFAULT TRUE,
    invalidated_at DATETIME,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_project_id (project_id),
    INDEX idx_conversation_id (conversation_id)
);

-- =====================================================
-- USER AI PROVIDERS TABLE
-- =====================================================
CREATE TABLE user_ai_providers (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    provider VARCHAR(50) NOT NULL,
    api_key VARCHAR(500) NOT NULL,
    model_preference VARCHAR(100),
    is_active BOOLEAN DEFAULT TRUE,
    is_default BOOLEAN DEFAULT FALSE,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    UNIQUE KEY unique_user_provider (user_id, provider),
    INDEX idx_user_id (user_id)
);

-- =====================================================
-- CREDIT HISTORY TABLE
-- =====================================================
CREATE TABLE credit_history (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    delta DECIMAL(10,4) NOT NULL,
    reason VARCHAR(255) NOT NULL,
    reference_type VARCHAR(50),
    reference_id VARCHAR(36),
    balance_after DECIMAL(10,4) NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_created_at (created_at)
);

-- =====================================================
-- PAYMENT TRANSACTIONS TABLE
-- =====================================================
CREATE TABLE payment_transactions (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    session_id VARCHAR(255) NOT NULL,
    package_id VARCHAR(50) NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    currency VARCHAR(10) DEFAULT 'usd',
    credits INT NOT NULL,
    status VARCHAR(30) DEFAULT 'pending',
    payment_status VARCHAR(30) DEFAULT 'initiated',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    completed_at DATETIME,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_session_id (session_id)
);

-- =====================================================
-- SUBSCRIPTION PLANS TABLE
-- =====================================================
CREATE TABLE subscription_plans (
    id VARCHAR(36) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    price_monthly DECIMAL(10,2) DEFAULT 0,
    price_yearly DECIMAL(10,2) DEFAULT 0,
    daily_credits INT DEFAULT 0,
    max_concurrent_workspaces INT DEFAULT 1,
    allows_own_api_keys BOOLEAN DEFAULT FALSE,
    features JSON,
    is_active BOOLEAN DEFAULT TRUE,
    sort_order INT DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- =====================================================
-- USER SUBSCRIPTIONS TABLE
-- =====================================================
CREATE TABLE user_subscriptions (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    plan_id VARCHAR(36) NOT NULL,
    status VARCHAR(30) DEFAULT 'active',
    stripe_subscription_id VARCHAR(255),
    start_date DATETIME NOT NULL,
    end_date DATETIME,
    next_billing_date DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (plan_id) REFERENCES subscription_plans(id),
    INDEX idx_user_id (user_id)
);

-- =====================================================
-- CREDIT PACKAGES TABLE
-- =====================================================
CREATE TABLE credit_packages (
    id VARCHAR(36) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    credits INT NOT NULL,
    price DECIMAL(10,2) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    sort_order INT DEFAULT 0
);

-- =====================================================
-- FREE AI PROVIDERS TABLE (Admin Configured)
-- =====================================================
CREATE TABLE free_ai_providers (
    id VARCHAR(36) PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    provider VARCHAR(50) NOT NULL,
    api_key VARCHAR(500) DEFAULT '',
    model VARCHAR(100),
    is_enabled BOOLEAN DEFAULT TRUE,
    priority INT DEFAULT 0,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- =====================================================
-- SYSTEM SETTINGS TABLE
-- =====================================================
CREATE TABLE system_settings (
    setting_key VARCHAR(100) PRIMARY KEY,
    setting_value TEXT,
    setting_type VARCHAR(20) DEFAULT 'string',
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

-- =====================================================
-- DEFAULT SETTINGS TABLE
-- =====================================================
CREATE TABLE default_settings (
    setting_key VARCHAR(100) PRIMARY KEY,
    free_credits INT DEFAULT 100,
    language VARCHAR(10) DEFAULT 'en',
    theme_json JSON
);

-- =====================================================
-- PROJECT RUNS TABLE
-- =====================================================
CREATE TABLE project_runs (
    id VARCHAR(36) PRIMARY KEY,
    project_id VARCHAR(36) NOT NULL,
    run_type VARCHAR(20) DEFAULT 'run',
    status VARCHAR(20) DEFAULT 'pending',
    started_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    ended_at DATETIME,
    output TEXT,
    logs JSON,
    errors JSON,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    INDEX idx_project_id (project_id)
);

-- =====================================================
-- IP RECORDS TABLE (Security)
-- =====================================================
CREATE TABLE ip_records (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    ip_address VARCHAR(45) NOT NULL,
    action VARCHAR(50) NOT NULL,
    user_agent TEXT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_timestamp (timestamp)
);

-- =====================================================
-- TOS VERSIONS TABLE
-- =====================================================
CREATE TABLE tos_versions (
    id VARCHAR(36) PRIMARY KEY,
    version VARCHAR(20) NOT NULL UNIQUE,
    title VARCHAR(255) NOT NULL,
    content JSON NOT NULL,
    effective_date DATETIME NOT NULL,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- KNOWLEDGE BASE TABLE
-- =====================================================
CREATE TABLE knowledge_base (
    id VARCHAR(36) PRIMARY KEY,
    question_hash VARCHAR(64) UNIQUE,
    question TEXT NOT NULL,
    answer TEXT NOT NULL,
    provider VARCHAR(50),
    usage_count INT DEFAULT 0,
    is_valid BOOLEAN DEFAULT TRUE,
    invalidated_at DATETIME,
    created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_question_hash (question_hash),
    INDEX idx_usage_count (usage_count)
);

-- =====================================================
-- AGENT ACTIVITY TABLE
-- =====================================================
CREATE TABLE agent_activity (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    project_id VARCHAR(36),
    job_id VARCHAR(36),
    agent_id VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL,
    tokens_used INT DEFAULT 0,
    credits_used DECIMAL(10,4) DEFAULT 0,
    success BOOLEAN DEFAULT TRUE,
    error TEXT,
    timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_agent_id (agent_id),
    INDEX idx_timestamp (timestamp)
);

-- =====================================================
-- INSERT DEFAULT DATA
-- =====================================================

-- Subscription Plans
INSERT INTO subscription_plans (id, name, description, price_monthly, price_yearly, daily_credits, max_concurrent_workspaces, allows_own_api_keys, features, sort_order) VALUES
('free', 'Free', 'Basic access with limited credits', 0, 0, 50, 1, FALSE, '["50 daily credits", "1 workspace", "Basic code generation", "Community support"]', 0),
('starter', 'Starter', 'For individual developers getting started', 9.99, 99.00, 200, 3, FALSE, '["200 daily credits", "3 workspaces", "All 7 AI agents", "Priority queue", "Email support"]', 1),
('pro', 'Pro', 'For professional developers and freelancers', 29.99, 299.00, 1000, 10, TRUE, '["1000 daily credits", "10 workspaces", "All 7 AI agents", "Own API keys", "Advanced AI models", "Priority support"]', 2),
('team', 'Team', 'For small teams and startups', 79.99, 799.00, 3000, 25, TRUE, '["3000 daily credits", "25 workspaces", "All 7 AI agents", "Own API keys", "Team collaboration", "Priority support", "Custom integrations"]', 3),
('enterprise', 'Enterprise', 'For large teams and organizations', 199.99, 1999.00, 10000, -1, TRUE, '["10000 daily credits", "Unlimited workspaces", "All 7 AI agents", "Own API keys", "Custom AI models", "SLA support", "Custom integrations", "Dedicated account manager"]', 4);

-- Credit Packages
INSERT INTO credit_packages (id, name, credits, price, sort_order) VALUES
('pack-50', 'Starter Pack', 50, 2.99, 0),
('pack-100', '100 Credits', 100, 4.99, 1),
('pack-250', '250 Credits', 250, 9.99, 2),
('pack-500', '500 Credits', 500, 17.99, 3),
('pack-1000', '1000 Credits', 1000, 29.99, 4),
('pack-2500', '2500 Credits', 2500, 69.99, 5),
('pack-5000', '5000 Credits', 5000, 129.99, 6),
('pack-10000', '10000 Credits', 10000, 229.99, 7);

-- System Settings
INSERT INTO system_settings (setting_key, setting_value, setting_type) VALUES
('credits_per_1k_tokens_chat', '0.5', 'decimal'),
('credits_per_1k_tokens_project', '1.0', 'decimal'),
('emergent_llm_enabled', 'true', 'boolean');

-- Default Settings
INSERT INTO default_settings (setting_key, free_credits, language) VALUES 
('new_user_defaults', 100, 'en');

-- Free AI Providers
INSERT INTO free_ai_providers (id, name, provider, api_key, model, is_enabled, priority) VALUES
('groq', 'Groq (Free)', 'groq', '', 'llama-3.1-70b-versatile', TRUE, 1),
('together', 'Together AI (Free)', 'together', '', 'meta-llama/Llama-3.2-11B-Vision-Instruct-Turbo', FALSE, 2),
('huggingface', 'HuggingFace (Free)', 'huggingface', '', 'microsoft/DialoGPT-large', FALSE, 3),
('openrouter', 'OpenRouter (Free)', 'openrouter', '', 'google/gemma-2-9b-it:free', FALSE, 4),
('ollama', 'Local Ollama (Free)', 'ollama', '', 'qwen2.5-coder:1.5b', FALSE, 5);

-- Terms of Service
INSERT INTO tos_versions (id, version, title, content, effective_date) VALUES
('tos-v1', '1.0', 'Terms of Service', 
'{"title":"Terms of Service","sections":[{"title":"1. Acceptance of Terms","content":"By accessing or using LittleHelper AI, you agree to be bound by these Terms of Service. If you do not agree to all the terms and conditions, you may not access or use the Service."},{"title":"2. Description of Service","content":"LittleHelper AI provides AI-powered code generation, debugging, and software development assistance. The Service uses artificial intelligence models to generate code and provide development recommendations."},{"title":"3. No Warranty","content":"THE SERVICE IS PROVIDED AS IS AND AS AVAILABLE WITHOUT WARRANTIES OF ANY KIND, EXPRESS OR IMPLIED. WE DO NOT WARRANT THAT THE SERVICE WILL BE UNINTERRUPTED, ERROR-FREE, OR THAT ANY CODE GENERATED WILL BE FREE OF BUGS OR SECURITY VULNERABILITIES."},{"title":"4. Limitation of Liability","content":"IN NO EVENT SHALL LITTLEHELPER AI BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES, INCLUDING BUT NOT LIMITED TO LOSS OF PROFITS, DATA, USE, OR GOODWILL, ARISING OUT OF OR IN CONNECTION WITH YOUR USE OF THE SERVICE OR ANY CODE GENERATED."},{"title":"5. User Responsibility","content":"You are solely responsible for reviewing, testing, and validating all code generated by the Service before deploying it in any production environment. You agree to test all generated code in a safe environment and ensure it meets your security and quality standards."},{"title":"6. AI-Generated Content","content":"All code and content generated by the Service is produced by artificial intelligence. While we strive for accuracy, AI-generated code may contain errors, bugs, security vulnerabilities, or may not work as expected. You acknowledge these limitations."},{"title":"7. Acceptable Use","content":"You agree not to use the Service for any illegal purposes, to generate malicious code, or to violate any applicable laws. You are responsible for ensuring your use of the Service complies with all applicable laws and regulations."},{"title":"8. Account Security","content":"You are responsible for maintaining the confidentiality of your account credentials and for all activities that occur under your account. You agree to notify us immediately of any unauthorized use of your account."},{"title":"9. Credits and Payments","content":"Credits are non-refundable unless required by applicable law. Unused credits expire according to your subscription plan terms. All fees are stated in USD and do not include applicable taxes."},{"title":"10. Termination","content":"We reserve the right to suspend or terminate your access to the Service at any time, with or without cause. Upon termination, your right to use the Service will immediately cease."}],"disclaimer":"BY USING THIS SERVICE, YOU ACKNOWLEDGE THAT AI-GENERATED CODE MAY CONTAIN ERRORS, SECURITY VULNERABILITIES, OR OTHER ISSUES. YOU ASSUME ALL RISKS ASSOCIATED WITH THE USE OF ANY CODE OR CONTENT GENERATED BY THE SERVICE."}',
NOW());

-- Default Admin User (password: admin123)
-- BCrypt hash for 'admin123'
INSERT INTO users (id, email, name, password_hash, role, credits, plan, tos_accepted, tos_accepted_at, tos_version, created_at) VALUES
('admin-default', 'admin@littlehelper.ai', 'System Admin', '$2a$11$K8FHKFt1Y0kzKXCVpPGWoOjPF8Gw8QJQzXHnDrxXxJkCRvYJKMIwK', 'admin', 999999, 'enterprise', TRUE, NOW(), '1.0', NOW());

-- =====================================================
-- DONE!
-- =====================================================
SELECT 'Database setup complete!' AS Status;
SELECT COUNT(*) AS 'Tables Created' FROM information_schema.tables WHERE table_schema = 'littlehelper_ai';
