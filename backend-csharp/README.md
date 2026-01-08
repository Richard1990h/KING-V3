# LittleHelper AI - C# Backend

## Quick Start (Windows + XAMPP)

### Prerequisites
1. **XAMPP** with MySQL running - [Download](https://www.apachefriends.org/)
2. **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
3. **Node.js 18+** - [Download](https://nodejs.org/)
4. **Yarn** - Install via `npm install -g yarn`

### Step 1: Import Database

**Option A: Using phpMyAdmin (Recommended)**
1. Start MySQL from XAMPP Control Panel
2. Open http://localhost/phpmyadmin
3. Click **Import** tab
4. Select file: `../database/littlehelper_ai_complete.sql`
5. Click **Go**

**Option B: Using Command Line**
```bash
cd backend-csharp
setup_database.bat
```

### Step 2: Start Application

**One-Click Start (Recommended)**
```bash
start_all.bat
```

This will:
- Check all prerequisites
- Install dependencies
- Start backend on http://localhost:8002
- Start frontend on http://localhost:3000

**Manual Start**
```bash
# Terminal 1: Backend
cd backend-csharp/LittleHelperAI.API
dotnet run --urls=http://localhost:8002

# Terminal 2: Frontend
cd frontend
yarn start
```

### Step 3: Access the Application

- **Frontend:** http://localhost:3000
- **Backend API:** http://localhost:8002
- **Swagger UI:** http://localhost:8002/swagger

### Default Login Credentials

| Role  | Email                    | Password  |
|-------|--------------------------|-----------|  
| Admin | admin@littlehelper.ai    | admin123  |
| User  | test@example.com         | test123   |

---

## Available Scripts

| Script               | Description                          |
|---------------------|--------------------------------------|
| `start_all.bat`     | Start both backend and frontend      |
| `stop_all.bat`      | Stop all services                    |
| `start_backend.bat` | Start backend only                   |
| `start_frontend.bat`| Start frontend only                  |
| `setup_database.bat`| Import database via command line     |
| `setup_and_run.bat` | Full setup with verification         |
| `verify_installation.bat` | Quick health check             |

---

## AI API Keys (Free Tiers)

The application supports multiple AI providers. Here's how to get free API keys:

| Provider    | Free Tier                | Get Key At                         |
|-------------|--------------------------|-----------------------------------|
| **Groq**    | 30 req/min, FREE         | https://console.groq.com          |
| **OpenRouter** | Many free models      | https://openrouter.ai/keys        |
| **HuggingFace** | Rate-limited, FREE   | https://huggingface.co/settings/tokens |
| **Google AI** | Generous free tier     | https://aistudio.google.com/apikey |

Add your API keys in `LittleHelperAI.API/appsettings.json` under `FreeAIProviders`.

---

## Project Structure

```
backend-csharp/
├── LittleHelperAI.API/      # ASP.NET Core Web API
│   ├── Controllers/         # API endpoints
│   ├── Services/            # Business logic
│   └── appsettings.json     # Configuration
├── LittleHelperAI.Agents/   # AI Agent logic
├── LittleHelperAI.Data/     # Database models & context
└── LittleHelperAI.sln       # Solution file

frontend/
├── src/
│   ├── components/          # React components
│   ├── pages/               # Page components
│   └── lib/                 # Utilities
└── package.json

database/
└── littlehelper_ai_complete.sql  # Complete database setup
```

---

## Troubleshooting

### MySQL Connection Error
- Ensure MySQL is running in XAMPP Control Panel
- Verify port 3306 is not blocked
- Check user `root` has no password (default XAMPP)

### Port Already in Use
```bash
# Find process using port 8002
netstat -ano | findstr :8002

# Kill the process
taskkill /f /pid <PID>
```

### Build Errors
```bash
# Clean and rebuild
dotnet clean
dotnet restore
dotnet build
```

### Frontend Not Loading
- Check backend is running first
- Verify `.env` has correct `REACT_APP_BACKEND_URL`
- Clear browser cache

---

## API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/auth/register` | POST | Register new user |
| `/api/auth/login` | POST | Login |
| `/api/projects` | GET/POST | Manage projects |
| `/api/admin/users` | GET | Admin: List users |
| `/api/plans` | GET | Get subscription plans |
| `/api/credits/packages` | GET | Get credit packages |
| `/api/system/health` | GET | Health check |

Full API documentation available at `/swagger` when backend is running.
