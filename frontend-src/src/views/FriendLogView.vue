<template>
  <div class="friend-log-page">
    <div class="page-header">
      <h1>📜 Activity Log</h1>
      <div class="header-actions">
        <select v-model="eventFilter" class="filter-select">
          <option value="">All Events</option>
          <option value="online">Online</option>
          <option value="offline">Offline</option>
          <option value="location_change">Location Change</option>
          <option value="status_change">Status Change</option>
        </select>
        <button class="btn btn-primary" @click="refresh">↻ Refresh</button>
      </div>
    </div>

    <div v-if="loading" class="loading">Loading activity...</div>

    <div v-else-if="events.length === 0" class="empty-state">
      <span class="empty-icon">📜</span>
      <p>No activity recorded yet</p>
    </div>

    <div v-else class="timeline">
      <div v-for="event in events" :key="event.id" class="timeline-item">
        <div class="timeline-dot" :class="event.eventType"></div>
        <div class="timeline-content">
          <div class="event-header">
            <span class="event-user">{{ event.displayName }}</span>
            <span class="event-type-badge" :class="event.eventType">{{ formatType(event.eventType) }}</span>
          </div>
          <div class="event-detail" v-if="event.eventType === 'location_change'">
            Moved to {{ event.newValue || 'unknown' }}
          </div>
          <div class="event-detail" v-else-if="event.eventType === 'status_change'">
            {{ event.newValue || 'Status updated' }}
          </div>
          <div class="event-time">{{ formatTime(event.timestamp) }}</div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch } from 'vue'
import { api } from '../services/api.js'

const events = ref([])
const loading = ref(false)
const eventFilter = ref('')

onMounted(() => refresh())
watch(eventFilter, () => refresh())

async function refresh() {
  loading.value = true
  try {
    const res = await api.getFriendEvents(null, eventFilter.value || null, 0, 200)
    if (res.success && res.data) {
      events.value = res.data
    }
  } finally {
    loading.value = false
  }
}

function formatType(type) {
  const labels = {
    online: 'Online',
    offline: 'Offline',
    location_change: 'Location',
    status_change: 'Status',
  }
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
  return d.toLocaleString()
}
</script>

<style scoped>
.friend-log-page {
  max-width: 800px;
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

.timeline {
  position: relative;
  padding-left: 32px;
}

.timeline::before {
  content: '';
  position: absolute;
  left: 11px;
  top: 0;
  bottom: 0;
  width: 2px;
  background: var(--border);
}

.timeline-item {
  position: relative;
  margin-bottom: 16px;
}

.timeline-dot {
  position: absolute;
  left: -32px;
  top: 4px;
  width: 22px;
  height: 22px;
  border-radius: 50%;
  background: var(--bg-card);
  border: 2px solid var(--border);
}

.timeline-dot.online {
  border-color: var(--green);
  background: var(--green);
}

.timeline-dot.offline {
  border-color: var(--text-muted);
  background: var(--text-muted);
}

.timeline-dot.location_change {
  border-color: var(--blue);
  background: var(--blue);
}

.timeline-dot.status_change {
  border-color: var(--yellow);
  background: var(--yellow);
}

.timeline-content {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 12px 16px;
}

.event-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}

.event-user {
  font-weight: 600;
  color: var(--text-primary);
  font-size: 14px;
}

.event-type-badge {
  font-size: 11px;
  padding: 2px 8px;
  border-radius: 12px;
  font-weight: 500;
}

.event-type-badge.online {
  background: rgba(63, 185, 80, 0.15);
  color: var(--green);
}

.event-type-badge.offline {
  background: rgba(139, 148, 158, 0.15);
  color: var(--text-secondary);
}

.event-type-badge.location_change {
  background: rgba(88, 166, 255, 0.15);
  color: var(--blue);
}

.event-type-badge.status_change {
  background: rgba(210, 153, 34, 0.15);
  color: var(--yellow);
}

.event-detail {
  font-size: 13px;
  color: var(--text-secondary);
  margin-bottom: 4px;
}

.event-time {
  font-size: 12px;
  color: var(--text-muted);
}
</style>
