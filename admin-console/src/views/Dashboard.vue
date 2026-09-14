<template>
  <div>
    <h2 class="page-title">概览</h2>
    <p class="page-desc">运营总览 · 数据来自 <code>GET /api/admin/dashboard</code>，无数据时显示「—」。</p>

    <el-row :gutter="16" v-loading="loading">
      <el-col :xs="24" :sm="12" :lg="8" v-for="item in kpis" :key="item.label">
        <KpiCard :label="item.label" :value="item.value" :hint="item.hint" />
      </el-col>
    </el-row>

    <el-card shadow="never" class="chart-card">
      <template #header>
        <div class="card-head">
          <span>近 7 日会话量</span>
          <el-tag v-if="!chartReady" type="info" size="small" effect="plain">暂无趋势数据</el-tag>
          <el-tag v-else type="success" size="small" effect="plain">真实聚合</el-tag>
        </div>
      </template>
      <UsageChart :points="chartPoints" />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import KpiCard from '@/components/KpiCard.vue'
import UsageChart from '@/components/UsageChart.vue'
import { getDashboard, getUsage } from '@/api/admin'

const loading = ref(false)
const chartReady = ref(false)
const chartPoints = ref<{ date: string; count: number }[]>([])
const kpis = ref([
  { label: '商家数', value: '—', hint: '加载中' },
  { label: '连接店铺', value: '—', hint: '加载中' },
  { label: '今日会话', value: '—', hint: '加载中' },
  { label: '待手审草稿', value: '—', hint: '加载中' },
  { label: '待人工会话', value: '—', hint: '加载中' },
  { label: 'SLA overdue', value: '—', hint: '粗计数' },
])

function fmt(v: unknown) {
  if (v === null || v === undefined) return '—'
  return String(v)
}

onMounted(async () => {
  loading.value = true
  try {
    const [dash, usage] = await Promise.all([getDashboard(), getUsage().catch(() => null)])
    kpis.value = [
      { label: '商家数', value: fmt(dash.merchantCount), hint: 'Sellers' },
      { label: '连接店铺', value: fmt(dash.connectedShops), hint: '活跃 PlatformConnection' },
      { label: '今日会话', value: fmt(dash.sessionsToday), hint: 'UTC 日界' },
      { label: '待手审草稿', value: fmt(dash.pendingDrafts), hint: 'Draft Pending' },
      { label: '待人工会话', value: fmt(dash.handoffPending), hint: 'PendingHumanHandoff' },
      { label: 'SLA overdue', value: fmt(dash.slaOverdue), hint: '未结束会话粗计数' },
    ]
    const daily = (usage?.dailySessions as { date: string; count: number }[]) || []
    if (daily.length) {
      chartPoints.value = daily.map((d) => ({
        date: String(d.date).slice(0, 10),
        count: Number(d.count) || 0,
      }))
      chartReady.value = true
    }
  } catch {
    ElMessage.error('加载 Dashboard 失败（需 Admin JWT）')
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="scss">
.chart-card {
  margin-top: 20px;
  border-radius: 12px;
}
.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.el-col {
  margin-bottom: 16px;
}
</style>
