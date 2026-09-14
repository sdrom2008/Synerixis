<template>
  <div>
    <h2 class="page-title">计费</h2>
    <p class="page-desc">套餐说明对齐 docs/PRICING_DRAFT.md；用量来自 GET /api/merchant/usage，无数据不伪造。</p>

    <el-row :gutter="16">
      <el-col :xs="24" :md="8" v-for="plan in plans" :key="plan.name">
        <el-card shadow="never" class="sx-card plan">
          <h3>{{ plan.name }}</h3>
          <p class="price">{{ plan.price }}</p>
          <ul>
            <li v-for="f in plan.features" :key="f">{{ f }}</li>
          </ul>
        </el-card>
      </el-col>
    </el-row>

    <el-card shadow="never" class="sx-card" style="margin-top: 20px" v-loading="loading">
      <template #header>本月用量</template>
      <el-descriptions :column="2" border>
        <el-descriptions-item v-for="row in usageRows" :key="row.label" :label="row.label">
          {{ row.value }}
        </el-descriptions-item>
      </el-descriptions>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getMerchantUsage } from '@/api/merchant'

const loading = ref(false)
const usageRows = ref<{ label: string; value: string }[]>([
  { label: '消息数', value: '—' },
  { label: '会话数', value: '—' },
])

const plans = [
  {
    name: '入门',
    price: '见定价草案',
    features: ['单店起步', 'DraftFirst 人审', '基础收件箱'],
  },
  {
    name: '专业',
    price: '见定价草案',
    features: ['多店', 'SLA 视图', '团队坐席（规划中）'],
  },
  {
    name: '企业',
    price: '商务洽谈',
    features: ['定制集成', '合规审计', '专属支持'],
  },
]

onMounted(async () => {
  loading.value = true
  try {
    const u = await getMerchantUsage()
    const pick = (...keys: string[]) => {
      for (const k of keys) {
        const v = u[k]
        if (v !== undefined && v !== null) return String(v)
      }
      return '—'
    }
    usageRows.value = [
      { label: '本月消息', value: pick('messagesThisMonth', 'MessagesThisMonth', 'messageCount') },
      { label: '本月会话', value: pick('sessionsThisMonth', 'SessionsThisMonth', 'sessionCount') },
      { label: '待发草稿', value: pick('pendingDrafts', 'PendingDrafts') },
      { label: '统计月份', value: pick('month', 'Month', 'period') },
    ]
  } catch {
    ElMessage.warning('用量接口暂不可用')
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="scss">
.plan {
  margin-bottom: 16px;
  h3 {
    margin: 0 0 8px;
  }
  .price {
    color: #2563eb;
    font-weight: 700;
    font-size: 18px;
    margin: 0 0 12px;
  }
  ul {
    margin: 0;
    padding-left: 18px;
    color: #475569;
    line-height: 1.7;
    font-size: 13px;
  }
}
</style>
