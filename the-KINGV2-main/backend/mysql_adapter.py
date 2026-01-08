"""
MySQL Database Adapter for LittleHelper AI
Provides async MySQL support using aiomysql
"""
import aiomysql
import os
from typing import Any, Dict, List, Optional
from datetime import datetime
import json

class MySQLDatabase:
    """Async MySQL database adapter that mimics MongoDB-style operations"""
    
    def __init__(self):
        self.pool: Optional[aiomysql.Pool] = None
        self.host = os.getenv("MYSQL_HOST", "localhost")
        self.port = int(os.getenv("MYSQL_PORT", 3306))
        self.user = os.getenv("MYSQL_USER", "root")
        self.password = os.getenv("MYSQL_PASSWORD", "")
        self.database = os.getenv("MYSQL_DATABASE", os.getenv("DB_NAME", "littlehelper_ai"))
    
    async def connect(self):
        """Create connection pool"""
        # First, ensure database exists
        await self._ensure_database()
        
        self.pool = await aiomysql.create_pool(
            host=self.host,
            port=self.port,
            user=self.user,
            password=self.password,
            db=self.database,
            autocommit=True,
            minsize=1,
            maxsize=10
        )
        
        # Initialize tables
        await self._init_tables()
        print(f"Connected to MySQL: {self.host}:{self.port}/{self.database}")
    
    async def _ensure_database(self):
        """Create database if it doesn't exist"""
        conn = await aiomysql.connect(
            host=self.host,
            port=self.port,
            user=self.user,
            password=self.password
        )
        async with conn.cursor() as cur:
            await cur.execute(f"CREATE DATABASE IF NOT EXISTS `{self.database}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci")
        conn.close()
    
    async def _init_tables(self):
        """Create all required tables"""
        tables = [
            # Users table
            """
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
                theme JSON,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                last_login_at DATETIME,
                INDEX idx_email (email),
                INDEX idx_role (role)
            )
            """,
            # Projects table
            """
            CREATE TABLE IF NOT EXISTS projects (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                name VARCHAR(255) NOT NULL,
                description TEXT,
                language VARCHAR(50) DEFAULT 'Python',
                status VARCHAR(20) DEFAULT 'active',
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_user_id (user_id)
            )
            """,
            # Project files table
            """
            CREATE TABLE IF NOT EXISTS project_files (
                id VARCHAR(36) PRIMARY KEY,
                project_id VARCHAR(36) NOT NULL,
                path VARCHAR(500) NOT NULL,
                content LONGTEXT,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                UNIQUE KEY unique_project_path (project_id, path),
                INDEX idx_project_id (project_id)
            )
            """,
            # Todos table
            """
            CREATE TABLE IF NOT EXISTS todos (
                id VARCHAR(36) PRIMARY KEY,
                project_id VARCHAR(36) NOT NULL,
                text TEXT NOT NULL,
                completed BOOLEAN DEFAULT FALSE,
                priority VARCHAR(20) DEFAULT 'medium',
                agent VARCHAR(50),
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_project_id (project_id)
            )
            """,
            # Jobs table
            """
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
                INDEX idx_user_status (user_id, status),
                INDEX idx_status (status)
            )
            """,
            # Chat history table
            """
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
                INDEX idx_user_conversation (user_id, conversation_id),
                INDEX idx_project_id (project_id)
            )
            """,
            # User AI providers table
            """
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
                UNIQUE KEY unique_user_provider (user_id, provider),
                INDEX idx_user_id (user_id)
            )
            """,
            # Credit history table
            """
            CREATE TABLE IF NOT EXISTS credit_history (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                delta DECIMAL(10,4) NOT NULL,
                reason VARCHAR(255) NOT NULL,
                reference_type VARCHAR(50),
                reference_id VARCHAR(36),
                balance_after DECIMAL(10,4) NOT NULL,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_user_id (user_id),
                INDEX idx_created_at (created_at)
            )
            """,
            # Payment transactions table
            """
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
                INDEX idx_session_id (session_id),
                INDEX idx_user_id (user_id)
            )
            """,
            # System settings table
            """
            CREATE TABLE IF NOT EXISTS system_settings (
                setting_key VARCHAR(100) PRIMARY KEY,
                setting_value TEXT NOT NULL,
                setting_type VARCHAR(20) DEFAULT 'string',
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
            )
            """,
            # Knowledge base table
            """
            CREATE TABLE IF NOT EXISTS knowledge_base (
                id VARCHAR(36) PRIMARY KEY,
                question_hash VARCHAR(64) NOT NULL UNIQUE,
                question TEXT NOT NULL,
                answer TEXT NOT NULL,
                provider VARCHAR(100),
                hit_count INT DEFAULT 1,
                is_valid BOOLEAN DEFAULT TRUE,
                invalidated_at DATETIME,
                created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                updated_at DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                INDEX idx_question_hash (question_hash)
            )
            """,
            # IP records table
            """
            CREATE TABLE IF NOT EXISTS ip_records (
                id VARCHAR(36) PRIMARY KEY,
                user_id VARCHAR(36) NOT NULL,
                ip_address VARCHAR(45) NOT NULL,
                action VARCHAR(50) NOT NULL,
                user_agent TEXT,
                timestamp DATETIME DEFAULT CURRENT_TIMESTAMP,
                INDEX idx_user_id (user_id),
                INDEX idx_timestamp (timestamp)
            )
            """,
            # Subscription plans table
            """
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
            )
            """,
            # Credit packages table
            """
            CREATE TABLE IF NOT EXISTS credit_packages (
                id VARCHAR(36) PRIMARY KEY,
                name VARCHAR(100) NOT NULL,
                credits INT NOT NULL,
                price DECIMAL(10,2) NOT NULL,
                is_active BOOLEAN DEFAULT TRUE,
                sort_order INT DEFAULT 0
            )
            """
        ]
        
        async with self.pool.acquire() as conn:
            async with conn.cursor() as cur:
                for table_sql in tables:
                    await cur.execute(table_sql)
                
                # Insert default data
                await self._insert_defaults(cur)
    
    async def _insert_defaults(self, cur):
        """Insert default subscription plans and credit packages"""
        # Default plans
        plans = [
            ("free", "Free", "Basic access with limited credits", 0, 0, 50, 1, False, '["Basic code generation", "50 daily credits", "1 workspace"]', 0),
            ("starter", "Starter", "For individual developers", 9.99, 99, 200, 3, False, '["200 daily credits", "3 workspaces", "Priority support"]', 1),
            ("pro", "Pro", "For professional developers", 29.99, 299, 1000, 10, True, '["1000 daily credits", "10 workspaces", "Own API keys", "Advanced AI models"]', 2),
            ("enterprise", "Enterprise", "For teams and organizations", 99.99, 999, 5000, -1, True, '["5000 daily credits", "Unlimited workspaces", "Own API keys", "SLA support"]', 3)
        ]
        
        for plan in plans:
            await cur.execute("""
                INSERT IGNORE INTO subscription_plans 
                (id, name, description, price_monthly, price_yearly, daily_credits, max_concurrent_workspaces, allows_own_api_keys, features, sort_order)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            """, plan)
        
        # Default credit packages
        packages = [
            ("pack-100", "100 Credits", 100, 4.99, 0),
            ("pack-500", "500 Credits", 500, 19.99, 1),
            ("pack-1000", "1000 Credits", 1000, 34.99, 2),
            ("pack-5000", "5000 Credits", 5000, 149.99, 3)
        ]
        
        for pkg in packages:
            await cur.execute("""
                INSERT IGNORE INTO credit_packages (id, name, credits, price, sort_order)
                VALUES (%s, %s, %s, %s, %s)
            """, pkg)
    
    async def close(self):
        """Close connection pool"""
        if self.pool:
            self.pool.close()
            await self.pool.wait_closed()
    
    def __getattr__(self, name: str) -> 'MySQLCollection':
        """Get a collection (table) by name - mimics MongoDB db.collection syntax"""
        return MySQLCollection(self.pool, name)


class MySQLCollection:
    """Mimics MongoDB collection operations for MySQL tables"""
    
    def __init__(self, pool: aiomysql.Pool, table: str):
        self.pool = pool
        self.table = table
    
    async def find_one(self, filter_dict: Dict[str, Any], projection: Dict[str, Any] = None) -> Optional[Dict[str, Any]]:
        """Find a single document"""
        where, values = self._build_where(filter_dict)
        columns = self._build_columns(projection)
        
        async with self.pool.acquire() as conn:
            async with conn.cursor(aiomysql.DictCursor) as cur:
                await cur.execute(f"SELECT {columns} FROM {self.table} {where} LIMIT 1", values)
                result = await cur.fetchone()
                return self._process_result(result) if result else None
    
    async def find(self, filter_dict: Dict[str, Any] = None, projection: Dict[str, Any] = None) -> 'MySQLCursor':
        """Find multiple documents - returns a cursor-like object"""
        return MySQLCursor(self.pool, self.table, filter_dict or {}, projection)
    
    async def insert_one(self, document: Dict[str, Any]) -> Any:
        """Insert a single document"""
        columns = list(document.keys())
        values = [self._serialize_value(v) for v in document.values()]
        placeholders = ", ".join(["%s"] * len(columns))
        
        async with self.pool.acquire() as conn:
            async with conn.cursor() as cur:
                await cur.execute(
                    f"INSERT INTO {self.table} ({', '.join(columns)}) VALUES ({placeholders})",
                    values
                )
                return document.get("id")
    
    async def update_one(self, filter_dict: Dict[str, Any], update: Dict[str, Any], upsert: bool = False) -> int:
        """Update a single document"""
        if "$set" in update:
            update_data = update["$set"]
        else:
            update_data = update
        
        set_clause = ", ".join([f"{k} = %s" for k in update_data.keys()])
        set_values = [self._serialize_value(v) for v in update_data.values()]
        
        where, where_values = self._build_where(filter_dict)
        
        async with self.pool.acquire() as conn:
            async with conn.cursor() as cur:
                if upsert:
                    # Try to find existing
                    existing = await self.find_one(filter_dict)
                    if not existing:
                        # Insert new
                        combined = {**filter_dict, **update_data}
                        await self.insert_one(combined)
                        return 1
                
                await cur.execute(
                    f"UPDATE {self.table} SET {set_clause} {where}",
                    set_values + where_values
                )
                return cur.rowcount
    
    async def delete_one(self, filter_dict: Dict[str, Any]) -> int:
        """Delete a single document"""
        where, values = self._build_where(filter_dict)
        
        async with self.pool.acquire() as conn:
            async with conn.cursor() as cur:
                await cur.execute(f"DELETE FROM {self.table} {where} LIMIT 1", values)
                return cur.rowcount
    
    async def delete_many(self, filter_dict: Dict[str, Any]) -> int:
        """Delete multiple documents"""
        where, values = self._build_where(filter_dict)
        
        async with self.pool.acquire() as conn:
            async with conn.cursor() as cur:
                await cur.execute(f"DELETE FROM {self.table} {where}", values)
                return cur.rowcount
    
    async def count_documents(self, filter_dict: Dict[str, Any] = None) -> int:
        """Count documents"""
        where, values = self._build_where(filter_dict or {})
        
        async with self.pool.acquire() as conn:
            async with conn.cursor() as cur:
                await cur.execute(f"SELECT COUNT(*) FROM {self.table} {where}", values)
                result = await cur.fetchone()
                return result[0] if result else 0
    
    def _build_where(self, filter_dict: Dict[str, Any]) -> tuple:
        """Build WHERE clause from filter dictionary"""
        if not filter_dict:
            return "", []
        
        conditions = []
        values = []
        
        for key, value in filter_dict.items():
            if isinstance(value, dict):
                # Handle operators like $in, $ne, etc.
                for op, val in value.items():
                    if op == "$in":
                        placeholders = ", ".join(["%s"] * len(val))
                        conditions.append(f"{key} IN ({placeholders})")
                        values.extend(val)
                    elif op == "$ne":
                        conditions.append(f"{key} != %s")
                        values.append(val)
                    elif op == "$gt":
                        conditions.append(f"{key} > %s")
                        values.append(val)
                    elif op == "$gte":
                        conditions.append(f"{key} >= %s")
                        values.append(val)
                    elif op == "$lt":
                        conditions.append(f"{key} < %s")
                        values.append(val)
                    elif op == "$lte":
                        conditions.append(f"{key} <= %s")
                        values.append(val)
            else:
                conditions.append(f"{key} = %s")
                values.append(value)
        
        return f"WHERE {' AND '.join(conditions)}" if conditions else "", values
    
    def _build_columns(self, projection: Dict[str, Any]) -> str:
        """Build column selection from projection"""
        if not projection:
            return "*"
        
        # Handle MongoDB-style projection where 0 means exclude
        includes = [k for k, v in projection.items() if v == 1]
        excludes = [k for k, v in projection.items() if v == 0]
        
        if includes:
            return ", ".join(includes)
        elif excludes:
            # We can't easily exclude columns in SQL, so return all
            return "*"
        return "*"
    
    def _serialize_value(self, value: Any) -> Any:
        """Serialize value for MySQL storage"""
        if isinstance(value, (dict, list)):
            return json.dumps(value)
        if isinstance(value, datetime):
            return value.strftime("%Y-%m-%d %H:%M:%S")
        return value
    
    def _process_result(self, result: Dict[str, Any]) -> Dict[str, Any]:
        """Process result, parsing JSON fields"""
        if not result:
            return result
        
        for key, value in result.items():
            if isinstance(value, str) and value.startswith('{') or isinstance(value, str) and value.startswith('['):
                try:
                    result[key] = json.loads(value)
                except:
                    pass
        return result


class MySQLCursor:
    """Cursor-like object for MySQL queries"""
    
    def __init__(self, pool: aiomysql.Pool, table: str, filter_dict: Dict[str, Any], projection: Dict[str, Any] = None):
        self.pool = pool
        self.table = table
        self.filter_dict = filter_dict
        self.projection = projection
        self._sort = None
        self._limit = None
        self._skip = None
    
    def sort(self, key_or_list, direction: int = 1) -> 'MySQLCursor':
        """Sort results"""
        if isinstance(key_or_list, str):
            self._sort = [(key_or_list, direction)]
        else:
            self._sort = key_or_list
        return self
    
    def limit(self, count: int) -> 'MySQLCursor':
        """Limit results"""
        self._limit = count
        return self
    
    def skip(self, count: int) -> 'MySQLCursor':
        """Skip results"""
        self._skip = count
        return self
    
    async def to_list(self, length: int = None) -> List[Dict[str, Any]]:
        """Execute query and return results as list"""
        collection = MySQLCollection(self.pool, self.table)
        where, values = collection._build_where(self.filter_dict)
        columns = collection._build_columns(self.projection)
        
        query = f"SELECT {columns} FROM {self.table} {where}"
        
        if self._sort:
            order_parts = []
            for field, direction in self._sort:
                order_parts.append(f"{field} {'DESC' if direction == -1 else 'ASC'}")
            query += f" ORDER BY {', '.join(order_parts)}"
        
        limit = length or self._limit
        if limit:
            query += f" LIMIT {limit}"
        
        if self._skip:
            query += f" OFFSET {self._skip}"
        
        async with self.pool.acquire() as conn:
            async with conn.cursor(aiomysql.DictCursor) as cur:
                await cur.execute(query, values)
                results = await cur.fetchall()
                return [collection._process_result(r) for r in results]


# Global database instance
mysql_db = MySQLDatabase()

async def get_mysql_db() -> MySQLDatabase:
    """Get the MySQL database instance"""
    return mysql_db
