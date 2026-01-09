-- =====================================================
-- LittleHelper AI - Complete Database Setup for XAMPP
-- Import this file into phpMyAdmin
-- User: root | Password: (none)
-- =====================================================
-- Compatible with: XAMPP MySQL/MariaDB
-- Tested on: Windows 10/11 with XAMPP
-- =====================================================

-- Drop and recreate database for fresh start
DROP DATABASE IF EXISTS `littlehelper_ai`;
CREATE DATABASE `littlehelper_ai` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `littlehelper_ai`;

-- =====================================================
-- USERS TABLE
-- =====================================================
CREATE TABLE `users` (
    `id` VARCHAR(36) PRIMARY KEY,
    `email` VARCHAR(255) NOT NULL UNIQUE,
    `name` VARCHAR(255) NOT NULL,
    `display_name` VARCHAR(255),
    `password_hash` VARCHAR(255) NOT NULL,
    `role` VARCHAR(20) DEFAULT 'user',
    `credits` DECIMAL(10,4) DEFAULT 100.0000,
    `credits_enabled` TINYINT(1) DEFAULT 1,
    `plan` VARCHAR(50) DEFAULT 'free',
    `language` VARCHAR(10) DEFAULT 'en',
    `avatar_url` TEXT,
    `registration_ip` VARCHAR(45),
    `last_login_ip` VARCHAR(45),
    `tos_accepted` TINYINT(1) DEFAULT 0,
    `tos_accepted_at` DATETIME,
    `tos_version` VARCHAR(20),
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `last_login_at` DATETIME,
    INDEX `idx_email` (`email`),
    INDEX `idx_role` (`role`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- USER THEMES TABLE
-- =====================================================
CREATE TABLE `user_themes` (
    `user_id` VARCHAR(36) PRIMARY KEY,
    `primary_color` VARCHAR(20) DEFAULT '#d946ef',
    `secondary_color` VARCHAR(20) DEFAULT '#06b6d4',
    `background_color` VARCHAR(20) DEFAULT '#030712',
    `card_color` VARCHAR(20) DEFAULT '#0B0F19',
    `text_color` VARCHAR(20) DEFAULT '#ffffff',
    `hover_color` VARCHAR(20) DEFAULT '#a855f7',
    `credits_color` VARCHAR(20) DEFAULT '#d946ef',
    `background_image` TEXT,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- PROJECTS TABLE
-- =====================================================
CREATE TABLE `projects` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `name` VARCHAR(255) NOT NULL,
    `description` TEXT,
    `language` VARCHAR(50) DEFAULT 'Python',
    `status` VARCHAR(20) DEFAULT 'active',
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_user_id` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- PROJECT FILES TABLE
-- =====================================================
CREATE TABLE `project_files` (
    `id` VARCHAR(36) PRIMARY KEY,
    `project_id` VARCHAR(36) NOT NULL,
    `path` VARCHAR(500) NOT NULL,
    `content` LONGTEXT,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (`project_id`) REFERENCES `projects`(`id`) ON DELETE CASCADE,
    INDEX `idx_project_id` (`project_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- TODOS TABLE
-- =====================================================
CREATE TABLE `todos` (
    `id` VARCHAR(36) PRIMARY KEY,
    `project_id` VARCHAR(36) NOT NULL,
    `text` TEXT NOT NULL,
    `completed` TINYINT(1) DEFAULT 0,
    `priority` VARCHAR(20) DEFAULT 'medium',
    `agent` VARCHAR(50),
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`project_id`) REFERENCES `projects`(`id`) ON DELETE CASCADE,
    INDEX `idx_project_id` (`project_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- JOBS TABLE (Multi-Agent Pipeline)
-- =====================================================
CREATE TABLE `jobs` (
    `id` VARCHAR(36) PRIMARY KEY,
    `project_id` VARCHAR(36) NOT NULL,
    `user_id` VARCHAR(36) NOT NULL,
    `prompt` TEXT NOT NULL,
    `status` VARCHAR(30) DEFAULT 'pending',
    `multi_agent_mode` TINYINT(1) DEFAULT 1,
    `tasks` JSON,
    `total_estimated_credits` DECIMAL(10,4) DEFAULT 0,
    `credits_used` DECIMAL(10,4) DEFAULT 0,
    `credits_approved` DECIMAL(10,4) DEFAULT 0,
    `current_task_index` INT DEFAULT -1,
    `error_count` INT DEFAULT 0,
    `max_errors` INT DEFAULT 5,
    `planner_output` TEXT,
    `planner_metadata` JSON,
    `error` TEXT,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    `started_at` DATETIME,
    `completed_at` DATETIME,
    FOREIGN KEY (`project_id`) REFERENCES `projects`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_project_id` (`project_id`),
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_status` (`status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- CHAT HISTORY TABLE
-- =====================================================
CREATE TABLE `chat_history` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `project_id` VARCHAR(36),
    `conversation_id` VARCHAR(36),
    `conversation_title` VARCHAR(255),
    `role` VARCHAR(20) NOT NULL,
    `content` TEXT NOT NULL,
    `agent_id` VARCHAR(50),
    `provider` VARCHAR(50),
    `model` VARCHAR(100),
    `tokens_used` INT DEFAULT 0,
    `credits_deducted` DECIMAL(10,4) DEFAULT 0,
    `multi_agent_mode` TINYINT(1) DEFAULT 0,
    `deleted_by_user` TINYINT(1) DEFAULT 0,
    `is_valid` TINYINT(1) DEFAULT 1,
    `invalidated_at` DATETIME,
    `timestamp` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_project_id` (`project_id`),
    INDEX `idx_conversation_id` (`conversation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- USER AI PROVIDERS TABLE
-- =====================================================
CREATE TABLE `user_ai_providers` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `provider` VARCHAR(50) NOT NULL,
    `api_key` VARCHAR(500) NOT NULL,
    `model_preference` VARCHAR(100),
    `is_active` TINYINT(1) DEFAULT 1,
    `is_default` TINYINT(1) DEFAULT 0,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    UNIQUE KEY `unique_user_provider` (`user_id`, `provider`),
    INDEX `idx_user_id` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- CREDIT HISTORY TABLE
-- =====================================================
CREATE TABLE `credit_history` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `delta` DECIMAL(10,4) NOT NULL,
    `reason` VARCHAR(255) NOT NULL,
    `reference_type` VARCHAR(50),
    `reference_id` VARCHAR(36),
    `balance_after` DECIMAL(10,4) NOT NULL,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- PAYMENT TRANSACTIONS TABLE
-- =====================================================
CREATE TABLE `payment_transactions` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `session_id` VARCHAR(255) NOT NULL,
    `package_id` VARCHAR(50) NOT NULL,
    `amount` DECIMAL(10,2) NOT NULL,
    `currency` VARCHAR(10) DEFAULT 'usd',
    `credits` INT NOT NULL,
    `status` VARCHAR(30) DEFAULT 'pending',
    `payment_status` VARCHAR(30) DEFAULT 'initiated',
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `completed_at` DATETIME,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_session_id` (`session_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- SUBSCRIPTION PLANS TABLE (Monthly Plans)
-- =====================================================
CREATE TABLE `subscription_plans` (
    `id` VARCHAR(36) PRIMARY KEY,
    `name` VARCHAR(100) NOT NULL,
    `description` TEXT,
    `price_monthly` DECIMAL(10,2) DEFAULT 0,
    `price_yearly` DECIMAL(10,2) DEFAULT 0,
    `daily_credits` INT DEFAULT 0,
    `max_concurrent_workspaces` INT DEFAULT 1,
    `allows_own_api_keys` TINYINT(1) DEFAULT 0,
    `features` JSON,
    `is_active` TINYINT(1) DEFAULT 1,
    `sort_order` INT DEFAULT 0,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- USER SUBSCRIPTIONS TABLE
-- =====================================================
CREATE TABLE `user_subscriptions` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `plan_id` VARCHAR(36) NOT NULL,
    `status` VARCHAR(30) DEFAULT 'active',
    `stripe_subscription_id` VARCHAR(255),
    `start_date` DATETIME NOT NULL,
    `end_date` DATETIME,
    `next_billing_date` DATETIME,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`plan_id`) REFERENCES `subscription_plans`(`id`),
    INDEX `idx_user_id` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- CREDIT PACKAGES TABLE (Add-on Credits)
-- =====================================================
CREATE TABLE `credit_packages` (
    `id` VARCHAR(36) PRIMARY KEY,
    `name` VARCHAR(100) NOT NULL,
    `description` TEXT,
    `credits` INT NOT NULL,
    `price` DECIMAL(10,2) NOT NULL,
    `bonus_credits` INT DEFAULT 0,
    `is_active` TINYINT(1) DEFAULT 1,
    `is_featured` TINYINT(1) DEFAULT 0,
    `sort_order` INT DEFAULT 0,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- FREE AI PROVIDERS TABLE (Admin Configured)
-- =====================================================
CREATE TABLE `free_ai_providers` (
    `id` VARCHAR(36) PRIMARY KEY,
    `name` VARCHAR(100) NOT NULL,
    `provider` VARCHAR(50) NOT NULL,
    `api_key` VARCHAR(500) DEFAULT '',
    `base_url` VARCHAR(255),
    `model` VARCHAR(100),
    `is_enabled` TINYINT(1) DEFAULT 1,
    `priority` INT DEFAULT 0,
    `rate_limit` INT DEFAULT 100,
    `description` TEXT,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- SYSTEM SETTINGS TABLE
-- =====================================================
CREATE TABLE `system_settings` (
    `setting_key` VARCHAR(100) PRIMARY KEY,
    `setting_value` TEXT,
    `setting_type` VARCHAR(20) DEFAULT 'string',
    `description` TEXT,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- DEFAULT SETTINGS TABLE
-- =====================================================
CREATE TABLE `default_settings` (
    `setting_key` VARCHAR(100) PRIMARY KEY,
    `free_credits` INT DEFAULT 100,
    `language` VARCHAR(10) DEFAULT 'en',
    `theme_json` JSON
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- PROJECT RUNS TABLE
-- =====================================================
CREATE TABLE `project_runs` (
    `id` VARCHAR(36) PRIMARY KEY,
    `project_id` VARCHAR(36) NOT NULL,
    `run_type` VARCHAR(20) DEFAULT 'run',
    `status` VARCHAR(20) DEFAULT 'pending',
    `started_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `ended_at` DATETIME,
    `output` TEXT,
    `logs` JSON,
    `errors` JSON,
    FOREIGN KEY (`project_id`) REFERENCES `projects`(`id`) ON DELETE CASCADE,
    INDEX `idx_project_id` (`project_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- IP RECORDS TABLE (Security)
-- =====================================================
CREATE TABLE `ip_records` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `ip_address` VARCHAR(45) NOT NULL,
    `action` VARCHAR(50) NOT NULL,
    `user_agent` TEXT,
    `timestamp` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_timestamp` (`timestamp`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- TOS VERSIONS TABLE
-- =====================================================
CREATE TABLE `tos_versions` (
    `id` VARCHAR(36) PRIMARY KEY,
    `version` VARCHAR(20) NOT NULL UNIQUE,
    `title` VARCHAR(255) NOT NULL,
    `content` JSON NOT NULL,
    `effective_date` DATETIME NOT NULL,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- KNOWLEDGE BASE TABLE
-- =====================================================
CREATE TABLE `knowledge_base` (
    `id` VARCHAR(36) PRIMARY KEY,
    `question_hash` VARCHAR(64) UNIQUE,
    `question` TEXT NOT NULL,
    `answer` TEXT NOT NULL,
    `provider` VARCHAR(50),
    `usage_count` INT DEFAULT 0,
    `is_valid` TINYINT(1) DEFAULT 1,
    `invalidated_at` DATETIME,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX `idx_question_hash` (`question_hash`),
    INDEX `idx_usage_count` (`usage_count`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- AGENT ACTIVITY TABLE
-- =====================================================
CREATE TABLE `agent_activity` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `project_id` VARCHAR(36),
    `job_id` VARCHAR(36),
    `agent_id` VARCHAR(50) NOT NULL,
    `action` VARCHAR(100) NOT NULL,
    `tokens_used` INT DEFAULT 0,
    `credits_used` DECIMAL(10,4) DEFAULT 0,
    `success` TINYINT(1) DEFAULT 1,
    `error` TEXT,
    `timestamp` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_user_id` (`user_id`),
    INDEX `idx_agent_id` (`agent_id`),
    INDEX `idx_timestamp` (`timestamp`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- INSERT SUBSCRIPTION PLANS (Monthly Plans)
-- =====================================================
INSERT INTO `subscription_plans` (`id`, `name`, `description`, `price_monthly`, `price_yearly`, `daily_credits`, `max_concurrent_workspaces`, `allows_own_api_keys`, `features`, `is_active`, `sort_order`) VALUES
('free', 'Free', 'Perfect for trying out LittleHelper AI', 0.00, 0.00, 50, 1, 0, '["50 daily credits", "1 workspace", "Basic code generation", "Community support", "Access to free AI models"]', 1, 0),
('starter', 'Starter', 'For individual developers getting started', 9.99, 99.00, 200, 3, 0, '["200 daily credits", "3 workspaces", "All 7 AI agents", "Priority queue", "Email support", "Code execution", "Project history"]', 1, 1),
('pro', 'Pro', 'For professional developers and freelancers', 29.99, 299.00, 1000, 10, 1, '["1000 daily credits", "10 workspaces", "All 7 AI agents", "Use your own API keys", "Advanced AI models (GPT-4, Claude)", "Priority support", "Custom themes", "API access"]', 1, 2),
('team', 'Team', 'For small teams and startups', 79.99, 799.00, 3000, 25, 1, '["3000 daily credits", "25 workspaces", "All 7 AI agents", "Use your own API keys", "Team collaboration", "Priority support", "Custom integrations", "Shared projects", "Team analytics"]', 1, 3),
('enterprise', 'Enterprise', 'For large teams and organizations', 199.99, 1999.00, 10000, -1, 1, '["10000 daily credits", "Unlimited workspaces", "All 7 AI agents", "Use your own API keys", "Custom AI models", "24/7 SLA support", "Custom integrations", "Dedicated account manager", "SSO integration", "On-premise deployment option"]', 1, 4);

-- =====================================================
-- INSERT CREDIT PACKAGES (Add-on Credits)
-- =====================================================
INSERT INTO `credit_packages` (`id`, `name`, `description`, `credits`, `price`, `bonus_credits`, `is_active`, `is_featured`, `sort_order`) VALUES
('pack-50', 'Starter Pack', 'Try it out with a small credit pack', 50, 2.99, 0, 1, 0, 0),
('pack-100', '100 Credits', 'Good for small projects', 100, 4.99, 0, 1, 0, 1),
('pack-250', '250 Credits', 'Perfect for medium projects', 250, 9.99, 25, 1, 0, 2),
('pack-500', '500 Credits', 'Best value for regular users', 500, 17.99, 50, 1, 1, 3),
('pack-1000', '1000 Credits', 'Great for power users', 1000, 29.99, 150, 1, 0, 4),
('pack-2500', '2500 Credits', 'For heavy development work', 2500, 69.99, 500, 1, 0, 5),
('pack-5000', '5000 Credits', 'Professional pack', 5000, 129.99, 1000, 1, 1, 6),
('pack-10000', '10000 Credits', 'Enterprise pack - best value!', 10000, 229.99, 2500, 1, 0, 7);

-- =====================================================
-- INSERT SYSTEM SETTINGS
-- =====================================================
INSERT INTO `system_settings` (`setting_key`, `setting_value`, `setting_type`, `description`) VALUES
('credits_per_1k_tokens_chat', '0.5', 'decimal', 'Credits charged per 1000 tokens in chat'),
('credits_per_1k_tokens_project', '1.0', 'decimal', 'Credits charged per 1000 tokens in project mode'),
('free_credits_on_signup', '100', 'integer', 'Free credits given to new users'),
('max_tokens_per_request', '4096', 'integer', 'Maximum tokens per API request'),
('rate_limit_per_minute', '60', 'integer', 'API rate limit per minute'),
('maintenance_mode', 'false', 'boolean', 'Enable maintenance mode'),
('allow_new_registrations', 'true', 'boolean', 'Allow new user registrations'),
('default_ai_provider', 'groq', 'string', 'Default AI provider for free users'),
('stripe_enabled', 'true', 'boolean', 'Enable Stripe payments'),
('email_verification_required', 'false', 'boolean', 'Require email verification');

-- =====================================================
-- INSERT DEFAULT SETTINGS
-- =====================================================
INSERT INTO `default_settings` (`setting_key`, `free_credits`, `language`, `theme_json`) VALUES 
('new_user_defaults', 100, 'en', '{"primary":"#d946ef","secondary":"#06b6d4","background":"#030712"}');

-- =====================================================
-- INSERT FREE AI PROVIDERS (With descriptions for getting keys)
-- Note: Users should get their own API keys from these providers
-- All providers listed offer free tiers!
-- =====================================================
INSERT INTO `free_ai_providers` (`id`, `name`, `provider`, `api_key`, `base_url`, `model`, `is_enabled`, `priority`, `rate_limit`, `description`) VALUES
('groq', 'Groq Cloud', 'groq', '', 'https://api.groq.com/openai/v1', 'llama-3.1-70b-versatile', 1, 1, 30, 'Fast inference with Llama 3.1 - Free tier: 30 req/min. Get key at: https://console.groq.com'),
('openrouter', 'OpenRouter', 'openrouter', '', 'https://openrouter.ai/api/v1', 'google/gemma-2-9b-it:free', 1, 2, 60, 'Access 50+ models including free ones - Get key at: https://openrouter.ai/keys'),
('huggingface', 'HuggingFace', 'huggingface', '', 'https://api-inference.huggingface.co/models', 'mistralai/Mistral-7B-Instruct-v0.2', 1, 3, 100, 'Free inference API for open models - Get key at: https://huggingface.co/settings/tokens'),
('together', 'Together AI', 'together', '', 'https://api.together.xyz/v1', 'meta-llama/Llama-3.2-11B-Vision-Instruct-Turbo', 0, 4, 60, 'High-performance open models - Free trial available at: https://api.together.ai'),
('google', 'Google AI Studio', 'google', '', 'https://generativelanguage.googleapis.com/v1beta', 'gemini-1.5-flash', 0, 5, 60, 'Gemini models with generous free tier - Get key at: https://aistudio.google.com/apikey'),
('ollama', 'Local Ollama', 'ollama', '', 'http://localhost:11434', 'qwen2.5-coder:1.5b', 0, 10, 100, 'Run models locally - Free & unlimited. Install from: https://ollama.ai');

-- =====================================================
-- INSERT TERMS OF SERVICE
-- =====================================================
INSERT INTO `tos_versions` (`id`, `version`, `title`, `content`, `effective_date`) VALUES
('tos-v1', '1.0', 'Terms of Service', 
'{"title":"Terms of Service","lastUpdated":"2025-01-01","sections":[{"title":"1. Acceptance of Terms","content":"By accessing or using LittleHelper AI, you agree to be bound by these Terms of Service. If you do not agree to all the terms and conditions, you may not access or use the Service."},{"title":"2. Description of Service","content":"LittleHelper AI provides AI-powered code generation, debugging, and software development assistance. The Service uses artificial intelligence models to generate code and provide development recommendations."},{"title":"3. No Warranty","content":"THE SERVICE IS PROVIDED AS IS AND AS AVAILABLE WITHOUT WARRANTIES OF ANY KIND, EXPRESS OR IMPLIED. WE DO NOT WARRANT THAT THE SERVICE WILL BE UNINTERRUPTED, ERROR-FREE, OR THAT ANY CODE GENERATED WILL BE FREE OF BUGS OR SECURITY VULNERABILITIES."},{"title":"4. Limitation of Liability","content":"IN NO EVENT SHALL LITTLEHELPER AI BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES, INCLUDING BUT NOT LIMITED TO LOSS OF PROFITS, DATA, USE, OR GOODWILL, ARISING OUT OF OR IN CONNECTION WITH YOUR USE OF THE SERVICE OR ANY CODE GENERATED."},{"title":"5. User Responsibility","content":"You are solely responsible for reviewing, testing, and validating all code generated by the Service before deploying it in any production environment. You agree to test all generated code in a safe environment and ensure it meets your security and quality standards."},{"title":"6. AI-Generated Content","content":"All code and content generated by the Service is produced by artificial intelligence. While we strive for accuracy, AI-generated code may contain errors, bugs, security vulnerabilities, or may not work as expected. You acknowledge these limitations."},{"title":"7. Acceptable Use","content":"You agree not to use the Service for any illegal purposes, to generate malicious code, or to violate any applicable laws. You are responsible for ensuring your use of the Service complies with all applicable laws and regulations."},{"title":"8. Account Security","content":"You are responsible for maintaining the confidentiality of your account credentials and for all activities that occur under your account. You agree to notify us immediately of any unauthorized use of your account."},{"title":"9. Credits and Payments","content":"Credits are non-refundable unless required by applicable law. Unused credits expire according to your subscription plan terms. All fees are stated in USD and do not include applicable taxes."},{"title":"10. Termination","content":"We reserve the right to suspend or terminate your access to the Service at any time, with or without cause. Upon termination, your right to use the Service will immediately cease."}],"disclaimer":"BY USING THIS SERVICE, YOU ACKNOWLEDGE THAT AI-GENERATED CODE MAY CONTAIN ERRORS, SECURITY VULNERABILITIES, OR OTHER ISSUES. YOU ASSUME ALL RISKS ASSOCIATED WITH THE USE OF ANY CODE OR CONTENT GENERATED BY THE SERVICE."}',
NOW());

-- =====================================================
-- INSERT DEFAULT ADMIN USER
-- Email: admin@littlehelper.ai
-- Password: admin123 (SHA256 hashed)
-- =====================================================
INSERT INTO `users` (`id`, `email`, `name`, `display_name`, `password_hash`, `role`, `credits`, `credits_enabled`, `plan`, `language`, `tos_accepted`, `tos_accepted_at`, `tos_version`, `created_at`) VALUES
('admin-default', 'admin@littlehelper.ai', 'System Admin', 'Admin', 'r9Nw+28uMjWOzYBdVDDR502p9PopmTzsduIyClt6VS4=', 'admin', 999999.0000, 1, 'enterprise', 'en', 1, NOW(), '1.0', NOW());

-- Insert admin theme
INSERT INTO `user_themes` (`user_id`, `primary_color`, `secondary_color`, `background_color`, `card_color`, `text_color`, `hover_color`, `credits_color`) VALUES
('admin-default', '#d946ef', '#06b6d4', '#030712', '#0B0F19', '#ffffff', '#a855f7', '#d946ef');

-- =====================================================
-- INSERT TEST USER (Optional - for development)
-- Email: test@example.com
-- Password: test123 (SHA256 hashed)
-- =====================================================
INSERT INTO `users` (`id`, `email`, `name`, `display_name`, `password_hash`, `role`, `credits`, `credits_enabled`, `plan`, `language`, `tos_accepted`, `tos_accepted_at`, `tos_version`, `created_at`) VALUES
('test-user-001', 'test@example.com', 'Test User', 'Tester', 'BQOs0QUShl0qPnMUFFKCJyTElcf3lpGAbGW9rF4cVJs=', 'user', 500.0000, 1, 'starter', 'en', 1, NOW(), '1.0', NOW());

-- Insert test user theme
INSERT INTO `user_themes` (`user_id`, `primary_color`, `secondary_color`, `background_color`, `card_color`, `text_color`, `hover_color`, `credits_color`) VALUES
('test-user-001', '#d946ef', '#06b6d4', '#030712', '#0B0F19', '#ffffff', '#a855f7', '#d946ef');

-- =====================================================
-- INSERT KING ADMIN USER
-- Email: king@example.com
-- Password: king123 (SHA256 hashed)
-- =====================================================
INSERT INTO `users` (`id`, `email`, `name`, `display_name`, `password_hash`, `role`, `credits`, `credits_enabled`, `plan`, `language`, `tos_accepted`, `tos_accepted_at`, `tos_version`, `created_at`) VALUES
('king-admin-001', 'king@example.com', 'King Admin', 'King', 'r9Nw+28uMjWOzYBdVDDR502p9PopmTzsduIyClt6VS4=', 'admin', 999999.0000, 1, 'enterprise', 'en', 1, NOW(), '1.0', NOW());

-- Insert king admin theme
INSERT INTO `user_themes` (`user_id`, `primary_color`, `secondary_color`, `background_color`, `card_color`, `text_color`, `hover_color`, `credits_color`) VALUES
('king-admin-001', '#d946ef', '#06b6d4', '#030712', '#0B0F19', '#ffffff', '#a855f7', '#d946ef');

-- =====================================================
-- SETUP COMPLETE!
-- =====================================================
-- 
-- LOGIN CREDENTIALS:
--   Admin: admin@littlehelper.ai / admin123
--   Admin: king@example.com / admin123
--   Test:  test@example.com / test123
--
-- Tables created: 21
-- Data inserted: Users, Plans, Credits, AI Providers, Settings
-- =====================================================

-- =====================================================
-- SITE SETTINGS TABLE (Admin-configurable settings)
-- =====================================================
CREATE TABLE IF NOT EXISTS `site_settings` (
    `id` VARCHAR(36) PRIMARY KEY DEFAULT 'default',
    `announcement_enabled` TINYINT(1) DEFAULT 0,
    `announcement_message` TEXT,
    `announcement_type` VARCHAR(20) DEFAULT 'info',
    `maintenance_mode` TINYINT(1) DEFAULT 0,
    `admins_auto_friend` TINYINT(1) DEFAULT 1,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    `updated_by` VARCHAR(36)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Insert default site settings
INSERT INTO `site_settings` (`id`, `announcement_enabled`, `announcement_message`, `announcement_type`, `maintenance_mode`, `admins_auto_friend`, `updated_at`)
VALUES ('default', 0, NULL, 'info', 0, 1, NOW())
ON DUPLICATE KEY UPDATE `id` = `id`;


-- =====================================================
-- FRIEND REQUESTS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS `friend_requests` (
    `id` VARCHAR(36) PRIMARY KEY,
    `sender_id` VARCHAR(36) NOT NULL,
    `receiver_id` VARCHAR(36) NOT NULL,
    `status` VARCHAR(20) DEFAULT 'pending',
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (`sender_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`receiver_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    UNIQUE KEY `unique_friend_request` (`sender_id`, `receiver_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- FRIENDS TABLE (Accepted friendships)
-- =====================================================
CREATE TABLE IF NOT EXISTS `friends` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL,
    `friend_user_id` VARCHAR(36) NOT NULL,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`friend_user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    UNIQUE KEY `unique_friendship` (`user_id`, `friend_user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- DIRECT MESSAGES TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS `direct_messages` (
    `id` VARCHAR(36) PRIMARY KEY,
    `sender_id` VARCHAR(36) NOT NULL,
    `receiver_id` VARCHAR(36) NOT NULL,
    `message` TEXT NOT NULL,
    `message_type` VARCHAR(20) DEFAULT 'text',
    `is_read` TINYINT(1) DEFAULT 0,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`sender_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`receiver_id`) REFERENCES `users`(`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- =====================================================
-- PROJECT COLLABORATORS TABLE
-- =====================================================
CREATE TABLE IF NOT EXISTS `project_collaborators` (
    `id` VARCHAR(36) PRIMARY KEY,
    `project_id` VARCHAR(36) NOT NULL,
    `user_id` VARCHAR(36) NOT NULL,
    `permission_level` VARCHAR(20) DEFAULT 'edit',
    `invited_by` VARCHAR(36),
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    FOREIGN KEY (`project_id`) REFERENCES `projects`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    FOREIGN KEY (`invited_by`) REFERENCES `users`(`id`) ON DELETE SET NULL,
    UNIQUE KEY `unique_project_collaborator` (`project_id`, `user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =====================================================
-- PERFORMANCE INDEXES
-- =====================================================

-- Friend requests indexes
CREATE INDEX IF NOT EXISTS idx_friend_requests_sender ON friend_requests(sender_id);
CREATE INDEX IF NOT EXISTS idx_friend_requests_receiver ON friend_requests(receiver_id);
CREATE INDEX IF NOT EXISTS idx_friend_requests_status ON friend_requests(status);
CREATE INDEX IF NOT EXISTS idx_friend_requests_created ON friend_requests(created_at);

-- Friends indexes
CREATE INDEX IF NOT EXISTS idx_friends_user ON friends(user_id);
CREATE INDEX IF NOT EXISTS idx_friends_friend ON friends(friend_user_id);

-- Direct messages indexes
CREATE INDEX IF NOT EXISTS idx_dm_sender ON direct_messages(sender_id);
CREATE INDEX IF NOT EXISTS idx_dm_receiver ON direct_messages(receiver_id);
CREATE INDEX IF NOT EXISTS idx_dm_created ON direct_messages(created_at);
CREATE INDEX IF NOT EXISTS idx_dm_is_read ON direct_messages(is_read);
CREATE INDEX IF NOT EXISTS idx_dm_conversation ON direct_messages(sender_id, receiver_id, created_at);

-- Chat history indexes
CREATE INDEX IF NOT EXISTS idx_chat_project ON chat_history(project_id);
CREATE INDEX IF NOT EXISTS idx_chat_user ON chat_history(user_id);
CREATE INDEX IF NOT EXISTS idx_chat_conversation ON chat_history(conversation_id);
CREATE INDEX IF NOT EXISTS idx_chat_timestamp ON chat_history(timestamp);

-- Project collaborators indexes
CREATE INDEX IF NOT EXISTS idx_collab_project ON project_collaborators(project_id);
CREATE INDEX IF NOT EXISTS idx_collab_user ON project_collaborators(user_id);


-- =====================================================
-- USER GOOGLE DRIVE CONFIG TABLE
-- Stores per-user Google Drive connection details
-- =====================================================
CREATE TABLE IF NOT EXISTS `user_google_drive_config` (
    `id` VARCHAR(36) PRIMARY KEY,
    `user_id` VARCHAR(36) NOT NULL UNIQUE,
    `is_connected` TINYINT(1) DEFAULT 0,
    `email` VARCHAR(255),
    `access_token` TEXT,
    `refresh_token` TEXT,
    `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
    `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    FOREIGN KEY (`user_id`) REFERENCES `users`(`id`) ON DELETE CASCADE,
    INDEX `idx_gdrive_user` (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Add appear_offline column to users table for admin visibility feature
ALTER TABLE `users` ADD COLUMN IF NOT EXISTS `appear_offline` TINYINT(1) DEFAULT 0;

