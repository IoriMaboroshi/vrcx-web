<template>
  <span class="location-component" v-if="location">
    <span class="loc-icon">{{ locationIcon }}</span>
    <span class="loc-text" :title="rawLocation">
      {{ displayLocation }}
    </span>
    <span v-if="region" class="loc-region" :class="'region-' + region.toLowerCase()">{{ region }}</span>
  </span>
  <span v-else class="location-component offline">
    <span class="loc-icon">⚫</span>
    <span class="loc-text">{{ t('common.offline') }}</span>
  </span>
</template>

<script setup>
import { ref, computed, watch } from 'vue'
import { useI18n } from '../composables/useI18n.js'
import { api } from '../services/api.js'

const { t } = useI18n()

const props = defineProps({
  location: { type: String, default: '' },
  worldName: { type: String, default: '' }
})

const resolvedWorldName = ref('')
const worldNameCache = new Map()

const rawLocation = computed(() => props.location || '')

const locationIcon = computed(() => {
  if (!props.location) return '⚫'
  if (props.location === 'offline') return '⚫'
  if (props.location === 'private') return '🔒'
  if (props.location === 'traveling') return '✈️'
  return '🌍'
})

const displayLocation = computed(() => {
  if (!props.location) return t('common.offline')
  if (props.location === 'offline') return t('common.offline')
  if (props.location === 'private') return t('common.privateWorld')
  if (props.location === 'traveling') return t('common.traveling')

  if (props.worldName) {
    const instanceInfo = extractInstanceInfo(props.location)
    return instanceInfo ? `${props.worldName} · ${instanceInfo}` : props.worldName
  }

  if (resolvedWorldName.value) {
    const instanceInfo = extractInstanceInfo(props.location)
    return instanceInfo ? `${resolvedWorldName.value} · ${instanceInfo}` : resolvedWorldName.value
  }

  const parts = props.location.split(':')
  if (parts[0] && parts[0].startsWith('wrld_')) {
    const worldId = parts[0]
    const shortId = worldId.length > 16 ? worldId.substring(0, 16) + '…' : worldId
    const instanceInfo = extractInstanceInfo(props.location)
    return instanceInfo ? `${shortId} · ${instanceInfo}` : shortId
  }

  return props.location
})

const region = computed(() => {
  if (!props.location) return ''
  const match = props.location.match(/~region\((\w+)\)/)
  return match ? match[1].toUpperCase() : ''
})

function extractWorldId(location) {
  if (!location) return null
  const parts = location.split(':')
  if (parts[0] && parts[0].startsWith('wrld_')) {
    return parts[0]
  }
  return null
}

async function resolveWorldName(worldId) {
  if (worldNameCache.has(worldId)) {
    resolvedWorldName.value = worldNameCache.get(worldId)
    return
  }
  try {
    const res = await api.getWorld(worldId)
    if (res.success && res.data) {
      const name = res.data.name || worldId
      worldNameCache.set(worldId, name)
      resolvedWorldName.value = name
    }
  } catch {
    worldNameCache.set(worldId, worldId)
  }
}

watch(() => props.location, (loc) => {
  resolvedWorldName.value = ''
  const worldId = extractWorldId(loc)
  if (worldId) {
    resolveWorldName(worldId)
  }
}, { immediate: true })

function extractInstanceInfo(location) {
  if (!location) return ''
  const match = location.match(/~(hidden|friends\+\+|friends|group\+\+|group|private)\(/)
  if (match) {
    const typeMap = {
      'hidden': '🔒 Private',
      'friends++': '👥 Friends+',
      'friends': '👥 Friends',
      'group++': '🏢 Group+',
      'group': '🏢 Group',
      'private': '🔐 Invite+',
    }
    return typeMap[match[1]] || match[1]
  }
  const plusMatch = location.match(/~canRequestInvite/)
  if (plusMatch) {
    return '🔐 Invite+'
  }
  if (location.includes(':') && !location.match(/~/)) {
    return '🌍 Public'
  }
  return ''
}
</script>

<style scoped>
.location-component {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 13px;
  color: var(--text-secondary);
}

.loc-icon { font-size: 12px; }

.loc-text {
  max-width: 300px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.loc-region {
  display: inline-block;
  padding: 1px 4px;
  border-radius: 3px;
  font-size: 10px;
  font-weight: 600;
  background: rgba(139, 148, 158, 0.2);
  color: var(--text-muted);
}

.region-us { background: rgba(88, 166, 255, 0.2); color: var(--blue); }
.region-jp { background: rgba(255, 154, 158, 0.2); color: #ff9a9e; }
.region-eu { background: rgba(63, 185, 80, 0.2); color: var(--green); }
</style>
