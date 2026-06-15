<template>
  <div class="dashboard-page container">
    <h1 class="page-title">{{ t('dashboard.title') }}</h1>

    <!-- Stats widgets -->
    <div class="stats-grid">
      <div class="stat-card online">
        <div class="stat-icon">🟢</div>
        <div class="stat-info">
          <div class="stat-number">{{ friendsStore.onlineFriends.length }}</div>
          <div class="stat-label">{{ t('dashboard.onlineNow') }}</div>
        </div>
      </div>
      <div class="stat-card active">
        <div class="stat-icon">🟢</div>
        <div class="stat-info">
          <div class="stat-number">{{ friendsStore.activeFriends.length }}</div>
          <div class="stat-label">{{ t('dashboard.active') }}</div>
        </div>
      </div>
      <div class="stat-card offline">
        <div class="stat-icon">⚫</div>
        <div class="stat-info">
          <div class="stat-number">{{ friendsStore.offlineFriends.length }}</div>
          <div class="stat-label">{{ t('dashboard.offline') }}</div>
        </div>
      </div>
      <div class="stat-card total">
        <div class="stat-icon">👥</div>
        <div class="stat-info">
          <div class="stat-number">{{ friendsStore.friends.length }}</div>
          <div class="stat-label">{{ t('dashboard.totalFriends') }}</div>
        </div>
      </div>
      <div class="stat-card notifications">
        <div class="stat-icon">🔔</div>
        <div class="stat-info">
          <div class="stat-number">{{ unreadNotifications }}</div>
          <div class="stat-label">{{ t('dashboard.unreadNotifications') }}</div>
        </div>
      </div>
      <div class="stat-card ws">
        <div class="stat-icon">{{ friendsStore.wsConnected ? '🟢' : '🔴' }}</div>
        <div class="stat-info">
          <div class="stat-number">{{ friendsStore.wsConnected ? t('dashboard.live') : t('dashboard.down') }}</div>
          <div class="stat-label">{{ t('dashboard.websocket') }}</div>
        </div>
      </div>
    </div>

    <!-- Widgets row -->
    <div class="widgets-row">
      <!-- Recent activity widget -->
      <div class="widget">
        <div class="widget-header">
          <h3>{{ t('dashboard.recentActivity') }}</h3>
          <router-link to="/feed" class="widget-link">{{ t('dashboard.viewAll') }}</router-link>
        </div>
        <div class="widget-body">
          <div v-if="recentEvents.length === 0" class="widget-empty">
            {{ t('dashboard.noRecentActivity') }}
          </div>
          <div v-else class="activity-list">
            <div
              v-for="evt in recentEvents"
              :key="evt.id"
              class="activity-item"
            >
              <span class="activity-icon">{{ eventIcon(evt.eventType) }}</span>
              <div class="activity-info">
                <span class="activity-friend">{{ evt.displayName }}</span>
                <span class="activity-text">{{ eventText(evt) }}</span>
              </div>
              <span class="activity-time">{{ formatTime(evt.timestamp) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Online friends widget -->
      <div class="widget">
        <div class="widget-header">
          <h3>{{ t('dashboard.onlineFriends') }}</h3>
          <router-link to="/" class="widget-link">{{ t('dashboard.viewAll') }}</router-link>
        </div>
        <div class="widget-body">
          <div v-if="friendsStore.onlineFriends.length === 0" class="widget-empty">
            {{ t('dashboard.noFriendsOnline') }}
          </div>
          <div v-else class="online-list">
            <div
              v-for="friend in friendsStore.onlineFriends.slice(0, 15)"
              :key="friend.id"
              class="online-item"
            >
              <img
                :src="api.proxyImage(friend.currentAvatarThumbnailImageUrl || friend.currentAvatarImageUrl || friend.userIcon)"
                :alt="friend.displayName"
                class="online-avatar"
                @error="onAvatarError"
              />
              <div class="online-info">
                <router-link :to="`/friend/${friend.id}`" class="online-name">
                  {{ friend.displayName }}
                </router-link>
                <span class="online-location" v-if="friend.location">
                  {{ formatLocation(friend.location) }}
                </span>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, onMounted } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'
import { api } from '../services/api.js'

const friendsStore = useFriendsStore()
const { t } = useI18n()

onMounted(() => {
  friendsStore.fetchFriends()
  friendsStore.fetchNotifications()
  friendsStore.fetchEvents(null, 20)
})

const unreadNotifications = computed(() =>
  friendsStore.notifications.filter((n) => !n.seen).length
)

const recentEvents = computed(() =>
  friendsStore.events.slice(0, 10)
)

function eventIcon(type) {
  if (!type) return '❓'
  if (type === 'online') return '🟢'
  if (type === 'offline') return '⚫'
  if (type === 'location_change') return '🌍'
  if (type === 'status_change') return '💬'
  if (type === 'avatar_change') return '🧑'
  if (type === 'name_change') return '✏️'
  return '📌'
}

function eventText(evt) {
  if (evt.eventType === 'online') return t('dashboard.cameOnline')
  if (evt.eventType === 'offline') return t('dashboard.wentOffline')
  if (evt.eventType === 'location_change') return t('dashboard.movedTo', { where: evt.newValue || t('common.unknown') })
  if (evt.eventType === 'status_change') return t('dashboard.statusChanged', { value: evt.newValue || t('common.unknown') })
  if (evt.eventType === 'avatar_change') return t('dashboard.changedAvatar')
  if (evt.eventType === 'name_change') return t('dashboard.nameChanged', { value: evt.newValue || t('common.unknown') })
  return evt.eventType
}

function formatLocation(loc) {
  if (!loc) return ''
  if (loc === 'private') return t('common.private')
  return loc.split(':')[0]
}

function formatTime(ts) {
  if (!ts) return ''
  const d = new Date(ts)
  const now = new Date()
  const diff = now - d
  if (diff < 60000) return t('common.justNow')
  if (diff < 3600000) return t('common.minutesAgo', { n: Math.floor(diff / 60000) })
  if (diff < 86400000) return t('common.hoursAgo', { n: Math.floor(diff / 3600000) })
  return d.toLocaleDateString()
}

function onAvatarError(e) {
  e.target.src =
    'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="50" fill="%23333"/><text x="50" y="55" text-anchor="middle" fill="%23fff" font-size="40">👤</text></svg>'
}
</script>

<style scoped>
.dashboard-page {
  padding: 20px 16px;
}

.page-title {
  font-size: 22px;
  font-weight: 700;
  margin-bottom: 20px;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(180px, 1fr));
  gap: 12px;
  margin-bottom: 24px;
}

.stat-card {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 18px 20px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  transition: transform var(--transition);
}
.stat-card:hover {
  transform: translateY(-2px);
}

.stat-icon {
  font-size: 28px;
}

.stat-number {
  font-size: 28px;
  font-weight: 700;
}
.stat-card.online .stat-number {
  color: var(--green);
}
.stat-card.active .stat-number {
  color: var(--green);
}
.stat-card.offline .stat-number {
  color: var(--gray);
}
.stat-card.notifications .stat-number {
  color: var(--highlight);
}

.stat-label {
  font-size: 13px;
  color: var(--text-secondary);
}

.widgets-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.widget {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.widget-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  border-bottom: 1px solid var(--border);
}

.widget-header h3 {
  font-size: 15px;
  font-weight: 600;
}

.widget-link {
  font-size: 13px;
  color: var(--highlight);
}

.widget-body {
  padding: 8px;
  max-height: 350px;
  overflow-y: auto;
}

.widget-empty {
  text-align: center;
  padding: 30px;
  color: var(--text-muted);
  font-size: 14px;
}

.activity-list {
  display: flex;
  flex-direction: column;
}

.activity-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 10px;
  border-radius: var(--radius);
  transition: background var(--transition);
}
.activity-item:hover {
  background: var(--bg-card-hover);
}

.activity-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.activity-info {
  flex: 1;
  min-width: 0;
}

.activity-friend {
  font-weight: 600;
  font-size: 13px;
}

.activity-text {
  font-size: 13px;
  color: var(--text-secondary);
  margin-left: 4px;
}

.activity-time {
  font-size: 11px;
  color: var(--text-muted);
  flex-shrink: 0;
}

.online-list {
  display: flex;
  flex-direction: column;
}

.online-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 10px;
  border-radius: var(--radius);
  transition: background var(--transition);
}
.online-item:hover {
  background: var(--bg-card-hover);
}

.online-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--bg-primary);
  flex-shrink: 0;
}

.online-info {
  flex: 1;
  min-width: 0;
}

.online-name {
  font-size: 13px;
  font-weight: 600;
  color: var(--text-primary);
}
.online-name:hover {
  color: var(--highlight);
  text-decoration: none;
}

.online-location {
  display: block;
  font-size: 11px;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

@media (max-width: 768px) {
  .widgets-row {
    grid-template-columns: 1fr;
  }
}
</style>
