<template>
  <div>
    <h2 class="page-title">审计日志</h2>
    <p class="page-desc">全站关键写操作 · <code>GET /api/admin/audit-logs</code> · 支持导出 CSV</p>

    <div class="toolbar">
      <el-input
        v-model="actionFilter"
        clearable
        placeholder="按动作过滤（如 AdminLogin）"
        style="max-width: 260px"
        @keyup.enter="load"
      />
      <el-button type="primary" :loading="loading" @click="load">查询</el-button>
      <el-button size="small" type="primary" plain :loading="exporting" @click="exportCsv">
        导出 CSV
      </el-button>
    </div>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <template #header>
        <div class="head">
          <span>最近 {{ items.length }} 条</span>
        </div>
      </template>
      <el-table :data="items" stripe size="small">
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
        <template #empty>
          <el-empty
            description="暂无审计日志。Admin 登录、改商家状态/订阅、改系统设置会写入。"
            :image-size="72"
          />
        </template>
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
const actionFilter = ref('')

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
    const res = await getAuditLogs(80, undefined, actionFilter.value.trim() || undefined)
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
    await exportAuditLogsCsv(5000, undefined, actionFilter.value.trim() || undefined)
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
.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
  align-items: center;
}
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
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
