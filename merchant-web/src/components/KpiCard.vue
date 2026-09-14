<template>
  <div class="kpi sx-card">
    <div class="label">{{ label }}</div>
    <div class="value">{{ display }}</div>
    <div v-if="hint" class="hint">{{ hint }}</div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  label: string
  value: string | number | null | undefined
  hint?: string
}>()

const display = computed(() => {
  if (props.value === null || props.value === undefined || props.value === '') return '—'
  return String(props.value)
})
</script>

<style scoped lang="scss">
.kpi {
  padding: 16px 18px;
}
.label {
  font-size: 13px;
  color: #64748b;
}
.value {
  margin-top: 8px;
  font-size: 28px;
  font-weight: 700;
  letter-spacing: -0.02em;
  color: #0f172a;
}
.hint {
  margin-top: 6px;
  font-size: 12px;
  color: #94a3b8;
}
</style>
