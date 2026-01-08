# LittleHelper AI - Product Requirements Document

## Original Problem Statement
Build a comprehensive AI-powered code generation platform with multi-agent capabilities, real-time collaboration, and social features.

## Core Requirements
1. **Multi-Agent Build System**: AI-powered project generation (plan → tasks → execution → file creation)
2. **Global Assistant**: Floating chat widget for quick AI assistance
3. **Admin Plans Management**: Subscription Plans and Credit Packages
4. **Real-time Collaboration**: WebSocket live editing + project sharing
5. **Multiple AI Providers**: Groq, Together AI, OpenRouter, HuggingFace, Ollama
6. **Friends System**: Discord-style friend requests and direct messaging
7. **Credit Sharing**: Own credits vs Shared credits per project

## Tech Stack
- **Backend**: C# ASP.NET Core 8.0, Dapper ORM, WebSocket
- **Frontend**: React 18, Tailwind CSS, Shadcn UI, Framer Motion
- **Database**: MariaDB
- **AI Integration**: Emergent LLM + 5 additional providers

---

## Implementation Status (January 8, 2026)

### ✅ Completed Features

#### Core Features
- [x] Multi-Agent Build System (E2E tested)
- [x] Global Assistant Chat
- [x] Admin Subscription Plans & Credit Packages CRUD
- [x] CodeBlock Component (Notepad++ style)

#### Real-time Collaboration
- [x] WebSocket service (`CollaborationService.cs`)
- [x] Share link generation with 7-day expiry
- [x] Project download as ZIP
- [x] `useCollaboration` React hook

#### Friends System (Discord-style)
- [x] Send/accept/deny friend requests
- [x] Friends list management
- [x] Direct messages (1-to-1 chat)
- [x] System messages (friend accepted, project shared)
- [x] Unread message count

#### Credit System
- [x] "Use Own Credits" mode (default)
- [x] "Shared Credits" mode (owner pays)
- [x] Credit usage logging with audit trail

#### Project Collaboration
- [x] Add friends as collaborators
- [x] Permission levels: view, edit, admin
- [x] Remove collaborator
- [x] Only friends can be added

#### Admin Features
- [x] Google Drive config in admin panel
- [x] All AI providers management

---

## Key API Endpoints

### Friends API
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/friends` | GET | Get friends list |
| `/api/friends/request` | POST | Send friend request |
| `/api/friends/requests` | GET | Get pending requests |
| `/api/friends/requests/{id}` | PUT | Accept/deny request |
| `/api/friends/{userId}` | DELETE | Remove friend |
| `/api/friends/dm/{userId}` | GET | Get DM messages |
| `/api/friends/dm/{userId}` | POST | Send DM |
| `/api/friends/dm/unread` | GET | Get unread count |

### Collaborators API
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/projects/{id}/collaborators` | GET | Get collaborators |
| `/api/projects/{id}/collaborators` | POST | Add collaborator |
| `/api/projects/{id}/collaborators/credit-mode` | PUT | Set credit mode |

### Collaboration API
| Endpoint | Method | Description |
|----------|--------|-------------|
| `/api/collaboration/ws/{projectId}` | WS | WebSocket connection |
| `/api/collaboration/{id}/share` | POST | Create share link |
| `/api/collaboration/{id}/download` | GET | Download as ZIP |

---

## Database Schema (New Tables)

```sql
-- Friend Requests
friend_requests (id, sender_id, receiver_id, status, created_at, updated_at)

-- Friends (bidirectional)
friends (id, user_id, friend_user_id, created_at)

-- Direct Messages
direct_messages (id, sender_id, receiver_id, message, message_type, is_read, created_at)

-- Project Collaborators
project_collaborators (id, project_id, user_id, permission_level, invited_by, created_at)

-- Credit Usage Logs
credit_usage_logs (id, project_id, user_id, credit_source, amount, action_type, description, created_at)

-- Project Shares
project_shares (id, project_id, share_token, created_by, can_edit, expires_at, created_at)

-- Google Drive Config
google_drive_config (id, client_id, client_secret, redirect_uri, is_configured, updated_at)
```

---

## Credit Rules

### Use Own Credits (Default)
- Each collaborator spends their own credits
- AI requests blocked if user has insufficient credits
- Usage logged per user

### Shared Credits
- All AI usage draws from project owner's balance
- Owner pays for all collaborator AI requests
- Usage logged with `credit_source: 'shared'`

---

## Test Results
- **Friends API**: ✅ All endpoints working
- **DM System**: ✅ Messages sent/received correctly
- **Share Links**: ✅ Generated and validated
- **AI Build E2E**: ✅ Files created successfully

## Test Credentials
- **Test User**: test@example.com / test123
- **Admin User**: admin@littlehelper.ai / admin123

---

## Future Enhancements
- [ ] Live cursor rendering in editor
- [ ] Google Drive OAuth for direct upload
- [ ] Mobile responsive improvements
- [ ] Real-time DM notifications via WebSocket
