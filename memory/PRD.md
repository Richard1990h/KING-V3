# LittleHelper AI - Product Requirements Document

## Original Problem Statement
Build a full-stack application with a C#/.NET backend and React frontend featuring:
- AI Code Execution Environment (sandboxed)
- Google Drive Integration
- Advanced Collaboration Features (Friends, DMs, Project Sharing)
- Admin Features (Announcements, Auto-friending)
- Gamification (Wave Defense game on login)
- Real-time Notifications via WebSockets
- Redis Caching

## Architecture Constraint
**CRITICAL**: The C#/.NET backend CANNOT run in the Emergent development environment (Python/FastAPI only). All backend code must be deployed externally using Docker.

## Tech Stack
- **Frontend**: React, Tailwind CSS, Shadcn UI, Framer Motion
- **Backend**: C# ASP.NET Core, Dapper, JWT, SignalR
- **Database**: MariaDB/MySQL
- **Caching**: Redis
- **Deployment**: Docker, Docker Compose

---

## NEW: Sandboxed Execution Layer & Agent Pipeline (January 9, 2025)

### Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                      Frontend (React)                            │
│   Submit Job → Poll Status → Get Results                         │
└─────────────────────────┬────────────────────────────────────────┘
                          │
                          ▼
┌──────────────────────────────────────────────────────────────────┐
│                    SandboxController                             │
│   /api/sandbox/jobs (async) │ /api/sandbox/execute (sync)        │
└─────────────────────────┬────────────────────────────────────────┘
                          │
          ┌───────────────┼───────────────┐
          ▼               ▼               ▼
    ┌──────────┐   ┌──────────┐   ┌──────────────┐
    │ JobQueue │   │RateLimiter│   │AgentPipeline │
    │  (Async) │   │           │   │ (Sync Mode)  │
    └────┬─────┘   └───────────┘   └──────┬───────┘
         │                                │
         ▼                                ▼
    ┌──────────┐                ┌─────────────────────┐
    │JobWorker │                │   Pipeline Phases   │
    │(Background)               │                     │
    └────┬─────┘                │ 1. Code Generation  │
         │                      │ 2. Static Analysis  │
         │                      │ 3. Build/Compile    │
         │                      │ 4. Test Generation  │
         │                      │ 5. Test Execution   │
         │                      │ 6. Runtime Exec     │
         │                      │ 7. Verification Gate│
         └──────────────────────┴─────────┬───────────┘
                                          │
                               ┌──────────┴──────────┐
                               ▼                     ▼
                        ┌────────────┐        ┌────────────┐
                        │  Sandbox   │        │   Static   │
                        │  Executor  │        │  Analyzer  │
                        │  (Docker)  │        │  (Linters) │
                        └────────────┘        └────────────┘
```

### Components Implemented

#### 1. SandboxExecutor (`SandboxExecutor.cs`)
- Docker-based code execution with resource limits
- CPU, memory, PID limits
- Network isolation (optional)
- Read-only filesystem with temp writable
- Multi-language support: Python, JavaScript/TypeScript, C#, Go, Java, Rust, Ruby, PHP
- Error parsing with file/line/column info
- Stack trace extraction
- Retry logic for transient failures

#### 2. StaticAnalyzer (`StaticAnalyzer.cs`)
- Syntax validation (AST-level checks)
- Language-specific linters (pylint, ESLint, Roslyn, go vet)
- Bracket matching
- Code quality scoring
- Pass/fail gate based on configurable thresholds

#### 3. TestGenerator (`TestGenerator.cs`)
- Function/method extraction from code
- Auto-generates test cases for:
  - Python (pytest)
  - JavaScript (Jest)
  - C# (xUnit)
  - Go (testing)
  - Java (JUnit)
- Handles async functions
- Generates edge case tests

#### 4. VerificationGate (`VerificationGate.cs`)
- Deterministic pass/fail before delivery
- Multi-category validation:
  - Code Quality (syntax, lint score)
  - Tests (pass rate, coverage)
  - Security (hardcoded secrets, SQL injection, dangerous functions)
  - Build (errors, warnings)
  - Runtime (exceptions)
- Configurable thresholds
- Weighted scoring

#### 5. RateLimiter (`RateLimiter.cs`)
- Per-user rate limits (requests/min, requests/hour)
- Per-user daily cost limits
- Per-project daily cost limits
- Concurrent execution limits
- Cost calculation based on:
  - Tokens used
  - Iterations
  - Sandbox execution time
  - Number of executions

#### 6. JobQueue (`JobQueue.cs`)
- Async job submission
- Background workers (configurable count)
- Job status tracking
- Job cancellation
- Webhook notifications on completion
- Result storage with TTL

#### 7. AgentPipeline (`AgentPipeline.cs`)
- Closed-loop execution with self-correction
- Max iteration limits (configurable)
- Error context fed back to AI for fixes
- Phase-by-phase execution
- Complete audit trail

### API Endpoints

```
POST /api/sandbox/execute       - Sync execution (quick operations)
POST /api/sandbox/analyze       - Static analysis
POST /api/sandbox/generate-tests - Test generation
POST /api/sandbox/verify        - Verification gate check
POST /api/sandbox/pipeline      - Full pipeline (sync)
POST /api/sandbox/jobs          - Submit async job
GET  /api/sandbox/jobs          - List user's jobs
GET  /api/sandbox/jobs/{id}     - Get job status
GET  /api/sandbox/jobs/{id}/result - Get job result
DELETE /api/sandbox/jobs/{id}   - Cancel job
GET  /api/sandbox/usage         - Usage statistics
GET  /api/sandbox/limits/check  - Check rate limits
```

### Configuration (appsettings.json)

```json
{
  "Sandbox": {
    "WorkspacePath": "/tmp/sandbox-workspaces",
    "MaxConcurrentExecutions": 5,
    "MemoryLimitMb": 512,
    "CpuLimit": 1.0,
    "PidsLimit": 100,
    "DefaultTimeoutSeconds": 60,
    "PythonImage": "python:3.11-slim",
    "NodeImage": "node:20-slim",
    "DotNetImage": "mcr.microsoft.com/dotnet/sdk:8.0"
  },
  "RateLimits": {
    "MaxRequestsPerMinute": 10,
    "MaxRequestsPerHour": 100,
    "MaxDailyCostPerUser": 10.00,
    "MaxDailyCostPerProject": 50.00,
    "CostPerToken": 0.00001
  },
  "Verification": {
    "MinQualityScore": 70,
    "MinTestPassRate": 80,
    "RequireTests": true,
    "MaxBuildWarnings": 10
  }
}
```

### Service Registration

```csharp
// In Program.cs or Startup.cs
services.AddSandboxServices(configuration);
services.AddCodeGeneratorService<AICodeGenerator>(); // Or your implementation
```

---

## Current Status

### ✅ Completed

#### Core Backend Services
- Sandbox execution layer with Docker isolation
- Static analysis with multi-language support
- Auto test generation
- Verification gate with security checks
- Rate limiting and cost tracking
- Async job queue with background workers
- Closed-loop agent pipeline with self-correction

#### Frontend
- Integrated Chat System (AI + Friends + DMs)
- Project Collaboration (invite friends)
- Admin Panel with Site Settings
- Admin Team Status (online/offline visibility)

#### Database
- All tables defined
- Indexes for performance

### 🔴 Blocked (Requires External Deployment)
- All backend functionality requires Docker deployment

### 📋 Next Steps

1. **Deploy Backend**
   - `docker-compose up -d` in `/app/backend-csharp/Docker/`
   - Configure Docker daemon on host for sandbox execution

2. **Implement AI Code Generator**
   - Replace `DefaultCodeGenerator` with actual AI service
   - Options: OpenAI, Claude, Groq, Together AI

3. **Frontend Integration**
   - Add UI for job submission
   - Real-time job status updates
   - Results visualization

### 📦 Future/Backlog
- Google Drive integration
- Live collaboration (cursor tracking)
- Credit-sharing rules
- WebSocket-based real-time job updates

---

## Key Files

### Sandbox Services
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/SandboxExecutor.cs`
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/StaticAnalyzer.cs`
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/TestGenerator.cs`
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/VerificationGate.cs`
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/RateLimiter.cs`
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/JobQueue.cs`
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/AgentPipeline.cs`
- `/app/backend-csharp/LittleHelperAI.API/Services/Sandbox/SandboxServiceExtensions.cs`
- `/app/backend-csharp/LittleHelperAI.API/Controllers/SandboxController.cs`

### Other Backend
- `/app/backend-csharp/Docker/docker-compose.yml`
- `/app/database/littlehelper_ai_complete.sql`

### Frontend
- `/app/frontend/src/components/GlobalAssistant.jsx`
- `/app/frontend/src/pages/Workspace.jsx`
- `/app/frontend/src/pages/Admin.jsx`

---

## Deployment Instructions

1. **Backend Deployment**
   ```bash
   cd /app/backend-csharp/Docker/
   docker-compose up -d
   ```

2. **Database Setup**
   ```bash
   mysql -u root -p littlehelper_ai < /app/database/littlehelper_ai_complete.sql
   ```

3. **Docker Daemon for Sandbox**
   - Ensure Docker is installed on the host
   - The sandbox executor needs access to Docker socket
   - Consider using Docker-in-Docker (DinD) for isolation

4. **Frontend Configuration**
   - Update `REACT_APP_BACKEND_URL` to deployed backend URL

## Test Credentials
- Admin: `admin@littlehelper.ai` / `admin123`
- Test User: `test@example.com` / `test123`

---
Last Updated: January 9, 2025
