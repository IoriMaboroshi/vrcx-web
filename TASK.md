# VRCX Server — ULW Full Feature Parity Task

ulw

## Project Structure
- Backend: `/workspace/vrcx-server/VrcxServer/` (.NET 9 Minimal API)
- Frontend: `/workspace/vrcx-server/frontend-src/` (Vue 3 + Vite)
- VRCX reference source: `/workspace/vrcx-server/vrcx-reference-pick/` (key stores for feature reference)
- Current built frontend: `/workspace/vrcx-server/VrcxServer/wwwroot/`

## CRITICAL: Reference VRCX Source
You MUST read the VRCX reference files in `/workspace/vrcx-server/vrcx-reference-pick/` to understand the exact feature behavior. These are the actual VRCX frontend store files. Do NOT guess how features work — READ the reference code.

Key reference files:
- `friend.js` — Friend state management, state derivation, VIP/favorites, friend groups
- `user.js` — User model, status handling, avatar data
- `feed.js` — Event feed types, filtering, display
- `sharedFeed.js` — Event deduplication, timeline processing
- `location.js` — Location parsing, instance detection
- `instance.js` — Instance management, player counts, world instances
- `favorite.js` — Favorite friends, categories, custom groups
- `group.js` — VRChat groups support
- `avatar.js` — Avatar management
- `world.js` — World data handling

---

## BUG FIX 1: DeriveState (CRITICAL)

**File:** `VrcxServer/Services/VrchatApiService.cs` — `DeriveState()` method (line ~479)

**Current broken code:**
```csharp
private static string DeriveState(string? location, string? status)
{
    if (string.IsNullOrEmpty(location) || location == "offline" || location == "private")
        return "offline";
    if (status == "active")
        return "active";
    if (location.StartsWith("wrld_"))
        return "online";
    return "offline";
}
```

**Problems:**
1. `location == "private"` returns offline — WRONG. Private means they're in a private instance, still online.
2. `location == "traveling"` returns offline — WRONG. Traveling means teleporting.
3. Only checks `status == "active"`, missing `join me`, `ask me`, `busy`.
4. Returns "online" for wrld_ locations — should return the actual VRChat status.

**Correct implementation based on VRChat API:**
The VRChat API returns a `status` field on each user with these exact values:
- `"active"` — green dot
- `"join me"` — blue dot
- `"ask me"` — yellow/orange dot
- `"busy"` — red dot

And a `location` field:
- Empty string or `"offline"` → user is offline
- `"private"` → user is in a private instance (still has status)
- `"traveling"` → user is teleporting between worlds (still has status)
- `"wrld_xxx~region(N)..."` → user is in a public world instance

**Correct logic:**
```
if location is empty OR location == "offline":
    state = "offline"
else:
    // User is somewhere (world, private, or traveling)
    // Use the VRChat status field directly
    state = status  // "active", "join me", "ask me", "busy"
```

**Implementation:**
```csharp
private static string DeriveState(string? location, string? status)
{
    // Offline: no location or explicitly offline
    if (string.IsNullOrEmpty(location) || location == "offline")
        return "offline";

    // User is somewhere (world, private instance, or traveling)
    // Use VRChat's status field directly
    return status?.ToLowerInvariant() switch
    {
        "active" => "active",
        "join me" => "joinme",
        "ask me" => "askme",
        "busy" => "busy",
        _ => "active"  // default to active if status is unknown but location exists
    };
}
```

**Also update** `FriendService.cs` event detection: When checking `previous.State != friend.State`, the state values must match. Since DeriveState now returns "joinme"/"askme"/"busy" instead of just "online"/"active", the comparison logic stays the same but produces more granular events.

---

## BUG FIX 2: World Name Resolution

**File:** `VrcxServer/Services/FriendService.cs` — `PollFriendsAsync()`

**Problem:** When saving events, `WorldName` is set to the raw `wrld_xxxx` UUID. Need to resolve to human-readable world name.

**Solution:**
1. Add a `ResolveWorldNameAsync(string worldId)` method to `FriendService` or create a new `WorldService`
2. Add a `world_cache` table in `DatabaseService` with columns: `id`, `name`, `description`, `author_name`, `image_url`, `capacity`, `fetched_at`
3. Cache world data for 24 hours
4. In `PollFriendsAsync`, when generating events with location changes, resolve the world ID to a name before storing

**VRChat API for world info:** `GET /api/1/worlds/{worldId}` returns:
```json
{
  "id": "wrld_xxx",
  "name": "World Name",
  "description": "...",
  "authorName": "Author",
  "thumbnailImageUrl": "...",
  "imageUrl": "...",
  "capacity": 20,
  "occupants": 5,
  "publicOccupants": [...],
  "privateOccupants": [...]
}
```

---

## BUG FIX 3: World Instances API

**File:** `VrcxServer/Program.cs` — need to add/improve instances endpoint

**Problem:** World detail page shows "未找到活跃实例" (no active instances found).

**Solution:** Add a proper instances API that returns real instance data.

**Add endpoint:** `GET /api/worlds/{worldId}/instances`

This should call VRChat API `GET /api/1/worlds/{worldId}` and extract:
- `publicOccupants` — users in public instances
- `privateOccupants` — users in private instances  
- `instances` — if available in the response

Also add: `GET /api/instances/{instanceId}` for instance details.

---

## FEATURE 1: Friend Groups / Categories (VRCX Feature)

**Reference:** `/workspace/vrcx-server/vrcx-reference-pick/favorite.js` and `group.js`

VRCX supports:
1. **VIP Friends** — marked as favorite, shown at top
2. **Friend Groups** — custom named groups (like "Close Friends", "Gaming Buddies")
3. **Auto-categorization** — by platform, status, last seen

**Implementation:**
1. Add `friend_groups` table: `id`, `user_id`, `group_name`, `created_at`
2. Add `friend_group_members` table: `group_id`, `friend_id`
3. Backend APIs:
   - `GET /api/friend-groups` — list all groups
   - `POST /api/friend-groups` — create group
   - `DELETE /api/friend-groups/{id}` — delete group
   - `POST /api/friend-groups/{id}/members` — add friend to group
   - `DELETE /api/friend-groups/{id}/members/{friendId}` — remove from group
4. Frontend: Add group tabs/filters to FriendList, drag-and-drop to categorize

---

## FEATURE 2: User Notes / Memos

**Reference:** VRCX friend.js has `memo` field per friend

**Implementation:**
1. Add `user_notes` table: `user_id`, `friend_id`, `note`, `updated_at`
2. Backend APIs:
   - `GET /api/notes/{friendId}` — get note
   - `PUT /api/notes/{friendId}` — save note
3. Frontend: Show note textarea in FriendDetailView, editable inline

---

## FEATURE 3: Notification Actions

**File:** `VrcxServer/Services/VrchatApiService.cs`

VRChat notifications include:
- `friend_request` — accept/decline
- `invite` — accept/decline
- `world_invite` — accept/decline
- `requestInvite` — accept/decline

**Add methods to VrchatApiService:**
```csharp
Task<bool> AcceptNotificationAsync(string notificationId);
Task<bool> DeclineNotificationAsync(string notificationId);
Task<bool> DeleteNotificationAsync(string notificationId);
```

**VRChat API:**
- `POST /api/1/auth/user/notifications/{notificationId}/accept`
- `POST /api/1/auth/user/notifications/{notificationId}/decline`  
- `DELETE /api/1/auth/user/notifications/{notificationId}`

**Add backend endpoints in Program.cs:**
- `POST /api/notifications/{id}/accept`
- `POST /api/notifications/{id}/decline`
- `DELETE /api/notifications/{id}`

**Frontend:** Update Notifications.vue with working action buttons.

---

## FEATURE 4: Settings Persistence

**File:** `VrcxServer/Services/DatabaseService.cs` + `Program.cs`

**Add settings table and API:**
- `GET /api/settings` — get all settings
- `PUT /api/settings` — save settings

**Settings to support:**
- `pollingInterval` (seconds, default 60)
- `theme` (dark/light)
- `language` (zh/en)
- `notificationSound` (boolean)
- `showOfflineFriends` (boolean)
- `friendSortOrder` (string)

---

## FEATURE 5: Frontend State Color CSS

**File:** `frontend-src/src/styles/main.css` + `frontend-src/src/components/FriendCard.vue`

Ensure these exact colors for VRChat states:
- `active` → green (`#3fb950`)
- `joinme` → blue (`#58a6ff`)
- `askme` → yellow/orange (`#d29922`)
- `busy` → red (`#e94560`)
- `offline` → grey (`#8b949e`)

Add CSS classes:
```css
.state-active { color: #3fb950; }
.state-joinme { color: #58a6ff; }
.state-askme { color: #d29922; }
.state-busy { color: #e94560; }
.state-offline { color: #8b949e; }
```

Also add dot indicators:
```css
.status-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  display: inline-block;
}
.status-dot.active { background: #3fb950; }
.status-dot.joinme { background: #58a6ff; }
.status-dot.askme { background: #d29922; }
.status-dot.busy { background: #e94560; }
.status-dot.offline { background: #8b949e; }
```

---

## FEATURE 6: Friend Detail Enhancements

**File:** `frontend-src/src/views/FriendDetailView.vue`

1. **Platform display** — No duplicate emojis. Format: `🥽 PC (VR)`, `🥽 Quest`, `💻 PC`, etc.
2. **Join date** — VRChat API doesn't return `date_joined`. Show "Last Login" and "Last Activity" instead.
3. **World name** — Show resolved world name, not raw `wrld_xxx`. Make it clickable to world detail.
4. **Avatar display** — Priority: `profilePicOverride` → `currentAvatarImageUrl` → `userIcon`. Never show default robot.
5. **Status history** — Show recent status changes from friend_events table.
6. **Bio links** — Display clickable bio links.

---

## FEATURE 7: Dashboard Enhancements

**File:** `frontend-src/src/views/Dashboard.vue`

1. **Statistics** — Show total friends, online count, offline count, by status (active/joinme/askme/busy)
2. **Favorite friends** — Show VIP/favorite friends at top with status
3. **Recent activity** — Show last 10 friend events
4. **World distribution** — Show which worlds friends are in (top worlds by player count)

---

## BUILD & DEPLOY

After all changes:

1. **Build frontend:**
```bash
cd /workspace/vrcx-server/frontend-src
npm run build
# Copy output to VrcxServer/wwwroot/
rm -rf /workspace/vrcx-server/VrcxServer/wwwroot/*
cp -r dist/* /workspace/vrcx-server/VrcxServer/wwwroot/
```

2. **Build backend:**
```bash
cd /workspace/vrcx-server/VrcxServer
dotnet publish -c Release -o /app/publish
```

3. **Verify** no compilation errors in both frontend and backend.

---

## IMPORTANT RULES

1. **READ VRCX reference files** before implementing any feature. Do NOT guess behavior.
2. **Do NOT remove existing working features.** Only add/fix.
3. **Keep all existing API endpoints** — frontend depends on them.
4. **i18n** — All new strings must have both Chinese and English translations in `src/i18n/zh.js` and `src/i18n/en.js`.
5. **Image proxy** — All VRChat image URLs must go through `/api/image-proxy?url={base64}`.
6. **VRChat API base URL** — Always `https://api.vrchat.cloud/api/1/` with trailing slash.
7. **Cookie-based auth** — Use `SocketsHttpHandler` with `UseCookies = true`. Do NOT manually set Cookie headers.
8. **Friend API pagination** — Max `n=100` per page. Must paginate for users with many friends.
9. **Test that backend compiles** with `dotnet build` before finishing.
10. **Test that frontend builds** with `npm run build` before finishing.
