<template>
  <div class="detail-page container">
    <!-- Back button -->
    <router-link to="/" class="back-link">{{ t('friendDetail.backToFriends') }}</router-link>

    <!-- Loading -->
    <div v-if="loading" class="loading-state">
      <div class="spinner"></div>
      <p>{{ t('friendDetail.loadingDetails') }}</p>
    </div>

    <!-- Not found -->
    <div v-else-if="!friend" class="empty-state">
      <div class="empty-icon">😢</div>
      <p>{{ t('friendDetail.friendNotFound') }}</p>
      <button class="btn-secondary" @click="retryFetch">{{ t('common.retry') }}</button>
    </div>

    <!-- Friend detail -->
    <div v-else class="detail-card">
      <!-- Profile Banner / Cover Image -->
      <div class="detail-banner">
        <img
          v-if="bannerUrl"
          :src="bannerUrl"
          :alt="`${friend.displayName} profile banner`"
          class="banner-img"
          :class="{ loaded: bannerLoaded }"
          @load="bannerLoaded = true"
          @error="onBannerError"
        />
        <div v-else class="banner-placeholder"></div>
        <div class="banner-overlay"></div>
      </div>

      <!-- Header with Avatar and Info -->
      <div class="detail-header">
        <div class="detail-avatar">
          <img
            :src="avatarUrl"
            :alt="friend.displayName"
            :class="{ loaded: avatarLoaded }"
            @load="avatarLoaded = true"
            @error="onAvatarError"
          />
          <span class="status-badge" :class="detailStatusClass(friend)">
            {{ friend.status || friend.state }}
          </span>
        </div>
        <div class="detail-name-section">
          <h1 class="detail-name">
            {{ friend.displayName }}
            <span class="rank-badge" :class="getTrustClass(friend)">
              {{ getTrustLevel(friend) }}
            </span>
          </h1>
          <div class="detail-username" v-if="friend.username">@{{ friend.username }}</div>
          <div class="detail-pronouns" v-if="friend.pronouns">🌐 {{ friend.pronouns }}</div>
          <div class="detail-bio" v-if="friend.bio">{{ friend.bio }}</div>
          <div class="detail-bio-links" v-if="friend.bioLinks?.length">
            <a v-for="(link, i) in friend.bioLinks" :key="i" :href="link" target="_blank" class="bio-link">
              {{ link }}
            </a>
          </div>
          <div class="detail-meta">
            <span class="meta-item">🎮 {{ formatPlatform(friend.platform || friend.lastPlatform) }}</span>
            <span class="meta-item" v-if="friend.developerType && friend.developerType !== 'none'">
              🛠️ {{ friend.developerType }}
            </span>
          </div>
        </div>
      </div>

      <!-- Status -->
      <div class="detail-section">
        <h3>{{ t('friendDetail.status') }}</h3>
        <div class="status-detail">
          <div class="status-row">
            <span class="label">{{ t('friendDetail.status') }}:</span>
            <span class="value status-text" :class="friend.status || friend.state">
              <span class="status-dot-inline" :class="friend.status || friend.state"></span>
              {{ friend.status || friend.state }}
            </span>
          </div>
          <div class="status-row" v-if="friend.statusDescription">
            <span class="label">{{ t('friendDetail.statusText') }}</span>
            <span class="value">{{ friend.statusDescription }}</span>
          </div>
          <div class="status-row">
            <span class="label">{{ t('friendDetail.lastLogin') }}</span>
            <span class="value">{{ formatTime(friend.lastLogin || friend.last_login) }}</span>
          </div>
          <div class="status-row" v-if="friend.lastActivity">
            <span class="label">{{ t('friendDetail.lastActivity') }}</span>
            <span class="value">{{ formatTime(friend.lastActivity) }}</span>
          </div>
          <div class="status-row">
            <span class="label">{{ t('friendDetail.userId') }}</span>
            <span class="value mono">{{ friend.id }}</span>
          </div>
          <div class="status-row" v-if="friend.last_mobile">
            <span class="label">📱 Mobile</span>
            <span class="value">{{ formatTime(friend.last_mobile) }}</span>
          </div>
        </div>
      </div>

      <!-- Current Avatar -->
      <div class="detail-section">
        <h3>{{ t('friendDetail.currentAvatar') }}</h3>
        <div class="avatar-detail">
          <div class="avatar-preview">
            <img
              :src="avatarUrl"
              :alt="friend.displayName"
              @error="onAvatarError"
            />
          </div>
          <div class="avatar-info">
            <div class="avatar-id mono" v-if="friend.currentAvatarId">
              {{ friend.currentAvatarId }}
            </div>
            <div class="avatar-urls">
              <div class="url-row" v-if="friend.profilePicOverride">
                <span class="url-label">VRC+ Profile:</span>
                <span class="url-value mono">{{ friend.profilePicOverride }}</span>
              </div>
              <div class="url-row" v-if="friend.currentAvatarThumbnailImageUrl">
                <span class="url-label">Thumbnail:</span>
                <span class="url-value mono">{{ friend.currentAvatarThumbnailImageUrl }}</span>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- Location -->
      <div class="detail-section" v-if="friend.state !== 'offline' && friend.location">
        <h3>{{ t('friendDetail.currentLocation') }}</h3>
        <div class="location-detail">
          <div class="location-icon">🌍</div>
          <div class="location-info">
            <div class="location-name">{{ formatLocation(friend.location) }}</div>
            <div class="location-id mono" v-if="friend.location && friend.location !== 'private'">
              {{ friend.location }}
            </div>
          </div>
          <a
            v-if="friend.location && friend.location !== 'private'"
            :href="`vrchat://launch?ref=world:${friend.location.split(':')[0]}`"
            class="btn-primary launch-btn"
            :title="t('friendDetail.openInVRChat')"
          >
            {{ t('friendDetail.openInVRChat') }}
          </a>
        </div>
      </div>

      <!-- Recent Events -->
      <div class="detail-section">
        <h3>{{ t('friendDetail.recentActivity') }}</h3>
        <div v-if="friendsStore.eventsLoading" class="loading-small">{{ t('friendDetail.loadingEvents') }}</div>
        <div v-else-if="friendsStore.events.length === 0" class="no-events">
          {{ t('friendDetail.noRecentEvents') }}
        </div>
        <div v-else class="events-list">
          <div
            v-for="evt in friendsStore.events.slice(0, 50)"
            :key="evt.id"
            class="event-item"
          >
            <span class="event-icon">{{ eventIcon(evt.eventType) }}</span>
            <div class="event-info">
              <span class="event-text">{{ eventText(evt) }}</span>
              <span class="event-time">{{ formatTime(evt.timestamp) }}</span>
            </div>
          </div>
        </div>
      </div>

      <!-- Tags -->
      <div class="detail-section" v-if="friend.tags?.length">
        <h3>{{ t('friendDetail.tags') }}</h3>
        <div class="tags-list">
          <span v-for="tag in friend.tags" :key="tag" class="tag">{{ formatTag(tag) }}</span>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'
import { useRoute, useRouter } from 'vue-router'
import { api } from '../services/api.js'
const route = useRoute()
const friendsStore = useFriendsStore()
const { t } = useI18n()

const friend = ref(null)
const loading = ref(true)
const bannerLoaded = ref(false)
const avatarLoaded = ref(false)

const DEFAULT_AVATAR = 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="50" fill="%23333"/><text x="50" y="55" text-anchor="middle" fill="%23fff" font-size="40">👤</text></svg>'

/**
 * Profile banner / cover image URL with VRCX-style fallback:
 * profilePicOverride → userIcon → currentAvatarImageUrl
 */
const bannerUrl = computed(() => {
  if (!friend.value) return null
  const raw = friend.value.profilePicOverride
    || friend.value.userIcon
    || friend.value.currentAvatarImageUrl
    || null
  return raw ? api.proxyImage(raw) : null
})

/**
 * Avatar thumbnail URL with VRCX-style fallback:
 * userIcon → profilePicOverrideThumbnail → profilePicOverride → currentAvatarThumbnailImageUrl → currentAvatarImageUrl
 */
const avatarUrl = computed(() => {
  if (!friend.value) return DEFAULT_AVATAR
  const raw = friend.value.userIcon
    || friend.value.profilePicOverrideThumbnail
    || friend.value.profilePicOverride
    || friend.value.currentAvatarThumbnailImageUrl
    || friend.value.currentAvatarImageUrl
  return raw ? api.proxyImage(raw) : DEFAULT_AVATAR
})

async function loadFriend(userId) {
  loading.value = true
  friend.value = null
  bannerLoaded.value = false
  avatarLoaded.value = false
  try {
    friend.value = await friendsStore.fetchFriend(userId)
    if (friend.value) {
      await friendsStore.fetchEvents(userId, 50)
    }
  } finally {
    loading.value = false
  }
}

onMounted(() => {
  loadFriend(route.params.userId)
})

watch(
  () => route.params.userId,
  (newId) => {
    if (newId) loadFriend(newId)
  }
)

function retryFetch() {
  loadFriend(route.params.userId)
}

function detailStatusClass(user) {
  const s = user.status || user.state || 'offline'
  if (s === 'ask me') return 'askme'
  return s
}

function formatLocation(loc) {
  if (!loc) return ''
  if (loc === 'private') return t('friendDetail.currentLocation') === 'Current Location' ? '🔒 Private World' : '🔒 私人世界'
  return loc.split(':')[0]
}

function formatTime(ts) {
  if (!ts) return t('common.never')
  const d = new Date(ts)
  return d.toLocaleString()
}

function formatPlatform(platform) {
  if (!platform) return t('common.unknown')
  const map = {
    'standalonewindows': '🥽 PC (VR)',
    'standalonewindowsquest': '🥽 Quest',
    'android': '📱 Quest',
    'ios': '📱 iOS',
    'web': '🌐 Web',
  }
  return map[platform.toLowerCase()] || platform
}

function formatTag(tag) {
  if (!tag) return ''
  return tag
    .replace(/^system_trust_/, '')
    .replace(/^system_/, '')
    .replace(/_/g, ' ')
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
  if (evt.eventType === 'online') return t('friendDetail.cameOnline')
  if (evt.eventType === 'offline') return t('friendDetail.wentOffline')
  if (evt.eventType === 'location_change') return t('friendDetail.movedTo', { where: evt.newValue || t('common.unknown') })
  if (evt.eventType === 'status_change') return t('friendDetail.statusChanged', { value: evt.newValue || t('common.unknown') })
  if (evt.eventType === 'avatar_change') return t('friendDetail.changedAvatar')
  if (evt.eventType === 'name_change') return t('friendDetail.nameChanged', { value: evt.newValue || t('common.unknown') })
  return evt.eventType
}

function onBannerError(e) {
  e.target.style.display = 'none'
}

function onAvatarError(e) {
  e.target.src = DEFAULT_AVATAR
}
</script>

<style scoped>
.detail-page {
  padding: 20px 16px;
  max-width: 800px;
}

.back-link {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-bottom: 20px;
  font-size: 14px;
  color: var(--text-secondary);
}
.back-link:hover {
  color: var(--text-primary);
  text-decoration: none;
}

.detail-card {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

/* Profile Banner / Cover Image */
.detail-banner {
  position: relative;
  width: 100%;
  height: 200px;
  overflow: hidden;
  background: linear-gradient(135deg, var(--accent) 0%, var(--bg-card) 100%);
}

.banner-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  opacity: 0;
  transition: opacity 0.4s ease;
}
.banner-img.loaded {
  opacity: 1;
}

.banner-placeholder {
  width: 100%;
  height: 100%;
  background: linear-gradient(135deg, var(--accent) 0%, var(--bg-card) 100%);
}

.banner-overlay {
  position: absolute;
  bottom: 0;
  left: 0;
  right: 0;
  height: 60px;
  background: linear-gradient(transparent, var(--bg-card));
  pointer-events: none;
}

/* Header with Avatar and Info */
.detail-header {
  display: flex;
  gap: 24px;
  padding: 0 24px 24px;
  border-bottom: 1px solid var(--border);
  margin-top: -40px;
  position: relative;
}

.detail-avatar {
  position: relative;
  flex-shrink: 0;
}

.detail-avatar img {
  width: 120px;
  height: 120px;
  border-radius: var(--radius-lg);
  object-fit: cover;
  border: 4px solid var(--bg-card);
  background: var(--bg-primary);
  opacity: 0;
  transition: opacity 0.4s ease;
}
.detail-avatar img.loaded {
  opacity: 1;
}

.status-badge {
  position: absolute;
  bottom: -4px;
  right: -4px;
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 11px;
  font-weight: 600;
  text-transform: capitalize;
  border: 2px solid var(--bg-card);
}
.status-badge.online {
  background: var(--green);
  color: #fff;
}
.status-badge.active {
  background: var(--green);
  color: #fff;
}
.status-badge.busy {
  background: var(--red);
  color: #fff;
}
.status-badge.askme {
  background: rgba(88, 166, 255, 0.2);
  color: var(--blue);
}
.status-badge.offline {
  background: var(--gray);
  color: #fff;
}

.detail-name-section {
  flex: 1;
  min-width: 0;
}

.detail-name {
  font-size: 24px;
  font-weight: 700;
  margin-bottom: 4px;
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}

.detail-username {
  font-size: 14px;
  color: var(--text-muted);
  margin-bottom: 2px;
}

.detail-pronouns {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.rank-badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
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

.detail-bio {
  font-size: 14px;
  color: var(--text-secondary);
  margin-bottom: 8px;
  line-height: 1.5;
}

.detail-bio-links {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  margin-bottom: 8px;
}

.bio-link {
  font-size: 12px;
  padding: 2px 8px;
  background: var(--bg-primary);
  border-radius: var(--radius);
}

.detail-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 16px;
  margin-top: 8px;
}

.meta-item {
  font-size: 13px;
  color: var(--text-muted);
}

.detail-section {
  padding: 20px 24px;
  border-bottom: 1px solid var(--border);
}
.detail-section:last-child {
  border-bottom: none;
}

.detail-section h3 {
  font-size: 14px;
  font-weight: 600;
  color: var(--text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 12px;
}

.status-detail {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.status-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.status-row .label {
  font-size: 13px;
  color: var(--text-muted);
  min-width: 100px;
}

.status-row .value {
  font-size: 14px;
}

.mono {
  font-family: monospace;
  font-size: 12px;
}

.status-text {
  display: flex;
  align-items: center;
  gap: 6px;
  text-transform: capitalize;
}

.status-dot-inline {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  display: inline-block;
}
.status-dot-inline.online {
  background: var(--green);
}
.status-dot-inline.active {
  background: var(--green);
}
.status-dot-inline.busy {
  background: var(--red);
}
.status-dot-inline.offline {
  background: var(--gray);
}

.location-detail {
  display: flex;
  align-items: center;
  gap: 12px;
}

.location-icon {
  font-size: 24px;
}

.location-info {
  flex: 1;
}

.location-name {
  font-size: 15px;
  font-weight: 500;
}

.location-id {
  font-size: 12px;
  color: var(--text-muted);
}

.launch-btn {
  white-space: nowrap;
}

.loading-small {
  text-align: center;
  padding: 20px;
  color: var(--text-secondary);
}

.no-events {
  text-align: center;
  padding: 20px;
  color: var(--text-muted);
}

.events-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.event-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  border-radius: var(--radius);
  transition: background var(--transition);
}
.event-item:hover {
  background: var(--bg-card-hover);
}

.event-icon {
  font-size: 16px;
  flex-shrink: 0;
}

.event-info {
  flex: 1;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.event-text {
  font-size: 13px;
}

.event-time {
  font-size: 12px;
  color: var(--text-muted);
}

.tags-list {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

.tag {
  font-size: 11px;
  padding: 3px 8px;
  background: var(--bg-primary);
  border-radius: var(--radius);
  color: var(--text-secondary);
}

.avatar-detail {
  display: flex;
  gap: 16px;
}

.avatar-preview img {
  width: 120px;
  height: 120px;
  border-radius: var(--radius-lg);
  object-fit: cover;
  background: var(--bg-primary);
}

.avatar-info {
  flex: 1;
  min-width: 0;
}

.avatar-id {
  margin-bottom: 8px;
}

.avatar-urls {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.url-row {
  display: flex;
  gap: 8px;
  align-items: baseline;
}

.url-label {
  font-size: 12px;
  color: var(--text-muted);
  white-space: nowrap;
}

.url-value {
  font-size: 11px;
  word-break: break-all;
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
</style>
