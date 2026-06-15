<template>
  <router-link :to="`/friend/${friend.id}`" class="friend-card" :class="friend.state">
    <div class="card-avatar">
      <img
        :src="getAvatarUrl(friend)"
        :alt="friend.displayName"
        @error="onAvatarError"
      />
      <span class="status-dot" :class="friend.state"></span>
    </div>
    <div class="card-info">
      <div class="card-name">{{ friend.displayName }}</div>
      <div class="card-status" v-if="friend.statusDescription">
        {{ friend.statusDescription }}
      </div>
      <div class="card-location" v-if="friend.state !== 'offline' && friend.location">
        <span class="loc-icon">📍</span>
        {{ formatLocation(friend.location) }}
      </div>
      <div class="card-location offline" v-else-if="friend.state === 'offline'">
        <span class="loc-icon">⚫</span>
        {{ t('common.offline') }}
      </div>
    </div>
    <div class="card-meta">
      <span class="platform-icon" :title="friend.platform">{{ platformIcon(friend.platform) }}</span>
    </div>
  </router-link>
</template>

<script setup>
import { useI18n } from '../composables/useI18n.js'
import { api } from '../services/api.js'

const { t } = useI18n()

const props = defineProps({
  friend: { type: Object, required: true },
})

const DEFAULT_AVATAR = 'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="50" fill="%23333"/><text x="50" y="55" text-anchor="middle" fill="%23fff" font-size="40">👤</text></svg>'

/**
 * Get avatar URL with VRCX-style fallback chain:
 * userIcon → profilePicOverrideThumbnail → profilePicOverride → currentAvatarThumbnailImageUrl → currentAvatarImageUrl
 */
function getAvatarUrl(user) {
  const raw = user.userIcon
    || user.profilePicOverrideThumbnail
    || user.profilePicOverride
    || user.currentAvatarThumbnailImageUrl
    || user.currentAvatarImageUrl
  return raw ? api.proxyImage(raw) : DEFAULT_AVATAR
}

function formatLocation(loc) {
  if (!loc) return ''
  if (loc === 'private') return t('common.private')
  const parts = loc.split(':')
  return parts[0] || loc
}

function platformIcon(platform) {
  if (!platform) return ''
  const p = platform.toLowerCase()
  if (p.includes('standalon')) return '🥽'
  if (p.includes('quest')) return '📱'
  if (p.includes('win')) return '🖥️'
  if (p.includes('linux')) return '🐧'
  return '🎮'
}

function onAvatarError(e) {
  e.target.src = DEFAULT_AVATAR
}
</script>

<style scoped>
.friend-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  cursor: pointer;
  transition: background var(--transition), border-color var(--transition), transform var(--transition);
  text-decoration: none;
  color: inherit;
  animation: fadeIn 0.3s ease;
}

.friend-card:hover {
  background: var(--bg-card-hover);
  border-color: var(--accent-light);
  transform: translateY(-1px);
}

.friend-card.offline {
  opacity: 0.6;
}
.friend-card.offline:hover {
  opacity: 0.85;
}

.card-avatar {
  position: relative;
  flex-shrink: 0;
  width: 48px;
  height: 48px;
}

.card-avatar img {
  width: 48px;
  height: 48px;
  border-radius: 50%;
  object-fit: cover;
  background: var(--bg-primary);
}

.status-dot {
  position: absolute;
  bottom: 0;
  right: 0;
  width: 14px;
  height: 14px;
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

.card-info {
  flex: 1;
  min-width: 0;
}

.card-name {
  font-weight: 600;
  font-size: 15px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.card-status {
  font-size: 13px;
  color: var(--text-secondary);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin-top: 1px;
}

.card-location {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 3px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.card-location.offline {
  color: var(--text-muted);
}
.loc-icon {
  font-size: 11px;
}

.card-meta {
  flex-shrink: 0;
}

.platform-icon {
  font-size: 20px;
}
</style>
