# Fix: Image Proxy + Status Colors + Missing Features

ulw

## Three issues to fix

### Issue 1: Image Proxy Route Missing
The `/api/image-proxy` route was lost from Program.cs. VRChat images require auth cookies to load.

Add this route to Program.cs BEFORE the SPA fallback:
```csharp
// GET /api/image-proxy?url={base64EncodedUrl}
app.MapGet("/api/image-proxy", async (string url, VrchatApiService vrchatApi) =>
{
    try
    {
        var decodedUrl = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(url));
        var imageBytes = await vrchatApi.GetImageAsync(decodedUrl);
        if (imageBytes == null || imageBytes.Length == 0)
            return Results.NotFound();
        return Results.File(imageBytes, "image/jpeg");
    }
    catch (Exception ex)
    {
        return Results.Problem(ex.Message);
    }
});
```

Also ensure VrchatApiService has GetImageAsync:
```csharp
public async Task<byte[]?> GetImageAsync(string url)
{
    EnsureAuthenticated();
    try
    {
        var response = await _httpClient.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Failed to get image from {Url}", url);
        return null;
    }
}
```

IMPORTANT: The frontend uses path format `/api/image-proxy/{base64}` NOT query param. Make the route accept both:
```csharp
app.MapGet("/api/image-proxy/{encodedUrl}", async (string encodedUrl, VrchatApiService vrchatApi) => { ... });
```

### Issue 2: Status Colors — active must be GREEN
In `frontend-src/src/views/FriendsView.vue`:
- The filter dropdown shows `🟡 active` — change to `🟢 active`
- The `statusClass()` function and CSS must map:
  - `active` → green (#3fb950)
  - `joinme` → blue (#58a6ff)  
  - `askme` → yellow (#d29922)
  - `busy` → red (#e94560)
  - `offline` → grey (#8b949e)

Check ALL Vue files for status color CSS:
- FriendsView.vue
- FriendDetailView.vue
- FriendCard.vue
- Dashboard.vue
- ActivityLog.vue

Make sure the CSS uses these EXACT colors:
```css
.status-dot.active, .status-badge.active { color: #3fb950; background: rgba(63,185,80,0.15); }
.status-dot.joinme, .status-badge.joinme { color: #58a6ff; background: rgba(88,166,255,0.15); }
.status-dot.askme, .status-badge.askme { color: #d29922; background: rgba(210,153,34,0.15); }
.status-dot.busy, .status-badge.busy { color: #e94560; background: rgba(233,69,96,0.15); }
.status-dot.offline, .status-badge.offline { color: #8b949e; background: rgba(139,148,158,0.15); }
```

Also update the filter options in FriendsView.vue:
- `🟢 Active` instead of `🟡 Active`
- Add filter options for `joinme`, `askme`, `busy`

### Issue 3: Add Missing Navigation Pages
The navbar currently only has: Dashboard, Friends, ActivityLog, Search

Add these nav items and pages:
1. **Groups** (群组) — route `/groups`, manage friend groups/favorites
2. **Worlds** (世界) — route `/worlds`, browse VRChat worlds with instances

Add to `frontend-src/src/router.js`:
```javascript
{ path: '/groups', name: 'Groups', component: () => import('../views/GroupsView.vue') },
{ path: '/worlds', name: 'Worlds', component: () => import('../views/WorldsView.vue') },
```

Add to `frontend-src/src/components/Navbar.vue` nav-links:
```html
<router-link to="/groups" class="nav-link" :class="{ active: $route.name === 'Groups' }">
  <span class="nav-link-icon">⭐</span>
  <span class="nav-link-text">{{ t('navbar.groups') }}</span>
</router-link>
<router-link to="/worlds" class="nav-link" :class="{ active: $route.name === 'Worlds' }">
  <span class="nav-link-icon">🌍</span>
  <span class="nav-link-text">{{ t('navbar.worlds') }}</span>
</router-link>
```

Create `frontend-src/src/views/GroupsView.vue`:
- Show friend groups list from `GET /api/friend-groups`
- Create/delete groups
- Add/remove friends from groups
- Show friends in each group with their status

Create `frontend-src/src/views/WorldsView.vue`:
- Search worlds via `GET /api/search/worlds?q=`
- Show world results with image, name, author, capacity
- Click world to see instances
- Show instance player counts

Add i18n keys to both zh.js and en.js:
```javascript
// en.js
navbar: { groups: 'Groups', worlds: 'Worlds' },
groups: { title: 'Friend Groups', create: 'Create Group', delete: 'Delete', addFriend: 'Add Friend', noGroups: 'No groups yet', members: 'members' },
worlds: { title: 'Worlds', search: 'Search worlds...', instances: 'Instances', capacity: 'Capacity', noWorlds: 'Search for VRChat worlds' }

// zh.js  
navbar: { groups: '群组', worlds: '世界' },
groups: { title: '好友分组', create: '创建分组', delete: '删除', addFriend: '添加好友', noGroups: '暂无分组', members: '成员' },
worlds: { title: '世界', search: '搜索世界...', instances: '实例', capacity: '容量', noWorlds: '搜索VRChat世界' }
```

## Build Steps
After all changes:
1. `cd frontend-src && npm run build`
2. `cp -r dist/* ../VrcxServer/wwwroot/`
