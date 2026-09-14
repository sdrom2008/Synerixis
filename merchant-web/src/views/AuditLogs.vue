<template>
  <div>
    <h2 class="page-title">操作日志</h2>
    <p class="page-desc">
      本店关键写操作审计 · <code>GET /api/merchant/audit-logs</code>（Seller / Supervisor）
    </p>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <template #header>
        <div class="head">
          <span>最近 {{ items.length }} 条</span>
          <div class="filters">
            <el-select
              v-model="actionFilter"
              clearable
              filterable
              allow-create
              default-first-option
              placeholder="筛选 action"
              style="width: 220px"
              @change="load"
            >
              <el-option v-for="a in actionOptions" :key="a" :label="a" :value="a" />
            </el-select>
            <el-button size="small" :loading="loading" @click="load">刷新</el-button>
          </div>
        </div>
      </template>

      <el-table v-if="items.length" :data="items" stripe size="small">
        <el-table-column label="时间" min-width="160">
          <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
        </el-table-column>
        <el-table-column label="操作者" width="120">
          <template #default="{ row }">{{ row.actorType || '—' }}</template>
        </el-table-column>
        <el-table-column label="动作" min-width="160">
          <template #default="{ row }">
            <el-tag size="small" effect="plain">{{ row.action }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="资源" min-width="140">
          <template #default="{ row }">
            {{ row.resourceType || '—' }}
            <span v-if="row.resourceId" class="muted"> · {{ shortId(row.resourceId) }}</span>
          </template>
        </el-table-column>
        <el-table-column label="明细" min-width="220">
          <template #default="{ row }">
            <code class="detail">{{ row.detailJson || '—' }}</code>
          </template>
        </el-table-column>
      </el-table>
      <EmptyState v-else title="暂无审计记录" desc="绑店、团队变更、草稿审发、AI 设置等写操作会落在此。" />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import EmptyState from '@/components/EmptyState.vue'
import { getAuditLogs } from '@/api/merchant'

const loading = ref(false)
const items = ref<Record<string, unknown>[]>([])
const actionFilter = ref<string | undefined>()

const actionOptions = [
  'connection.bind',
  'connection.unbind',
  'connection.refresh',
  'team.create',
  'team.update',
  'team.disable',
  'team.reset_password',
  'draft.approve',
  'draft.reject',
  'session.handoff',
  'ai_settings.update',
]

function formatTime(v: unknown) {
  if (!v) return '—'
  try {
    return new Date(String(v)).toLocaleString('zh-CN', { hour12: false })
  } catch {
    return String(v)
  }
}

function shortId(v: unknown) {
  const s = String(v || '')
  return s.length > 8 ? s.slice(0, 8) + '…' : s
}

async function load() {
  loading.value = true
  try {
    const res = await getAuditLogs(50, actionFilter.value || undefined)
    items.value = (res.items || []) as Record<string, unknown>[]
  } catch {
    items.value = []
    ElMessage.warning('加载操作日志失败（需商家或主管权限）')
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
.filters {
  display: flex;
  gap: 8px;
  align-items: center;
}
.muted {
  color: var(--sx-muted);
  font-size: 12px;
}
.detail {
  font-size: 11px;
  word-break: break-all;
  color: var(--sx-muted);
}
</style>
