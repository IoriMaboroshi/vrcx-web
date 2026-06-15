<template>
  <nav class="navbar">
    <div class="nav-inner container">
      <!-- Left: brand + nav links -->
      <div class="nav-left">
        <router-link to="/" class="nav-brand">
          <img class="brand-icon" src="/favicon.png" alt="VRCX" />
          <span class="brand-text">VRCX <span class="brand-web">Web</span></span>
        </router-link>

        <div class="nav-links">
          <router-link to="/" class="nav-link" :class="{ active: $route.name === 'Dashboard' }">
            <span class="nav-link-icon">📊</span>
            <span class="nav-link-text">{{ t('navbar.dashboard') }}</span>
          </router-link>
          <router-link to="/friends" class="nav-link" :class="{ active: $route.name === 'Friends' }">
            <span class="nav-link-icon">👥</span>
            <span class="nav-link-text">{{ t('navbar.friends') }}</span>
          </router-link>
          <router-link to="/log" class="nav-link" :class="{ active: $route.name === 'ActivityLog' }">
            <span class="nav-link-icon">📋</span>
            <span class="nav-link-text">{{ t('navbar.log') }}</span>
          </router-link>
          <router-link to="/search" class="nav-link" :class="{ active: $route.name === 'Search' }">
            <span class="nav-link-icon">🔍</span>
            <span class="nav-link-text">{{ t('navbar.search') }}</span>
          </router-link>
        </div>
      </div>

      <!-- Right: status, lang switch, notifications, user -->
      <div class="nav-right">
        <div class="ws-status" :class="{ connected: friendsStore.wsConnected }">
          <span class="ws-dot"></span>
          <span class="ws-label">{{ friendsStore.wsConnected ? t('navbar.live') : t('navbar.offline') }}</span>
        </div>

        <button class="lang-switch" @click="toggleLocale" :title="locale === 'en' ? 'Switch to 中文' : 'Switch to English'">
          {{ locale === 'en' ? '中文' : 'EN' }}
        </button>

        <router-link to="/notifications" class="nav-icon-btn" :title="t('navbar.notifications')">
          🔔
          <span v-if="unreadCount" class="notif-badge">{{ unreadCount }}</span>
        </router-link>

        <router-link to="/settings" class="nav-icon-btn" :title="t('navbar.settings')">
          ⚙️
        </router-link>

        <div class="nav-user" @click="showUserMenu = !showUserMenu">
          <div class="user-avatar-small" v-if="auth.currentUser">
            <img
              :src="auth.currentUser.userIcon || auth.currentUser.profilePicOverrideThumbnail || auth.currentUser.profilePicOverride || auth.currentUser.currentAvatarThumbnailImageUrl || auth.currentUser.currentAvatarImageUrl"
              :alt="auth.currentUser.displayName"
              @error="onAvatarError"
            />
          </div>
          <span class="user-name">{{ auth.currentUser?.displayName || t('navbar.user') }}</span>
          <span class="dropdown-arrow">▾</span>
        </div>

        <div v-if="showUserMenu" class="dropdown-menu">
          <router-link to="/settings" class="dropdown-item" @click="showUserMenu = false">⚙️ {{ t('navbar.settings') }}</router-link>
          <button class="dropdown-item" @click="doLogout">{{ t('navbar.logout') }}</button>
        </div>
      </div>
    </div>
  </nav>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth.js'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'

const router = useRouter()
const auth = useAuthStore()
const friendsStore = useFriendsStore()
const { t, locale } = useI18n()

const showUserMenu = ref(false)

const unreadCount = computed(() =>
  friendsStore.notifications.filter((n) => !n.seen).length
)

function toggleLocale() {
  locale.value = locale.value === 'en' ? 'zh' : 'en'
}

function doLogout() {
  auth.logout()
  friendsStore.disconnectWebSocket()
  router.push('/login')
}

function onAvatarError(e) {
  e.target.src =
    'data:image/svg+xml,<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100"><circle cx="50" cy="50" r="50" fill="%23333"/><text x="50" y="55" text-anchor="middle" fill="%23fff" font-size="40">👤</text></svg>'
}

function closeMenus(e) {
  if (!e.target.closest('.nav-user') && !e.target.closest('.dropdown-menu')) {
    showUserMenu.value = false
  }
}

onMounted(() => document.addEventListener('click', closeMenus))
onUnmounted(() => document.removeEventListener('click', closeMenus))
</script>

<style scoped>
.navbar {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  height: 56px;
  background: var(--bg-secondary);
  border-bottom: 1px solid var(--border);
  z-index: 1000;
  backdrop-filter: blur(8px);
}

.nav-inner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 100%;
  position: relative;
}

.nav-left {
  display: flex;
  align-items: center;
  gap: 24px;
}

.nav-brand {
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 18px;
  font-weight: 700;
  color: var(--text-primary);
  text-decoration: none;
  flex-shrink: 0;
}
.brand-icon {
  height: 32px;
  width: auto;
}
.brand-web {
  color: var(--highlight);
  font-weight: 400;
}

.nav-links {
  display: flex;
  align-items: center;
  gap: 2px;
}

.nav-link {
  display: flex;
  align-items: center;
  gap: 5px;
  padding: 6px 10px;
  border-radius: var(--radius);
  color: var(--text-secondary);
  font-size: 13px;
  font-weight: 500;
  text-decoration: none;
  transition: background var(--transition), color var(--transition);
  white-space: nowrap;
}
.nav-link:hover {
  background: var(--bg-card);
  color: var(--text-primary);
  text-decoration: none;
}
.nav-link.active {
  background: var(--accent);
  color: #fff;
}

.nav-link-icon {
  font-size: 15px;
}

.nav-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.ws-status {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-muted);
}
.ws-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--gray);
  transition: background var(--transition);
}
.ws-status.connected .ws-dot {
  background: var(--green);
  animation: pulse 2s ease-in-out infinite;
}

.lang-switch {
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: 4px 10px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-secondary);
  cursor: pointer;
  transition: background var(--transition), color var(--transition);
}
.lang-switch:hover {
  background: var(--accent);
  color: #fff;
  border-color: var(--accent);
}

.nav-icon-btn {
  position: relative;
  background: none;
  font-size: 20px;
  padding: 6px;
  border-radius: var(--radius);
  text-decoration: none;
  color: inherit;
}
.nav-icon-btn:hover {
  background: var(--bg-card);
  text-decoration: none;
}
.notif-badge {
  position: absolute;
  top: 0;
  right: 0;
  background: var(--highlight);
  color: #fff;
  font-size: 10px;
  min-width: 16px;
  height: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 8px;
  padding: 0 4px;
}

.nav-user {
  display: flex;
  align-items: center;
  gap: 8px;
  cursor: pointer;
  padding: 4px 8px;
  border-radius: var(--radius);
  transition: background var(--transition);
}
.nav-user:hover {
  background: var(--bg-card);
}

.user-avatar-small {
  width: 28px;
  height: 28px;
  border-radius: 50%;
  overflow: hidden;
  background: var(--bg-card);
}
.user-avatar-small img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.user-name {
  font-size: 14px;
  font-weight: 500;
}

.dropdown-arrow {
  font-size: 12px;
  color: var(--text-muted);
}

.dropdown-menu {
  position: absolute;
  top: 50px;
  right: 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  box-shadow: var(--shadow-lg);
  min-width: 160px;
  z-index: 1001;
  overflow: hidden;
}

.dropdown-item {
  display: block;
  width: 100%;
  text-align: left;
  padding: 10px 16px;
  background: none;
  color: var(--text-primary);
  font-size: 14px;
  border-radius: 0;
  text-decoration: none;
}
.dropdown-item:hover {
  background: var(--bg-card-hover);
  text-decoration: none;
}

@keyframes pulse {
  0%,
  100% {
    opacity: 1;
  }
  50% {
    opacity: 0.5;
  }
}

@media (max-width: 768px) {
  .nav-links {
    display: none;
  }
  .ws-label {
    display: none;
  }
  .user-name {
    display: none;
  }
}
</style>
