const BASE = ''

async function request(method, path, body = null, token = null) {
  const headers = { 'Content-Type': 'application/json' }
  if (token) {
    headers['Authorization'] = `Bearer ${token}`
  }

  const opts = { method, headers }
  if (body) {
    opts.body = JSON.stringify(body)
  }

  const res = await fetch(`${BASE}${path}`, opts)
  const json = await res.json()
  return json
}

export const api = {
  // Auth
  login(username, password) {
    return request('POST', '/api/auth/login', { username, password })
  },

  verify2FA(code, method = 'totp') {
    return request('POST', '/api/auth/2fa', { code, method })
  },

  getStatus() {
    return request('GET', '/api/status')
  },

  getConfig() {
    return request('GET', '/api/config')
  },

  // Friends
  getFriends(offset = 0, n = 100, offline = false) {
    const params = new URLSearchParams({
      offset: String(offset),
      n: String(n),
      offline: String(offline),
    })
    return request('GET', `/api/friends?${params}`)
  },

  getFriend(userId) {
    return request('GET', `/api/friends/${encodeURIComponent(userId)}`)
  },

  getFriendsLocal() {
    return request('GET', '/api/friends-local')
  },

  getFriendLocal(userId) {
    return request('GET', `/api/friends-local/${encodeURIComponent(userId)}`)
  },

  // Worlds
  getWorld(worldId) {
    return request('GET', `/api/worlds/${encodeURIComponent(worldId)}`)
  },

  resolveWorld(worldId) {
    const params = new URLSearchParams({ id: worldId })
    return request('GET', `/api/worlds/resolve?${params}`)
  },

  // Search
  searchUsers(query, offset = 0, n = 10) {
    const params = new URLSearchParams({
      q: query,
      offset: String(offset),
      n: String(n),
    })
    return request('GET', `/api/search/users?${params}`)
  },

  searchWorlds(query, offset = 0, n = 10, offline = false) {
    const params = new URLSearchParams({
      q: query,
      offset: String(offset),
      n: String(n),
      offline: String(offline),
    })
    return request('GET', `/api/search/worlds?${params}`)
  },

  // Notifications
  getNotifications(offset = 0, n = 100, type = null) {
    const params = new URLSearchParams({
      offset: String(offset),
      n: String(n),
    })
    if (type) params.set('type', type)
    return request('GET', `/api/notifications?${params}`)
  },

  acceptNotification(notificationId) {
    return request('POST', `/api/notifications/${encodeURIComponent(notificationId)}/accept`)
  },

  declineNotification(notificationId) {
    return request('POST', `/api/notifications/${encodeURIComponent(notificationId)}/decline`)
  },

  deleteNotification(notificationId) {
    return request('DELETE', `/api/notifications/${encodeURIComponent(notificationId)}`)
  },

  // Friend events
  getFriendEvents(userId = null, eventType = null, offset = 0, limit = 100) {
    const params = new URLSearchParams({
      offset: String(offset),
      limit: String(limit),
    })
    if (userId) params.set('userId', userId)
    if (eventType) params.set('eventType', eventType)
    return request('GET', `/api/friend-events?${params}`)
  },

  // Feed
  getFeed(offset = 0, n = 100, type = null) {
    const params = new URLSearchParams({
      offset: String(offset),
      n: String(n),
    })
    if (type) params.set('type', type)
    return request('GET', `/api/feed?${params}`)
  },

  // Settings
  getSettings() {
    return request('GET', '/api/settings')
  },

  updateSettings(settings) {
    return request('POST', '/api/settings', settings)
  },

  // User profile
  getUser(userId) {
    return request('GET', `/api/users/${encodeURIComponent(userId)}`)
  },

  // Image proxy (VRChat images require auth)
  // URL is base64url-encoded to avoid routing issues with slashes
  proxyImage(url) {
    if (!url) return ''
    if (url.startsWith('/api/image-proxy')) return url
    if (url.startsWith('/')) return url
    // Base64url encode
    const encoded = btoa(url).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
    return `/api/image-proxy/${encoded}`
  },

  // Generic .NET proxy
  callDotNet(className, methodName, args = {}) {
    return request('POST', `/api/dotnet/${className}/${methodName}`, args)
  },
}
