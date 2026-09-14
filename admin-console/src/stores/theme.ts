import { defineStore } from 'pinia'
import { ref, watch } from 'vue'

export const useThemeStore = defineStore('theme', () => {
  const isDark = ref(localStorage.getItem('sx_admin_theme') === 'dark')

  function apply() {
    document.documentElement.classList.toggle('dark', isDark.value)
    localStorage.setItem('sx_admin_theme', isDark.value ? 'dark' : 'light')
  }

  function toggle() {
    isDark.value = !isDark.value
  }

  watch(isDark, apply, { immediate: true })

  return { isDark, toggle, apply }
})
