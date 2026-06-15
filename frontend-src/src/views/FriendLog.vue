<template>
  <div class="friend-log-page container">
    <h1 class="page-title">{{ t('friendLog.title') }}</h1>

    <!-- Toolbar -->
    <div class="toolbar">
      <div class="filter-group">
        <select v-model="typeFilter" class="filter-select" multiple>
          <option value="Friend">{{ t('friendLog.typeFriend') }}</option>
          <option value="Unfriend">{{ t('friendLog.typeUnfriend') }}</option>
          <option value="FriendRequest">{{ t('friendLog.typeFriendRequest') }}</option>
          <option value="CancelFriendRequest">{{ t('friendLog.typeCancelFriendRequest') }}</option>
          <option value="DisplayName">{{ t('friendLog.typeDisplayName') }}</option>
          <option value="TrustLevel">{{ t('friendLog.typeTrustLevel') }}</option>
        </select>
        <input v-model="searchFilter" type="text" :placeholder="t('friendLog.searchPlaceholder')" class="filter-input" />
      </div>
      <div class="toolbar-info">
        {{ t('friendLog.totalEntries', { n: filteredEvents.length }) }}
      </div>
    </div>

    <!-- Loading -->
    <div v-if="friendsStore.eventsLoading" class="loading-state">
      <div class="spinner"></div>
      <p>{{ t('friendLog.loadingLog') }}</p>
    </div>

    <!-- Data Table -->
    <div v-else-if="filteredEvents.length > 0" class="data-table">
      <div class="table-header">
        <span class="col-date">{{ t('friendLog.colDate') }}</span>
        <span class="col-type">{{ t('friendLog.colType') }}</span>
        <span class="col-user">{{ t('friendLog.colUser') }}</span>
        <span class="col-detail">{{ t('friendLog.colDetail') }}</span>
      </div>
      <div
        v-for="evt in paginatedEvents"
        :key="evt.id || evt.timestamp"
        class="table-row"
        @click="showUser(evt.userId)"
      >
        <span class="col-date">
          <span class="date-short">{{ formatDateShort(evt.timestamp) }}</span>
          <span class="date-full" :title="formatDateFull(evt.timestamp)">{{ timeAgo(evt.timestamp) }}</span>
        </span>
        <span class="col-type">
          <span class="type-badge" :class="eventTypeClass(evt.type)">{{ t('friendLog.type_' + evt.type) }}</span>
        </span>
        <span class="col-user">
          <img v-if="getAvatar(evt)" :src="getAvatar(evt)" class="user-avatar" @error="onAvatarError" />
          <span class="user-name">{{ evt.displayName || evt.userId }}</span>
        </span>
        <span class="col-detail">
          <!-- Friend / Unfriend -->
          <template v-if="evt.type === 'Friend' || evt.type === 'Unfriend'">
            <Location v-if="evt.newValue" :location="evt.newValue" :world-name="evt.worldName" />
          </template>
          <!-- DisplayName -->
          <template v-else-if="evt.type === 'DisplayName'">
            <span class="old-val">{{ evt.previousDisplayName }}</span>
            <span class="arrow">→</span>
            <span class="new-val">{{ evt.displayName }}</span>
          </template>
          <!-- TrustLevel -->
          <template v-else-if="evt.type === 'TrustLevel'">
            <span class="old-val">{{ evt.previousTrustLevel }}</span>
            <span class="arrow">→</span>
            <span class="new-val">{{ evt.trustLevel }}</span>
          </template>
          <!-- FriendRequest / CancelFriendRequest -->
          <template v-else-if="evt.type === 'FriendRequest' || evt.type === 'CancelFriendRequest'">
            <span>{{ evt.type === 'FriendRequest' ? '→' : '✕' }}</span>
          </template>
          <!-- Default -->
          <template v-else>
            <span>{{ evt.type }}</span>
          </template>
        </span>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="empty-state">
      <div class="empty-icon">📋</div>
      <p>{{ t('friendLog.noActivity') }}</p>
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

const typeFilter = ref([])
const searchFilter = ref('')
const currentPage = ref(1)
const pageSize = 50

onMounted(() => {
  friendsStore.fetchEvents(null, 500)
})

watch([typeFilter, searchFilter], () => {
  currentPage.value = 1
})

const filteredEvents = computed(() => {
  let list = [...friendsStore.events]

  // Filter by type (multi-select)
  if (typeFilter.value.length > 0) {
    const types = new Set(typeFilter.value.map(t => t.toLowerCase()))
    list = list.filter(e => types.has((e.type || '').toLowerCase()))
  }

  // Search by displayName
  if (searchFilter.value) {
    const q = searchFilter.value.toLowerCase()
    list = list.filter(e => (e.displayName || '').toLowerCase().includes(q))
  }

  // Sort by timestamp descending
  list.sort((a, b) => {
    const ta = a.timestamp ? new Date(a.timestamp).getTime() : 0
    const tb = b.timestamp ? new Date(b.timestamp).getTime() : 0
    return tb - ta
  })

  return list
})

const totalPages = computed(() => Math.ceil(filteredEvents.value.length / pageSize))

const paginatedEvents = computed(() => {
  const start = (currentPage.value - 1) * pageSize
  return filteredEvents.value.slice(start, start + pageSize)
})

function eventTypeClass(type) {
  const map = {
    Friend: 'type-friend',
    Unfriend: 'type-unfriend',
    FriendRequest: 'type-request',
    CancelFriendRequest: 'type-cancel',
    DisplayName: 'type-name',
    TrustLevel: 'type-trust'
  }
  return map[type] || 'type-default'
}

function getAvatar(evt) {
  const friend = friendsStore.friends.find(f => f.id === evt.userId)
  return api.proxyImage(friend?.currentAvatarThumbnailImageUrl || friend?.userIcon || '')
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
  if (diff < 60000) return '刚刚'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}分钟前`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}小时前`
  if (diff < 604800000) return `${Math.floor(diff / 86400000)}天前`
  return formatDateShort(ts)
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
.friend-log-page { padding: 20px 16px; }

.page-title {
  font-size: 20px;
  font-weight: 700;
  color: var(--text-primary);
  margin-bottom: 16px;
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}

.filter-group {
  display: flex;
  gap: 8px;
  flex: 1;
}

.filter-select {
  padding: 8px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  color: var(--text-primary);
  font-size: 13px;
  min-width: 120px;
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
  grid-template-columns: 120px 160px 260px 1fr;
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
  grid-template-columns: 120px 160px 260px 1fr;
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
.type-friend { background: rgba(63, 185, 80, 0.2); color: var(--green); }
.type-unfriend { background: rgba(233, 69, 96, 0.2); color: var(--red); }
.type-request { background: rgba(88, 166, 255, 0.2); color: var(--blue); }
.type-cancel { background: rgba(139, 148, 158, 0.2); color: var(--gray); }
.type-name { background: rgba(210, 153, 34, 0.2); color: var(--yellow); }
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
}

.old-val { color: var(--text-muted); text-decoration: line-through; }
.arrow { margin: 0 6px; color: var(--text-muted); }
.new-val { color: var(--text-primary); font-weight: 500; }

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
