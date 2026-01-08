# LittleHelper AI - Product Requirements Document

## Original Problem Statement
Build a comprehensive AI-powered code generation platform with multi-agent capabilities, real-time collaboration, and social features.

## Architecture (LOCKED - DO NOT CHANGE)
- **Backend**: C# ASP.NET Core 8.0, Dapper ORM, WebSocket, MariaDB/MySQL
- **Frontend**: React 18, Tailwind CSS, Shadcn UI, Framer Motion
- **Database**: MariaDB (MySQL compatible)
- **AI Integration**: Emergent LLM + 5 additional providers (Groq, OpenRouter, etc.)

> **IMPORTANT**: The backend must remain C# ASP.NET Core. No rewrites to Python/FastAPI or other languages permitted.

---

## Implementation Status (January 8, 2026)

### ✅ Completed Features

#### Core Features
- [x] Multi-Agent Build System (Planner → Developer → Verifier)
- [x] Global Assistant Chat (floating widget)
- [x] Admin Subscription Plans & Credit Packages CRUD
- [x] CodeBlock Component (Notepad++ style with syntax highlighting)

#### Real-time Collaboration (Backend Ready)
- [x] WebSocket service (`CollaborationService.cs`)
- [x] Share link generation with 7-day expiry
- [x] Project download as ZIP
- [x] `useCollaboration` React hook

#### Friends System (Backend Ready)
- [x] Send/accept/deny friend requests
- [x] Friends list management
- [x] Direct messages (1-to-1 chat)
- [x] Unread message count

#### Frontend Features (January 8, 2026)
- [x] **Defensive Error Handling** - All `.map()` operations have null/undefined checks
- [x] **CodeRunner Integration** - "Run Code" button in editor (HTML/CSS/JS, Python via Pyodide)
- [x] **Save to Google Drive** - Button in workspace header
- [x] **FriendsSidebar** - Integrated with defensive checks
- [x] **Backend Unavailable Handling** - Graceful error messages when C# backend not running

---

## Key API Endpoints (C# Backend)

### Authentication
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/auth/register` | POST | User registration |
| `/api/auth/login` | POST | User login (JWT) |
| `/api/auth/me` | GET | Get current user |

### Projects
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/projects` | GET | List user projects |
| `/api/projects` | POST | Create project |
| `/api/projects/{id}` | GET | Get project details |
| `/api/projects/{id}/files` | GET | Get project files |
| `/api/projects/{id}/chat` | POST | Send chat message |

### Friends API
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/friends` | GET | Get friends list |
| `/api/friends/request` | POST | Send friend request |
| `/api/friends/requests` | GET | Get pending requests |
| `/api/friends/dm/{userId}` | GET/POST | Direct messages |

### Collaboration API
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/collaboration/{id}/share` | POST | Create share link |
| `/api/collaboration/{id}/download` | GET | Download as ZIP |
| `/api/collaboration/{id}/export/drive` | POST | Export to Google Drive |

---

## Database Schema (MariaDB)

See `/app/database/littlehelper_ai_complete.sql` for full schema.

Key tables:
- `users` - User accounts with credits
- `projects` - User projects
- `project_files` - Project file contents
- `friend_requests` - Friend request tracking
- `friends` - Bidirectional friendships
- `direct_messages` - DM conversations

---

## Test Credentials
- **Test User**: test@example.com / test123
- **Admin User**: admin@littlehelper.ai / admin123

---

## Deployment Requirements

### C# Backend
- .NET 8.0 Runtime
- MariaDB/MySQL database
- HTTPS for production
- Environment variables for connection strings

### Frontend
- Node.js 18+
- `REACT_APP_BACKEND_URL` pointing to C# API

---

## Pending Tasks

### High Priority
1. **Deploy C# Backend** - Set up hosting (Azure, AWS, or VPS)
2. **Configure Database** - MariaDB with schema from SQL file
3. **E2E Testing** - Full flow test once backend running

### Medium Priority
- Live cursor positions in code editor
- Real-time DM notifications via WebSocket

### Future/Backlog
- Docker containerization
- Mobile responsive improvements
- Additional AI providers
