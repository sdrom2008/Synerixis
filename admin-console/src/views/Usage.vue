<template>
  <div>
    <h2 class="page-title">用量计费</h2>
    <p class="page-desc">
      全站诚实聚合 · <code>GET /api/admin/usage</code> +
      <code>/usage/daily</code>（含 AI token / exact·estimated，无数据为 0）
    </p>

    <el-row :gutter="16" v-loading="loading">
      <el-col :xs="24" :sm="12" :lg="6" v-for="item in cards" :key="item.label">
        <el-card shadow="never" class="stat">
          <div class="label">{{ item.label }}</div>
          <div class="value">{{ item.value }}</div>
        </el-card>
      </el-col>
    </el-row>

    <el-card shadow="never" class="block chart-card">
      <template #header>
        <div class="card-head">
          <span>近 7 日会话趋势</span>
          <el-tag v-if="chartReady" type="success" size="small" effect="plain">真实聚合</el-tag>
          <el-tag v-else type="info" size="small" effect="plain">暂无趋势数据</el-tag>
        </div>
      </template>
      <UsageChart :points="chartPoints" />
    </el-card>

    <el-card shadow="never" class="block" v-if="bySub.length">
      <template #header>按订阅档位</template>
      <el-table :data="bySub" stripe>
        <el-table-column prop="level" label="档位" />
        <el-table-column prop="count" label="商家数" />
        <el-table-column prop="totalQuota" label="额度合计" />
      </el-table>
    </el-card>
    <el-empty v-else-if="!loading" description="暂无用量数据" />

    <p v-if="note" class="note">{{ note }}</p>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import UsageChart from '@/components/UsageChart.vue'
import { getUsage, getUsageDaily } from '@/api/admin'

const loading = ref(false)
const cards = ref<{ label: string; value: string }[]>([])
const bySub = ref<{ level: string; count: number; totalQuota: number }[]>([])
const note = ref('')
const chartReady = ref(false)
const chartPoints = ref<{ date: string; count: number }[]>([])

function fmt(v: unknown) {
  if (v === null || v === undefined) return '—'
  return String(v)
}

onMounted(async () => {
  loading.value = true
  try {
    const [u, daily] = await Promise.all([getUsage(), getUsageDaily(7).catch(() => null)])
    const z = (v: unknown) => (v === null || v === undefined || v === '' ? '0' : String(v))
    cards.value = [
      { label: '本月消息', value: fmt(u.messagesThisMonth) },
      { label: '今日消息', value: fmt(u.messagesToday) },
      { label: '本月会话', value: fmt(u.sessionsThisMonth) },
      { label: '今日草稿', value: fmt(u.draftsToday) },
      { label: '今日 AI Token', value: z(u.totalTokensToday) },
      { label: '今日精确 Token', value: z(u.exactTokensToday) },
      { label: '今日估算 Token', value: z(u.estimatedTokensToday) },
      { label: '今日估算费用(USD)', value: z(u.estimatedCostUsdToday) },
      { label: '本月 AI Token', value: z(u.totalTokensThisMonth) },
      { label: '本月估算费用(USD)', value: z(u.estimatedCostUsdThisMonth) },
      { label: '商家数', value: fmt(u.merchants) },
      { label: '连接店铺', value: fmt(u.connectedShops) },
    ]
    bySub.value = (u.bySubscription as typeof bySub.value) || []
    note.value = String(u.note || '')

    const items = daily?.items || (u.dailySessions as { date: string; count: number }[]) || []
    if (items.length) {
      chartPoints.value = items.map((d) => ({
        date: String(d.date).slice(0, 10),
        count: Number(d.count) || 0,
      }))
      chartReady.value = !!(daily?.hasData || chartPoints.value.some((p) => p.count > 0))
    }
  } catch {
    ElMessage.error('加载用量失败')
    cards.value = []
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="scss">
.stat {
  margin-bottom: 16px;
  border-radius: 12px;
  .label {
    color: #64748b;
    font-size: 13px;
  }
  .value {
    font-size: 28px;
    font-weight: 600;
    margin-top: 8px;
  }
}
.block {
  margin-top: 8px;
  border-radius: 12px;
}
.chart-card {
  margin-top: 16px;
  margin-bottom: 16px;
}
.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.note {
  margin-top: 16px;
  color: #94a3b8;
  font-size: 13px;
}
</style>
