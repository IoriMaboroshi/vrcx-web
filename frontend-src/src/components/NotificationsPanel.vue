<template>
  <div class="notifications-panel">
    <div class="panel-header">
      <h3>{{ t('navbar.notifications') }}</h3>
      <button class="close-btn" @click="$emit('close')">&times;</button>
    </div>
    <div class="panel-body">
      <div v-if="friendsStore.notifications.length === 0" class="empty-state">
        {{ t('notifications.noNotifications') }}
      </div>
      <div
        v-for="notif in friendsStore.notifications"
        :key="notif.id"
        class="notif-item"
        :class="{ unread: !notif.seen }"
      >
        <div class="notif-icon">{{ typeIcon(notif.type) }}</div>
        <div class="notif-content">
          <div class="notif-message">{{ notif.message || notif.type }}</div>
          <div class="notif-time">{{ formatTime(notif.createdAt) }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { onMounted } from 'vue'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'

const friendsStore = useFriendsStore()
const { t } = useI18n()

defineEmits(['close'])

onMounted(() => {
  friendsStore.fetchNotifications()
})

function typeIcon(type) {
  if (!type) return '🔔'
  const t = type.toLowerCase()
  if (t.includes('friend')) return '👥'
  if (t.includes('invite')) return '📨'
  if (t.includes('request')) return '🤝'
  if (t.includes('message')) return '💬'
  return '🔔'
}

function formatTime(ts) {
  if (!ts) return ''
  const d = new Date(ts)
  const now = new Date()
  const diff = now - d
  if (diff < 60000) return useI18n().t('common.justNow')
  if (diff < 3600000) return useI18n().t('common.minutesAgo', { n: Math.floor(diff / 60000) })
  if (diff < 86400000) return useI18n().t('common.hoursAgo', { n: Math.floor(diff / 3600000) })
  return d.toLocaleDateString()
}
</script>

<style scoped>
.notifications-panel {
  width: 380px;
  max-height: calc(100vh - 56px);
  background: var(--bg-secondary);
  border-left: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  box-shadow: var(--shadow-lg);
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--border);
}
.panel-header h3 {
  font-size: 16px;
  font-weight: 600;
}

.close-btn {
  background: none;
  font-size: 22px;
  color: var(--text-muted);
  padding: 0 4px;
}
.close-btn:hover {
  color: var(--text-primary);
}

.panel-body {
  flex: 1;
  overflow-y: auto;
  padding: 8px;
}

.empty-state {
  text-align: center;
  color: var(--text-muted);
  padding: 40px 20px;
  font-size: 14px;
}

.notif-item {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 12px;
  border-radius: var(--radius);
  transition: background var(--transition);
}
.notif-item:hover {
  background: var(--bg-card);
}
.notif-item.unread {
  background: rgba(15, 52, 96, 0.3);
}

.notif-icon {
  font-size: 20px;
  flex-shrink: 0;
  margin-top: 2px;
}

.notif-content {
  flex: 1;
  min-width: 0;
}

.notif-message {
  font-size: 14px;
  line-height: 1.4;
}

.notif-time {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 2px;
}

@media (max-width: 768px) {
  .notifications-panel {
    width: 100%;
  }
}
</style>
