<template>
  <div class="dashboard-page">
    <div class="page-header">
      <h1>📊 Dashboard</h1>
      <button class="btn btn-primary" @click="refresh">↻ Refresh</button>
    </div>

    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-icon online">🟢</div>
        <div class="stat-value">{{ onlineCount }}</div>
        <div class="stat-label">Online</div>
      </div>
      <div class="stat-card">
        <div class="stat-icon active">🟡</div>
        <div class="stat-value">{{ activeCount }}</div>
        <div class="stat-label">Active</div>
      </div>
      <div class="stat-card">
        <div class="stat-icon offline">⚫</div>
        <div class="stat-value">{{ offlineCount }}</div>
        <div class="stat-label">Offline</div>
      </div>
      <div class="stat-card">
        <div class="stat-icon total">👥</div>
        <div class="stat-value">{{ totalCount }}</div>
        <div class="stat-label">Total Friends</div>
      </div>
    </div>

    <div class="widgets-grid">
      <div class="widget">
        <h3>🔔 Recent Notifications</h3>
        <div v-if="notifications.length === 0" class="widget-empty">No notifications</div>
        <div v-else class="widget-list">
          <div v-for="n in notifications.slice(0, 5)" :key="n.id" class="widget-item">
            <span class="widget-item-icon">{{ n.type === 'friendRequest' ? '👋' : '🔔' }}</span>
            <span class="widget-item-text">{{ n.message || n.type }}</span>
          </div>
        </div>
      </div>

      <div class="widget">
        <h3>📜 Recent Activity</h3>
        <div v-if="recentEvents.length === 0" class="widget-empty">No activity yet</div>
        <div v-else class="widget-list">
          <div v-for="evt in recentEvents.slice(0, 5)" :key="evt.id" class="widget-item">
            <span class="widget-item-icon">{{ evt.eventType === 'online' ? '🟢' : evt.eventType === 'offline' ? '⚫' : '🔄' }}</span>
            <span class="widget-item-text">{{ evt.displayName }} - {{ evt.eventType }}</span>
          </div>
        </div>
      </div>

      <div class="widget">
        <h3>🟢 Online Friends</h3>
        <div v-if="onlineFriends.length === 0" class="widget-empty">No friends online</div>
        <div v-else class="widget-list">
          <div v-for="f in onlineFriends.slice(0, 8)" :key="f.id" class="widget-item">
            <img :src="api.proxyImage(f.currentAvatarThumbnailImageUrl)" class="widget-avatar" @error="onAvatarError" />
            <span class="widget-item-text">{{ f.displayName }}</span>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { api } from '../services/api.js'

const friendsStore = useFriendsStore()
const notifications = ref([])
const recentEvents = ref([])

const onlineFriends = computed(() => friendsStore.onlineFriends)
const onlineCount = computed(() => friendsStore.friends.filter(f => f.state === 'online').length)
const activeCount = computed(() => friendsStore.friends.filter(f => f.state === 'active').length)
const offlineCount = computed(() => friendsStore.friends.filter(f => f.state === 'offline').length)
const totalCount = computed(() => friendsStore.friends.length)

onMounted(() => refresh())

async function refresh() {
  await Promise.all([
    friendsStore.fetchFriends(),
    friendsStore.fetchNotifications().then(() => { notifications.value = friendsStore.notifications }),
    friendsStore.fetchEvents(null, 20).then(() => { recentEvents.value = friendsStore.events }),
  ])
}

function onAvatarError(e) {
  e.target.src = 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="50" fill="%23333"/><text x="50" y="55" text-anchor="middle" fill="%23fff" font-size="40">👤</text></svg>'
}
</script>

<style scoped>
.dashboard-page {
  max-width: 1200px;
  margin: 0 auto;
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h1 {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
}

.btn {
  padding: 8px 16px;
  border-radius: var(--radius);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all var(--transition);
  border: none;
}

.btn-primary {
  background: var(--highlight);
  color: #fff;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 16px;
  margin-bottom: 24px;
}

.stat-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 20px;
  text-align: center;
}

.stat-icon {
  font-size: 28px;
  margin-bottom: 8px;
}

.stat-value {
  font-size: 32px;
  font-weight: 700;
  color: var(--text-primary);
}

.stat-label {
  font-size: 13px;
  color: var(--text-secondary);
  margin-top: 4px;
}

.widgets-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
  gap: 16px;
}

.widget {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 16px;
}

.widget h3 {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 12px;
}

.widget-empty {
  color: var(--text-muted);
  font-size: 14px;
  padding: 16px 0;
}

.widget-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.widget-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px;
  border-radius: 6px;
  transition: background var(--transition);
}

.widget-item:hover {
  background: var(--bg-secondary);
}

.widget-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  object-fit: cover;
}

.widget-item-text {
  font-size: 13px;
  color: var(--text-primary);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
</style>
