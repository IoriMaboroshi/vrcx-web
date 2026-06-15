<template>
  <div class="notifications-page">
    <div class="page-header">
      <h1>🔔 Notifications</h1>
      <div class="header-actions">
        <select v-model="typeFilter" class="filter-select">
          <option value="">All Types</option>
          <option value="friendRequest">Friend Requests</option>
          <option value="invite">Invites</option>
          <option value="requestInvite">Invite Requests</option>
          <option value="halp">HALP</option>
        </select>
        <button class="btn btn-primary" @click="refresh">↻ Refresh</button>
      </div>
    </div>

    <div v-if="loading" class="loading">Loading notifications...</div>

    <div v-else-if="filteredNotifications.length === 0" class="empty-state">
      <span class="empty-icon">🔕</span>
      <p>No notifications found</p>
    </div>

    <div v-else class="notifications-list">
      <div v-for="notif in filteredNotifications" :key="notif.id" class="notification-card" :class="{ unseen: !notif.seen }">
        <div class="notif-icon">{{ getIcon(notif.type) }}</div>
        <div class="notif-content">
          <div class="notif-type">{{ formatType(notif.type) }}</div>
          <div class="notif-message">{{ notif.message || 'No message' }}</div>
          <div class="notif-meta">
            <span class="notif-sender">From: {{ notif.senderUserId }}</span>
            <span class="notif-time">{{ formatTime(notif.createdAt) }}</span>
          </div>
        </div>
        <div class="notif-actions" v-if="notif.type === 'friendRequest'">
          <button class="btn btn-success btn-sm" @click="accept(notif)" :disabled="notif._processing">✓ Accept</button>
          <button class="btn btn-danger btn-sm" @click="decline(notif)" :disabled="notif._processing">✕ Decline</button>
        </div>
        <div class="notif-actions" v-else>
          <button class="btn btn-secondary btn-sm" @click="hide(notif)" :disabled="notif._processing">Hide</button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { api } from '../services/api.js'

const notifications = ref([])
const loading = ref(false)
const typeFilter = ref('')

const filteredNotifications = computed(() => {
  if (!typeFilter.value) return notifications.value
  return notifications.value.filter(n => n.type === typeFilter.value)
})

onMounted(() => refresh())

async function refresh() {
  loading.value = true
  try {
    const res = await api.getNotifications(0, 100)
    if (res.success && res.data) {
      notifications.value = res.data.map(n => ({ ...n, _processing: false }))
    }
  } finally {
    loading.value = false
  }
}

async function accept(notif) {
  notif._processing = true
  try {
    await api.acceptNotification(notif.id)
    notifications.value = notifications.value.filter(n => n.id !== notif.id)
  } finally {
    notif._processing = false
  }
}

async function decline(notif) {
  notif._processing = true
  try {
    await api.declineNotification(notif.id)
    notifications.value = notifications.value.filter(n => n.id !== notif.id)
  } finally {
    notif._processing = false
  }
}

async function hide(notif) {
  notif._processing = true
  try {
    await api.deleteNotification(notif.id)
    notifications.value = notifications.value.filter(n => n.id !== notif.id)
  } finally {
    notif._processing = false
  }
}

function getIcon(type) {
  const icons = { friendRequest: '👋', invite: '📨', requestInvite: '📩', halp: '🆘' }
  return icons[type] || '🔔'
}

function formatType(type) {
  const labels = { friendRequest: 'Friend Request', invite: 'Invite', requestInvite: 'Invite Request', halp: 'HALP' }
  return labels[type] || type
}

function formatTime(dateStr) {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  const now = new Date()
  const diff = now - d
  if (diff < 60000) return 'Just now'
  if (diff < 3600000) return `${Math.floor(diff / 60000)}m ago`
  if (diff < 86400000) return `${Math.floor(diff / 3600000)}h ago`
  return d.toLocaleDateString()
}
</script>

<style scoped>
.notifications-page {
  max-width: 900px;
  margin: 0 auto;
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header h1 {
  font-size: 24px;
  font-weight: 600;
  color: var(--text-primary);
}

.header-actions {
  display: flex;
  gap: 12px;
  align-items: center;
}

.filter-select {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  padding: 8px 12px;
  font-size: 14px;
}

.btn {
  padding: 8px 16px;
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

.btn-success {
  background: var(--green);
  color: #fff;
}
.btn-success:hover {
  opacity: 0.9;
}

.btn-danger {
  background: #da3633;
  color: #fff;
}
.btn-danger:hover {
  opacity: 0.9;
}

.btn-secondary {
  background: var(--bg-card);
  color: var(--text-secondary);
  border: 1px solid var(--border);
}
.btn-secondary:hover {
  background: var(--bg-card-hover);
}

.btn-sm {
  padding: 6px 12px;
  font-size: 12px;
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

.notifications-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.notification-card {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  transition: all var(--transition);
}

.notification-card.unseen {
  border-left: 3px solid var(--highlight);
}

.notif-icon {
  font-size: 24px;
  flex-shrink: 0;
}

.notif-content {
  flex: 1;
  min-width: 0;
}

.notif-type {
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  color: var(--text-muted);
  margin-bottom: 4px;
}

.notif-message {
  font-size: 14px;
  color: var(--text-primary);
  margin-bottom: 8px;
}

.notif-meta {
  display: flex;
  gap: 16px;
  font-size: 12px;
  color: var(--text-muted);
}

.notif-actions {
  display: flex;
  gap: 8px;
  flex-shrink: 0;
}
</style>
