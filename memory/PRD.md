# LittleHelper AI - Product Requirements Document

## Original Problem Statement
Build a comprehensive AI-powered code generation platform with multi-agent capabilities, real-time collaboration, and social features.

## Architecture (LOCKED - DO NOT CHANGE)
- **Backend**: C# ASP.NET Core 8.0, Dapper ORM, WebSocket, MariaDB/MySQL
- **Frontend**: React 18, Tailwind CSS, Shadcn UI, Framer Motion
- **Database**: MariaDB (MySQL compatible)

---

## Implementation Status (January 8, 2026)

### ✅ Completed Features

#### Core Features
- [x] Multi-Agent Build System
- [x] Global Assistant Chat
- [x] Admin Panel (Plans, Credits, Users CRUD)
- [x] CodeBlock Component

#### Social & Collaboration
- [x] Friends system (requests, accept/deny)
- [x] Direct messaging
- [x] WebSocket collaboration service
- [x] FriendsSidebar component

#### Frontend Features
- [x] Defensive `.map()` error handling
- [x] Mobile responsive workspace
- [x] useNotifications hook
- [x] Notification badge

#### NEW: Admin Site Settings (January 8, 2026)
- [x] **Announcement Banner** - Admin-configurable message on login page
  - Enable/disable toggle
  - Message type (info/warning/success/error)
  - Custom message text
  - Live preview
- [x] **Auto-Friend Admins** - Option to automatically add admins to all users' friends
- [x] **Maintenance Mode** - Lock out non-admin users

#### NEW: Downtime Entertainment (January 8, 2026)
- [x] **Mini Shooter Game** - FPS-style target shooting game when backend unavailable
  - Score tracking with high score persistence
  - 3 lives system
  - Level progression (difficulty increases)
  - Combo multiplier
  - Sound effects (toggleable)
  - Touch support for mobile
  - "Check Server Status" button
  - "Back to Login" button

#### Docker Configuration
- [x] Dockerfile with health checks
- [x] docker-compose.yml with networking
- [x] Frontend Dockerfile + nginx

---

## New API Endpoints (Required in C# Backend)

### Site Settings
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/site-settings` | GET | Get all site settings (admin) |
| `/api/site-settings/public` | GET | Get public settings (announcement) |
| `/api/site-settings` | PUT | Update site settings (admin) |

### Site Settings Schema
```json
{
  "announcement_enabled": true,
  "announcement_message": "🚧 Early access - bugs expected!",
  "announcement_type": "warning",
  "maintenance_mode": false,
  "admins_auto_friend": true
}
```

---

## Key Files Added/Modified

### New Components
- `/app/frontend/src/components/MiniShooterGame.jsx` - Downtime game

### Modified Files
- `/app/frontend/src/pages/auth/Login.jsx` - Game + announcement integration
- `/app/frontend/src/pages/Admin.jsx` - Site Settings tab
- `/app/frontend/src/lib/api.js` - siteSettingsAPI

---

## Test Credentials
- **User**: test@example.com / test123
- **Admin**: admin@littlehelper.ai / admin123

---

## Deployment

```bash
cd /app/backend-csharp/Docker
cp .env.example .env
docker-compose up -d
```

---

## Next Steps (Requires C# Backend)
1. Implement site-settings API endpoints in C#
2. Implement auto-friend admins logic in user registration
3. Full E2E testing once backend deployed
