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

### ✅ Completed (January 9, 2025)

#### C# Build Fixes
- Fixed duplicate `GoogleDriveConfigRequest` class (renamed to `UserGoogleDriveConfigRequest`)
- Added missing `IDbContext` methods: `CreateConnection()`, `ExecuteScalarAsync<T>()`
- Fixed `IAuthService` with Google Drive config methods
- Fixed `SiteSettingsController` connection opening issues
- Fixed `project_collaborators` table column name (`permission_level`)
- Fixed `admins_auto_friend` boolean comparison for MySQL (1/0 vs true/false)

#### Frontend Enhancements
- **Integrated Chat System**: Merged Friends/DMs into GlobalAssistant chat widget
  - AI Tab: Original AI chat functionality
  - Friends Tab: View friends, send requests, accept/deny
  - DMs Tab: Private messaging with tabs for each conversation
  - Notification badges for unread messages and pending requests
- **Project Collaboration**: Added "Invite" button to Workspace header
  - Shows current collaborators
  - Invite friends as collaborators
  - Remove collaborators
- **Admin Panel**: Admin Team Status with online/offline visibility toggle
- **Site Settings**: Fixed PascalCase/snake_case transformation for C# API

#### Database Schema
- Added `friend_requests`, `friends`, `direct_messages`, `project_collaborators` tables
- Added `user_google_drive_config` table
- Added `appear_offline` column to users table
- Added performance indexes

### 🔴 Blocked (Requires External Deployment)
- All backend functionality requires the C# backend to be deployed externally
- Site Settings, Friends, DMs, Collaboration features depend on running backend

### 📋 Upcoming Tasks (P1)
1. Deploy C# backend externally using Docker
2. Test end-to-end: auth, admin, friends, DMs, collaboration
3. Google Drive OAuth integration (backend logic)
4. Secure sandboxed code execution environment

### 📦 Future/Backlog (P2)
- Live collaboration (cursor tracking)
- Credit-sharing rules for collaborative projects
- Collaborator avatars in workspace

## Key Files

### Frontend
- `/app/frontend/src/components/GlobalAssistant.jsx` - Integrated chat (AI + Friends + DMs)
- `/app/frontend/src/pages/Workspace.jsx` - Project workspace with collaboration
- `/app/frontend/src/pages/Admin.jsx` - Admin panel with Site Settings
- `/app/frontend/src/lib/api.js` - API functions including collaborators

### Backend (C#)
- `/app/backend-csharp/LittleHelperAI.API/Controllers/FriendsController.cs` - Friends & DMs
- `/app/backend-csharp/LittleHelperAI.API/Controllers/CollaboratorsController.cs` - Project collaboration
- `/app/backend-csharp/LittleHelperAI.API/Controllers/SiteSettingsController.cs` - Admin settings
- `/app/backend-csharp/LittleHelperAI.API/Services/AuthService.cs` - Auto-friend admins logic

### Database
- `/app/database/littlehelper_ai_complete.sql` - Full database schema

### Deployment
- `/app/backend-csharp/Docker/docker-compose.yml` - Docker Compose configuration

## API Endpoints

### Friends & DMs
- `GET /api/friends` - Get friends list
- `POST /api/friends/request` - Send friend request
- `GET /api/friends/requests` - Get pending requests
- `PUT /api/friends/requests/{id}` - Accept/deny request
- `DELETE /api/friends/{friendUserId}` - Remove friend
- `GET /api/friends/dm/{friendUserId}` - Get DM messages
- `POST /api/friends/dm/{friendUserId}` - Send DM
- `GET /api/friends/dm/unread` - Get unread counts

### Collaborators
- `GET /api/projects/{id}/collaborators` - Get collaborators
- `POST /api/projects/{id}/collaborators` - Add collaborator
- `PUT /api/projects/{id}/collaborators/{userId}` - Update permission
- `DELETE /api/projects/{id}/collaborators/{userId}` - Remove collaborator
- `PUT /api/projects/{id}/collaborators/credit-mode` - Set credit mode

### Site Settings
- `GET /api/site-settings` - Get settings (admin)
- `PUT /api/site-settings` - Update settings (admin)
- `GET /api/site-settings/public` - Get public announcement

### User Visibility
- `GET /api/user/visibility` - Get visibility status
- `PUT /api/user/visibility` - Update appear offline

## Deployment Instructions
1. Navigate to `/app/backend-csharp/Docker/`
2. Run `docker-compose up -d`
3. Import `/app/database/littlehelper_ai_complete.sql` into MariaDB
4. Update frontend `REACT_APP_BACKEND_URL` to deployed backend URL

## Test Credentials
- Admin: `admin@littlehelper.ai` / `admin123`
- Test User: `test@example.com` / `test123`

---
Last Updated: January 9, 2025
