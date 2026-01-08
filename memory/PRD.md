# LittleHelper AI - Product Requirements Document

## Original Problem Statement
Build a comprehensive AI-powered code generation platform with multi-agent capabilities. The application features a C# ASP.NET Core backend with React frontend, using MariaDB for data persistence.

## Core Requirements
1. **Multi-Agent Build System (P0)**: AI-powered project generation through plan → tasks → execution → file creation workflow
2. **Global Assistant (P1)**: Floating chat widget available across all pages for quick AI assistance
3. **Admin Panel Plans Management (P2)**: Separate management for Monthly Subscription Plans and Credit Add-on Packages
4. **Real-time Collaboration**: WebSocket-based live editing with cursor positions and file sharing
5. **Multiple AI Providers**: Groq, Together AI, OpenRouter, HuggingFace, Ollama support

## Tech Stack
- **Backend**: C# ASP.NET Core 8.0, Dapper ORM, WebSocket
- **Frontend**: React 18, Tailwind CSS, Shadcn UI, Framer Motion
- **Database**: MariaDB
- **AI Integration**: Emergent LLM + Groq, Together AI, OpenRouter, HuggingFace, Ollama

## Architecture
```
/app/
├── backend-csharp/          # ASP.NET Core Web API
│   ├── LittleHelperAI.API/
│   │   ├── Controllers/     # API endpoints (including CollaborationController)
│   │   ├── Services/        # Business logic (AIService, CollaborationService)
│   │   └── Models/          # Data models
│   ├── LittleHelperAI.Data/ # Data access layer
│   └── LittleHelperAI.Agents/
├── frontend/                # React application
│   └── src/
│       ├── components/      # UI components (CodeBlock, GlobalAssistant)
│       ├── hooks/           # Custom hooks (useCollaboration)
│       ├── pages/           # Route pages
│       └── lib/             # Utilities and API client
├── database/                # SQL scripts
└── tests/                   # Backend tests
```

## Key API Endpoints
- `POST /api/auth/login` - User authentication
- `POST /api/auth/register` - User registration
- `GET/POST /api/projects` - Project management
- `POST /api/ai/plan` - Generate build plan from prompt
- `POST /api/ai/execute-task` - Execute task and generate files
- `POST /api/assistant/chat` - Global assistant chat
- `GET /api/conversations` - List user conversations
- `GET/POST/PUT/DELETE /api/admin/subscription-plans` - Plan CRUD
- `GET/POST/PUT/DELETE /api/admin/credit-packages` - Package CRUD
- `WS /api/collaboration/ws/{projectId}` - WebSocket for real-time collab
- `POST /api/collaboration/{projectId}/share` - Create shareable link
- `GET /api/collaboration/{projectId}/download` - Download project as ZIP

## AI Providers Supported
| Provider | Model | Status |
|----------|-------|--------|
| Emergent LLM | gpt-4o-mini | ✅ Active (default) |
| Groq | llama-3.1-70b-versatile | ✅ Ready (needs key) |
| Together AI | Llama-3.2-11B | ✅ Ready (needs key) |
| OpenRouter | gemma-2-9b-it:free | ✅ Ready (needs key) |
| HuggingFace | Mistral-7B-Instruct | ✅ Ready (needs key) |
| Ollama | qwen2.5-coder:1.5b | ✅ Ready (local) |

## Test Credentials
- **Test User**: test@example.com / test123
- **Admin User**: admin@littlehelper.ai / admin123

---

## Implementation Status

### ✅ Completed (January 8, 2026)

#### P0 - Multi-Agent Build System
- [x] `/api/ai/plan` endpoint generates structured build plans
- [x] `/api/ai/execute-task` endpoint generates actual code files
- [x] Fixed API path issues (removed duplicate /api prefix)
- [x] E2E tested: User prompt → Plan → Approval → File creation ✓

#### P1 - Global Assistant
- [x] Floating chat bubble in bottom-right corner
- [x] Opens/closes correctly
- [x] Sends messages and displays AI responses

#### P2 - Admin Plans Split
- [x] Monthly Subscription Plans section
- [x] Credit Add-on Packages section
- [x] Full CRUD for both

#### Additional Features
- [x] **Conversations API** - List and retrieve chat history
- [x] **CodeBlock Component** - Notepad++ style code display
- [x] **Agent Pulsing Animation** - Glow effect when working
- [x] **File Explorer Integration** - AI-generated files displayed

#### Real-time Collaboration
- [x] `CollaborationService` - WebSocket connection management
- [x] `CollaborationController` - API endpoints
- [x] `useCollaboration` hook - React integration
- [x] Share link generation with 7-day expiry
- [x] Project download as ZIP

#### AI Provider Support
- [x] All 6 providers implemented in AIService.cs
- [x] Providers stored in database with API keys
- [x] Priority-based fallback system

### 🔄 Future Enhancements

- [ ] Google Drive OAuth integration for direct upload
- [ ] Live cursor rendering in editor
- [ ] Mobile responsive improvements
- [ ] Docker support for deployment

---

## E2E Test Results
- **Multi-Agent Build**: ✅ PASSED
  - Prompt: "Create a simple Python hello world file"
  - Result: `hello.py` created with `print("Hello, World!")`
- **Share Link**: ✅ PASSED
  - Generated: `share_url` with 7-day expiry

## Known Working Features
1. ✅ User login/logout
2. ✅ Project creation
3. ✅ AI build plan generation
4. ✅ AI task execution with file creation
5. ✅ Global Assistant chat
6. ✅ Admin subscription & credit management
7. ✅ Project sharing via link
8. ✅ Project download as ZIP
9. ✅ Multiple AI providers
