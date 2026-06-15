<template>
  <div class="login-page">
    <div class="login-card">
      <div class="login-header">
        <img class="login-logo" src="/favicon.png" alt="VRCX" />
        <h1>VRCX <span class="accent">Web</span></h1>
        <p class="login-subtitle">{{ t('login.title') }}</p>
      </div>

      <TwoFactorDialog v-if="auth.requiresTwoFactor" />

      <form v-else @submit.prevent="doLogin" class="login-form">
        <div class="field">
          <label for="username">{{ t('login.username') }}</label>
          <input
            id="username"
            v-model="username"
            type="text"
            :placeholder="t('login.usernamePlaceholder')"
            autocomplete="username"
            autofocus
          />
        </div>

        <div class="field">
          <label for="password">{{ t('login.password') }}</label>
          <input
            id="password"
            v-model="password"
            type="password"
            :placeholder="t('login.passwordPlaceholder')"
            autocomplete="current-password"
          />
        </div>

        <div v-if="auth.error" class="error-msg">{{ auth.error }}</div>

        <button
          type="submit"
          class="btn-primary login-btn"
          :disabled="auth.loading || !username || !password"
        >
          {{ auth.loading ? t('login.loggingIn') : t('login.signIn') }}
        </button>
      </form>

      <div class="login-footer">
        <p>{{ t('login.footer') }}</p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth.js'
import { useFriendsStore } from '../stores/friends.js'
import TwoFactorDialog from '../components/TwoFactorDialog.vue'
import { useI18n } from '../composables/useI18n.js'

const router = useRouter()
const auth = useAuthStore()
const friendsStore = useFriendsStore()
const { t } = useI18n()

const username = ref('')
const password = ref('')

onMounted(() => {
  if (auth.isAuthenticated) {
    router.push('/')
  }
})

async function doLogin() {
  const result = await auth.login(username.value, password.value)
  if (result.needs2FA) {
    // 2FA required — TwoFactorDialog will render via auth.requiresTwoFactor
    return
  }
  if (result.success) {
    friendsStore.connectWebSocket()
    friendsStore.fetchFriends()
    router.push('/')
  }
}
</script>

<style scoped>
.login-page {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 20px;
  background: var(--bg-primary);
}

.login-card {
  width: 100%;
  max-width: 400px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  border-radius: var(--radius-lg);
  padding: 40px 32px;
  box-shadow: var(--shadow-lg);
}

.login-header {
  text-align: center;
  margin-bottom: 32px;
}

.login-logo {
  height: 48px;
  width: auto;
  margin-bottom: 12px;
}

.login-header h1 {
  font-size: 28px;
  font-weight: 700;
}
.accent {
  color: var(--highlight);
}

.login-subtitle {
  color: var(--text-secondary);
  font-size: 14px;
  margin-top: 4px;
}

.login-form {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.field label {
  display: block;
  font-size: 13px;
  font-weight: 500;
  color: var(--text-secondary);
  margin-bottom: 6px;
}

.error-msg {
  color: var(--red);
  font-size: 13px;
  text-align: center;
  padding: 8px;
  background: rgba(233, 69, 96, 0.1);
  border-radius: var(--radius);
}

.login-btn {
  width: 100%;
  padding: 12px;
  font-size: 15px;
  margin-top: 4px;
}

.login-footer {
  text-align: center;
  margin-top: 24px;
  font-size: 12px;
  color: var(--text-muted);
}
</style>
