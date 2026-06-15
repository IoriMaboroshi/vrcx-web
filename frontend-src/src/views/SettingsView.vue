<template>
  <div class="settings-page">
    <div class="page-header">
      <h1>⚙️ Settings</h1>
    </div>

    <div class="settings-section">
      <h2>General</h2>
      <div class="setting-row">
        <div class="setting-info">
          <div class="setting-label">Auto-refresh friends</div>
          <div class="setting-desc">Automatically refresh friend list periodically</div>
        </div>
        <label class="toggle">
          <input type="checkbox" v-model="settings.autoRefresh" @change="save" />
          <span class="toggle-slider"></span>
        </label>
      </div>
      <div class="setting-row">
        <div class="setting-info">
          <div class="setting-label">Show offline friends</div>
          <div class="setting-desc">Display offline friends in the friend list</div>
        </div>
        <label class="toggle">
          <input type="checkbox" v-model="settings.showOffline" @change="save" />
          <span class="toggle-slider"></span>
        </label>
      </div>
      <div class="setting-row">
        <div class="setting-info">
          <div class="setting-label">Desktop notifications</div>
          <div class="setting-desc">Show browser notifications for friend events</div>
        </div>
        <label class="toggle">
          <input type="checkbox" v-model="settings.desktopNotifications" @change="save" />
          <span class="toggle-slider"></span>
        </label>
      </div>
    </div>

    <div class="settings-section">
      <h2>About</h2>
      <div class="about-info">
        <p><strong>VRCX Web</strong> v1.0.0</p>
        <p>A web-based companion for VRChat</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { api } from '../services/api.js'

const settings = ref({
  autoRefresh: true,
  showOffline: true,
  desktopNotifications: false,
})

onMounted(async () => {
  try {
    const res = await api.getSettings()
    if (res.success && res.data) {
      settings.value = { ...settings.value, ...res.data }
    }
  } catch (e) {
    console.error('Failed to load settings:', e)
  }
})

async function save() {
  try {
    await api.updateSettings(settings.value)
  } catch (e) {
    console.error('Failed to save settings:', e)
  }
}
</script>

<style scoped>
.settings-page {
  max-width: 700px;
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

.settings-section {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 20px;
  margin-bottom: 16px;
}

.settings-section h2 {
  font-size: 16px;
  font-weight: 600;
  color: var(--text-primary);
  margin-bottom: 16px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--border);
}

.setting-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 12px 0;
  border-bottom: 1px solid var(--border);
}

.setting-row:last-child {
  border-bottom: none;
}

.setting-label {
  font-size: 14px;
  font-weight: 500;
  color: var(--text-primary);
}

.setting-desc {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 2px;
}

.toggle {
  position: relative;
  display: inline-block;
  width: 44px;
  height: 24px;
  flex-shrink: 0;
}

.toggle input {
  opacity: 0;
  width: 0;
  height: 0;
}

.toggle-slider {
  position: absolute;
  cursor: pointer;
  inset: 0;
  background: var(--bg-secondary);
  border: 1px solid var(--border);
  border-radius: 12px;
  transition: all var(--transition);
}

.toggle-slider::before {
  content: '';
  position: absolute;
  height: 18px;
  width: 18px;
  left: 2px;
  bottom: 2px;
  background: var(--text-secondary);
  border-radius: 50%;
  transition: all var(--transition);
}

.toggle input:checked + .toggle-slider {
  background: var(--highlight);
  border-color: var(--highlight);
}

.toggle input:checked + .toggle-slider::before {
  transform: translateX(20px);
  background: #fff;
}

.about-info {
  color: var(--text-secondary);
  font-size: 14px;
}

.about-info p {
  margin-bottom: 4px;
}
</style>
