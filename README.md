# LittleHelper AI

A full-stack AI-powered code generation and development assistance platform.

## Tech Stack

- **Backend**: C# .NET 8 Web API
- **Frontend**: React 18 + Tailwind CSS
- **Database**: MySQL/MariaDB

## Project Structure

```
├── backend-csharp/          # C# .NET 8 Backend
│   ├── LittleHelperAI.API/      # Main API project
│   ├── LittleHelperAI.Agents/   # AI Agent implementations
│   └── LittleHelperAI.Data/     # Data layer & models
├── frontend/                # React Frontend
│   ├── src/
│   │   ├── components/          # UI Components
│   │   ├── pages/               # Page components
│   │   └── lib/                 # Utilities & API
│   └── public/
└── database/                # Database scripts
    └── littlehelper_ai_complete.sql
```

## Setup Instructions

### 1. Database Setup

1. Open MySQL Workbench or phpMyAdmin
2. Import `/database/littlehelper_ai_complete.sql`
3. This creates the `littlehelper_ai` database with all tables and default data

**Default Admin Login:**
- Email: `admin@littlehelper.ai`
- Password: `admin123`

### 2. Backend Setup (C#)

```bash
cd backend-csharp
dotnet restore
dotnet build
dotnet run --project LittleHelperAI.API
```

Backend runs on: `http://localhost:8002`

### 3. Frontend Setup (React)

```bash
cd frontend
npm install
```

Create `.env` file:
```
REACT_APP_BACKEND_URL=http://localhost:8002
```

```bash
npm start
```

Frontend runs on: `http://localhost:3000`

## Features

- 🤖 **7 AI Agents**: Planner, Researcher, Developer, Test Designer, Executor, Debugger, Verifier
- 💳 **Credit System**: Pay-per-use with multiple packages
- 📊 **Admin Dashboard**: User management, AI providers, analytics
- 🎨 **Customizable Themes**: User-personalized UI colors
- 🔐 **JWT Authentication**: Secure token-based auth
- 📱 **Responsive Design**: Works on all devices

## Subscription Plans

| Plan | Price/Month | Daily Credits | Workspaces |
|------|------------|---------------|------------|
| Free | $0 | 50 | 1 |
| Starter | $9.99 | 200 | 3 |
| Pro | $29.99 | 1,000 | 10 |
| Team | $79.99 | 3,000 | 25 |
| Enterprise | $199.99 | 10,000 | Unlimited |

## Free AI Providers

Configure in Admin Panel:
- Groq (llama-3.1-70b-versatile)
- Together AI
- HuggingFace
- OpenRouter
- Local Ollama

## License

MIT License
