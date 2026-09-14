<template>
  <div class="empty">
    <el-icon :size="40" color="#94a3b8"><component :is="icon" /></el-icon>
    <p class="title">{{ title }}</p>
    <p v-if="desc" class="desc">{{ desc }}</p>
    <slot />
  </div>
</template>

<script setup lang="ts">
withDefaults(
  defineProps<{
    title: string
    desc?: string
    icon?: string
  }>(),
  { icon: 'Box', desc: '' },
)
</script>

<style scoped lang="scss">
.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 48px 24px;
  text-align: center;
  color: #64748b;
}
.title {
  margin: 12px 0 4px;
  font-size: 15px;
  font-weight: 600;
  color: #334155;
}
.desc {
  margin: 0;
  font-size: 13px;
  max-width: 320px;
  line-height: 1.5;
}
</style>
