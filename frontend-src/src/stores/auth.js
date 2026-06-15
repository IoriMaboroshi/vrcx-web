import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import { api } from '../services/api.js'

export const useAuthStore = defineStore('auth', () => {
  const token = ref(localStorage.getItem('vrcx_token') || '')
  const currentUser = ref(null)
  const requiresTwoFactor = ref(false)
  const twoFactorMethods = ref([])
  const error = ref('')
  const loading = ref(false)

  const isAuthenticated = computed(() => !!token.value)

  async function login(username, password) {
    loading.value = true
    error.value = ''
    try {
      const res = await api.login(username, password)
      if (res.requiresTwoFactorAuth) {
        requiresTwoFactor.value = true
        twoFactorMethods.value = res.twoFactorAuthMethods || ['totp']
        return { success: true, needs2FA: true }
      }
      if (res.success) {
        // Cookie-based auth — set placeholder token for frontend state
        token.value = 'cookie-auth'
        localStorage.setItem('vrcx_token', 'cookie-auth')
        currentUser.value = res.currentUser || null
        return { success: true }
      }
      error.value = res.error || 'Login failed'
      return { error: error.value }
    } catch (e) {
      error.value = 'Network error: ' + e.message
      return { error: error.value }
    } finally {
      loading.value = false
    }
  }

  async function verify2FA(code, method = 'totp') {
    loading.value = true
    error.value = ''
    try {
      const res = await api.verify2FA(code, method)
      if (res.success) {
        // Cookie-based auth — set placeholder token for frontend state
        token.value = 'cookie-auth'
        localStorage.setItem('vrcx_token', 'cookie-auth')
        currentUser.value = res.currentUser || null
        requiresTwoFactor.value = false
        return { success: true }
      }
      error.value = res.error || '2FA verification failed'
      return { error: error.value }
    } catch (e) {
      error.value = 'Network error: ' + e.message
      return { error: error.value }
    } finally {
      loading.value = false
    }
  }

  function logout() {
    token.value = ''
    currentUser.value = null
    localStorage.removeItem('vrcx_token')
  }

  async function fetchCurrentUser() {
    if (!token.value) return
    try {
      const res = await api.callDotNet('VrchatApi', 'GetCurrentUser')
      if (res.user) {
        currentUser.value = res.user
      }
    } catch {
      // silently ignore
    }
  }

  return {
    token,
    currentUser,
    requiresTwoFactor,
    twoFactorMethods,
    error,
    loading,
    isAuthenticated,
    login,
    verify2FA,
    logout,
    fetchCurrentUser,
  }
})
