<template>
  <div class="feed-page container">
    <h1 class="page-title">{{ t('feed.title') }}</h1>

    <!-- Filter tabs (VRCX style) -->
    <div class="filter-tabs">
      <button
        v-for="tab in feedTabs"
        :key="tab.key"
        class="tab-btn"
        :class="{ active: activeTab === tab.key }"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
        <span class="tab-count">{{ getTabCount(tab.key) }}</span>
      </button>
    </div>

    <!-- Search -->
    <div class="search-bar">
      <input v-model="search" type="text" :placeholder="t('feed.searchPlaceholder')" class="search-input" />
    </div>

    <!-- Loading -->
    <div v-if="friendsStore.eventsLoading" class="loading-state">
      <div class="spinner"></div>
      <p>{{ t('feed.loadingFeed') }}</p>
    </div>

    <!-- Feed list -->
    <div v-else-if="filteredFeed.length > 0" class="feed-list">
      <div
        v-for="(group, dateKey) in groupedFeed"
        :key="dateKey"
        class="feed-group"
      >
        <div class="date-header">
          <span class="date-label">{{ dateKey }}</span>
          <span class="date-count">{{ group.length }} {{ t('feed.events') }}</span>
        </div>
        <div
          v-for="evt in group"
          :key="evt.id || evt.timestamp"
          class="feed-item"
          @click="showUser(evt.userId)"
        >
          <div class="feed-avatar">
            <img v-if="getAvatar(evt)" :src="getAvatar(evt)" class="avatar-img" @error="onAvatarError" />
            <span v-else class="avatar-placeholder">👤</span>
            <span class="event-dot" :class="eventDotClass(evt)"></span>
          </div>
          <div class="feed-content">
            <div class="feed-header">
              <span class="feed-user">{{ evt.displayName || evt.userId }}</span>
              <span class="feed-action">{{ formatAction(evt) }}</span>
              <span class="feed-time">{{ timeAgo(evt.timestamp) }}</span>
            </div>
            <div class="feed-detail" v-if="getEventDetail(evt)">
              <!-- GPS: show location change -->
              <template v-if="evt.type === 'GPS' || evt.eventType === 'location_change'">
                <div class="location-flow" v-if="evt.oldValue">
                  <Location :location="evt.oldValue" :world-name="evt.worldName" />
                  <span class="flow-arrow">↓</span>
                </div>
                <Location :location="evt.newValue" :world-name="evt.worldName" />
              </template>
              <!-- Online/Offline: show location -->
              <template v-else-if="evt.type === 'Online' || evt.type === 'Offline' || evt.eventType === 'online' || evt.eventType === 'offline'">
                <Location v-if="evt.newValue" :location="evt.newValue" :world-name="evt.worldName" />
              </template>
              <!-- Avatar: show images -->
              <template v-else-if="evt.type === 'Avatar' || evt.eventType === 'avatar_change'">
                <div class="avatar-change">
                  <img v-if="evt.previousCurrentAvatarThumbnailImageUrl || evt.oldValue" :src="api.proxyImage(evt.previousCurrentAvatarThumbnailImageUrl || evt.oldValue)" class="change-img" @error="onAvatarError" />
                  <span class="flow-arrow">→</span>
                  <img v-if="evt.currentAvatarThumbnailImageUrl || evt.newValue" :src="api.proxyImage(evt.currentAvatarThumbnailImageUrl || evt.newValue)" class="change-img" @error="onAvatarError" />
                </div>
              </template>
              <!-- Status -->
              <template v-else-if="evt.type === 'Status' || evt.eventType === 'status_change'">
                <span class="old-val">{{ evt.previousStatus || evt.oldValue }}</span>
                <span class="flow-arrow">→</span>
                <span class="new-val" :class="'status-' + (evt.status || evt.newValue)">{{ evt.status || evt.newValue }}</span>
              </template>
              <!-- Bio -->
              <template v-else-if="evt.type === 'Bio' || evt.eventType === 'bio_change'">
                <div class="bio-change">
                  <div v-if="evt.previousBio || evt.oldValue" class="old-bio">{{ evt.previousBio || evt.oldValue }}</div>
                  <span class="flow-arrow">↓</span>
                  <div v-if="evt.bio || evt.newValue" class="new-bio">{{ evt.bio || evt.newValue }}</div>
                </div>
              </template>
              <!-- DisplayName -->
              <template v-else-if="evt.type === 'DisplayName' || evt.eventType === 'name_change'">
                <span class="old-val">{{ evt.previousDisplayName || evt.oldValue }}</span>
                <span class="flow-arrow">→</span>
                <span class="new-val">{{ evt.displayName || evt.newValue }}</span>
              </template>
              <!-- Other -->
              <template v-else>
                <span>{{ evt.type || evt.eventType }}</span>
              </template>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="empty-state">
      <div class="empty-icon">📡</div>
      <p>{{ t('feed.noFeed') }}</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'
import { useRouter } from 'vue-router'
import { api } from '../services/api.js'

const friendsStore = useFriendsStore()
const { t } = useI18n()
const router = useRouter()

const activeTab = ref('all')
const search = ref('')

const feedTabs = computed(() => [
  { key: 'all', icon: '📡', label: t('feed.tabAll') },
  { key: 'GPS', icon: '🌍', label: t('feed.tabGPS') },
  { key: 'Online', icon: '🟢', label: t('feed.tabOnline') },
  { key: 'Offline', icon: '⚫', label: t('feed.tabOffline') },
  { key: 'Avatar', icon: '🧑', label: t('feed.tabAvatar') },
  { key: 'Status', icon: '💬', label: t('feed.tabStatus') },
  { key: 'Bio', icon: '📝', label: t('feed.tabBio') },
])

onMounted(() => {
  friendsStore.fetchEvents(null, 500)
})

function getTabCount(key) {
  if (key === 'all') return friendsStore.events.length
  return friendsStore.events.filter(e => normalizeType(e) === key).length
}

function normalizeType(evt) {
  const t = evt.type || evt.eventType || ''
  if (t === 'online' || t === 'Offline') return 'Offline'
  if (t === 'offline' || t === 'Online') return 'Online'
  if (t === 'location_change') return 'GPS'
  if (t === 'avatar_change') return 'Avatar'
  if (t === 'status_change') return 'Status'
  if (t === 'bio_change') return 'Bio'
  if (t === 'name_change') return 'DisplayName'
  return t
}

const filteredFeed = computed(() => {
  let list = [...friendsStore.events]

  // Filter by tab
  if (activeTab.value !== 'all') {
    list = list.filter(e => normalizeType(e) === activeTab.value)
  }

  // Search
  if (search.value) {
    const q = search.value.toLowerCase()
    list = list.filter(e => (e.displayName || '').toLowerCase().includes(q))
  }

  // Sort by time descending
  list.sort((a, b) => {
    const ta = a.timestamp ? new Date(a.timestamp).getTime() : 0
    const tb = b.timestamp ? new Date(b.timestamp).getTime() : 0
    return tb - ta
  })

  return list
})

const groupedFeed = computed(() => {
  const groups = {}
  for (const evt of filteredFeed.value) {
    const dateKey = getDateKey(evt.timestamp)
    if (!groups[dateKey]) groups[dateKey] = []
    groups[dateKey].push(evt)
  }
  return groups
})

function getDateKey(ts) {
  if (!ts) return '未知日期'
  const d = new Date(ts)
  const now = new Date()
  const today = new Date(now.getFullYear(), now.getMonth(), now.getDate())
  const yesterday = new Date(today.getTime() - 86400000)
  const evtDate = new Date(d.getFullYear(), d.getMonth(), d.getDate())

  if (evtDate.getTime() === today.getTime()) return '今天'
  if (evtDate.getTime() === yesterday.getTime()) return '昨天'
  return d.toLocaleDateString('zh-CN', { month: 'long', day: 'numeric', timeZone: 'Asia/Shanghai' })
}

function eventDotClass(evt) {
  const t = normalizeType(evt)
  const map = { Online: 'dot-online', Offline: 'dot-offline', GPS: 'dot-gps', Avatar: 'dot-avatar', Status: 'dot-status', Bio: 'dot-bio', DisplayName: 'dot-name', Friend: 'dot-friend', Unfriend: 'dot-unfriend' }
  return map[t] || 'dot-default'
}

function formatAction(evt) {
  const t = normalizeType(evt)
  const map = {
    Online: '上线了',
    Offline: '下线了',
    GPS: '移动到了',
    Avatar: '更换了头像',
    Status: '更改了状态',
    Bio: '更新了简介',
    DisplayName: '更改了昵称',
    Friend: '成为了好友',
    Unfriend: '解除了好友关系',
    FriendRequest: '发送了好友请求',
    CancelFriendRequest: '取消了好友请求',
  }
  return map[t] || t
}

function getEventDetail(evt) {
  const t = normalizeType(evt)
  if (['GPS', 'Online', 'Offline'].includes(t)) return evt.newValue || evt.oldValue
  if (t === 'Avatar') return evt.currentAvatarThumbnailImageUrl || evt.previousCurrentAvatarThumbnailImageUrl || evt.newValue || evt.oldValue
  if (t === 'Status') return evt.status || evt.newValue || evt.previousStatus || evt.oldValue
  if (t === 'Bio') return evt.bio || evt.newValue || evt.previousBio || evt.oldValue
  if (t === 'DisplayName') return evt.displayName || evt.newValue || evt.previousDisplayName || evt.oldValue
  return true
}

function getAvatar(evt) {
  const friend = friendsStore.friends.find(f => f.id === evt.userId)
  return api.proxyImage(friend?.currentAvatarThumbnailImageUrl || friend?.userIcon || '')
}

function timeAgo(ts) {
  if (!ts) return ''
  const diff = Date.now() - new Date(ts).getTime()
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  if (diff < 604800000) return `${Math.floor(diff / 86400000)}天前`
  return new Date(ts).toLocaleDateString('zh-CN', { timeZone: 'Asia/Shanghai' })
}

function showUser(userId) {
  if (userId) router.push(`/friend/${userId}`)
}

function onAvatarError(e) {
  e.target.style.display = 'none'
}
</script>

<style scoped>
.feed-page { padding: 20px 16px; }

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

.search-bar {
  margin-bottom: 16px;
}

.search-input {
  width: 100%;
  padding: 10px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  color: var(--text-primary);
  font-size: 14px;
}
.search-input:focus { border-color: var(--highlight); }

.feed-group { margin-bottom: 24px; }

.date-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 0;
  border-bottom: 1px solid var(--border);
  margin-bottom: 8px;
}
.date-label { font-weight: 600; font-size: 14px; color: var(--text-primary); }
.date-count { font-size: 12px; color: var(--text-secondary); }

.feed-item {
  display: flex;
  gap: 12px;
  padding: 12px;
  border-radius: var(--radius-lg);
  cursor: pointer;
  transition: background 0.15s;
}
.feed-item:hover { background: var(--bg-card-hover); }

.feed-avatar {
  position: relative;
  flex-shrink: 0;
}
.avatar-img {
  width: 40px;
  height: 40px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--bg-primary);
}
.avatar-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  border-radius: 50%;
  background: var(--bg-secondary);
  font-size: 20px;
}

.event-dot {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  border: 2px solid var(--bg-card);
}
.dot-online { background: var(--green); }
.dot-offline { background: var(--gray); }
.dot-gps { background: var(--blue); }
.dot-avatar { background: var(--yellow); }
.dot-status { background: #bc8cff; }
.dot-bio { background: #ff9a9e; }
.dot-name { background: #f0c040; }
.dot-friend { background: var(--green); }
.dot-unfriend { background: var(--red); }
.dot-default { background: var(--gray); }

.feed-content { flex: 1; min-width: 0; }

.feed-header {
  display: flex;
  align-items: baseline;
  gap: 6px;
  flex-wrap: wrap;
}
.feed-user { font-weight: 600; font-size: 14px; color: var(--text-primary); }
.feed-action { font-size: 13px; color: var(--text-secondary); }
.feed-time { font-size: 12px; color: var(--text-muted); margin-left: auto; }

.feed-detail {
  margin-top: 6px;
  padding: 8px 12px;
  background: var(--bg-secondary);
  border-radius: var(--radius-lg);
  font-size: 13px;
  color: var(--text-secondary);
}

.location-flow { margin-bottom: 4px; }
.flow-arrow { margin: 0 6px; color: var(--text-muted); }

.avatar-change {
  display: flex;
  align-items: center;
  gap: 8px;
}
.change-img {
  width: 80px;
  height: 60px;
  border-radius: 6px;
  object-fit: cover;
  background: var(--bg-primary);
}

.old-val { color: var(--text-muted); text-decoration: line-through; }
.new-val { color: var(--text-primary); font-weight: 500; }
.status-online { color: var(--green); }
.status-active { color: var(--yellow); }
.status-busy { color: var(--red); }
.status-offline { color: var(--gray); }
.status-ask me { color: var(--blue); }

.bio-change .old-bio { color: var(--text-muted); margin-bottom: 4px; }
.bio-change .new-bio { color: var(--text-primary); }

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
</style>
