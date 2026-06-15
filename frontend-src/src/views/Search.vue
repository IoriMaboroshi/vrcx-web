<template>
  <div class="search-page container">
    <h1 class="page-title">{{ t('searchPage.title') }}</h1>

    <!-- Tabs -->
    <div class="search-tabs">
      <button
        v-for="tab in tabs"
        :key="tab.value"
        class="search-tab"
        :class="{ active: activeTab === tab.value }"
        @click="activeTab = tab.value"
      >
        {{ tab.icon }} {{ tab.label }}
      </button>
    </div>

    <!-- Search input -->
    <div class="search-bar">
      <input
        v-model="searchText"
        type="text"
        :placeholder="searchPlaceholder"
        class="search-input"
        @keyup.enter="doSearch"
      />
      <button class="btn-primary search-btn" @click="doSearch" :disabled="!searchText || isSearching">
        {{ isSearching ? t('searchPage.searching') : t('searchPage.search') }}
      </button>
      <button class="btn-secondary" @click="clearSearch">{{ t('searchPage.clear') }}</button>
    </div>

    <!-- Results -->
    <div class="results-section">
      <!-- Loading -->
      <div v-if="isSearching" class="loading-state">
        <div class="spinner"></div>
        <p>{{ t('searchPage.searching') }}</p>
      </div>

      <!-- User results -->
      <div v-else-if="activeTab === 'user' && userResults.length > 0" class="results-list">
        <div
          v-for="user in userResults"
          :key="user.id"
          class="result-card"
          @click="viewUser(user)"
        >
          <div class="result-avatar">
            <img
              :src="user.currentAvatarThumbnailImageUrl"
              :alt="user.displayName"
              @error="onAvatarError"
            />
          </div>
          <div class="result-info">
            <div class="result-name">
              {{ user.displayName }}
              <span class="result-rank" :class="getTrustClass(user)">
                {{ getTrustLevel(user) }}
              </span>
            </div>
            <div class="result-detail" v-if="user.bio">{{ user.bio }}</div>
            <div class="result-meta">
              <span v-if="user.status" class="meta-status" :class="user.status">{{ user.status }}</span>
              <span v-if="user.lastLogin" class="meta-time">{{ t('searchPage.lastLogin', { time: formatTime(user.lastLogin) }) }}</span>
            </div>
          </div>
          <div class="result-actions">
            <router-link :to="`/friend/${user.id}`" class="btn-secondary btn-sm">{{ t('searchPage.viewProfile') }}</router-link>
          </div>
        </div>
      </div>

      <!-- World results -->
      <div v-else-if="activeTab === 'world' && worldResults.length > 0" class="results-grid">
        <div
          v-for="world in worldResults"
          :key="world.id"
          class="world-card"
        >
          <div class="world-image">
            <img
              :src="world.thumbnailImageUrl || world.imageUrl"
              :alt="world.name"
              @error="onWorldError"
            />
            <span class="world-occupants" v-if="world.occupants">
              {{ world.occupants }} {{ t('searchPage.online') }}
            </span>
          </div>
          <div class="world-info">
            <div class="world-name">{{ world.name }}</div>
            <div class="world-author">{{ world.authorName }}</div>
            <div class="world-meta">
              <span v-if="world.favorites" class="world-fav">❤️ {{ world.favorites }}</span>
              <span class="world-cap">👥 {{ world.capacity || '?' }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- No results -->
      <div v-else-if="hasSearched && !isSearching" class="empty-state">
        <div class="empty-icon">🔍</div>
        <p>{{ t('searchPage.noResults') }}</p>
      </div>

      <!-- Initial state -->
      <div v-else class="initial-state">
        <div class="initial-icon">{{ tabs.find(tab => tab.value === activeTab)?.icon || '🔍' }}</div>
        <p>{{ initialMessage }}</p>
      </div>

      <!-- Pagination -->
      <div v-if="showPagination" class="pagination">
        <button class="btn-secondary" :disabled="offset === 0" @click="prevPage">
          {{ t('searchPage.previous') }}
        </button>
        <span class="page-info">{{ t('searchPage.page', { n: currentPage }) }}</span>
        <button class="btn-secondary" :disabled="results.length < pageSize" @click="nextPage">
          {{ t('searchPage.next') }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useRouter } from 'vue-router'
import { api } from '../services/api.js'
import { useI18n } from '../composables/useI18n.js'

const router = useRouter()
const { t } = useI18n()

const activeTab = ref('user')
const searchText = ref('')
const isSearching = ref(false)
const hasSearched = ref(false)
const offset = ref(0)
const pageSize = 10
const results = ref([])

const tabs = computed(() => [
  { value: 'user', label: t('searchPage.users'), icon: '👤' },
  { value: 'world', label: t('searchPage.worlds'), icon: '🌍' },
])

const searchPlaceholder = computed(() => {
  if (activeTab.value === 'user') return t('searchPage.searchUsersPlaceholder')
  if (activeTab.value === 'world') return t('searchPage.searchWorldsPlaceholder')
  return ''
})

const initialMessage = computed(() => {
  if (activeTab.value === 'user') return t('searchPage.searchForUsers')
  if (activeTab.value === 'world') return t('searchPage.searchForWorlds')
  return ''
})

const currentPage = computed(() => Math.floor(offset.value / pageSize) + 1)

const showPagination = computed(() => hasSearched.value && results.value.length >= pageSize)

const userResults = computed(() => activeTab.value === 'user' ? results.value : [])
const worldResults = computed(() => activeTab.value === 'world' ? results.value : [])

watch(activeTab, () => {
  results.value = []
  hasSearched.value = false
  offset.value = 0
})

async function doSearch() {
  if (!searchText.value || isSearching.value) return
  isSearching.value = true
  hasSearched.value = true
  results.value = []

  try {
    let res
    if (activeTab.value === 'user') {
      res = await api.searchUsers(searchText.value, offset.value, pageSize)
    } else {
      res = await api.searchWorlds(searchText.value, offset.value, pageSize)
    }
    if (res.success && res.data) {
      results.value = res.data
    }
  } catch (e) {
    console.error('Search failed:', e)
  } finally {
    isSearching.value = false
  }
}

function clearSearch() {
  searchText.value = ''
  results.value = []
  hasSearched.value = false
  offset.value = 0
}

function nextPage() {
  offset.value += pageSize
  doSearch()
}

function prevPage() {
  offset.value = Math.max(0, offset.value - pageSize)
  doSearch()
}

function viewUser(user) {
  router.push(`/friend/${user.id}`)
}

function getTrustLevel(user) {
  if (user.trustRank) return user.trustRank
  if (user.userRank) return user.userRank
  if (!user.tags) return t('common.user')
  if (user.tags.some((t) => t.includes('system_trust_legend'))) return t('common.legend')
  if (user.tags.some((t) => t.includes('system_trust_veteran'))) return t('common.veteran')
  if (user.tags.some((t) => t.includes('system_trust_trusted'))) return t('common.trusted')
  if (user.tags.some((t) => t.includes('system_trust_known'))) return t('common.known')
  if (user.tags.some((t) => t.includes('system_trust_basic'))) return t('common.basic')
  return t('common.new')
}

function getTrustClass(user) {
  return `trust-${getTrustLevel(user).toLowerCase()}`
}

function formatTime(ts) {
  if (!ts) return t('common.never')
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

function onWorldError(e) {
  e.target.src =
    'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 200 120"><rect width="200" height="120" fill="%23222"/><text x="100" y="65" text-anchor="middle" fill="%23666" font-size="24">🌍</text></svg>'
}
</script>

<style scoped>
.search-page {
  padding: 20px 16px;
}

.page-title {
  font-size: 22px;
  font-weight: 700;
  margin-bottom: 20px;
}

.search-tabs {
  display: flex;
  gap: 4px;
  margin-bottom: 16px;
}

.search-tab {
  padding: 8px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-secondary);
  font-size: 14px;
}
.search-tab.active {
  background: var(--accent);
  border-color: var(--accent-light);
  color: #fff;
}
.search-tab:hover:not(.active) {
  background: var(--bg-card-hover);
}

.search-bar {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
}

.search-input {
  flex: 1;
  padding: 10px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 14px;
}
.search-input:focus {
  border-color: var(--highlight);
  outline: none;
}

.search-btn {
  white-space: nowrap;
}
.btn-sm {
  padding: 4px 12px;
  font-size: 12px;
}

.results-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.result-card {
  display: flex;
  align-items: center;
  gap: 14px;
  padding: 14px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  cursor: pointer;
  transition: background var(--transition);
}
.result-card:hover {
  background: var(--bg-card-hover);
}

.result-avatar img {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--bg-primary);
}

.result-info {
  flex: 1;
  min-width: 0;
}

.result-name {
  font-weight: 600;
  font-size: 15px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.result-rank {
  display: inline-block;
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 10px;
  font-weight: 600;
  text-transform: uppercase;
}
.trust-legend { background: rgba(233,69,96,0.2); color: #ff6b8a; }
.trust-veteran { background: rgba(210,153,34,0.2); color: #f0c040; }
.trust-trusted { background: rgba(63,185,80,0.2); color: var(--green); }
.trust-known { background: rgba(88,166,255,0.2); color: var(--blue); }
.trust-basic { background: rgba(139,148,158,0.2); color: var(--gray); }
.trust-user { background: rgba(139,148,158,0.15); color: var(--text-muted); }

.result-detail {
  font-size: 13px;
  color: var(--text-secondary);
  margin-top: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.result-meta {
  display: flex;
  gap: 12px;
  margin-top: 4px;
}

.meta-status {
  font-size: 12px;
  text-transform: capitalize;
  font-weight: 500;
}
.meta-status.online { color: var(--green); }
.meta-status.active { color: var(--yellow); }
.meta-status.busy { color: var(--red); }
.meta-status.offline { color: var(--gray); }

.meta-time {
  font-size: 12px;
  color: var(--text-muted);
}

.results-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(200px, 1fr));
  gap: 12px;
}

.world-card,
.avatar-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  overflow: hidden;
  transition: background var(--transition);
}
.world-card:hover,
.avatar-card:hover {
  background: var(--bg-card-hover);
}

.world-image,
.avatar-image {
  position: relative;
}
.world-image img,
.avatar-image img {
  width: 100%;
  aspect-ratio: 16 / 10;
  object-fit: cover;
  background: var(--bg-primary);
}

.world-occupants {
  position: absolute;
  bottom: 8px;
  right: 8px;
  padding: 2px 8px;
  background: rgba(0, 0, 0, 0.7);
  border-radius: 12px;
  font-size: 11px;
  color: #fff;
}

.world-info,
.avatar-info {
  padding: 10px 12px;
}

.world-name,
.avatar-name {
  font-weight: 600;
  font-size: 14px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.world-author,
.avatar-author {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 2px;
}

.world-meta {
  display: flex;
  gap: 12px;
  margin-top: 4px;
  font-size: 12px;
  color: var(--text-muted);
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 16px;
  margin-top: 24px;
}

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

.empty-state,
.initial-state {
  text-align: center;
  padding: 60px 20px;
  color: var(--text-secondary);
}

.empty-icon,
.initial-icon {
  font-size: 48px;
  margin-bottom: 12px;
}
</style>
