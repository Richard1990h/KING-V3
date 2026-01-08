# LittleHelper AI - C# / .NET 8 Backend Reference

This is the **production-ready C# backend** for LittleHelper AI, designed to run with MySQL database managed via XAMPP.

**Last Updated:** January 2025
**Feature Parity:** Synced with Python/FastAPI backend

## Technology Stack

- **Framework**: .NET 8 (LTS)
- **Web API**: ASP.NET Core
- **Database**: MySQL 8.x (via XAMPP)
- **ORM**: Dapper (lightweight, high-performance)
- **Cache**: Redis (optional, falls back to in-memory)
- **Payments**: Stripe
- **Authentication**: JWT Bearer tokens

## Project Structure

```
LittleHelperAI/
├── LittleHelperAI.sln              # Solution file
├── LittleHelperAI.API/             # Web API project
│   ├── Controllers/                # API endpoints
│   │   ├── AuthController.cs       # Authentication (TOS, Login, Register)
│   │   ├── UserController.cs       # Profile, Theme, Avatar, API Keys
│   │   ├── LegalController.cs      # Terms of Service, Privacy Policy
│   │   ├── PlansController.cs      # Subscription Plans & Add-ons
│   │   ├── ProjectsController.cs   # Projects & Files
│   │   ├── JobsController.cs       # Multi-agent pipeline
│   │   ├── CreditsController.cs    # Credit system
│   │   └── AdminController.cs      # Admin (users, settings, KB, AI)
│   ├── Services/                   # Business logic
│   │   ├── IAuthService.cs         # Auth interface
│   │   ├── AuthService.cs          # Auth implementation
│   │   ├── ICreditService.cs       # Credits interface
│   │   ├── CreditService.cs        # Credits implementation
│   │   ├── IAIService.cs           # AI interface
│   │   ├── AIService.cs            # AI implementation
│   │   ├── IProjectService.cs      # Project interface
│   │   ├── ProjectService.cs       # Project implementation
│   │   ├── IJobOrchestrationService.cs  # Job interface
│   │   ├── JobOrchestrationService.cs   # Job implementation
│   │   ├── ICacheService.cs        # Cache interface
│   │   └── JobWorkerService.cs     # Background worker
│   ├── Middleware/                 # Custom middleware
│   │   ├── ErrorHandlingMiddleware.cs   # Global error handling
│   │   └── RequestLoggingMiddleware.cs  # Request logging
│   ├── Program.cs                  # Application entry point
│   └── appsettings.json           # Configuration
├── LittleHelperAI.Agents/          # Agent library
│   ├── BaseAgent.cs               # Abstract base class
│   ├── AgentRegistry.cs           # Agent registry
│   ├── PlannerAgent.cs            # Task breakdown
│   ├── ResearcherAgent.cs         # Knowledge gathering
│   ├── DeveloperAgent.cs          # Code generation
│   ├── TestDesignerAgent.cs       # Test creation
│   ├── ExecutorAgent.cs           # Code execution
│   ├── DebuggerAgent.cs           # Error fixing
│   ├── VerifierAgent.cs           # Validation
│   └── ErrorAnalyzerAgent.cs      # Error analysis
├── LittleHelperAI.Data/           # Data access layer
│   ├── MySqlDbContext.cs          # Database context (schema creation)
│   ├── Models/                    # Entity models
│   │   └── Models.cs              # All data models
│   └── Repositories/              # Data repositories
├── LittleHelperAI.Workers/        # Background jobs
│   └── JobWorkerService.cs        # Multi-agent orchestration
└── Docker/
    ├── Dockerfile
    └── docker-compose.yml
```

## Feature Parity with Python Backend

| Feature | Status |
|---------|--------|
| User Authentication (JWT) | ✅ |
| Terms of Service (TOS) | ✅ |
| User Profiles & Themes | ✅ |
| IP Address Tracking | ✅ |
| Subscription Plans | ✅ |
| Credit System | ✅ |
| Credit Add-on Packages | ✅ |
| Knowledge Base | ✅ |
| Free AI Providers | ✅ |
| Multi-Agent Pipeline | ✅ |
| Project Management | ✅ |
| File Management | ✅ |
| Chat History | ✅ |
| Todo Items | ✅ |
| Admin Panel APIs | ✅ |

## Setup Instructions

### Prerequisites

1. **.NET 8 SDK** - Download from https://dotnet.microsoft.com/download
2. **XAMPP** - With MySQL running on localhost:3306
3. **Redis** (optional) - For caching and rate limiting
4. **Visual Studio 2022** or **VS Code** with C# extension

### Database Setup

1. Start XAMPP and ensure MySQL is running
2. The application will automatically create the `littlehelper_ai` database on first run
3. All tables are created via the `MySqlDbContext.InitializeAsync()` method

### Configuration

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "MySQL": "Server=localhost;Database=littlehelper_ai;User=root;Password=;",
    "Redis": "localhost:6379"
  },
  "JWT": {
    "Secret": "your-secure-secret-key-here",
    "ExpirationHours": 24
  },
  "Stripe": {
    "SecretKey": "sk_test_your_stripe_key"
  },
  "LocalLLM": {
    "Url": "http://localhost:11434",
    "Model": "qwen2.5-coder:1.5b"
  }
}
```

### Running the Application

```bash
# Restore packages
dotnet restore

# Build
dotnet build

# Run
cd LittleHelperAI.API
dotnet run

# Or with hot reload
dotnet watch run
```

The API will start on `http://localhost:8001`.

### API Documentation

Swagger UI is available at `http://localhost:8001/swagger` when running in Development mode.

## Key Features

### Multi-Agent Pipeline

The system uses 8 specialized agents:

1. **Planner** - Analyzes requirements, creates task breakdown
2. **Researcher** - Gathers best practices and documentation
3. **Developer** - Writes code and creates files
4. **Test Designer** - Creates comprehensive tests
5. **Executor** - Runs code in sandbox
6. **Debugger** - Identifies and fixes errors
7. **Verifier** - Validates against requirements
8. **Error Analyzer** - Analyzes errors and dispatches fixes

### Credit System

- Users purchase credits via Stripe
- Credits are deducted based on token usage
- Users with their own API keys don't use credits
- Admin can disable credits for specific users
- Cost estimation before job execution

### SSE Streaming

Real-time job updates via Server-Sent Events:

```javascript
const eventSource = new EventSource(`/api/jobs/${jobId}/execute`);
eventSource.onmessage = (event) => {
    const update = JSON.parse(event.data);
    console.log('Job update:', update);
};
```

## Default Credentials

- **Admin**: admin@littlehelper.ai / admin123

## Docker Deployment

```bash
cd Docker
docker-compose up -d
```

This will start:
- The .NET API on port 8001
- MySQL on port 3306
- Redis on port 6379

## License

Proprietary - LittleHelper AI
