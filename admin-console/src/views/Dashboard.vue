<template>
  <div>
    <h2 class="page-title">概览</h2>
    <p class="page-desc">运营总览。数值在接通 API 前显示为占位，不表示真实生产数据。</p>

    <el-row :gutter="16">
      <el-col :xs="24" :sm="12" :lg="6" v-for="item in kpis" :key="item.label">
        <KpiCard :label="item.label" :value="item.value" :hint="item.hint" />
      </el-col>
    </el-row>

    <el-card shadow="never" class="chart-card">
      <template #header>
        <div class="card-head">
          <span>近 7 日消息量</span>
          <el-tag type="info" size="small" effect="plain">图表占位 · 非生产数据</el-tag>
        </div>
      </template>
      <UsageChart />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import KpiCard from '@/components/KpiCard.vue'
import UsageChart from '@/components/UsageChart.vue'

const kpis = [
  { label: '租户数', value: '—', hint: '待对接 API' },
  { label: '商家数', value: '—', hint: '待对接 API' },
  { label: '今日会话', value: '—', hint: '待对接 API' },
  { label: '模型费用（今日）', value: '—', hint: '待对接 API' },
]
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
