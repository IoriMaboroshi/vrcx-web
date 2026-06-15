<template>
  <div class="twofa-dialog">
    <div class="dialog-content">
      <div class="dialog-icon">🔐</div>
      <h2>{{ t('login.twoFactorTitle') }}</h2>
      <p class="dialog-desc">{{ t('login.twoFactorDesc') }}</p>

      <form @submit.prevent="submit">
        <div class="method-select" v-if="auth.twoFactorMethods.length > 1">
          <button
            v-for="m in auth.twoFactorMethods"
            :key="m"
            type="button"
            class="method-btn"
            :class="{ active: method === m }"
            @click="method = m"
          >
            {{ m.toUpperCase() }}
          </button>
        </div>

        <input
          ref="codeInput"
          v-model="code"
          type="text"
          :placeholder="t('login.enterCode')"
          autocomplete="one-time-code"
          maxlength="10"
          autofocus
        />

        <div v-if="auth.error" class="error-msg">{{ auth.error }}</div>

        <button type="submit" class="btn-primary submit-btn" :disabled="auth.loading || !code">
          {{ auth.loading ? t('login.verifying') : t('login.verify') }}
        </button>
      </form>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth.js'
import { useFriendsStore } from '../stores/friends.js'
import { useI18n } from '../composables/useI18n.js'

const router = useRouter()
const auth = useAuthStore()
const friendsStore = useFriendsStore()
const { t } = useI18n()

const code = ref('')
const method = ref('totp')
const codeInput = ref(null)

onMounted(() => {
  if (auth.twoFactorMethods.length > 0) {
    method.value = auth.twoFactorMethods[0]
  }
  codeInput.value?.focus()
})

async function submit() {
  if (!code.value || auth.loading) return
  const result = await auth.verify2FA(code.value, method.value)
  if (result.success) {
    friendsStore.connectWebSocket()
    friendsStore.fetchFriends()
    router.push('/')
  }
}
</script>

<style scoped>
.twofa-dialog {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  padding: 20px;
  background: var(--bg-primary);
}

.dialog-content {
  width: 100%;
  max-width: 400px;
  text-align: center;
}

.dialog-icon {
  font-size: 48px;
  margin-bottom: 16px;
}

h2 {
  font-size: 22px;
  margin-bottom: 8px;
}

.dialog-desc {
  color: var(--text-secondary);
  margin-bottom: 24px;
}

.method-select {
  display: flex;
  gap: 8px;
  justify-content: center;
  margin-bottom: 16px;
}

.method-btn {
  padding: 6px 16px;
  background: var(--bg-card);
  border: 1px solid var(--border);
  color: var(--text-secondary);
  font-size: 13px;
  font-weight: 500;
}
.method-btn.active {
  background: var(--accent);
  border-color: var(--highlight);
  color: #fff;
}

input {
  margin-bottom: 12px;
  text-align: center;
  font-size: 18px;
  letter-spacing: 4px;
}

.error-msg {
  color: var(--red);
  font-size: 13px;
  margin-bottom: 12px;
}

.submit-btn {
  width: 100%;
  padding: 12px;
}
</style>
