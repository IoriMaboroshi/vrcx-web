<template>
  <div class="activity-log-page container">
    <h1 class="page-title">{{ t('activityLog.title') }}</h1>

    <!-- Tab filter (VRCX style) -->
    <div class="filter-tabs">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        class="tab-btn"
        :class="{ active: activeTab === tab.key }"
        @click="activeTab = tab.key"
      >
        {{ tab.icon }} {{ tab.label }}
        <span class="tab-count">{{ getTabCount(tab.key) }}</span>
      </button>
    </div>

    <!-- Search + info -->
    <div class="toolbar">
      <input
        v-model="searchFilter"
        type="text"
        :placeholder="t('activityLog.searchPlaceholder')"
        class="filter-input"
      />
      <div class="toolbar-info">
        {{ t('activityLog.totalEntries', { n: filteredEvents.length }) }}
      </div>
    </div>

    <!-- Loading -->
    <div v-if="friendsStore.eventsLoading" class="loading-state">
      <div class="spinner"></div>
      <p>{{ t('activityLog.loadingLog') }}</p>
    </div>

    <!-- Data Table -->
    <div v-else-if="filteredEvents.length > 0" class="data-table">
      <div class="table-header">
        <span class="col-date">{{ t('activityLog.colDate') }}</span>
        <span class="col-type">{{ t('activityLog.colType') }}</span>
        <span class="col-user">{{ t('activityLog.colUser') }}</span>
        <span class="col-detail">{{ t('activityLog.colDetail') }}</span>
      </div>
      <div
        v-for="evt in paginatedEvents"
        :key="evt.id || evt._dedupeKey || evt.timestamp"
        class="table-row"
        @click="showUser(evt.userId)"
      >
        <span class="col-date">
          <span class="date-short">{{ formatDateShort(evt.timestamp) }}</span>
          <span class="date-full" :title="formatDateFull(evt.timestamp)">{{ timeAgo(evt.timestamp) }}</span>
        </span>
        <span class="col-type">
          <span class="type-badge" :class="eventTypeClass(evt)">
            {{ formatTypeBadge(evt) }}
          </span>
        </span>
        <span class="col-user">
          <img v-if="getAvatar(evt)" :src="getAvatar(evt)" class="user-avatar" @error="onAvatarError" />
          <span class="user-name">{{ evt.displayName || evt.userId }}</span>
        </span>
        <span class="col-detail">
          <!-- Session (merged online→offline) -->
          <template v-if="evt._mergedType === 'session'">
            <span class="session-indicator">
              <span class="status-dot dot-online"></span>
              {{ t('activityLog.online') }}
              <span class="time-range">({{ formatSessionDuration(evt) }})</span>
              <span class="arrow">→</span>
              <span class="status-dot dot-offline"></span>
              {{ t('activityLog.offline') }}
            </span>
            <Location v-if="evt.location" :location="evt.location" :world-name="evt.worldName" />
          </template>

          <!-- Grouped location changes -->
          <template v-else-if="evt._mergedType === 'locationGroup'">
            <div class="location-flow">
              <div v-for="(loc, i) in evt._locations" :key="i" class="location-step">
                <span v-if="i > 0" class="flow-arrow">↓</span>
                <Location :location="loc.location" :world-name="loc.worldName" />
                <span class="step-time">{{ formatStepTime(loc.timestamp) }}</span>
              </div>
            </div>
          </template>

          <!-- GPS / location_change -->
          <template v-else-if="isType(evt, 'GPS')">
            <div v-if="evt.previousLocation" class="location-flow">
              <Location :location="evt.previousLocation" :world-name="evt.worldName" />
              <span class="flow-arrow">↓</span>
            </div>
            <Location v-if="evt.location" :location="evt.location" :world-name="evt.worldName" />
          </template>

          <!-- Online / Offline -->
          <template v-else-if="isType(evt, 'Online') || isType(evt, 'Offline')">
            <Location v-if="evt.location" :location="evt.location" :world-name="evt.worldName" />
          </template>

          <!-- Avatar change with old→new thumbnail -->
          <template v-else-if="isType(evt, 'Avatar')">
            <div class="avatar-change">
              <img
                v-if="evt.previousCurrentAvatarThumbnailImageUrl"
                :src="evt.previousCurrentAvatarThumbnailImageUrl"
                class="change-img"
                @error="onAvatarError"
              />
              <span class="flow-arrow">→</span>
              <img
                v-if="evt.currentAvatarThumbnailImageUrl"
                :src="evt.currentAvatarThumbnailImageUrl"
                class="change-img"
                @error="onAvatarError"
              />
            </div>
          </template>

          <!-- Status change -->
          <template v-else-if="isType(evt, 'Status')">
            <span class="old-val">{{ evt.previousStatus || evt.oldValue }}</span>
            <span class="flow-arrow">→</span>
            <span class="new-val" :class="'status-' + (evt.status || evt.newValue)">
              {{ evt.status || evt.newValue }}
            </span>
          </template>

          <!-- Bio change -->
          <template v-else-if="isType(evt, 'Bio')">
            <div class="bio-change">
              <div v-if="evt.previousBio" class="old-bio">{{ evt.previousBio }}</div>
              <span v-if="evt.previousBio" class="flow-arrow">↓</span>
              <div v-if="evt.bio" class="new-bio">{{ evt.bio }}</div>
            </div>
          </template>

          <!-- DisplayName -->
          <template v-else-if="isType(evt, 'DisplayName')">
            <span class="old-val">{{ evt.previousDisplayName || evt.oldValue }}</span>
            <span class="flow-arrow">→</span>
            <span class="new-val">{{ evt.displayName || evt.newValue }}</span>
          </template>

          <!-- TrustLevel -->
          <template v-else-if="isType(evt, 'TrustLevel')">
            <span class="old-val">{{ evt.previousTrustLevel }}</span>
            <span class="flow-arrow">→</span>
            <span class="new-val">{{ evt.trustLevel }}</span>
          </template>

          <!-- Friend / Unfriend -->
          <template v-else-if="isType(evt, 'Friend') || isType(evt, 'Unfriend')">
            <Location v-if="evt.location" :location="evt.location" :world-name="evt.worldName" />
          </template>

          <!-- FriendRequest / CancelFriendRequest -->
          <template v-else-if="isType(evt, 'FriendRequest') || isType(evt, 'CancelFriendRequest')">
            <span>{{ isType(evt, 'FriendRequest') ? '→' : '✕' }}</span>
          </template>

          <!-- Default -->
          <template v-else>
            <span>{{ evt.type || evt.eventType }}</span>
          </template>
        </span>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="empty-state">
      <div class="empty-icon">📋</div>
      <p>{{ t('activityLog.noActivity') }}</p>
    </div>

    <!-- Pagination -->
    <div v-if="totalPages > 1" class="pagination">
      <button :disabled="currentPage <= 1" @click="currentPage--" class="page-btn">←</button>
      <span class="page-info">{{ currentPage }} / {{ totalPages }}</span>
      <button :disabled="currentPage >= totalPages" @click="currentPage++" class="page-btn">→</button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'
import { useRouter } from 'vue-router'
import { api } from '../services/api.js'

const friendsStore = useFriendsStore()
const { t } = useI18n()
const router = useRouter()

const activeTab = ref('all')
const searchFilter = ref('')
const currentPage = ref(1)
const pageSize = 50

const tabs = computed(() => [
  { key: 'all', icon: '📋', label: t('activityLog.tabAll') },
  { key: 'GPS', icon: '🌍', label: t('activityLog.tabGPS') },
  { key: 'Online', icon: '🟢', label: t('activityLog.tabOnline') },
  { key: 'Offline', icon: '⚫', label: t('activityLog.tabOffline') },
  { key: 'Avatar', icon: '🧑', label: t('activityLog.tabAvatar') },
  { key: 'Status', icon: '💬', label: t('activityLog.tabStatus') },
  { key: 'Bio', icon: '📝', label: t('activityLog.tabBio') },
  { key: 'DisplayName', icon: '✏️', label: t('activityLog.tabDisplayName') },
  { key: 'Friend', icon: '💚', label: t('activityLog.tabFriend') },
])

onMounted(() => {
  friendsStore.fetchEvents(null, 500)
})

watch([activeTab, searchFilter], () => {
  currentPage.value = 1
})

// --- Helpers to normalize event types ---
function normalizeType(evt) {
  const t = (evt.type || evt.eventType || '').toLowerCase()
  if (t === 'online') return 'Online'
  if (t === 'offline') return 'Offline'
  if (t === 'location_change') return 'GPS'
  if (t === 'avatar_change') return 'Avatar'
  if (t === 'status_change') return 'Status'
  if (t === 'bio_change') return 'Bio'
  if (t === 'name_change') return 'DisplayName'
  // FriendLog types stay as-is
  return evt.type || evt.eventType || ''
}

function isType(evt, type) {
  return normalizeType(evt) === type
}

// --- Event deduplication / grouping (VRCX-style) ---

const SESSION_MERGE_WINDOW_MS = 5 * 60 * 1000 // 5 minutes
const LOCATION_GROUP_WINDOW_MS = 30 * 60 * 1000 // 30 minutes

function deduplicateAndGroup(events) {
  // Sort by time ascending for processing
  const sorted = [...events].sort((a, b) => {
    const ta = a.timestamp ? new Date(a.timestamp).getTime() : 0
    const tb = b.timestamp ? new Date(b.timestamp).getTime() : 0
    return ta - tb
  })

  const result = []
  const consumed = new Set()

  // Pass 1: Merge Online→Offline sessions for the same user within 5 minutes
  for (let i = 0; i < sorted.length; i++) {
    if (consumed.has(i)) continue
    const evt = sorted[i]
    const evtType = normalizeType(evt)

    if (evtType === 'Online') {
      // Look for the next Offline event for the same user within the merge window
      const onlineTime = new Date(evt.timestamp).getTime()
      for (let j = i + 1; j < sorted.length; j++) {
        if (consumed.has(j)) continue
        const next = sorted[j]
        const nextType = normalizeType(next)
        const nextTime = new Date(next.timestamp).getTime()

        if (nextTime - onlineTime > SESSION_MERGE_WINDOW_MS) break

        if (nextType === 'Offline' && next.userId === evt.userId) {
          // Merge into a session event
          consumed.add(i)
          consumed.add(j)
          result.push({
            ...evt,
            _mergedType: 'session',
            _dedupeKey: `session:${evt.userId}:${evt.timestamp}`,
            _offlineTimestamp: next.timestamp,
            _offlineLocation: next.location || next.newValue,
          })
          break
        }
      }
    }
  }

  // Pass 2: Group consecutive location_change events for the same user within 30 min
  for (let i = 0; i < sorted.length; i++) {
    if (consumed.has(i)) continue
    const evt = sorted[i]
    const evtType = normalizeType(evt)

    if (evtType === 'GPS') {
      const group = [{ location: evt.location || evt.newValue, worldName: evt.worldName, timestamp: evt.timestamp }]
      const groupStart = new Date(evt.timestamp).getTime()
      consumed.add(i)

      for (let j = i + 1; j < sorted.length; j++) {
        if (consumed.has(j)) continue
        const next = sorted[j]
        const nextType = normalizeType(next)
        const nextTime = new Date(next.timestamp).getTime()

        if (nextTime - groupStart > LOCATION_GROUP_WINDOW_MS) break
        if (next.userId !== evt.userId) continue
        if (nextType !== 'GPS') continue

        group.push({ location: next.location || next.newValue, worldName: next.worldName, timestamp: next.timestamp })
        consumed.add(j)
      }

      if (group.length > 1) {
        // Replace the group's previousLocation from the first event
        result.push({
          ...evt,
          _mergedType: 'locationGroup',
          _dedupeKey: `locGroup:${evt.userId}:${evt.timestamp}`,
          _locations: group,
        })
      } else {
        // Single location change, keep as-is
        result.push(evt)
      }
    }
  }

  // Pass 3: Add all remaining unconsumed events
  for (let i = 0; i < sorted.length; i++) {
    if (consumed.has(i)) continue
    result.push(sorted[i])
  }

  // Sort result by timestamp descending (newest first)
  result.sort((a, b) => {
    const ta = a.timestamp ? new Date(a.timestamp).getTime() : 0
    const tb = b.timestamp ? new Date(b.timestamp).getTime() : 0
    return tb - ta
  })

  return result
}

const filteredEvents = computed(() => {
  let list = deduplicateAndGroup(friendsStore.events)

  // Filter by tab
  if (activeTab.value !== 'all') {
    list = list.filter(e => {
      if (e._mergedType === 'session') return activeTab.value === 'Online' || activeTab.value === 'Offline'
      if (e._mergedType === 'locationGroup') return activeTab.value === 'GPS'
      return normalizeType(e) === activeTab.value
    })
  }

  // Search by displayName
  if (searchFilter.value) {
    const q = searchFilter.value.toLowerCase()
    list = list.filter(e => (e.displayName || '').toLowerCase().includes(q))
  }

  return list
})

function getTabCount(key) {
  if (key === 'all') return friendsStore.events.length
  return friendsStore.events.filter(e => {
    const t = normalizeType(e)
    if (key === 'Online') return t === 'Online' || t === 'online'
    if (key === 'Offline') return t === 'Offline' || t === 'offline'
    return t === key
  }).length
}

const totalPages = computed(() => Math.ceil(filteredEvents.value.length / pageSize))

const paginatedEvents = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  return filteredEvents.value.slice(start, start + pageSize)
})

function eventTypeClass(evt) {
  if (evt._mergedType === 'session') return 'type-session'
  if (evt._mergedType === 'locationGroup') return 'type-gps'
  const type = normalizeType(evt)
  const map = {
    Online: 'type-online',
    Offline: 'type-offline',
    GPS: 'type-gps',
    Avatar: 'type-avatar',
    Status: 'type-status',
    Bio: 'type-bio',
    DisplayName: 'type-name',
    Friend: 'type-friend',
    Unfriend: 'type-unfriend',
    FriendRequest: 'type-request',
    CancelFriendRequest: 'type-cancel',
    TrustLevel: 'type-trust',
  }
  return map[type] || 'type-default'
}

function formatTypeBadge(evt) {
  if (evt._mergedType === 'session') return t('activityLog.type_session')
  if (evt._mergedType === 'locationGroup') return t('activityLog.type_GPS')
  const type = normalizeType(evt)
  return t('activityLog.type_' + type) || type
}

function getAvatar(evt) {
  const friend = friendsStore.friends.find(f => f.id === evt.userId)
  const raw = friend?.currentAvatarThumbnailImageUrl || friend?.currentAvatarImageUrl || friend?.userIcon
  return raw ? api.proxyImage(raw) : ''
}

function formatDateShort(ts) {
  if (!ts) return ''
  const d = new Date(ts)
  const now = new Date()
  const isToday = d.toDateString() === now.toDateString()
  if (isToday) {
    return d.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Shanghai' })
  }
  return d.toLocaleDateString('zh-CN', { month: '2-digit', day: '2-digit', timeZone: 'Asia/Shanghai' })
}

function formatDateFull(ts) {
  if (!ts) return ''
  return new Date(ts).toLocaleString('zh-CN', { timeZone: 'Asia/Shanghai' })
}

function timeAgo(ts) {
  if (!ts) return ''
  const diff = Date.now() - new Date(ts).getTime()
  if (diff < 60000) return t('activityLog.justNow')
  if (diff < 3600000) return `${Math.floor(diff / 60000)}${t('activityLog.minutesAgo')}`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}${t('activityLog.hoursAgo')}`
  if (diff < 604800000) return `${Math.floor(diff / 86400000)}${t('activityLog.daysAgo')}`
  return formatDateShort(ts)
}

function formatSessionDuration(evt) {
  if (!evt.timestamp || !evt._offlineTimestamp) return ''
  const diff = new Date(evt._offlineTimestamp).getTime() - new Date(evt.timestamp).getTime()
  if (diff < 60000) return `<1min`
  if (diff < 3600000) return `${Math.floor(diff / 60000)}min`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}h ${Math.floor((diff % 3600000) / 60000)}m`
  return `${Math.floor(diff / 86400000)}d`
}

function formatStepTime(ts) {
  if (!ts) return ''
  return new Date(ts).toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Shanghai' })
}

function showUser(userId) {
  if (userId) {
    router.push(`/friend/${userId}`)
  }
}

function onAvatarError(e) {
  e.target.style.display = 'none'
}
</script>

<style scoped>
.activity-log-page { padding: 20px 16px; }

.page-title {
  font-size: 20px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.filter-tabs {
  display: flex;
  gap: 6px;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.tab-btn {
  display: flex;
  align-items: center;
  gap: 4px;
  padding: 6px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  color: var(--text-secondary);
  font-size: 13px;
  cursor: pointer;
  transition: all 0.2s;
}
.tab-btn:hover { border-color: var(--highlight); color: var(--text-primary); }
.tab-btn.active { background: var(--accent); border-color: var(--accent-light); color: #fff; }

.tab-count {
  font-size: 11px;
  opacity: 0.7;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.filter-input {
  padding: 8px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  color: var(--text-primary);
  font-size: 13px;
  flex: 0.4;
  min-width: 200px;
}
.filter-input:focus { border-color: var(--highlight); }

.toolbar-info {
  font-size: 13px;
  color: var(--text-secondary);
}

.data-table {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.table-header {
  display: grid;
  grid-template-columns: 120px 140px 240px 1fr;
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
  grid-template-columns: 120px 140px 240px 1fr;
  gap: 8px;
  padding: 10px 16px;
  align-items: center;
  border-bottom: 1px solid var(--border);
  cursor: pointer;
  transition: background var(--transition);
}
.table-row:last-child { border-bottom: none; }
.table-row:hover { background: var(--bg-card-hover); }

.col-date {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.date-short { font-size: 13px; color: var(--text-primary); }
.date-full { font-size: 11px; color: var(--text-secondary); }

.type-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}
.type-session { background: rgba(88, 166, 255, 0.2); color: var(--blue); }
.type-online { background: rgba(63, 185, 80, 0.2); color: var(--green); }
.type-offline { background: rgba(139, 148, 158, 0.2); color: var(--gray); }
.type-gps { background: rgba(88, 166, 255, 0.2); color: var(--blue); }
.type-avatar { background: rgba(210, 153, 34, 0.2); color: var(--yellow); }
.type-status { background: rgba(188, 140, 255, 0.2); color: #bc8cff; }
.type-bio { background: rgba(255, 154, 158, 0.2); color: #ff9a9e; }
.type-name { background: rgba(210, 153, 34, 0.2); color: #f0c040; }
.type-friend { background: rgba(63, 185, 80, 0.2); color: var(--green); }
.type-unfriend { background: rgba(233, 69, 96, 0.2); color: var(--red); }
.type-request { background: rgba(88, 166, 255, 0.2); color: var(--blue); }
.type-cancel { background: rgba(139, 148, 158, 0.2); color: var(--gray); }
.type-trust { background: rgba(188, 140, 255, 0.2); color: #bc8cff; }
.type-default { background: rgba(139, 148, 158, 0.15); color: var(--text-secondary); }

.col-user {
  display: flex;
  align-items: center;
  gap: 8px;
}

.user-avatar {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--bg-primary);
}

.user-name {
  font-weight: 500;
  font-size: 13px;
  color: var(--text-primary);
}

.col-detail {
  font-size: 13px;
  color: var(--text-secondary);
  min-width: 0;
}

.old-val { color: var(--text-muted); text-decoration: line-through; }
.flow-arrow { margin: 0 6px; color: var(--text-muted); }
.new-val { color: var(--text-primary); font-weight: 500; }

.session-indicator {
  display: inline-flex;
  align-items: center;
  gap: 4px;
}

.status-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
}
.dot-online { background: var(--green); }
.dot-offline { background: var(--gray); }

.time-range {
  font-size: 11px;
  color: var(--text-muted);
  margin-left: 4px;
}

.location-flow { }
.location-step {
  display: flex;
  align-items: center;
  gap: 4px;
}
.step-time {
  font-size: 11px;
  color: var(--text-muted);
  margin-left: 4px;
}

.avatar-change {
  display: flex;
  align-items: center;
  gap: 8px;
}
.change-img {
  width: 60px;
  height: 45px;
  border-radius: 6px;
  object-fit: cover;
  background: var(--bg-primary);
}

.bio-change .old-bio { color: var(--text-muted); margin-bottom: 4px; }
.bio-change .new-bio { color: var(--text-primary); }

.status-online { color: var(--green); }
.status-active { color: var(--yellow); }
.status-busy { color: var(--red); }
.status-offline { color: var(--gray); }
.status-ask\ me { color: var(--blue); }

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  margin-top: 16px;
}

.page-btn {
  padding: 6px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  color: var(--text-primary);
  cursor: pointer;
}
.page-btn:disabled { opacity: 0.4; cursor: not-allowed; }

.page-info {
  font-size: 13px;
  color: var(--text-secondary);
}

.loading-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.spinner {
  width: 36px; height: 36px;
  border: 3px solid var(--border);
  border-top-color: var(--highlight);
  border-radius: 50%;
  animation: spin 0.8s linear infinite;
  margin: 0 auto 16px;
}

@keyframes spin { to { transform: rotate(360deg); } }

.empty-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.empty-icon { font-size: 48px; margin-bottom: 12px; }

@media (max-width: 768px) {
  .table-header, .table-row {
    grid-template-columns: 80px 100px 1fr;
  }
  .col-detail { display: none; }
}
</style>
