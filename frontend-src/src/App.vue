<template>
  <div id="app-root">
    <Navbar v-if="auth.isAuthenticated" />
    <main class="main-content" :class="{ 'no-nav': !auth.isAuthenticated }">
      <router-view v-slot="{ Component }">
        <transition name="fade" mode="out-in">
          <component :is="Component" />
        </transition>
      </router-view>
    </main>

    <!-- Toast notifications -->
    <div class="toast-container">
      <TransitionGroup name="fade">
        <div
          v-for="toast in friendsStore.toasts"
          :key="toast.id"
          class="toast"
        >
          <span class="toast-icon">{{ toast.icon || '🔔' }}</span>
          <span class="toast-message">{{ toast.message }}</span>
          <button class="toast-close" @click="dismissToast(toast.id)">&times;</button>
        </div>
      </TransitionGroup>
    </div>
  </div>
</template>

<script setup>
import { onMounted, onUnmounted } from 'vue'
import Navbar from './components/Navbar.vue'
import { useAuthStore } from './stores/auth.js'
import { useFriendsStore } from './stores/friends.js'

const auth = useAuthStore()
const friendsStore = useFriendsStore()

onMounted(() => {
  if (auth.isAuthenticated) {
    friendsStore.connectWebSocket()
  }
})

onUnmounted(() => {
  friendsStore.disconnectWebSocket()
})

function dismissToast(id) {
  friendsStore.toasts = friendsStore.toasts.filter((t) => t.id !== id)
}
</script>

<style scoped>
.main-content {
  flex: 1;
  padding-top: 60px;
}
.main-content.no-nav {
  padding-top: 0;
}
</style>
