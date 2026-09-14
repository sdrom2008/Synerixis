<template>
  <div>
    <h2 class="page-title">概览</h2>
    <p class="page-desc">
      KPI 来自 <code>GET /api/merchant/dashboard</code>；趋势来自
      <code>GET /api/merchant/usage/daily?days=7</code>（真实聚合，无数据空态）。
    </p>

    <el-alert
      v-if="onboardingIncomplete"
      type="info"
      :closable="false"
      show-icon
      style="margin-bottom: 16px"
      :title="`上手指南进度 ${onboardingDone}/${onboardingTotal}`"
    >
      <el-button type="primary" size="small" @click="$router.push('/onboarding')">继续完成</el-button>
    </el-alert>

    <el-alert
      v-if="tokenAlert"
      :type="tokenAlert.type"
      :closable="false"
      show-icon
      style="margin-bottom: 16px"
      :title="tokenAlert.title"
    >
      <el-button type="primary" size="small" @click="$router.push('/shops')">前往店铺绑定</el-button>
    </el-alert>

    <el-row :gutter="16" v-loading="loading">
      <el-col :xs="24" :sm="12" :lg="8" v-for="item in kpis" :key="item.label">
        <KpiCard :label="item.label" :value="item.value" :hint="item.hint" />
      </el-col>
    </el-row>

    <el-card shadow="never" class="chart-card sx-card" style="margin-top: 20px" v-loading="chartLoading">
      <template #header>
        <div class="card-head">
          <span>近 7 日会话数</span>
          <el-tag v-if="chartReady" type="success" size="small" effect="plain">真实聚合</el-tag>
          <el-tag v-else type="info" size="small" effect="plain">暂无趋势数据</el-tag>
        </div>
      </template>
      <UsageChart :points="chartPoints" series-name="会话数" />
    </el-card>

    <el-card shadow="never" class="hint-card sx-card" style="margin-top: 20px">
      <template #header>
        <span>工作台说明</span>
      </template>
      <ul class="tips">
        <li>主战场在「收件箱」：三栏会话列表 · 消息时间线 · AI 草稿审发。</li>
        <li>默认 DraftFirst：AI 只写草稿，人工批准后才 SendReply；转人工后停止新草稿与 AutoSend。</li>
        <li>SLA 徽章（即将超时 / 已超时）与告警条来自真实会话数据与 GET /api/merchant/alerts。</li>
        <li>移动端仍用 <code>frontend/</code>（HBuilder）；本应用为 PC 桌面宽屏。</li>
      </ul>
      <el-button type="primary" @click="$router.push('/inbox')">进入收件箱</el-button>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import KpiCard from '@/components/KpiCard.vue'
import UsageChart from '@/components/UsageChart.vue'
import {
  getConnections,
  getMerchantDashboard,
  getMerchantUsageDaily,
  getOnboarding,
  type DashboardKpis,
} from '@/api/merchant'

const loading = ref(false)
const onboardingIncomplete = ref(false)
const onboardingDone = ref(0)
const onboardingTotal = ref(5)
const tokenAlert = ref<{ type: 'error' | 'warning'; title: string } | null>(null)
const chartLoading = ref(false)
const chartReady = ref(false)
const chartPoints = ref<{ date: string; count: number }[]>([])
const data = ref<DashboardKpis | null>(null)

const kpis = ref([
  { label: '今日会话', value: '—' as string | number, hint: 'sessionsToday' },
  { label: '待人工', value: '—' as string | number, hint: 'pendingHandoff' },
  { label: '待发草稿', value: '—' as string | number, hint: 'pendingDrafts' },
  { label: '已连店铺', value: '—' as string | number, hint: 'connectedShops' },
  { label: '自动解决率', value: '—' as string | number, hint: '无已结束会话时为 —' },
  { label: '本月消息', value: '—' as string | number, hint: 'messagesThisMonth' },
])

function fmtRate(v: number | null | undefined) {
  if (v === null || v === undefined) return '—'
  return `${v}%`
}

onMounted(async () => {
  loading.value = true
  chartLoading.value = true
  try {
    const [dash, daily, conns, onb] = await Promise.all([
      getMerchantDashboard(),
      getMerchantUsageDaily(7).catch(() => null),
      getConnections().catch(() => null),
      getOnboarding().catch(() => null),
    ])
    if (onb) {
      onboardingDone.value = onb.doneCount ?? 0
      onboardingTotal.value = onb.total ?? 5
      onboardingIncomplete.value = !onb.complete
    }
    data.value = dash
    try {
      const raw = conns as { items?: Record<string, unknown>[] } | Record<string, unknown>[] | null
      const items = !raw ? [] : Array.isArray(raw) ? raw : raw.items || []
      const expired = items.filter((c) => String(c.status || c.tokenStatus) === 'expired').length
      const expiring = items.filter((c) => String(c.status || c.tokenStatus) === 'expiring').length
      if (expired > 0) {
        tokenAlert.value = { type: 'error', title: `${expired} 个店铺 Token 已过期，请立即刷新` }
      } else if (expiring > 0) {
        tokenAlert.value = { type: 'warning', title: `${expiring} 个店铺 Token 将在 24 小时内过期` }
      } else {
        tokenAlert.value = null
      }
    } catch {
      tokenAlert.value = null
    }
    const d = dash
    kpis.value = [
      { label: '今日会话', value: d.sessionsToday ?? '—', hint: 'sessionsToday' },
      { label: '待人工', value: d.pendingHandoff ?? '—', hint: 'pendingHandoff' },
      { label: '待发草稿', value: d.pendingDrafts ?? '—', hint: 'pendingDrafts' },
      { label: '已连店铺', value: d.connectedShops ?? '—', hint: 'connectedShops' },
      { label: '自动解决率', value: fmtRate(d.autoResolveRate), hint: '无已结束会话时为 —' },
      { label: '本月消息', value: d.messagesThisMonth ?? '—', hint: 'messagesThisMonth' },
    ]

    const items = daily?.items || []
    chartPoints.value = items.map((x) => ({
      date: String(x.date).slice(0, 10),
      count: Number(x.count ?? x.sessions ?? 0) || 0,
    }))
    chartReady.value = !!(daily?.hasData || chartPoints.value.some((p) => p.count > 0))
  } catch {
    ElMessage.warning('无法加载概览（请确认已登录且 API 可用）')
  } finally {
    loading.value = false
    chartLoading.value = false
  }
})
</script>

<style scoped lang="scss">
.el-col {
  margin-bottom: 16px;
}
.card-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.chart-card {
  border-radius: 12px;
}
.tips {
  margin: 0 0 16px;
  padding-left: 18px;
  color: #475569;
  line-height: 1.7;
  font-size: 14px;
}
code {
  background: #f1f5f9;
  padding: 1px 6px;
  border-radius: 4px;
  font-size: 12px;
}
</style>
