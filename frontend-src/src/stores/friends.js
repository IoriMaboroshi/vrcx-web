import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { api } from '../services/api.js'

export const useFriendsStore = defineStore('friends', () => {
  const friends = ref([])
  const events = ref([])
  const notifications = ref([])
  const loading = ref(false)
  const eventsLoading = ref(false)
  const notificationsLoading = ref(false)
  const wsConnected = ref(false)
  const toasts = ref([])
  let ws = null
  let reconnectTimer = null

  // Sort and filter state
  const sortBy = ref('status') // 'status', 'name', 'lastSeen'
  const statusFilter = ref([]) // empty = all
  const worldFilter = ref('')

  const onlineFriends = computed(() =>
    friends.value.filter((f) => f.state === 'online' || f.state === 'active')
  )
  const offlineFriends = computed(() =>
    friends.value.filter((f) => f.state === 'offline')
  )
  const activeFriends = computed(() =>
    friends.value.filter((f) => f.state === 'active')
  )
  const busyFriends = computed(() =>
    friends.value.filter((f) => f.status === 'busy' || f.state === 'busy' || f.userStatus === 'busy')
  )
  const askMeFriends = computed(() =>
    friends.value.filter((f) => f.status === 'ask me' || f.state === 'askme' || f.userStatus === 'ask me')
  )

  async function fetchFriends() {
    loading.value = true
    try {
      // VRChat API: offline=true does NOT return online friends!
      // Must fetch both separately and merge.
      const friendsMap = new Map()

      // 1) Fetch online friends (offline=false)
      let offset = 0
      const pageSize = 100
      while (true) {
        const res = await api.getFriends(offset, pageSize, false)
        if (res.success && res.data && res.data.length > 0) {
          for (const f of res.data) friendsMap.set(f.id, f)
          if (res.data.length < pageSize) break
          offset += pageSize
        } else {
          break
        }
      }

      // 2) Fetch ALL friends (offline=true) — this returns offline + some online
      offset = 0
      while (true) {
        const res = await api.getFriends(offset, pageSize, true)
        if (res.success && res.data && res.data.length > 0) {
          for (const f of res.data) {
            // Keep online/active from offline=false if this one says offline
            const existing = friendsMap.get(f.id)
            if (existing && existing.state !== 'offline') {
              // existing has better data, skip this offline version
            } else {
              friendsMap.set(f.id, f)
            }
          }
          if (res.data.length < pageSize) break
          offset += pageSize
        } else {
          break
        }
      }

      friends.value = Array.from(friendsMap.values())
    } catch (e) {
      console.error('Failed to fetch friends:', e)
    } finally {
      loading.value = false
    }
  }

  async function fetchFriend(userId) {
    // Try local first
    try {
      const localRes = await api.getFriendLocal(userId)
      if (localRes.success && localRes.data) {
        return localRes.data
      }
    } catch {
      // ignore
    }
    // Try VRChat API
    try {
      const res = await api.getFriend(userId)
      if (res.success && res.data) {
        return res.data
      }
    } catch (e) {
      console.error('Failed to fetch friend:', e)
    }
    // Fallback: try user endpoint
    try {
      const res = await api.getUser(userId)
      if (res.success && res.data) {
        return res.data
      }
    } catch (e) {
      console.error('Failed to fetch user:', e)
    }
    return null
  }

  async function fetchEvents(userId = null, limit = 100) {
    eventsLoading.value = true
    try {
      const res = await api.getFriendEvents(userId, null, 0, limit)
      if (res.success && res.data) {
        events.value = res.data
      }
    } catch (e) {
      console.error('Failed to fetch events:', e)
    } finally {
      eventsLoading.value = false
    }
  }

  async function fetchFriendLog(userId = null, eventType = null, offset = 0, limit = 100) {
    eventsLoading.value = true
    try {
      const res = await api.getFriendEvents(userId, eventType, offset, limit)
      if (res.success && res.data) {
        events.value = res.data
      }
    } catch (e) {
      console.error('Failed to fetch friend log:', e)
    } finally {
      eventsLoading.value = false
    }
  }

  async function fetchNotifications() {
    notificationsLoading.value = true
    try {
      const allNotifications = []
      let offset = 0
      const pageSize = 100
      while (true) {
        const res = await api.getNotifications(offset, pageSize)
        if (res.success && res.data && res.data.length > 0) {
          allNotifications.push(...res.data)
          if (res.data.length < pageSize) break
          offset += pageSize
        } else {
          break
        }
      }
      notifications.value = allNotifications
    } catch (e) {
      console.error('Failed to fetch notifications:', e)
    } finally {
      notificationsLoading.value = false
    }
  }

  async function acceptNotification(notificationId) {
    try {
      const res = await api.acceptNotification(notificationId)
      if (res.success) {
        notifications.value = notifications.value.filter((n) => n.id !== notificationId)
        addToast({ icon: '✅', message: 'Notification accepted', type: 'success' })
      }
    } catch (e) {
      console.error('Failed to accept notification:', e)
      addToast({ icon: '❌', message: 'Failed to accept notification', type: 'error' })
    }
  }

  async function declineNotification(notificationId) {
    try {
      const res = await api.declineNotification(notificationId)
      if (res.success) {
        notifications.value = notifications.value.filter((n) => n.id !== notificationId)
        addToast({ icon: '✅', message: 'Notification declined', type: 'success' })
      }
    } catch (e) {
      console.error('Failed to decline notification:', e)
      addToast({ icon: '❌', message: 'Failed to decline notification', type: 'error' })
    }
  }

  async function deleteNotification(notificationId) {
    try {
      const res = await api.deleteNotification(notificationId)
      if (res.success) {
        notifications.value = notifications.value.filter((n) => n.id !== notificationId)
      }
    } catch (e) {
      console.error('Failed to delete notification:', e)
    }
  }

  async function fetchFeed(offset = 0, n = 100, type = null) {
    eventsLoading.value = true
    try {
      const res = await api.getFeed(offset, n, type)
      if (res.success && res.data) {
        events.value = res.data
      }
    } catch (e) {
      console.error('Failed to fetch feed:', e)
    } finally {
      eventsLoading.value = false
    }
  }

  function addToast(toast) {
    const id = Date.now() + Math.random()
    toasts.value.push({ id, ...toast })
    setTimeout(() => {
      toasts.value = toasts.value.filter((t) => t.id !== id)
    }, toast.duration || 5000)
  }

  function connectWebSocket() {
    if (ws && ws.readyState <= 1) return

    const protocol = location.protocol === 'https:' ? 'wss:' : 'ws:'
    const wsUrl = `${protocol}//${location.host}/ws`

    try {
      ws = new WebSocket(wsUrl)
    } catch {
      return
    }

    ws.onopen = () => {
      wsConnected.value = true
      console.log('WebSocket connected')
      if (reconnectTimer) {
        clearTimeout(reconnectTimer)
        reconnectTimer = null
      }
    }

    ws.onmessage = (event) => {
      try {
        const msg = JSON.parse(event.data)
        handleWsMessage(msg)
      } catch (e) {
        console.error('Failed to parse WS message:', e)
      }
    }

    ws.onclose = () => {
      wsConnected.value = false
      console.log('WebSocket disconnected')
      reconnectTimer = setTimeout(connectWebSocket, 5000)
    }

    ws.onerror = () => {
      wsConnected.value = false
    }
  }

  function handleWsMessage(msg) {
    if (msg.type === 'friend_event' && msg.data) {
      const evt = msg.data
      events.value.unshift(evt)
      if (events.value.length > 500) events.value.pop()

      // Update friend in local list
      const friend = friends.value.find((f) => f.id === evt.userId)
      if (friend) {
        if (evt.eventType === 'online') friend.state = 'online'
        else if (evt.eventType === 'offline') friend.state = 'offline'
        else if (evt.eventType === 'location_change' && evt.newValue) {
          friend.location = evt.newValue
        } else if (evt.eventType === 'status_change') {
          friend.status = evt.newValue
          if (evt.newValue) friend.userStatus = evt.newValue
          // Also update state for askme/busy variants
          if (evt.newValue === 'busy') friend.state = 'busy'
          else if (evt.newValue === 'ask me' || evt.newValue === 'askme') friend.state = 'askme'
          else if (evt.newValue === 'online') friend.state = 'online'
          else if (evt.newValue === 'active') friend.state = 'active'
        } else if (evt.eventType === 'avatar_change') {
          if (evt.newValue) friend.currentAvatarThumbnailImageUrl = evt.newValue
        } else if (evt.eventType === 'name_change') {
          if (evt.newValue) friend.displayName = evt.newValue
        }
      }

      // Toast notification
      let toastMsg = ''
      let icon = '👤'
      if (evt.eventType === 'online') {
        toastMsg = `${evt.displayName} is now online`
        icon = '🟢'
      } else if (evt.eventType === 'offline') {
        toastMsg = `${evt.displayName} went offline`
        icon = '⚫'
      } else if (evt.eventType === 'location_change') {
        toastMsg = `${evt.displayName} moved to ${evt.newValue || 'unknown'}`
        icon = '🌍'
      } else if (evt.eventType === 'status_change') {
        toastMsg = `${evt.displayName}: ${evt.newValue || 'status updated'}`
        icon = '💬'
      } else if (evt.eventType === 'avatar_change') {
        toastMsg = `${evt.displayName} changed their avatar`
        icon = '🧑'
      } else if (evt.eventType === 'name_change') {
        toastMsg = `${evt.oldValue || 'Someone'} → ${evt.newValue || 'Unknown'}`
        icon = '✏️'
      }

      if (toastMsg) {
        addToast({ icon, message: toastMsg, type: evt.eventType })
      }
    }

    if (msg.type === 'notification' && msg.data) {
      notifications.value.unshift(msg.data)
      addToast({
        icon: '🔔',
        message: msg.data.message || msg.data.type || 'New notification',
        type: 'notification'
      })
    }
  }

  function disconnectWebSocket() {
    if (ws) {
      ws.close()
      ws = null
    }
    if (reconnectTimer) {
      clearTimeout(reconnectTimer)
      reconnectTimer = null
    }
    wsConnected.value = false
  }

  return {
    friends,
    events,
    notifications,
    loading,
    eventsLoading,
    notificationsLoading,
    wsConnected,
    toasts,
    sortBy,
    statusFilter,
    worldFilter,
    onlineFriends,
    offlineFriends,
    activeFriends,
    busyFriends,
    askMeFriends,
    fetchFriends,
    fetchFriend,
    fetchEvents,
    fetchFriendLog,
    fetchNotifications,
    fetchFeed,
    acceptNotification,
    declineNotification,
    deleteNotification,
    addToast,
    connectWebSocket,
    disconnectWebSocket,
  }
})
