<template>
  <el-card shadow="never" class="kpi-card">
    <div class="kpi-label">{{ label }}</div>
    <div class="kpi-value">{{ value }}</div>
    <div v-if="hint" class="kpi-hint">{{ hint }}</div>
  </el-card>
</template>

<script setup lang="ts">
defineProps<{
  label: string
  value: string
  hint?: string
}>()
</script>

<style scoped lang="scss">
.kpi-card {
  border-radius: 12px;
  border: 1px solid var(--el-border-color-lighter);
}
.kpi-label {
  font-size: 13px;
  color: var(--el-text-color-secondary);
}
.kpi-value {
  margin-top: 8px;
  font-size: 28px;
  font-weight: 600;
  letter-spacing: -0.02em;
}
.kpi-hint {
  margin-top: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
