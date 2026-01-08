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

#### Real-time Collaboration
- [x] WebSocket service (`CollaborationService.cs`)
- [x] `useCollaboration` React hook with cursor tracking
- [x] Share link generation with 7-day expiry
- [x] Project download as ZIP
- [x] CollaboratorAvatars and CollaboratorCursor components

#### Friends & DM System
- [x] Send/accept/deny friend requests API
- [x] Friends list management
- [x] Direct messages (1-to-1 chat)
- [x] Unread message count tracking
- [x] FriendsSidebar component with defensive checks

#### Frontend (January 8, 2026)
- [x] **Defensive Error Handling** - All `.map()` operations have null/undefined checks
- [x] **CodeRunner Integration** - "Run Code" button in editor
- [x] **Save to Google Drive** - Button in workspace header
- [x] **FriendsSidebar** - Integrated with notification badge
- [x] **useNotifications Hook** - WebSocket + polling for real-time DM/friend notifications
- [x] **Mobile Responsiveness** - Workspace header, file tree, chat panel are mobile-friendly
- [x] **Backend Unavailable Handling** - Graceful error messages

#### Docker Configuration
- [x] Improved Dockerfile with health checks and security
- [x] docker-compose.yml with proper networking, health checks, init SQL
- [x] Frontend Dockerfile with nginx for production
- [x] nginx.conf with WebSocket proxy support
- [x] .env.example for deployment configuration

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
| `/api/friends/unread` | GET | Get unread DM counts |

### Collaboration API
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/collaboration/{id}/share` | POST | Create share link |
| `/api/collaboration/{id}/download` | GET | Download as ZIP |
| `/api/collaboration/{id}/export/drive` | POST | Export to Google Drive |
| `/api/collaboration/ws/{projectId}` | WS | Real-time collaboration |
| `/api/notifications/ws` | WS | DM/friend request notifications |

---

## Test Credentials
- **Test User**: test@example.com / test123
- **Admin User**: admin@littlehelper.ai / admin123

---

## Deployment Guide

### Quick Start with Docker
```bash
cd /app/backend-csharp/Docker

# Copy and edit environment variables
cp .env.example .env
nano .env  # Edit passwords and API keys

# Start all services
docker-compose up -d

# Include frontend (optional)
docker-compose --profile with-frontend up -d
```

### Environment Variables
- `DB_PASSWORD` - MySQL app user password
- `DB_ROOT_PASSWORD` - MySQL root password
- `JWT_SECRET` - JWT signing key (min 32 chars)
- `EMERGENT_LLM_KEY` - For AI features (or other provider keys)

### Database Setup
The `littlehelper_ai_complete.sql` file is automatically loaded on first run via Docker.

---

## Pending Tasks

### Requires C# Backend Deployment
1. E2E Testing - Full flow test once backend running
2. Google Drive OAuth - Complete flow testing
3. WebSocket Testing - Collaboration and notifications

### Future Enhancements
- Additional AI provider integrations
- Mobile app version
- Team workspaces
