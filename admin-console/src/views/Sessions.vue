<template>
  <div>
    <h2 class="page-title">会话监控</h2>
    <p class="page-desc">最近会话只读 · <code>GET /api/admin/sessions</code></p>

    <el-table :data="items" v-loading="loading" stripe empty-text="暂无数据">
      <el-table-column prop="platform" label="平台" width="90" />
      <el-table-column prop="customerName" label="买家" min-width="120">
        <template #default="{ row }">{{ row.customerName || row.customerId || '—' }}</template>
      </el-table-column>
      <el-table-column prop="status" label="状态" width="100" />
      <el-table-column label="转人工" width="90">
        <template #default="{ row }">
          <el-tag v-if="row.pendingHumanHandoff" type="warning" size="small">是</el-tag>
          <span v-else>—</span>
        </template>
      </el-table-column>
      <el-table-column prop="messageCount" label="消息数" width="90" />
      <el-table-column prop="pendingDraftCount" label="待审草稿" width="100" />
      <el-table-column prop="lastBuyerMessageAt" label="最近买家消息" min-width="160">
        <template #default="{ row }">{{ formatTime(row.lastBuyerMessageAt || row.lastActiveAt) }}</template>
      </el-table-column>
      <el-table-column prop="shopId" label="ShopId" min-width="120">
        <template #default="{ row }">{{ String(row.shopId || '').slice(0, 8) }}…</template>
      </el-table-column>
    </el-table>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getSessions } from '@/api/admin'

const loading = ref(false)
const items = ref<Record<string, unknown>[]>([])

function formatTime(v: unknown) {
  if (!v) return '—'
  try {
    return new Date(String(v)).toLocaleString('zh-CN', { hour12: false })
  } catch {
    return String(v)
  }
}

onMounted(async () => {
  loading.value = true
  try {
    const res = await getSessions(80)
    items.value = res.items || []
  } catch {
    ElMessage.error('加载会话失败')
    items.value = []
  } finally {
    loading.value = false
  }
})
</script>
