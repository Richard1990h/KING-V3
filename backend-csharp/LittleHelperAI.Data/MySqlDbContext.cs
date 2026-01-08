// MySQL Database Context using Dapper with snake_case mapping
using MySqlConnector;
using Dapper;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Text.Json;

namespace LittleHelperAI.Data;

// Custom type handler for JSON list columns
public class JsonListTypeHandler : SqlMapper.TypeHandler<List<string>>
{
    public override List<string> Parse(object value)
    {
        if (value == null || value == DBNull.Value)
            return new List<string>();
        
        var json = value.ToString();
        if (string.IsNullOrEmpty(json))
            return new List<string>();
        
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch
        {
            return new List<string>();
        }
    }

    public override void SetValue(IDbDataParameter parameter, List<string>? value)
    {
        parameter.Value = value == null ? DBNull.Value : JsonSerializer.Serialize(value);
    }
}

public interface IDbContext
{
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null);
    Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null);
    Task<int> ExecuteAsync(string sql, object? param = null);
    Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null);
    Task InitializeAsync();
    System.Data.IDbConnection CreateConnection();
}

// Custom type mapper for snake_case columns to PascalCase properties
public class ColumnAttributeTypeMapper<T> : SqlMapper.ITypeMap
{
    private readonly SqlMapper.ITypeMap _defaultMapper = new DefaultTypeMap(typeof(T));

    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
        => _defaultMapper.FindConstructor(names, types);

    public ConstructorInfo? FindExplicitConstructor()
        => _defaultMapper.FindExplicitConstructor();

    public SqlMapper.IMemberMap? GetConstructorParameter(ConstructorInfo constructor, string columnName)
        => _defaultMapper.GetConstructorParameter(constructor, columnName);

    public SqlMapper.IMemberMap? GetMember(string columnName)
    {
        var properties = typeof(T).GetProperties();
        foreach (var prop in properties)
        {
            var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
            if (columnAttr != null && string.Equals(columnAttr.Name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                return new SimpleMemberMap(columnName, prop);
            }
        }
        
        // Fallback: try to match snake_case to PascalCase
        var pascalName = string.Concat(columnName.Split('_').Select(s => 
            string.IsNullOrEmpty(s) ? "" : char.ToUpper(s[0]) + s.Substring(1).ToLower()));
        var prop2 = typeof(T).GetProperty(pascalName, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        if (prop2 != null)
        {
            return new SimpleMemberMap(columnName, prop2);
        }

        return _defaultMapper.GetMember(columnName);
    }
}

public class SimpleMemberMap : SqlMapper.IMemberMap
{
    public SimpleMemberMap(string columnName, PropertyInfo property)
    {
        ColumnName = columnName;
        Property = property;
    }

    public string ColumnName { get; }
    public Type MemberType => Property.PropertyType;
    public PropertyInfo? Property { get; }
    public FieldInfo? Field => null;
    public ParameterInfo? Parameter => null;
}

public class MySqlDbContext : IDbContext
{
    private readonly string _connectionString;
    private static bool _typeMappersRegistered = false;
    private static readonly object _lock = new();

    public MySqlDbContext(string connectionString)
    {
        _connectionString = connectionString;
        RegisterTypeMappersOnce();
    }

    private static void RegisterTypeMappersOnce()
    {
        lock (_lock)
        {
            if (_typeMappersRegistered) return;
            
            // Register JSON type handler for List<string> columns
            SqlMapper.AddTypeHandler(new JsonListTypeHandler());
            
            // Register type mappers for all model types
            var modelTypes = typeof(LittleHelperAI.Data.Models.User).Assembly
                .GetTypes()
                .Where(t => t.Namespace == "LittleHelperAI.Data.Models" && t.IsClass && !t.IsAbstract);

            foreach (var type in modelTypes)
            {
                var mapperType = typeof(ColumnAttributeTypeMapper<>).MakeGenericType(type);
                var mapper = Activator.CreateInstance(mapperType) as SqlMapper.ITypeMap;
                if (mapper != null)
                {
                    SqlMapper.SetTypeMap(type, mapper);
                }
            }

            // Set default mapping to handle underscore naming convention
            DefaultTypeMap.MatchNamesWithUnderscores = true;
            
            _typeMappersRegistered = true;
        }
    }

    public System.Data.IDbConnection CreateConnection() => new MySqlConnection(_connectionString);

    public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object? param = null)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        return await connection.QueryAsync<T>(sql, param);
    }

    public async Task<T?> QueryFirstOrDefaultAsync<T>(string sql, object? param = null)
    {
        using var connection = CreateConnection();
        await connection.OpenAsync();
        return await connection.QueryFirstOrDefaultAsync<T>(sql, param);
    }

    public async Task<int> ExecuteAsync(string sql, object? param = null)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return await connection.ExecuteAsync(sql, param);
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param = null)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return await connection.ExecuteScalarAsync<T>(sql, param);
    }

    public async Task InitializeAsync()
    {
        // Create database if not exists
        var builder = new MySqlConnectionStringBuilder(_connectionString);
        var database = builder.Database;
        builder.Database = "";

        using var connection = new MySqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        
        await connection.ExecuteAsync($"CREATE DATABASE IF NOT EXISTS `{database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        
        // Switch to the database and create tables
        connection.ChangeDatabase(database);
        
        // Create all tables
        await CreateTablesAsync(connection);
        
        // Run migrations to add missing columns to existing tables
        await RunMigrationsAsync(connection);
        
        // Insert default data
        await InsertDefaultDataAsync(connection);

        Console.WriteLine($"Database '{database}' initialized successfully.");
    }

    private async Task RunMigrationsAsync(MySqlConnection connection)
    {
        // Add missing columns to users table if they don't exist
        var userColumns = new Dictionary<string, string>
        {
            ["tos_accepted"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS tos_accepted BOOLEAN DEFAULT FALSE",
            ["tos_accepted_at"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS tos_accepted_at DATETIME",
            ["tos_version"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS tos_version VARCHAR(20)",
            ["display_name"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS display_name VARCHAR(255)",
            ["avatar_url"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS avatar_url TEXT",
            ["registration_ip"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS registration_ip VARCHAR(45)",
            ["last_login_ip"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS last_login_ip VARCHAR(45)",
            ["credits_enabled"] = "ALTER TABLE users ADD COLUMN IF NOT EXISTS credits_enabled BOOLEAN DEFAULT TRUE"
        };

        foreach (var migration in userColumns)
        {
            try
            {
                // Check if column exists first
                var exists = await connection.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'users' AND COLUMN_NAME = @Column",
                    new { Column = migration.Key });
                
                if (exists == 0)
                {
                    // MariaDB/MySQL 10.0.2+ supports ADD COLUMN IF NOT EXISTS, but for compatibility:
                    var alterSql = $"ALTER TABLE users ADD COLUMN {migration.Key} " + migration.Key switch
                    {
                        "tos_accepted" => "BOOLEAN DEFAULT FALSE",
                        "tos_accepted_at" => "DATETIME NULL",
                        "tos_version" => "VARCHAR(20) NULL",
                        "display_name" => "VARCHAR(255) NULL",
                        "avatar_url" => "TEXT NULL",
                        "registration_ip" => "VARCHAR(45) NULL",
                        "last_login_ip" => "VARCHAR(45) NULL",
                        "credits_enabled" => "BOOLEAN DEFAULT TRUE",
                        _ => "VARCHAR(255) NULL"
                    };
                    await connection.ExecuteAsync(alterSql);
                    Console.WriteLine($"Added column '{migration.Key}' to users table");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Migration warning for {migration.Key}: {ex.Message}");
            }
        }

        // Add missing columns to chat_history table
        try
        {
            var chatColumns = new[] { "is_valid", "invalidated_at" };
            foreach (var col in chatColumns)
            {
                var exists = await connection.QueryFirstOrDefaultAsync<int>(
                    @"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS 
                      WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'chat_history' AND COLUMN_NAME = @Column",
                    new { Column = col });
                
                if (exists == 0)
                {
                    var alterSql = col == "is_valid" 
                        ? "ALTER TABLE chat_history ADD COLUMN is_valid BOOLEAN DEFAULT TRUE"
                        : "ALTER TABLE chat_history ADD COLUMN invalidated_at DATETIME NULL";
                    await connection.ExecuteAsync(alterSql);
                    Console.WriteLine($"Added column '{col}' to chat_history table");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Chat history migration warning: {ex.Message}");
        }
    }

    private async Task CreateTablesAsync(MySqlConnection connection)
    {
        // Users table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS users (
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
            )");

        // Projects table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS projects (
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
            )");

        // Project files table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS project_files (
                id VARCHAR(36) PRIMARY KEY,
                project_id VARCHAR(36) NOT NULL,
                path VARCHAR(500) NOT NULL,
                content LONGTEXT,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
                UNIQUE KEY unique_project_path (project_id, path),
                INDEX idx_project_id (project_id)
            )");

        // Todos table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS todos (
                id VARCHAR(36) PRIMARY KEY,
                project_id VARCHAR(36) NOT NULL,
                text TEXT NOT NULL,
                completed BOOLEAN DEFAULT FALSE,
                priority VARCHAR(20) DEFAULT 'medium',
                agent VARCHAR(50),
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
                INDEX idx_project_id (project_id)
            )");

        // Jobs table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS jobs (
                id VARCHAR(36) PRIMARY KEY,
                project_id VARCHAR(36) NOT NULL,
                user_id VARCHAR(36) NOT NULL,
                prompt TEXT NOT NULL,
                status VARCHAR(50) DEFAULT 'pending',
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
                INDEX idx_user_status (user_id, status),
                INDEX idx_status (status)
            )");

        // Chat history table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS chat_history (
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
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                INDEX idx_user_conversation (user_id, conversation_id),
                INDEX idx_project_id (project_id)
            )");

        // User AI providers table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS user_ai_providers (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                provider VARCHAR(50) NOT NULL,
                api_key TEXT NOT NULL,
                model_preference VARCHAR(100),
                is_active BOOLEAN DEFAULT TRUE,
                is_default BOOLEAN DEFAULT FALSE,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                UNIQUE KEY unique_user_provider (user_id, provider),
                INDEX idx_user_id (user_id)
            )");

        // Credit history table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS credit_history (
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
            )");

        // Payment transactions table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS payment_transactions (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                session_id VARCHAR(255) NOT NULL UNIQUE,
                package_id VARCHAR(50) NOT NULL,
                amount DECIMAL(10,2) NOT NULL,
                currency VARCHAR(10) DEFAULT 'usd',
                credits INT NOT NULL,
                status VARCHAR(20) DEFAULT 'pending',
                payment_status VARCHAR(50) DEFAULT 'initiated',
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                completed_at DATETIME,
                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                INDEX idx_session_id (session_id),
                INDEX idx_user_id (user_id)
            )");

        // System settings table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS system_settings (
                setting_key VARCHAR(100) PRIMARY KEY,
                setting_value TEXT NOT NULL,
                setting_type VARCHAR(20) DEFAULT 'string',
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )");

        // Project runs table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS project_runs (
                id VARCHAR(36) PRIMARY KEY,
                project_id VARCHAR(36) NOT NULL,
                run_type VARCHAR(20) NOT NULL,
                status VARCHAR(20) DEFAULT 'pending',
                started_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                ended_at DATETIME,
                output TEXT,
                logs JSON,
                errors JSON,
                FOREIGN KEY (project_id) REFERENCES projects(id) ON DELETE CASCADE,
                INDEX idx_project_id (project_id)
            )");

        // Knowledge base table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS knowledge_base (
                id VARCHAR(36) PRIMARY KEY,
                question_hash VARCHAR(64) NOT NULL UNIQUE,
                question TEXT NOT NULL,
                answer TEXT NOT NULL,
                provider VARCHAR(100),
                usage_count INT DEFAULT 1,
                is_valid BOOLEAN DEFAULT TRUE,
                invalidated_at DATETIME,
                last_validated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_question_hash (question_hash)
            )");

        // User themes table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS user_themes (
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
            )");

        // IP records table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS ip_records (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                ip_address VARCHAR(45) NOT NULL,
                action VARCHAR(50) NOT NULL,
                user_agent TEXT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                INDEX idx_user_id (user_id),
                INDEX idx_timestamp (timestamp)
            )");

        // Subscription plans table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS subscription_plans (
                id VARCHAR(36) PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                description TEXT,
                price_monthly DECIMAL(10,2) NOT NULL,
                price_yearly DECIMAL(10,2),
                daily_credits INT DEFAULT 0,
                max_concurrent_workspaces INT DEFAULT 1,
                allows_own_api_keys BOOLEAN DEFAULT FALSE,
                features JSON,
                is_active BOOLEAN DEFAULT TRUE,
                sort_order INT DEFAULT 0,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )");

        // User subscriptions table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS user_subscriptions (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                plan_id VARCHAR(36) NOT NULL,
                status VARCHAR(20) DEFAULT 'active',
                stripe_subscription_id VARCHAR(255),
                start_date DATETIME NOT NULL,
                end_date DATETIME,
                next_billing_date DATETIME,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                INDEX idx_user_id (user_id),
                INDEX idx_status (status)
            )");

        // Credit packages table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS credit_packages (
                id VARCHAR(36) PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                credits INT NOT NULL,
                price DECIMAL(10,2) NOT NULL,
                is_active BOOLEAN DEFAULT TRUE,
                sort_order INT DEFAULT 0
            )");

        // Free AI providers table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS free_ai_providers (
                id VARCHAR(36) PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                provider VARCHAR(50) NOT NULL,
                api_key TEXT,
                model VARCHAR(100),
                is_enabled BOOLEAN DEFAULT TRUE,
                priority INT DEFAULT 0,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )");

        // Agent activity table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS agent_activity (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                project_id VARCHAR(36) NOT NULL,
                job_id VARCHAR(36),
                agent_id VARCHAR(50) NOT NULL,
                action VARCHAR(255) NOT NULL,
                tokens_used INT DEFAULT 0,
                credits_used DECIMAL(10,4) DEFAULT 0,
                success BOOLEAN DEFAULT TRUE,
                error TEXT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
                INDEX idx_user_id (user_id),
                INDEX idx_project_id (project_id),
                INDEX idx_timestamp (timestamp)
            )");

        // TOS (Terms of Service) versions table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS tos_versions (
                id VARCHAR(36) PRIMARY KEY,
                version VARCHAR(20) NOT NULL UNIQUE,
                title VARCHAR(255) NOT NULL,
                content JSON NOT NULL,
                effective_date DATETIME NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            )");

        // Default settings table
        await connection.ExecuteAsync(@"
            CREATE TABLE IF NOT EXISTS default_settings (
                setting_key VARCHAR(100) PRIMARY KEY,
                free_credits INT DEFAULT 100,
                language VARCHAR(10) DEFAULT 'en',
                theme_json JSON
            )");
    }

    private async Task InsertDefaultDataAsync(MySqlConnection connection)
    {
        // Insert default subscription plans - Monthly Plans
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO subscription_plans (id, name, description, price_monthly, price_yearly, daily_credits, max_concurrent_workspaces, allows_own_api_keys, features, sort_order) VALUES
            ('free', 'Free', 'Basic access with limited credits', 0, 0, 50, 1, FALSE, '[""50 daily credits"", ""1 workspace"", ""Basic code generation"", ""Community support""]', 0)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO subscription_plans (id, name, description, price_monthly, price_yearly, daily_credits, max_concurrent_workspaces, allows_own_api_keys, features, sort_order) VALUES
            ('starter', 'Starter', 'For individual developers getting started', 9.99, 99.00, 200, 3, FALSE, '[""200 daily credits"", ""3 workspaces"", ""All 7 AI agents"", ""Priority queue"", ""Email support""]', 1)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO subscription_plans (id, name, description, price_monthly, price_yearly, daily_credits, max_concurrent_workspaces, allows_own_api_keys, features, sort_order) VALUES
            ('pro', 'Pro', 'For professional developers and freelancers', 29.99, 299.00, 1000, 10, TRUE, '[""1000 daily credits"", ""10 workspaces"", ""All 7 AI agents"", ""Own API keys"", ""Advanced AI models"", ""Priority support""]', 2)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO subscription_plans (id, name, description, price_monthly, price_yearly, daily_credits, max_concurrent_workspaces, allows_own_api_keys, features, sort_order) VALUES
            ('team', 'Team', 'For small teams and startups', 79.99, 799.00, 3000, 25, TRUE, '[""3000 daily credits"", ""25 workspaces"", ""All 7 AI agents"", ""Own API keys"", ""Team collaboration"", ""Priority support"", ""Custom integrations""]', 3)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO subscription_plans (id, name, description, price_monthly, price_yearly, daily_credits, max_concurrent_workspaces, allows_own_api_keys, features, sort_order) VALUES
            ('enterprise', 'Enterprise', 'For large teams and organizations', 199.99, 1999.00, 10000, -1, TRUE, '[""10000 daily credits"", ""Unlimited workspaces"", ""All 7 AI agents"", ""Own API keys"", ""Custom AI models"", ""SLA support"", ""Custom integrations"", ""Dedicated account manager""]', 4)");

        // Insert default credit packages - Add More Credits
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-50', 'Starter Pack', 50, 2.99, 0)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-100', '100 Credits', 100, 4.99, 1)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-250', '250 Credits', 250, 9.99, 2)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-500', '500 Credits', 500, 17.99, 3)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-1000', '1000 Credits', 1000, 29.99, 4)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-2500', '2500 Credits', 2500, 69.99, 5)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-5000', '5000 Credits', 5000, 129.99, 6)");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order) VALUES
            ('pack-10000', '10000 Credits', 10000, 229.99, 7)");

        // Insert default system settings
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO system_settings (setting_key, setting_value, setting_type) VALUES
            ('credits_per_1k_tokens_chat', '0.5', 'decimal')");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO system_settings (setting_key, setting_value, setting_type) VALUES
            ('credits_per_1k_tokens_project', '1.0', 'decimal')");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO system_settings (setting_key, setting_value, setting_type) VALUES
            ('emergent_llm_enabled', 'true', 'boolean')");

        // Insert default TOS version
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO tos_versions (id, version, title, content, effective_date, created_at) VALUES
            ('tos-v1', '1.0', 'Terms of Service', '{""title"":""Terms of Service"",""sections"":[{""title"":""1. Acceptance of Terms"",""content"":""By accessing or using LittleHelper AI, you agree to be bound by these Terms of Service. If you do not agree to all the terms and conditions, you may not access or use the Service.""},{""title"":""2. Description of Service"",""content"":""LittleHelper AI provides AI-powered code generation, debugging, and software development assistance. The Service uses artificial intelligence models to generate code and provide development recommendations.""},{""title"":""3. No Warranty"",""content"":""THE SERVICE IS PROVIDED AS IS AND AS AVAILABLE WITHOUT WARRANTIES OF ANY KIND, EXPRESS OR IMPLIED. WE DO NOT WARRANT THAT THE SERVICE WILL BE UNINTERRUPTED, ERROR-FREE, OR THAT ANY CODE GENERATED WILL BE FREE OF BUGS OR SECURITY VULNERABILITIES.""},{""title"":""4. Limitation of Liability"",""content"":""IN NO EVENT SHALL LITTLEHELPER AI BE LIABLE FOR ANY INDIRECT, INCIDENTAL, SPECIAL, CONSEQUENTIAL, OR PUNITIVE DAMAGES ARISING OUT OF YOUR USE OF THE SERVICE OR ANY CODE GENERATED.""},{""title"":""5. User Responsibility"",""content"":""You are solely responsible for reviewing, testing, and validating all code generated by the Service before deploying it in any production environment.""},{""title"":""6. AI-Generated Content"",""content"":""All code generated by the Service is produced by artificial intelligence. AI-generated code may contain errors, bugs, or security vulnerabilities.""},{""title"":""7. Acceptable Use"",""content"":""You agree not to use the Service for any illegal purposes or to generate malicious code.""},{""title"":""8. Credits and Payments"",""content"":""Credits are non-refundable unless required by applicable law. Unused credits expire according to your subscription plan.""},{""title"":""9. Termination"",""content"":""We reserve the right to suspend or terminate your access to the Service at any time.""}],""disclaimer"":""BY USING THIS SERVICE, YOU ACKNOWLEDGE THAT AI-GENERATED CODE MAY CONTAIN ERRORS. YOU ASSUME ALL RISKS ASSOCIATED WITH THE USE OF ANY CODE GENERATED BY THE SERVICE.""}', NOW(), NOW())");

        // Insert default settings for new users
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO default_settings (setting_key, free_credits, language) VALUES ('new_user_defaults', 100, 'en')");

        // Insert default free AI providers
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO free_ai_providers (id, name, provider, api_key, model, is_enabled, priority, created_at, updated_at) VALUES
            ('groq', 'Groq (Free)', 'groq', '', 'llama-3.1-70b-versatile', TRUE, 1, NOW(), NOW())");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO free_ai_providers (id, name, provider, api_key, model, is_enabled, priority, created_at, updated_at) VALUES
            ('together', 'Together AI (Free)', 'together', '', 'meta-llama/Llama-3.2-11B-Vision-Instruct-Turbo', FALSE, 2, NOW(), NOW())");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO free_ai_providers (id, name, provider, api_key, model, is_enabled, priority, created_at, updated_at) VALUES
            ('huggingface', 'HuggingFace (Free)', 'huggingface', '', 'microsoft/DialoGPT-large', FALSE, 3, NOW(), NOW())");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO free_ai_providers (id, name, provider, api_key, model, is_enabled, priority, created_at, updated_at) VALUES
            ('openrouter', 'OpenRouter (Free)', 'openrouter', '', 'google/gemma-2-9b-it:free', FALSE, 4, NOW(), NOW())");
        
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO free_ai_providers (id, name, provider, api_key, model, is_enabled, priority, created_at, updated_at) VALUES
            ('ollama', 'Local Ollama (Free)', 'ollama', '', 'qwen2.5-coder:1.5b', FALSE, 5, NOW(), NOW())");

        // Insert default admin user (password: admin123)
        await connection.ExecuteAsync(@"
            INSERT IGNORE INTO users (id, email, name, password_hash, role, credits, plan, tos_accepted, tos_accepted_at, tos_version, created_at) VALUES
            ('admin-default', 'admin@littlehelper.ai', 'System Admin', '$2a$11$K8FHKFt1Y0kzKXCVpPGWoOjPF8Gw8QJQzXHnDrxXxJkCRvYJKMIwK', 'admin', 999999, 'enterprise', TRUE, NOW(), '1.0', NOW())");
    }
}
