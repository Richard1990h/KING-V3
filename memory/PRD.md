# LittleHelper AI - Product Requirements Document

## Original Problem Statement
Build a comprehensive AI-powered code generation platform with multi-agent capabilities, real-time collaboration, and social features.

## Architecture (LOCKED)
- **Backend**: C# ASP.NET Core 8.0, MariaDB/MySQL
- **Frontend**: React 18, Tailwind CSS, Shadcn UI

---

## Implementation Status (January 8, 2026)

### ✅ Completed Features

#### Core Features
- [x] Multi-Agent Build System
- [x] Global Assistant Chat
- [x] Admin Panel (Plans, Credits, Users)
- [x] CodeBlock Component

#### Social & Collaboration
- [x] Friends system
- [x] Direct messaging
- [x] WebSocket collaboration service
- [x] FriendsSidebar component

#### Admin Site Settings (NEW)
- [x] **Announcement Banner** - Admin-configurable login page message
- [x] **Auto-Friend Admins** - Toggle for automatic admin-user friendship
- [x] **Maintenance Mode** - Lock out non-admin users

#### Wave Defense Game (NEW) 🎮
Full tower-defense game when backend is unavailable:
- **20 Waves** of enemies with scaling difficulty
- **6 Enemy Types**: Runner, Grunt, Tank, Shooter, Exploder, Healer
- **Boss Battles** every 5 waves
- **Player Upgrades**: Damage, Health, Armor, Fire Rate
- **Base Upgrades**: Walls, Auto-Turret, Repair Station
- **5 Troop Types**: Soldier, Sniper, Heavy, Medic, Engineer
- **Credits System** for purchasing upgrades
- **High Score Tracking** (persisted to localStorage)
- **Sound Effects** (toggleable)

---

## New Components

### `/app/frontend/src/components/WaveDefenseGame.jsx`
Full wave-based defense game featuring:
- Entity classes (Enemy, Troop, Projectile, Particle)
- Wave generation with difficulty scaling
- Shop system between waves
- Victory/Game Over screens

### `/app/frontend/src/pages/Admin.jsx` (Updated)
New "Site Settings" tab with:
- Announcement message editor
- Type selector (info/warning/success/error)
- Preview panel
- Auto-friend admins toggle
- Maintenance mode toggle

---

## API Endpoints (Required in C# Backend)

### Site Settings
```
GET  /api/site-settings         - Admin: get all settings
GET  /api/site-settings/public  - Public: get announcement
PUT  /api/site-settings         - Admin: update settings
```

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

## Next Steps
1. Implement site-settings C# endpoints
2. Deploy C# backend
3. E2E testing
