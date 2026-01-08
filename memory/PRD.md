# LittleHelper AI - Product Requirements Document

## Architecture (LOCKED)
- **Backend**: C# ASP.NET Core 8.0, MariaDB/MySQL
- **Frontend**: React 18, Tailwind CSS, Shadcn UI

---

## Implementation Status (January 8, 2026)

### ✅ C# Backend - Site Settings Implemented

#### New Files Created:
1. **`/app/backend-csharp/LittleHelperAI.API/Controllers/SiteSettingsController.cs`**
   - `GET /api/site-settings` - Get all settings (admin only)
   - `GET /api/site-settings/public` - Get public settings (anonymous)
   - `PUT /api/site-settings` - Update settings (admin only)
   - `POST /api/site-settings/auto-friend-admins` - Trigger auto-friend

2. **Models added to `Models.cs`:**
   - `SiteSettings` - Database model
   - `SiteSettingsRequest` - Request DTO
   - `PublicSiteSettings` - Public response DTO

3. **Database table added to SQL schema:**
   - `site_settings` table with announcement, maintenance, auto-friend columns

#### Updated Files:
1. **`AuthService.cs`**
   - Added `AutoFriendAdminsForNewUser()` - Auto-friends admins on new user registration
   - Added maintenance mode check in `LoginAsync()` - Blocks non-admin logins during maintenance

---

## API Endpoints Summary

### Site Settings
| Endpoint | Method | Auth | Description |
|----------|--------|------|-------------|
| `/api/site-settings` | GET | Admin | Get all site settings |
| `/api/site-settings/public` | GET | None | Get announcement for login page |
| `/api/site-settings` | PUT | Admin | Update site settings |
| `/api/site-settings/auto-friend-admins` | POST | Admin | Manually trigger auto-friend |

### Request/Response Examples

**GET /api/site-settings/public**
```json
{
  "announcementEnabled": true,
  "announcementMessage": "🚧 Early access - bugs expected!",
  "announcementType": "warning",
  "maintenanceMode": false
}
```

**PUT /api/site-settings**
```json
{
  "announcementEnabled": true,
  "announcementMessage": "We're in development mode!",
  "announcementType": "warning",
  "maintenanceMode": false,
  "adminsAutoFriend": true
}
```

---

## Deployment Steps

1. **Update Database:**
   ```sql
   CREATE TABLE IF NOT EXISTS site_settings (
       id VARCHAR(36) PRIMARY KEY DEFAULT 'default',
       announcement_enabled TINYINT(1) DEFAULT 0,
       announcement_message TEXT,
       announcement_type VARCHAR(20) DEFAULT 'info',
       maintenance_mode TINYINT(1) DEFAULT 0,
       admins_auto_friend TINYINT(1) DEFAULT 1,
       updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
       updated_by VARCHAR(36)
   );
   
   INSERT INTO site_settings (id) VALUES ('default') ON DUPLICATE KEY UPDATE id = id;
   ```

2. **Build & Deploy:**
   ```bash
   cd /app/backend-csharp/Docker
   cp .env.example .env
   # Edit .env with passwords
   docker-compose up -d --build
   ```

3. **Set Announcement (via Admin Panel):**
   - Login as admin
   - Go to Admin → Site Settings
   - Enable announcement, type message, select type
   - Save

---

## Features Implemented

### Frontend
- [x] Wave Defense Game (when backend down)
- [x] Admin Site Settings panel
- [x] Announcement banner on login page
- [x] Friends sidebar with notifications
- [x] Mobile responsive workspace

### Backend (C#)
- [x] Site Settings API endpoints
- [x] Auto-friend admins on user registration
- [x] Maintenance mode blocking
- [x] Database schema for site_settings

---

## Test Credentials
- **User**: test@example.com / test123
- **Admin**: admin@littlehelper.ai / admin123
