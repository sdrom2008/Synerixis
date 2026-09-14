<template>
  <div>
    <h2 class="page-title">团队</h2>
    <p class="page-desc">可选模块 · 占位。坐席账号、角色与 handoff 将在 P2 人工协同阶段完善。</p>

    <el-card shadow="never" class="sx-card">
      <EmptyState
        title="团队管理尚未开通"
        desc="当前可用商家本人账号处理收件箱。多坐席、转接与排班为后续能力。"
        icon="UserFilled"
      />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import EmptyState from '@/components/EmptyState.vue'
</script>
