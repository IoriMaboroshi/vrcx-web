<template>
  <div class="settings-page container">
    <h1 class="page-title">{{ t('settings.title') }}</h1>

    <div class="settings-grid">
      <!-- Language -->
      <div class="settings-section">
        <h2 class="section-title">{{ t('settings.language') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.language') }}</div>
            <div class="setting-desc">{{ t('settings.languageDesc') }}</div>
          </div>
          <div class="setting-control">
            <select v-model="locale" class="setting-select">
              <option value="en">{{ t('settings.languageEnglish') }}</option>
              <option value="zh">{{ t('settings.languageChinese') }}</option>
            </select>
          </div>
        </div>
      </div>

      <!-- Appearance -->
      <div class="settings-section">
        <h2 class="section-title">{{ t('settings.appearance') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.darkMode') }}</div>
            <div class="setting-desc">{{ t('settings.darkModeDesc') }}</div>
          </div>
          <div class="setting-control">
            <label class="toggle">
              <input type="checkbox" v-model="settings.darkMode" disabled />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.compactMode') }}</div>
            <div class="setting-desc">{{ t('settings.compactModeDesc') }}</div>
          </div>
          <div class="setting-control">
            <label class="toggle">
              <input type="checkbox" v-model="settings.compactMode" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
      </div>

      <!-- Notifications -->
      <div class="settings-section">
        <h2 class="section-title">{{ t('settings.notificationsSection') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.desktopNotifications') }}</div>
            <div class="setting-desc">{{ t('settings.desktopNotificationsDesc') }}</div>
          </div>
          <div class="setting-control">
            <label class="toggle">
              <input type="checkbox" v-model="settings.desktopNotifications" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.toastNotifications') }}</div>
            <div class="setting-desc">{{ t('settings.toastNotificationsDesc') }}</div>
          </div>
          <div class="setting-control">
            <label class="toggle">
              <input type="checkbox" v-model="settings.toastNotifications" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.soundAlerts') }}</div>
            <div class="setting-desc">{{ t('settings.soundAlertsDesc') }}</div>
          </div>
          <div class="setting-control">
            <label class="toggle">
              <input type="checkbox" v-model="settings.soundAlerts" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
      </div>

      <!-- Friends List -->
      <div class="settings-section">
        <h2 class="section-title">{{ t('settings.friendsList') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.defaultSort') }}</div>
            <div class="setting-desc">{{ t('settings.defaultSortDesc') }}</div>
          </div>
          <div class="setting-control">
            <select v-model="settings.defaultSort" class="setting-select">
              <option value="status">{{ t('settings.byStatus') }}</option>
              <option value="name">{{ t('settings.byName') }}</option>
              <option value="lastSeen">{{ t('settings.byLastSeen') }}</option>
            </select>
          </div>
        </div>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.showOffline') }}</div>
            <div class="setting-desc">{{ t('settings.showOfflineDesc') }}</div>
          </div>
          <div class="setting-control">
            <label class="toggle">
              <input type="checkbox" v-model="settings.showOffline" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
      </div>

      <!-- Feed -->
      <div class="settings-section">
        <h2 class="section-title">{{ t('settings.feedSection') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.favoritesOnly') }}</div>
            <div class="setting-desc">{{ t('settings.favoritesOnlyDesc') }}</div>
          </div>
          <div class="setting-control">
            <label class="toggle">
              <input type="checkbox" v-model="settings.feedFavoritesOnly" />
              <span class="toggle-slider"></span>
            </label>
          </div>
        </div>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.feedLimit') }}</div>
            <div class="setting-desc">{{ t('settings.feedLimitDesc') }}</div>
          </div>
          <div class="setting-control">
            <select v-model="settings.feedLimit" class="setting-select">
              <option value="100">100</option>
              <option value="500">500</option>
              <option value="1000">1000</option>
            </select>
          </div>
        </div>
      </div>

      <!-- Account -->
      <div class="settings-section">
        <h2 class="section-title">{{ t('settings.account') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.currentUser') }}</div>
            <div class="setting-desc">{{ auth.currentUser?.displayName || t('settings.notLoggedIn') }}</div>
          </div>
        </div>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.logout') }}</div>
            <div class="setting-desc">{{ t('settings.logoutDesc') }}</div>
          </div>
          <div class="setting-control">
            <button class="btn-secondary" @click="doLogout">{{ t('settings.logout') }}</button>
          </div>
        </div>
      </div>

      <!-- About -->
      <div class="settings-section">
        <h2 class="section-title">{{ t('settings.about') }}</h2>
        <div class="setting-item">
          <div class="setting-info">
            <div class="setting-name">{{ t('settings.vrcxWeb') }}</div>
            <div class="setting-desc">{{ t('settings.version') }}</div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { reactive, watch, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth.js'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'

const router = useRouter()
const auth = useAuthStore()
const friendsStore = useFriendsStore()
const { t, locale } = useI18n()

const settings = reactive({
  darkMode: true,
  compactMode: false,
  desktopNotifications: true,
  toastNotifications: true,
  soundAlerts: false,
  defaultSort: 'status',
  showOffline: true,
  feedFavoritesOnly: false,
  feedLimit: '500',
})

// Load settings from localStorage
onMounted(() => {
  try {
    const saved = localStorage.getItem('vrcx_settings')
    if (saved) {
      const parsed = JSON.parse(saved)
      Object.assign(settings, parsed)
    }
  } catch {
    // ignore
  }
})

// Save settings on change
watch(settings, (val) => {
  try {
    localStorage.setItem('vrcx_settings', JSON.stringify(val))
  } catch {
    // ignore
  }
}, { deep: true })

function doLogout() {
  auth.logout()
  friendsStore.disconnectWebSocket()
  router.push('/login')
}
</script>

<style scoped>
.settings-page {
  padding: 20px 16px;
  max-width: 800px;
}

.page-title {
  font-size: 22px;
  font-weight: 700;
  margin-bottom: 20px;
}

.settings-grid {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.settings-section {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  overflow: hidden;
}

.section-title {
  font-size: 14px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--text-secondary);
  padding: 14px 18px;
  border-bottom: 1px solid var(--border);
}

.setting-item {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 18px;
  border-bottom: 1px solid var(--border);
}
.setting-item:last-child {
  border-bottom: none;
}

.setting-info {
  flex: 1;
  min-width: 0;
}

.setting-name {
  font-size: 14px;
  font-weight: 500;
}

.setting-desc {
  font-size: 12px;
  color: var(--text-muted);
  margin-top: 2px;
}

.setting-control {
  flex-shrink: 0;
  margin-left: 16px;
}

.setting-select {
  padding: 6px 10px;
  background: var(--bg-primary);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  color: var(--text-primary);
  font-size: 13px;
}
.setting-select:focus {
  border-color: var(--highlight);
  outline: none;
}

/* Toggle switch */
.toggle {
  position: relative;
  display: inline-block;
  width: 44px;
  height: 24px;
  cursor: pointer;
}
.toggle input {
  opacity: 0;
  width: 0;
  height: 0;
}
.toggle-slider {
  position: absolute;
  inset: 0;
  background: var(--gray);
  border-radius: 24px;
  transition: background var(--transition);
}
.toggle-slider::before {
  content: '';
  position: absolute;
  height: 18px;
  width: 18px;
  left: 3px;
  bottom: 3px;
  background: #fff;
  border-radius: 50%;
  transition: transform var(--transition);
}
.toggle input:checked + .toggle-slider {
  background: var(--highlight);
}
.toggle input:checked + .toggle-slider::before {
  transform: translateX(20px);
}

@media (max-width: 768px) {
  .setting-item {
    flex-direction: column;
    align-items: flex-start;
    gap: 10px;
  }
  .setting-control {
    margin-left: 0;
    align-self: flex-end;
  }
}
</style>
