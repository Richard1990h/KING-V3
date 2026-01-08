-- LittleHelper AI Database Schema for MySQL/XAMPP
-- Version 1.0
-- Run this script in phpMyAdmin or MySQL CLI

CREATE DATABASE IF NOT EXISTS littlehelper_ai CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE littlehelper_ai;

-- =====================================================
-- USERS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS users (
    id VARCHAR(36) PRIMARY KEY,
    email VARCHAR(255) NOT NULL UNIQUE,
    name VARCHAR(255) NOT NULL,
    password_hash VARCHAR(255) NOT NULL,
    role ENUM('user', 'admin') DEFAULT 'user',
    credits DECIMAL(10,2) DEFAULT 100.00,
    credits_enabled BOOLEAN DEFAULT TRUE COMMENT 'If FALSE, user can use AI without credit deduction',
    language VARCHAR(10) DEFAULT 'en',
    plan ENUM('free', 'starter', 'pro', 'enterprise') DEFAULT 'free',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    last_login_at TIMESTAMP NULL,
    INDEX idx_email (email),
    INDEX idx_role (role),
    INDEX idx_plan (plan)
) ENGINE=InnoDB;

-- =====================================================
-- USER AI PROVIDERS - Store user's own API keys
-- =====================================================
CREATE TABLE IF NOT EXISTS user_ai_providers (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    provider ENUM('openai', 'anthropic', 'google', 'azure', 'local', 'cohere', 'mistral') NOT NULL,
    api_key VARCHAR(500) NOT NULL COMMENT 'Encrypted API key',
    model_preference VARCHAR(100) DEFAULT NULL COMMENT 'Preferred model for this provider',
    is_active BOOLEAN DEFAULT TRUE,
    is_default BOOLEAN DEFAULT FALSE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    UNIQUE KEY unique_user_provider (user_id, provider),
    INDEX idx_user_id (user_id)
) ENGINE=InnoDB;

-- =====================================================
-- PROJECTS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS projects (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    name VARCHAR(255) NOT NULL,
    description TEXT,
    language VARCHAR(50) DEFAULT 'Python',
    status ENUM('active', 'building', 'running', 'completed', 'failed', 'archived') DEFAULT 'active',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_status (status)
) ENGINE=InnoDB;

-- =====================================================
-- PROJECT FILES TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS project_files (
    id VARCHAR(36) PRIMARY KEY,
    project_id VARCHAR(36) NOT NULL,
    path VARCHAR(500) NOT NULL,
    content LONGTEXT,
    file_type VARCHAR(50),
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    UNIQUE KEY unique_project_path (project_id, path),
    INDEX idx_project_id (project_id)
) ENGINE=InnoDB;

-- =====================================================
-- PROJECT RUNS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS project_runs (
    id VARCHAR(36) PRIMARY KEY,
    project_id VARCHAR(36) NOT NULL,
    run_type ENUM('build', 'run', 'test') DEFAULT 'run',
    status ENUM('pending', 'running', 'success', 'failed', 'cancelled') DEFAULT 'pending',
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ended_at TIMESTAMP NULL,
    exit_code INT NULL,
    output LONGTEXT,
    error_output LONGTEXT,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    INDEX idx_project_id (project_id),
    INDEX idx_status (status)
) ENGINE=InnoDB;

-- =====================================================
-- RUN LOGS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS run_logs (
    id VARCHAR(36) PRIMARY KEY,
    run_id VARCHAR(36) NOT NULL,
    log_level ENUM('debug', 'info', 'warning', 'error', 'critical') DEFAULT 'info',
    source ENUM('build', 'runtime', 'test', 'agent', 'system') DEFAULT 'runtime',
    message TEXT NOT NULL,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (run_id) REFERENCES project_runs(id) ON DELETE CASCADE,
    INDEX idx_run_id (run_id),
    INDEX idx_log_level (log_level)
) ENGINE=InnoDB;

-- =====================================================
-- CHAT HISTORY TABLE - All conversations persisted
-- =====================================================
CREATE TABLE IF NOT EXISTS chat_history (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    project_id VARCHAR(36) NULL COMMENT 'NULL for global assistant chat',
    conversation_id VARCHAR(36) NOT NULL COMMENT 'Groups messages in same conversation',
    role ENUM('user', 'assistant', 'system') NOT NULL,
    content LONGTEXT NOT NULL,
    agent_id VARCHAR(50) NULL COMMENT 'Which agent responded',
    provider VARCHAR(50) NULL COMMENT 'Which AI provider was used',
    model VARCHAR(100) NULL COMMENT 'Which model was used',
    tokens_used INT DEFAULT 0,
    credits_deducted DECIMAL(10,4) DEFAULT 0,
    timestamp TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_project_id (project_id),
    INDEX idx_conversation_id (conversation_id),
    INDEX idx_timestamp (timestamp)
) ENGINE=InnoDB;

-- =====================================================
-- KNOWLEDGE BASE - Cached questions and answers
-- =====================================================
CREATE TABLE IF NOT EXISTS knowledge_base (
    id VARCHAR(36) PRIMARY KEY,
    question_hash VARCHAR(64) NOT NULL COMMENT 'SHA256 hash of normalized question',
    question_text TEXT NOT NULL,
    answer_text LONGTEXT NOT NULL,
    category VARCHAR(100) NULL,
    tags JSON NULL,
    hit_count INT DEFAULT 1 COMMENT 'How many times this was reused',
    is_private BOOLEAN DEFAULT FALSE COMMENT 'If TRUE, only original user can see',
    original_user_id VARCHAR(36) NULL,
    provider VARCHAR(50) NULL,
    model VARCHAR(100) NULL,
    confidence_score DECIMAL(5,4) DEFAULT 1.0000,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    last_used_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    expires_at TIMESTAMP NULL COMMENT 'When answer should be revalidated',
    is_valid BOOLEAN DEFAULT TRUE,
    UNIQUE KEY unique_question_hash (question_hash),
    INDEX idx_question_hash (question_hash),
    INDEX idx_category (category),
    INDEX idx_is_valid (is_valid),
    INDEX idx_expires_at (expires_at),
    FULLTEXT INDEX ft_question (question_text)
) ENGINE=InnoDB;

-- =====================================================
-- AGENT ACTIVITY LOG
-- =====================================================
CREATE TABLE IF NOT EXISTS agent_activity (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    project_id VARCHAR(36) NULL,
    agent_id VARCHAR(50) NOT NULL,
    action VARCHAR(100) NOT NULL,
    input_summary TEXT,
    output_summary TEXT,
    status ENUM('started', 'running', 'completed', 'failed', 'skipped') DEFAULT 'started',
    duration_ms INT NULL,
    tokens_input INT DEFAULT 0,
    tokens_output INT DEFAULT 0,
    error_message TEXT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_agent_id (agent_id),
    INDEX idx_status (status),
    INDEX idx_created_at (created_at)
) ENGINE=InnoDB;

-- =====================================================
-- PIPELINE RUNS - Track full pipeline executions
-- =====================================================
CREATE TABLE IF NOT EXISTS pipeline_runs (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    project_id VARCHAR(36) NOT NULL,
    pipeline_type ENUM('full', 'build_only', 'test_only', 'debug') DEFAULT 'full',
    status ENUM('pending', 'running', 'completed', 'failed', 'cancelled') DEFAULT 'pending',
    current_stage VARCHAR(50) NULL,
    stages_completed JSON COMMENT 'Array of completed stage IDs',
    stages_failed JSON COMMENT 'Array of failed stage IDs with errors',
    total_stages INT DEFAULT 7,
    started_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    ended_at TIMESTAMP NULL,
    total_tokens_used INT DEFAULT 0,
    total_credits_used DECIMAL(10,4) DEFAULT 0,
    error_message TEXT NULL,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_project_id (project_id),
    INDEX idx_status (status)
) ENGINE=InnoDB;

-- =====================================================
-- PAYMENT TRANSACTIONS
-- =====================================================
CREATE TABLE IF NOT EXISTS payment_transactions (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    session_id VARCHAR(255) NULL COMMENT 'Stripe session ID',
    package_id VARCHAR(50) NOT NULL,
    amount DECIMAL(10,2) NOT NULL,
    currency VARCHAR(3) DEFAULT 'USD',
    credits INT NOT NULL,
    status ENUM('pending', 'complete', 'failed', 'refunded') DEFAULT 'pending',
    payment_status VARCHAR(50) DEFAULT 'initiated',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    completed_at TIMESTAMP NULL,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_session_id (session_id),
    INDEX idx_status (status)
) ENGINE=InnoDB;

-- =====================================================
-- CREDIT HISTORY - Track all credit changes
-- =====================================================
CREATE TABLE IF NOT EXISTS credit_history (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    delta DECIMAL(10,4) NOT NULL COMMENT 'Positive for additions, negative for deductions',
    reason VARCHAR(255) NOT NULL,
    reference_type VARCHAR(50) NULL COMMENT 'chat, project_build, pipeline, purchase, admin_adjustment',
    reference_id VARCHAR(36) NULL,
    balance_after DECIMAL(10,2) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_user_id (user_id),
    INDEX idx_created_at (created_at)
) ENGINE=InnoDB;

-- =====================================================
-- SYSTEM SETTINGS - Admin configurable settings
-- =====================================================
CREATE TABLE IF NOT EXISTS system_settings (
    setting_key VARCHAR(100) PRIMARY KEY,
    setting_value TEXT NOT NULL,
    setting_type ENUM('string', 'number', 'boolean', 'json') DEFAULT 'string',
    description TEXT,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    updated_by VARCHAR(36) NULL
) ENGINE=InnoDB;

-- =====================================================
-- SYSTEM HEALTH LOGS
-- =====================================================
CREATE TABLE IF NOT EXISTS system_health (
    id VARCHAR(36) PRIMARY KEY,
    component VARCHAR(100) NOT NULL COMMENT 'database, ai_provider, cache, etc.',
    status ENUM('healthy', 'degraded', 'unhealthy', 'unknown') DEFAULT 'unknown',
    response_time_ms INT NULL,
    error_message TEXT NULL,
    metadata JSON NULL,
    checked_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    INDEX idx_component (component),
    INDEX idx_status (status),
    INDEX idx_checked_at (checked_at)
) ENGINE=InnoDB;

-- =====================================================
-- RUNNING JOBS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS running_jobs (
    id VARCHAR(36) PRIMARY KEY,
    user_id VARCHAR(36) NOT NULL,
    project_id VARCHAR(36) NULL,
    job_type ENUM('build', 'run', 'pipeline', 'chat', 'analysis') NOT NULL,
    status ENUM('queued', 'running', 'completed', 'failed', 'cancelled') DEFAULT 'queued',
    priority INT DEFAULT 5 COMMENT '1=highest, 10=lowest',
    worker_id VARCHAR(100) NULL,
    progress INT DEFAULT 0 COMMENT '0-100 percentage',
    started_at TIMESTAMP NULL,
    completed_at TIMESTAMP NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
    INDEX idx_status (status),
    INDEX idx_user_id (user_id),
    INDEX idx_job_type (job_type)
) ENGINE=InnoDB;

-- =====================================================
-- DEFAULT SETTINGS INSERT
-- =====================================================
INSERT INTO system_settings (setting_key, setting_value, setting_type, description) VALUES
('credits_per_1k_tokens_chat', '0.5', 'number', 'Credits deducted per 1000 tokens for general chat'),
('credits_per_1k_tokens_project', '1.0', 'number', 'Credits deducted per 1000 tokens for project generation'),
('knowledge_cache_hours', '168', 'number', 'Hours before cached knowledge answers expire (168 = 1 week)'),
('default_ai_provider', 'local', 'string', 'Default AI provider for new users'),
('default_ai_model', 'qwen2.5-coder:1.5b', 'string', 'Default model for new users'),
('max_tokens_per_request', '4000', 'number', 'Maximum tokens allowed per single request'),
('free_credits_on_signup', '100', 'number', 'Free credits given to new users'),
('enable_knowledge_sharing', 'true', 'boolean', 'Whether to share knowledge base across users'),
('pipeline_timeout_seconds', '300', 'number', 'Maximum time for pipeline execution'),
('max_concurrent_jobs_per_user', '3', 'number', 'Maximum concurrent jobs per user')
ON DUPLICATE KEY UPDATE setting_value = VALUES(setting_value);

-- =====================================================
-- DEFAULT ADMIN USER
-- Password: admin123 (bcrypt hashed)
-- =====================================================
INSERT INTO users (id, email, name, password_hash, role, credits, credits_enabled, plan) VALUES
('00000000-0000-0000-0000-000000000001', 'admin@littlehelper.ai', 'System Admin', 
'$2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8/X4L6VnOFRpQFHQwPG', 
'admin', 999999.00, TRUE, 'enterprise')
ON DUPLICATE KEY UPDATE updated_at = CURRENT_TIMESTAMP;

-- =====================================================
-- VIEWS FOR ADMIN DASHBOARD
-- =====================================================
CREATE OR REPLACE VIEW v_user_stats AS
SELECT 
    COUNT(*) as total_users,
    SUM(CASE WHEN role = 'admin' THEN 1 ELSE 0 END) as admin_count,
    SUM(CASE WHEN DATE(created_at) = CURDATE() THEN 1 ELSE 0 END) as signups_today,
    SUM(credits) as total_credits_in_system,
    AVG(credits) as avg_credits_per_user
FROM users;

CREATE OR REPLACE VIEW v_active_jobs AS
SELECT 
    j.*,
    u.name as user_name,
    u.email as user_email,
    p.name as project_name
FROM running_jobs j
LEFT JOIN users u ON j.user_id = u.id
LEFT JOIN projects p ON j.project_id = p.id
WHERE j.status IN ('queued', 'running');

CREATE OR REPLACE VIEW v_agent_activity_summary AS
SELECT 
    agent_id,
    COUNT(*) as total_calls,
    SUM(CASE WHEN status = 'completed' THEN 1 ELSE 0 END) as successful_calls,
    SUM(CASE WHEN status = 'failed' THEN 1 ELSE 0 END) as failed_calls,
    AVG(duration_ms) as avg_duration_ms,
    SUM(tokens_input + tokens_output) as total_tokens,
    DATE(created_at) as activity_date
FROM agent_activity
GROUP BY agent_id, DATE(created_at);

CREATE OR REPLACE VIEW v_knowledge_stats AS
SELECT 
    COUNT(*) as total_entries,
    SUM(hit_count) as total_hits,
    SUM(CASE WHEN is_valid = TRUE THEN 1 ELSE 0 END) as valid_entries,
    SUM(CASE WHEN expires_at < NOW() THEN 1 ELSE 0 END) as expired_entries,
    AVG(hit_count) as avg_hit_count
FROM knowledge_base;

-- =====================================================
-- STORED PROCEDURES
-- =====================================================
DELIMITER //

-- Procedure to deduct credits with logging
CREATE PROCEDURE IF NOT EXISTS sp_deduct_credits(
    IN p_user_id VARCHAR(36),
    IN p_amount DECIMAL(10,4),
    IN p_reason VARCHAR(255),
    IN p_ref_type VARCHAR(50),
    IN p_ref_id VARCHAR(36)
)
BEGIN
    DECLARE v_credits_enabled BOOLEAN;
    DECLARE v_current_balance DECIMAL(10,2);
    DECLARE v_new_balance DECIMAL(10,2);
    
    -- Check if credits are enabled for user
    SELECT credits_enabled, credits INTO v_credits_enabled, v_current_balance
    FROM users WHERE id = p_user_id;
    
    -- If credits disabled, exit without deduction
    IF v_credits_enabled = FALSE THEN
        SELECT 'Credits disabled for user' as message, v_current_balance as balance;
    ELSE
        SET v_new_balance = v_current_balance - p_amount;
        
        -- Update user credits
        UPDATE users SET credits = v_new_balance WHERE id = p_user_id;
        
        -- Log the transaction
        INSERT INTO credit_history (id, user_id, delta, reason, reference_type, reference_id, balance_after)
        VALUES (UUID(), p_user_id, -p_amount, p_reason, p_ref_type, p_ref_id, v_new_balance);
        
        SELECT 'Credits deducted' as message, v_new_balance as balance;
    END IF;
END //

-- Procedure to check and return cached knowledge
CREATE PROCEDURE IF NOT EXISTS sp_get_cached_answer(
    IN p_question_hash VARCHAR(64),
    OUT p_found BOOLEAN,
    OUT p_answer TEXT
)
BEGIN
    DECLARE v_answer LONGTEXT;
    DECLARE v_is_valid BOOLEAN;
    DECLARE v_expires_at TIMESTAMP;
    
    SELECT answer_text, is_valid, expires_at INTO v_answer, v_is_valid, v_expires_at
    FROM knowledge_base 
    WHERE question_hash = p_question_hash
    LIMIT 1;
    
    IF v_answer IS NOT NULL AND v_is_valid = TRUE AND (v_expires_at IS NULL OR v_expires_at > NOW()) THEN
        -- Update hit count
        UPDATE knowledge_base SET hit_count = hit_count + 1, last_used_at = NOW()
        WHERE question_hash = p_question_hash;
        
        SET p_found = TRUE;
        SET p_answer = v_answer;
    ELSE
        SET p_found = FALSE;
        SET p_answer = NULL;
    END IF;
END //

DELIMITER ;

-- =====================================================
-- INDEXES FOR PERFORMANCE AT SCALE
-- =====================================================
-- Additional composite indexes for common queries
CREATE INDEX IF NOT EXISTS idx_chat_user_project ON chat_history(user_id, project_id, timestamp);
CREATE INDEX IF NOT EXISTS idx_knowledge_valid_expires ON knowledge_base(is_valid, expires_at);
CREATE INDEX IF NOT EXISTS idx_agent_user_date ON agent_activity(user_id, created_at);

-- =====================================================
-- GRANT SCRIPT (Adjust as needed for your XAMPP setup)
-- =====================================================
-- GRANT ALL PRIVILEGES ON littlehelper_ai.* TO 'root'@'localhost';
-- FLUSH PRIVILEGES;

SELECT 'LittleHelper AI Database Schema Created Successfully!' as status;
