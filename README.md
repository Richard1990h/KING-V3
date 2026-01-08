# LittleHelper AI

A full-stack AI-powered code generation and development assistance platform.

## 🚀 Quick Start (Windows + XAMPP)

### Prerequisites
- **XAMPP** with MySQL - [Download](https://www.apachefriends.org/)
- **.NET 8 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/8.0)
- **Node.js 18+** - [Download](https://nodejs.org/)

### One-Click Setup

```bash
# 1. Start MySQL from XAMPP Control Panel

# 2. Import database via phpMyAdmin
#    Open: http://localhost/phpmyadmin
#    Import: /database/littlehelper_ai_complete.sql

# 3. Start everything
cd backend-csharp
start_all.bat
```

That's it! The app will open at http://localhost:3000

### Default Logins

| Role  | Email                    | Password  |
|-------|--------------------------|-----------|
| Admin | admin@littlehelper.ai    | admin123  |
| User  | test@example.com         | test123   |

---

## 📁 Project Structure

```
littlehelper-ai/
├── backend-csharp/          # C# .NET 8 Backend
│   ├── LittleHelperAI.API/      # Main API project
│   ├── LittleHelperAI.Agents/   # AI Agent implementations
│   ├── LittleHelperAI.Data/     # Data layer & models
│   ├── start_all.bat            # ⭐ One-click startup
│   ├── setup_database.bat       # Database import helper
│   └── appsettings.json         # Configuration
│
├── frontend/                # React 19 Frontend
│   ├── src/
│   │   ├── components/          # UI Components (shadcn/ui)
│   │   ├── pages/               # Page components
│   │   └── lib/                 # Utilities & API client
│   └── .env                     # Backend URL config
│
└── database/                # Database scripts
    └── littlehelper_ai_complete.sql  # Complete DB setup
```

---

## ⚙️ Tech Stack

| Layer | Technology |
|-------|------------|
| **Backend** | C# .NET 8, ASP.NET Core, Dapper |
| **Frontend** | React 19, Tailwind CSS, shadcn/ui |
| **Database** | MySQL/MariaDB (XAMPP compatible) |
| **Auth** | JWT Bearer Tokens |
| **AI** | Multiple providers (Groq, OpenRouter, etc.) |

---

## 🤖 Features

### AI Agents
- **Planner** - Breaks down tasks into manageable steps
- **Researcher** - Finds relevant documentation and solutions
- **Developer** - Writes clean, functional code
- **Test Designer** - Creates comprehensive test cases
- **Executor** - Runs code and captures output
- **Debugger** - Identifies and fixes issues
- **Verifier** - Validates code quality and correctness

### Platform Features
- 💳 **Credit System** - Pay-per-use with multiple packages
- 📊 **Admin Dashboard** - User management, analytics, settings
- 🎨 **Custom Themes** - Personalize your workspace colors
- 📱 **Responsive Design** - Works on desktop and mobile
- 🔐 **Secure Auth** - JWT-based authentication
- 📝 **Terms of Service** - Built-in TOS acceptance flow

---

## 💰 Subscription Plans

| Plan | Price/Month | Daily Credits | Workspaces | Own API Keys |
|------|-------------|---------------|------------|--------------|
| Free | $0 | 50 | 1 | ❌ |
| Starter | $9.99 | 200 | 3 | ❌ |
| Pro | $29.99 | 1,000 | 10 | ✅ |
| Team | $79.99 | 3,000 | 25 | ✅ |
| Enterprise | $199.99 | 10,000 | Unlimited | ✅ |

### Credit Packages (Add-ons)

| Package | Credits | Price | Bonus |
|---------|---------|-------|-------|
| Starter Pack | 50 | $2.99 | - |
| 100 Credits | 100 | $4.99 | - |
| 250 Credits | 250 | $9.99 | +25 |
| 500 Credits | 500 | $17.99 | +50 |
| 1000 Credits | 1,000 | $29.99 | +150 |
| 2500 Credits | 2,500 | $69.99 | +500 |
| 5000 Credits | 5,000 | $129.99 | +1,000 |
| 10000 Credits | 10,000 | $229.99 | +2,500 |

---

## 🔑 Free AI API Keys

Get free API keys to power the AI features:

| Provider | Free Tier | Get Key |
|----------|-----------|---------|
| **Groq** | 30 req/min | [console.groq.com](https://console.groq.com) |
| **OpenRouter** | Many free models | [openrouter.ai/keys](https://openrouter.ai/keys) |
| **HuggingFace** | Rate-limited | [huggingface.co/settings/tokens](https://huggingface.co/settings/tokens) |
| **Google AI** | Generous free tier | [aistudio.google.com/apikey](https://aistudio.google.com/apikey) |

Add your keys in `backend-csharp/LittleHelperAI.API/appsettings.json` under `FreeAIProviders`.

---

## 🛠️ Manual Setup

### Backend

```bash
cd backend-csharp
dotnet restore
dotnet build
cd LittleHelperAI.API
dotnet run --urls=http://localhost:8002
```

### Frontend

```bash
cd frontend
yarn install  # or npm install
yarn start    # or npm start
```

### Database

1. Start MySQL from XAMPP
2. Open http://localhost/phpmyadmin
3. Import `/database/littlehelper_ai_complete.sql`

---

## 📡 API Endpoints

| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/auth/register` | POST | Register new user |
| `/api/auth/login` | POST | User login |
| `/api/projects` | GET/POST | Manage projects |
| `/api/plans` | GET | Get subscription plans |
| `/api/credits/packages` | GET | Get credit packages |
| `/api/admin/*` | Various | Admin endpoints |
| `/api/system/health` | GET | Health check |

Full API docs at: http://localhost:8002/swagger

---

## 🐛 Troubleshooting

### MySQL Connection Error
1. Check XAMPP Control Panel - MySQL should show "Running"
2. Verify user `root` with no password can connect
3. Import the database SQL file if not done

### Port Already in Use
```bash
# Windows - find process using port
netstat -ano | findstr :8002

# Kill process
taskkill /f /pid <PID>
```

### Build Errors
```bash
cd backend-csharp
dotnet clean
dotnet restore
dotnet build
```

---

## 📄 License

MIT License

---

## 🤝 Support

For issues or questions, please open a GitHub issue.
