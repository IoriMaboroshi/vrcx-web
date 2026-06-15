<template>
  <div class="notifications-page container">
    <h1 class="page-title">{{ t('notifications.title') }}</h1>

    <!-- Filters -->
    <div class="filters-bar">
      <select v-model="typeFilter" class="filter-select">
        <option value="">{{ t('notifications.allTypes') }}</option>
        <option value="friendRequest">{{ t('notifications.friendRequest') }}</option>
        <option value="invite">{{ t('notifications.invite') }}</option>
        <option value="requestInvite">{{ t('notifications.requestInvite') }}</option>
        <option value="inviteResponse">{{ t('notifications.inviteResponse') }}</option>
        <option value="requestInviteResponse">{{ t('notifications.requestInviteResponse') }}</option>
        <option value="message">{{ t('notifications.message') }}</option>
        <option value="boop">{{ t('notifications.boop') }}</option>
        <option value="event.announcement">{{ t('notifications.announcement') }}</option>
        <option value="groupChange">{{ t('notifications.groupChange') }}</option>
        <option value="group.invite">{{ t('notifications.groupInvite') }}</option>
        <option value="group.joinRequest">{{ t('notifications.groupJoinRequest') }}</option>
        <option value="moderation.warning.group">{{ t('notifications.moderationWarning') }}</option>
        <option value="instance.closed">{{ t('notifications.instanceClosed') }}</option>
      </select>
      <input
        v-model="search"
        type="text"
        :placeholder="t('notifications.searchPlaceholder')"
        class="filter-input"
      />
      <button class="btn-secondary" @click="refreshNotifications" :disabled="friendsStore.notificationsLoading">
        🔄 {{ t('common.refresh') }}
      </button>
    </div>

    <!-- Loading -->
    <div v-if="friendsStore.notificationsLoading && friendsStore.notifications.length === 0" class="loading-state">
      <div class="spinner"></div>
      <p>{{ t('notifications.loadingNotifications') }}</p>
    </div>

    <!-- Notification list -->
    <div v-else-if="filteredNotifications.length > 0" class="notif-list">
      <div
        v-for="notif in filteredNotifications"
        :key="notif.id"
        class="notif-card"
        :class="{ unread: !notif.seen }"
      >
        <div class="notif-icon">{{ typeIcon(notif.type) }}</div>
        <div class="notif-content">
          <div class="notif-type">{{ formatType(notif.type) }}</div>
          <div class="notif-message">{{ notif.message || notif.senderUsername || notif.type }}</div>
          <div class="notif-detail" v-if="notif.details">
            {{ notif.details }}
          </div>
          <div class="notif-time">{{ formatTime(notif.createdAt || notif.created_at) }}</div>
        </div>
        <div class="notif-actions">
          <button
            v-if="notif.type === 'friendRequest'"
            class="btn-primary btn-sm"
            @click="acceptNotification(notif)"
            :title="t('common.accept')"
          >
            {{ t('notifications.accept') }}
          </button>
          <button
            v-if="notif.type === 'invite' || notif.type === 'requestInvite'"
            class="btn-primary btn-sm"
            @click="acceptNotification(notif)"
            :title="t('common.accept')"
          >
            {{ t('notifications.accept') }}
          </button>
          <button
            v-if="notif.type === 'invite' || notif.type === 'requestInvite'"
            class="btn-secondary btn-sm"
            @click="declineNotification(notif)"
            :title="t('common.decline')"
          >
            {{ t('notifications.decline') }}
          </button>
          <button
            class="btn-secondary btn-sm"
            @click="dismissNotification(notif)"
            :title="t('common.clear')"
          >
            {{ t('notifications.dismiss') }}
          </button>
        </div>
      </div>
    </div>

    <!-- Empty state -->
    <div v-else class="empty-state">
      <div class="empty-icon">🔔</div>
      <p>{{ t('notifications.noNotifications') }}</p>
      <button class="btn-secondary" @click="refreshNotifications">{{ t('common.refresh') }}</button>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'

const friendsStore = useFriendsStore()
const { t } = useI18n()
const typeFilter = ref('')
const search = ref('')

onMounted(() => {
  friendsStore.fetchNotifications()
})

const filteredNotifications = computed(() => {
  let list = [...friendsStore.notifications]

  if (typeFilter.value) {
    list = list.filter((n) => n.type === typeFilter.value)
  }

  if (search.value) {
    const q = search.value.toLowerCase()
    list = list.filter(
      (n) =>
        (n.message && n.message.toLowerCase().includes(q)) ||
        (n.senderUsername && n.senderUsername.toLowerCase().includes(q)) ||
        (n.type && n.type.toLowerCase().includes(q))
    )
  }

  // Sort newest first
  list.sort((a, b) => {
    const ta = new Date(a.createdAt || a.created_at).getTime()
    const tb = new Date(b.createdAt || b.created_at).getTime()
    return tb - ta
  })

  return list
})

function typeIcon(type) {
  if (!type) return '🔔'
  const t = type.toLowerCase()
  if (t.includes('friend')) return '👥'
  if (t.includes('invite')) return '📨'
  if (t.includes('request')) return '📩'
  if (t.includes('response')) return '✅'
  if (t.includes('message')) return '💬'
  if (t.includes('boop')) return '👋'
  if (t.includes('announcement')) return '📢'
  if (t.includes('group')) return '👥'
  if (t.includes('moderation')) return '⚠️'
  if (t.includes('instance')) return '🔒'
  return '🔔'
}

function formatType(type) {
  if (!type) return t('notifications.notification')
  return type
    .replace(/\./g, ' ')
    .replace(/([A-Z])/g, ' $1')
    .trim()
}

function formatTime(ts) {
  if (!ts) return ''
  const d = new Date(ts)
  const now = new Date()
  const diff = now - d
  if (diff < 60000) return t('common.justNow')
  if (diff < 3600000) return t('common.minutesAgo', { n: Math.floor(diff / 60000) })
  if (diff < 86400000) return t('common.hoursAgo', { n: Math.floor(diff / 3600000) })
  return d.toLocaleString()
}

function refreshNotifications() {
  friendsStore.fetchNotifications()
}

async function acceptNotification(notif) {
  await friendsStore.acceptNotification(notif.id)
}

async function declineNotification(notif) {
  await friendsStore.declineNotification(notif.id)
}

async function dismissNotification(notif) {
  await friendsStore.deleteNotification(notif.id)
}
</script>

<style scoped>
.notifications-page {
  padding: 20px 16px;
}

.page-title {
  font-size: 22px;
  font-weight: 700;
  margin-bottom: 20px;
}

.filters-bar {
  display: flex;
  gap: 8px;
  margin-bottom: 20px;
  flex-wrap: wrap;
  align-items: center;
}

.filter-select,
.filter-input {
  padding: 8px 12px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 13px;
}
.filter-select:focus,
.filter-input:focus {
  border-color: var(--highlight);
  outline: none;
}

.filter-input {
  flex: 1;
  min-width: 180px;
}

.notif-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.notif-card {
  display: flex;
  align-items: flex-start;
  gap: 14px;
  padding: 14px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  transition: background var(--transition);
}
.notif-card:hover {
  background: var(--bg-card-hover);
}
.notif-card.unread {
  border-left: 3px solid var(--highlight);
  background: rgba(15, 52, 96, 0.2);
}

.notif-icon {
  font-size: 24px;
  flex-shrink: 0;
  margin-top: 2px;
}

.notif-content {
  flex: 1;
  min-width: 0;
}

.notif-type {
  font-size: 12px;
  font-weight: 600;
  color: var(--highlight);
  text-transform: uppercase;
  letter-spacing: 0.5px;
  margin-bottom: 2px;
}

.notif-message {
  font-size: 14px;
  line-height: 1.4;
}

.notif-detail {
  font-size: 13px;
  color: var(--text-secondary);
  margin-top: 4px;
}

.notif-time {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 4px;
}

.notif-actions {
  display: flex;
  gap: 6px;
  flex-shrink: 0;
}

.btn-sm {
  padding: 4px 10px;
  font-size: 12px;
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
  .notif-card {
    flex-direction: column;
    gap: 8px;
  }
  .notif-actions {
    align-self: flex-end;
  }
}
</style>
