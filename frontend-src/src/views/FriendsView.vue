<template>
  <div class="friends-page container">
    <!-- Stats bar -->
    <div class="stats-bar">
      <div class="stat">
        <span class="stat-num highlight">{{ friendsStore.onlineFriends.length }}</span>
        <span class="stat-label">{{ t('friends.online') }}</span>
      </div>
      <div class="stat">
        <span class="stat-num active-color">{{ friendsStore.activeFriends.length }}</span>
        <span class="stat-label">{{ t('friends.active') }}</span>
      </div>
      <div class="stat">
        <span class="stat-num askme-color">{{ friendsStore.askMeFriends.length }}</span>
        <span class="stat-label">{{ t('friends.askMe') }}</span>
      </div>
      <div class="stat">
        <span class="stat-num busy-color">{{ friendsStore.busyFriends.length }}</span>
        <span class="stat-label">{{ t('friends.busy') }}</span>
      </div>
      <div class="stat">
        <span class="stat-num offline-color">{{ friendsStore.offlineFriends.length }}</span>
        <span class="stat-label">{{ t('friends.offline') }}</span>
      </div>
      <div class="stat">
        <span class="stat-num">{{ friendsStore.friends.length }}</span>
        <span class="stat-label">{{ t('friends.total') }}</span>
      </div>
    </div>

    <!-- Search and filters -->
    <div class="search-bar">
      <input
        v-model="search"
        type="text"
        :placeholder="t('friends.searchPlaceholder')"
        class="search-input"
      />
      <div class="filter-group">
        <select v-model="statusFilter" class="filter-select">
          <option value="">{{ t('friends.allStatus') }}</option>
          <option value="online">🟢 {{ t('friends.online') }}</option>
          <option value="active">🟢 {{ t('friends.active') }}</option>
          <option value="ask me">🔵 {{ t('friends.askMe') }}</option>
          <option value="busy">🔴 {{ t('friends.busy') }}</option>
          <option value="offline">⚫ {{ t('friends.offline') }}</option>
        </select>
        <select v-model="sortBy" class="filter-select">
          <option value="status">{{ t('friends.sortStatus') }}</option>
          <option value="name">{{ t('friends.sortName') }}</option>
          <option value="lastSeen">{{ t('friends.sortLastSeen') }}</option>
        </select>
      </div>
    </div>

    <!-- Loading -->
    <div v-if="friendsStore.loading" class="loading-state">
      <div class="spinner"></div>
      <p>{{ t('friends.loadingFriends') }}</p>
    </div>

    <!-- Friends table -->
    <div v-else-if="filteredFriends.length > 0" class="friends-table">
      <div class="table-header">
        <span class="col-avatar"></span>
        <span class="col-status">{{ t('friends.status') }}</span>
        <span class="col-name">{{ t('friends.displayName') }}</span>
        <span class="col-rank">{{ t('friends.rank') }}</span>
        <span class="col-bio">{{ t('friends.bioStatus') }}</span>
        <span class="col-location">{{ t('friends.location') }}</span>
        <span class="col-platform">{{ t('friends.platform') }}</span>
        <span class="col-last-login">{{ t('friends.lastLogin') }}</span>
      </div>
      <router-link
        v-for="friend in filteredFriends"
        :key="friend.id"
        :to="`/friend/${friend.id}`"
        class="table-row"
        :class="friend.state"
      >
        <span class="col-avatar">
          <img
            :src="api.proxyImage(friend.userIcon || friend.profilePicOverrideThumbnail || friend.profilePicOverride || friend.currentAvatarThumbnailImageUrl || friend.currentAvatarImageUrl)"
            :alt="friend.displayName"
            @error="onAvatarError"
          />
          <span class="status-dot" :class="friend.state"></span>
        </span>
        <span class="col-status">
          <span class="status-badge" :class="statusClass(friend)">
            {{ friend.status || friend.state || 'offline' }}
          </span>
        </span>
        <span class="col-name">
          <span class="friend-name">{{ friend.displayName }}</span>
        </span>
        <span class="col-rank">
          <span class="rank-badge" :class="getTrustClass(friend)">
            {{ getTrustLevel(friend) }}
          </span>
        </span>
        <span class="col-bio">
          <span class="bio-text" :title="friend.statusDescription || friend.bio">
            {{ friend.statusDescription || friend.bio || '—' }}
          </span>
        </span>
        <span class="col-location">
          <span v-if="friend.state !== 'offline' && friend.location" class="location-text" :title="friend.location">
            {{ formatLocation(friend.location) }}
          </span>
          <span v-else class="location-text offline-text">—</span>
        </span>
        <span class="col-platform">
          <span class="platform-icon" :title="friend.platform || friend.lastPlatform">
            {{ platformIcon(friend.platform || friend.lastPlatform) }}
          </span>
        </span>
        <span class="col-last-login">
          <span class="time-text">{{ formatTime(friend.lastLogin) }}</span>
        </span>
      </router-link>
    </div>

    <!-- Empty state -->
    <div v-else class="empty-state">
      <div class="empty-icon">👥</div>
      <p v-if="search || statusFilter">{{ t('friends.noFriendsMatch') }}</p>
      <p v-else>{{ t('friends.noFriendsFound') }}</p>
      <button class="btn-secondary" @click="friendsStore.fetchFriends()">{{ t('common.refresh') }}</button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'
import { api } from '../services/api.js'

const friendsStore = useFriendsStore()
const { t } = useI18n()
const search = ref('')
const statusFilter = ref('')
const sortBy = ref('status')

onMounted(() => {
  friendsStore.fetchFriends()
  friendsStore.fetchNotifications()
})

const filteredFriends = computed(() => {
  let list = [...friendsStore.friends]

  // Filter by status
  if (statusFilter.value) {
    if (statusFilter.value === 'offline') {
      list = list.filter((f) => f.state === 'offline')
    } else if (statusFilter.value === 'ask me') {
      list = list.filter((f) =>
        (f.status === 'ask me' || f.state === 'askme' || f.userStatus === 'ask me') && f.state !== 'offline'
      )
    } else if (statusFilter.value === 'busy') {
      list = list.filter((f) =>
        (f.status === 'busy' || f.state === 'busy' || f.userStatus === 'busy') && f.state !== 'offline'
      )
    } else {
      list = list.filter((f) => f.state === statusFilter.value)
    }
  }

  // Search
  if (search.value) {
    const q = search.value.toLowerCase()
    list = list.filter(
      (f) =>
        f.displayName.toLowerCase().includes(q) ||
        (f.statusDescription && f.statusDescription.toLowerCase().includes(q)) ||
        (f.bio && f.bio.toLowerCase().includes(q)) ||
        (f.location && f.location.toLowerCase().includes(q)) ||
        (f.userRank && f.userRank.toLowerCase().includes(q))
    )
  }

  // Sort
  const statusOrder = { online: 0, active: 1, 'ask me': 2, askme: 2, busy: 3, offline: 4 }
  if (sortBy.value === 'status') {
    list.sort((a, b) => {
      const sa = statusOrder[a.status || a.state] ?? 4
      const sb = statusOrder[b.status || b.state] ?? 4
      if (sa !== sb) return sa - sb
      return a.displayName.localeCompare(b.displayName)
    })
  } else if (sortBy.value === 'name') {
    list.sort((a, b) => a.displayName.localeCompare(b.displayName))
  } else if (sortBy.value === 'lastSeen') {
    list.sort((a, b) => {
      const la = a.lastLogin ? new Date(a.lastLogin).getTime() : 0
      const lb = b.lastLogin ? new Date(b.lastLogin).getTime() : 0
      return lb - la
    })
  }

  return list
})

function statusClass(friend) {
  const s = friend.status || friend.state || 'offline'
  if (s === 'ask me' || s === 'askme') return 'askme'
  return s
}

function formatLocation(loc) {
  if (!loc) return ''
  if (loc === 'private') return t('common.private')
  if (loc === 'offline') return t('common.offline')
  return loc.split(':')[0]
}

function platformIcon(platform) {
  if (!platform) return ''
  const p = platform.toLowerCase()
  if (p.includes('standalon') || p.includes('quest')) return '🥽'
  if (p.includes('win')) return '🖥️'
  if (p.includes('linux')) return '🐧'
  if (p.includes('android')) return '📱'
  return '🎮'
}

function getTrustLevel(friend) {
  if (friend.trustRank) return friend.trustRank
  if (friend.userRank) return friend.userRank
  // Derive from tags
  if (!friend.tags) return t('common.user')
  if (friend.tags.some((t) => t.includes('system_trust_legend'))) return t('common.legend')
  if (friend.tags.some((t) => t.includes('system_trust_veteran'))) return t('common.veteran')
  if (friend.tags.some((t) => t.includes('system_trust_trusted'))) return t('common.trusted')
  if (friend.tags.some((t) => t.includes('system_trust_known'))) return t('common.known')
  if (friend.tags.some((t) => t.includes('system_trust_basic'))) return t('common.basic')
  return t('common.new')
}

function getTrustClass(friend) {
  const level = getTrustLevel(friend).toLowerCase()
  return `trust-${level}`
}

function formatTime(ts) {
  if (!ts) return t('common.never')
  const d = new Date(ts)
  const now = new Date()
  const diffMs = now - d
  if (diffMs < 60000) return t('common.justNow')
  if (diffMs < 3600000) return t('common.minutesAgo', { n: Math.floor(diffMs / 60000) })
  if (diffMs < 86400000) return t('common.hoursAgo', { n: Math.floor(diffMs / 3600000) })
  if (diffMs < 604800000) return t('common.daysAgo', { n: Math.floor(diffMs / 86400000) })
  return d.toLocaleDateString()
}

function onAvatarError(e) {
  e.target.src =
    'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="50" fill="%23333"/><text x="50" y="55" text-anchor="middle" fill="%23fff" font-size="40">👤</text></svg>'
}
</script>

<style scoped>
.friends-page {
  padding: 20px 16px;
}

.stats-bar {
  display: flex;
  gap: 24px;
  margin-bottom: 20px;
  padding: 16px 20px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  flex-wrap: wrap;
}

.stat {
  display: flex;
  align-items: baseline;
  gap: 6px;
}

.stat-num {
  font-size: 24px;
  font-weight: 700;
  color: var(--text-primary);
}
.stat-num.highlight {
  color: var(--green);
}
.stat-num.active-color {
  color: var(--green);
}
.stat-num.askme-color {
  color: var(--blue);
}
.stat-num.busy-color {
  color: var(--red);
}
.stat-num.offline-color {
  color: var(--gray);
}

.stat-label {
  font-size: 13px;
  color: var(--text-secondary);
}

.search-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.search-input {
  flex: 1;
  min-width: 200px;
  padding: 10px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  color: var(--text-primary);
  font-size: 14px;
}
.search-input:focus {
  border-color: var(--highlight);
}

.filter-group {
  display: flex;
  gap: 8px;
}

.filter-select {
  padding: 8px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  color: var(--text-primary);
  font-size: 13px;
  cursor: pointer;
}
.filter-select:focus {
  border-color: var(--highlight);
  outline: none;
}

.friends-table {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.table-header {
  display: grid;
  grid-template-columns: 50px 80px 1fr 80px 1.5fr 1.2fr 50px 90px;
  gap: 8px;
  padding: 10px 16px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border);
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.table-row {
  display: grid;
  grid-template-columns: 50px 80px 1fr 80px 1.5fr 1.2fr 50px 90px;
  gap: 8px;
  padding: 10px 16px;
  align-items: center;
  border-bottom: 1px solid var(--border);
  transition: background var(--transition);
  text-decoration: none;
  color: inherit;
}
.table-row:last-child {
  border-bottom: none;
}
.table-row:hover {
  background: var(--bg-card-hover);
}
.table-row.offline {
  opacity: 0.6;
}
.table-row.offline:hover {
  opacity: 0.85;
}

.col-avatar {
  position: relative;
  width: 40px;
  height: 40px;
  flex-shrink: 0;
}
.col-avatar img {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--bg-primary);
}

.status-dot {
  position: absolute;
  bottom: -2px;
  right: -2px;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  border: 2px solid var(--bg-card);
}
.status-dot.online {
  background: var(--green);
}
.status-dot.active {
  background: var(--green);
}
.status-dot.offline {
  background: var(--gray);
}

.status-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 600;
  text-transform: capitalize;
}
.status-badge.online {
  background: rgba(63, 185, 80, 0.2);
  color: var(--green);
}
.status-badge.active {
  background: rgba(63, 185, 80, 0.2);
  color: var(--green);
}
.status-badge.busy {
  background: rgba(233, 69, 96, 0.2);
  color: var(--red);
}
.status-badge.offline {
  background: rgba(139, 148, 158, 0.2);
  color: var(--gray);
}
.status-badge.askme {
  background: rgba(88, 166, 255, 0.2);
  color: var(--blue);
}

.friend-name {
  font-weight: 600;
  font-size: 14px;
}

.rank-badge {
  display: inline-block;
  padding: 2px 6px;
  border-radius: 4px;
  font-size: 10px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}
.trust-legend {
  background: rgba(233, 69, 96, 0.2);
  color: #ff6b8a;
}
.trust-veteran {
  background: rgba(210, 153, 34, 0.2);
  color: #f0c040;
}
.trust-trusted {
  background: rgba(63, 185, 80, 0.2);
  color: var(--green);
}
.trust-known {
  background: rgba(88, 166, 255, 0.2);
  color: var(--blue);
}
.trust-basic {
  background: rgba(139, 148, 158, 0.2);
  color: var(--gray);
}
.trust-user {
  background: rgba(139, 148, 158, 0.15);
  color: var(--text-muted);
}

.bio-text {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.location-text {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.offline-text {
  color: var(--text-muted);
}

.platform-icon {
  font-size: 16px;
}

.time-text {
  font-size: 12px;
  color: var(--text-muted);
  white-space: nowrap;
}

.loading-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.spinner {
  width: 36px;
  height: 36px;
  border: 3px solid var(--border);
  border-top-color: var(--highlight);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 16px;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.empty-icon {
  font-size: 48px;
  margin-bottom: 12px;
}

@media (max-width: 768px) {
  .table-header {
    display: none;
  }
  .table-row {
    grid-template-columns: 40px 1fr auto;
    gap: 8px;
  }
  .col-rank,
  .col-bio,
  .col-location,
  .col-platform,
  .col-last-login {
    display: none;
  }
}
</style>
