# LittleHelper AI - Product Requirements Document

## Architecture (LOCKED)
- **Backend**: C# ASP.NET Core 8.0, MariaDB/MySQL
- **Frontend**: React 18, Tailwind CSS, Shadcn UI

---

## Code Review Fixes Applied (January 8, 2026)

### 🔴 Critical Issues Fixed:

1. **FriendsController - display_name handling** ✅
   - Added `COALESCE(u.display_name, u.name, u.email)` fallback
   - Prevents null reference errors

2. **CollaborationController - HttpClient disposal** ✅
   - Changed from `new HttpClient()` to `IHttpClientFactory`
   - Registered in Program.cs with `AddHttpClient()`

3. **ClearChatHistory endpoint** ✅
   - Now actually deletes messages from database
   - Added `ClearChatHistoryAsync` to IProjectService and ProjectService
   - Returns count of deleted messages

4. **Rate limiting on friend requests** ✅
   - Max 10 requests per hour per user
   - Returns 429 Too Many Requests when exceeded

### 🟡 Medium Issues Fixed:

5. **Pagination on friends list** ✅
   - Added `page` and `limit` query parameters
   - Returns pagination metadata (total, pages)
   - Max 50 friends per page

6. **DM query order** ✅
   - Fixed with subquery: gets latest DESC, then orders ASC
   - Added `before` parameter for cursor pagination

7. **Message length validation** ✅
   - Max 5000 characters per message
   - Trims whitespace
   - Returns 400 Bad Request if too long

8. **Secure share token generation** ✅
   - Changed from `Random.Shared` to `RandomNumberGenerator`
   - Uses 256 bits of cryptographic randomness

### 🟢 Performance Improvements:

9. **Database indexes added** ✅
   - `friend_requests`: sender_id, receiver_id, status, created_at
   - `friends`: user_id, friend_user_id
   - `direct_messages`: sender_id, receiver_id, is_read, created_at
   - `chat_history`: project_id, user_id, conversation_id, timestamp
   - `project_collaborators`: project_id, user_id

---

## Files Modified:

| File | Changes |
|------|---------|
| `FriendsController.cs` | Rate limiting, pagination, message validation, query fixes |
| `CollaborationController.cs` | IHttpClientFactory, secure token generation |
| `ProjectsController.cs` | ClearChatHistory implementation |
| `IProjectService.cs` | Added ClearChatHistoryAsync |
| `ProjectService.cs` | Implemented ClearChatHistoryAsync |
| `Program.cs` | Added HttpClient factory registration |
| `littlehelper_ai_complete.sql` | Added performance indexes |

---

## API Changes:

### Friends API
```
GET /api/friends?page=1&limit=50  # Pagination added
POST /api/friends/request         # Rate limited (10/hour)
GET /api/friends/dm/{userId}?limit=50&before={timestamp}  # Cursor pagination
POST /api/friends/dm/{userId}     # Max 5000 chars
```

### Projects API
```
DELETE /api/projects/{id}/chat    # Now actually clears history
```

---

## Test Credentials
- **User**: test@example.com / test123
- **Admin**: admin@littlehelper.ai / admin123

---

## Deployment Steps

1. **Update database with new indexes:**
   ```sql
   -- Run the index creation statements from littlehelper_ai_complete.sql
   ```

2. **Build and deploy:**
   ```bash
   cd /app/backend-csharp/Docker
   docker-compose up -d --build
   ```
