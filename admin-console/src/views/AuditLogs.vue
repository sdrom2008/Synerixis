<template>
  <div>
    <h2 class="page-title">审计日志</h2>
    <p class="page-desc">全站关键写操作 · <code>GET /api/admin/audit-logs</code></p>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <template #header>
        <div class="head">
          <span>最近 {{ items.length }} 条</span>
          <div class="actions">
            <el-button size="small" :loading="loading" @click="load">刷新</el-button>
            <el-button size="small" type="primary" plain :loading="exporting" @click="exportCsv">导出 CSV</el-button>
          </div>
        </div>
      </template>
      <el-table :data="items" stripe empty-text="暂无数据" size="small">
        <el-table-column label="时间" min-width="160">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column prop="actorType" label="操作者" width="110" />
        <el-table-column prop="action" label="动作" min-width="150" />
        <el-table-column label="资源" min-width="140">
          <template #default="{ row }">
            {{ row.resourceType || '—' }}
            <span v-if="row.resourceId" class="muted"> · {{ String(row.resourceId).slice(0, 8) }}…</span>
          </template>
        </el-table-column>
        <el-table-column label="ShopId" width="110">
          <template #default="{ row }">
            {{ row.shopId ? String(row.shopId).slice(0, 8) + '…' : '—' }}
          </template>
        </el-table-column>
        <el-table-column label="明细" min-width="220">
          <template #default="{ row }">
            <code class="detail">{{ row.detailJson || '—' }}</code>
          </template>
        </el-table-column>
      </el-table>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getAuditLogs, exportAuditLogsCsv } from '@/api/admin'

const loading = ref(false)
const exporting = ref(false)
const items = ref<Record<string, unknown>[]>([])

function formatTime(v: unknown) {
  if (!v) return '—'
  try {
    return new Date(String(v)).toLocaleString('zh-CN', { hour12: false })
  } catch {
    return String(v)
  }
}

async function load() {
  loading.value = true
  try {
    const res = await getAuditLogs(80)
    items.value = res.items || []
  } catch {
    ElMessage.error('加载审计日志失败')
    items.value = []
  } finally {
    loading.value = false
  }
}

async function exportCsv() {
  exporting.value = true
  try {
    await exportAuditLogsCsv(5000)
    ElMessage.success('已开始下载 CSV')
  } catch {
    ElMessage.error('导出失败')
  } finally {
    exporting.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.actions {
  display: flex;
  gap: 8px;
}
.muted {
  color: #94a3b8;
  font-size: 12px;
}
.detail {
  font-size: 11px;
  word-break: break-all;
  color: #64748b;
}
</style>
