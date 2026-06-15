import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

export const useFavoritesStore = defineStore('favorites', () => {
  const favoriteFriends = ref([])
  const favoriteGroups = ref([])
  const loading = ref(false)

  const favoriteFriendIds = computed(() => new Set(favoriteFriends.value.map(f => f.userId)))

  function loadFavorites() {
    try {
      const saved = localStorage.getItem('vrcx_favorites')
      if (saved) {
        const data = JSON.parse(saved)
        favoriteFriends.value = data.friends || []
        favoriteGroups.value = data.groups || []
      }
    } catch (e) {
      console.error('Failed to load favorites:', e)
    }
  }

  function saveFavorites() {
    try {
      localStorage.setItem('vrcx_favorites', JSON.stringify({
        friends: favoriteFriends.value,
        groups: favoriteGroups.value
      }))
    } catch (e) {
      console.error('Failed to save favorites:', e)
    }
  }

  function addFriendToFavorites(userId, displayName, group = 'default') {
    if (isFriendFavorite(userId)) return

    favoriteFriends.value.push({
      userId,
      displayName,
      group,
      addedAt: new Date().toISOString()
    })
    saveFavorites()
  }

  function removeFriendFromFavorites(userId) {
    favoriteFriends.value = favoriteFriends.value.filter(f => f.userId !== userId)
    saveFavorites()
  }

  function isFriendFavorite(userId) {
    return favoriteFriendIds.value.has(userId)
  }

  function toggleFriendFavorite(userId, displayName) {
    if (isFriendFavorite(userId)) {
      removeFriendFromFavorites(userId)
    } else {
      addFriendToFavorites(userId, displayName)
    }
  }

  function getFavoriteFriendsByGroup(group = 'default') {
    return favoriteFriends.value.filter(f => f.group === group)
  }

  function addGroup(name) {
    if (favoriteGroups.value.includes(name)) return
    favoriteGroups.value.push(name)
    saveFavorites()
  }

  function removeGroup(name) {
    favoriteGroups.value = favoriteGroups.value.filter(g => g !== name)
    favoriteFriends.value = favoriteFriends.value.map(f => 
      f.group === name ? { ...f, group: 'default' } : f
    )
    saveFavorites()
  }

  function moveFriendToGroup(userId, newGroup) {
    const friend = favoriteFriends.value.find(f => f.userId === userId)
    if (friend) {
      friend.group = newGroup
      saveFavorites()
    }
  }

  loadFavorites()

  return {
    favoriteFriends,
    favoriteGroups,
    loading,
    favoriteFriendIds,
    addFriendToFavorites,
    removeFriendFromFavorites,
    isFriendFavorite,
    toggleFriendFavorite,
    getFavoriteFriendsByGroup,
    addGroup,
    removeGroup,
    moveFriendToGroup,
    loadFavorites,
    saveFavorites
  }
})
