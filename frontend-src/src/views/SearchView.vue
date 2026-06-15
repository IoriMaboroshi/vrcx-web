<template>
  <div class="search-page">
    <div class="page-header">
      <h1>🔍 Search</h1>
    </div>

    <div class="search-bar">
      <input
        v-model="query"
        type="text"
        class="search-input"
        placeholder="Search users, worlds, or avatars..."
        @keyup.enter="doSearch"
      />
      <div class="search-tabs">
        <button
          v-for="tab in tabs"
          :key="tab.value"
          class="tab-btn"
          :class="{ active: activeTab === tab.value }"
          @click="activeTab = tab.value"
        >
          {{ tab.icon }} {{ tab.label }}
        </button>
      </div>
      <button class="btn btn-primary" @click="doSearch" :disabled="!query.trim() || searching">
        {{ searching ? 'Searching...' : 'Search' }}
      </button>
    </div>

    <div v-if="searching" class="loading">Searching...</div>

    <div v-else-if="hasSearched && results.length === 0" class="empty-state">
      <span class="empty-icon">🔍</span>
      <p>No results found for "{{ query }}"</p>
    </div>

    <div v-else-if="results.length > 0" class="results-grid">
      <div v-for="item in results" :key="item.id" class="result-card">
        <img
          v-if="item.thumbnailImageUrl || item.currentAvatarThumbnailImageUrl || item.imageUrl"
          :src="api.proxyImage(item.thumbnailImageUrl || item.currentAvatarThumbnailImageUrl || item.imageUrl)"
          :alt="item.name || item.displayName"
          class="result-image"
          @error="onImageError"
        />
        <div v-else class="result-image-placeholder">🖼️</div>
        <div class="result-info">
          <div class="result-name">{{ item.name || item.displayName || 'Unknown' }}</div>
          <div class="result-desc">{{ truncate(item.description || item.statusDescription || '', 100) }}</div>
          <div class="result-meta" v-if="item.authorName">by {{ item.authorName }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { api } from '../services/api.js'

const query = ref('')
const activeTab = ref('users')
const searching = ref(false)
const hasSearched = ref(false)
const results = ref([])

const tabs = [
  { value: 'users', label: 'Users', icon: '👤' },
  { value: 'worlds', label: 'Worlds', icon: '🌍' },
  { value: 'avatars', label: 'Avatars', icon: '🎭' },
]

async function doSearch() {
  if (!query.value.trim()) return
  searching.value = true
  hasSearched.value = true
  try {
    let res
    if (activeTab.value === 'users') {
      res = await api.searchUsers(query.value)
    } else if (activeTab.value === 'worlds') {
      res = await api.searchWorlds(query.value)
    } else {
      res = await api.searchAvatars(query.value)
    }
    results.value = res.success ? (res.data || []) : []
  } catch (e) {
    console.error('Search failed:', e)
    results.value = []
  } finally {
    searching.value = false
  }
}

function truncate(str, len) {
  if (!str) return ''
  return str.length > len ? str.slice(0, len) + '...' : str
}

function onImageError(e) {
  e.target.src = 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><rect width="100" height="100" fill="%231c2333"/><text x="50" y="55" text-anchor="middle" fill="%23484f58" font-size="30">?</text></svg>'
}
</script>

<style scoped>
.search-page {
  max-width: 1000px;
  margin: 0 auto;
  padding: 24px;
}

.page-header {
  margin-bottom: 24px;
}

.page-header h1 {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
}

.search-bar {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 24px;
  flex-wrap: wrap;
}

.search-input {
  flex: 1;
  min-width: 200px;
  padding: 10px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 14px;
}

.search-input:focus {
  outline: none;
  border-color: var(--highlight);
}

.search-tabs {
  display: flex;
  gap: 4px;
  background: var(--bg-secondary);
  padding: 4px;
  border-radius: var(--radius);
}

.tab-btn {
  padding: 8px 14px;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  background: none;
  border: none;
  cursor: pointer;
  transition: all var(--transition);
}

.tab-btn:hover {
  color: var(--text-primary);
}

.tab-btn.active {
  background: var(--bg-card);
  color: var(--highlight);
}

.btn {
  padding: 10px 20px;
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

.btn-primary:hover {
  opacity: 0.9;
}

.btn:disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.loading {
  text-align: center;
  padding: 48px;
  color: var(--text-muted);
}

.empty-state {
  text-align: center;
  padding: 48px;
  color: var(--text-muted);
}

.empty-icon {
  font-size: 48px;
  display: block;
  margin-bottom: 16px;
}

.results-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.result-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
  transition: all var(--transition);
  cursor: pointer;
}

.result-card:hover {
  border-color: var(--highlight);
  transform: translateY(-2px);
}

.result-image {
  width: 100%;
  height: 160px;
  object-fit: cover;
  background: var(--bg-secondary);
}

.result-image-placeholder {
  width: 100%;
  height: 160px;
  display: flex;
  align-items: center;
  justify-content: center;
  background: var(--bg-secondary);
  font-size: 40px;
}

.result-info {
  padding: 12px;
}

.result-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 4px;
}

.result-desc {
  font-size: 12px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.result-meta {
  font-size: 11px;
  color: var(--text-muted);
}
</style>
