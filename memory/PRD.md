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

## Current Status

### ✅ Completed
- Frontend UI fully functional with defensive error handling
- Wave Defense game fallback when backend is down
- Admin Panel with Site Settings tab
- Admin Team Status panel (online/offline with visibility toggle)
- Friends sidebar with improved error handling
- All C# backend controllers and services implemented
- Database schema complete with all tables
- Docker deployment configuration ready

### 🔴 Blocked (Requires External Deployment)
- Site Settings saving (announcement, maintenance mode)
- Friends system (send requests, accept/deny)
- Direct messaging between friends
- Real-time notifications
- Admin auto-friend feature
- Maintenance mode login blocking

### 🟡 In Progress
- User needs to deploy C# backend externally

### 📋 Upcoming Tasks (P1)
1. Google Drive OAuth integration (backend logic)
2. Secure sandboxed code execution environment
3. End-to-end testing after backend deployment

### 📦 Future/Backlog (P2)
- Live collaboration (cursor tracking)
- Credit-sharing rules for collaborative projects
- Collaborator avatars in workspace

## Key Files
- `/app/backend-csharp/Docker/docker-compose.yml` - Deployment configuration
- `/app/database/littlehelper_ai_complete.sql` - Full database schema
- `/app/frontend/src/pages/Admin.jsx` - Admin Panel with Site Settings
- `/app/frontend/src/components/FriendsSidebar.jsx` - Friends functionality
- `/app/backend-csharp/LittleHelperAI.API/Services/` - All backend services

## API Endpoints
- `GET/PUT /api/site-settings` - Admin site settings
- `GET /api/site-settings/public` - Public announcement
- `GET/PUT /api/user/visibility` - Admin appear offline toggle
- `POST /api/friends/request` - Send friend request
- `GET /api/friends/dm/{userId}` - Get direct messages
- `WS /api/notifications` - Real-time WebSocket notifications

## Deployment Instructions
1. Navigate to `/app/backend-csharp/Docker/`
2. Run `docker-compose up -d`
3. Update frontend `REACT_APP_BACKEND_URL` to deployed backend URL
4. Import `/app/database/littlehelper_ai_complete.sql` into MariaDB

## Test Credentials
- Admin: `admin@littlehelper.ai` / `admin123`
- Test User: `test@example.com` / `test123`

---
Last Updated: January 9, 2025
