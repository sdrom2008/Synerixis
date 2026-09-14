<template>
  <div>
    <h2 class="page-title">计费</h2>
    <p class="page-desc">
      套餐说明摘要自 docs/PRICING_DRAFT.md；用量来自 GET /api/merchant/usage 与 Seller profile，无支付网关时不伪造数字。
    </p>

    <el-row :gutter="16">
      <el-col :xs="24" :md="6" v-for="plan in plans" :key="plan.name">
        <el-card shadow="never" class="sx-card plan" :class="{ current: plan.level === currentLevel }">
          <h3>{{ plan.name }}</h3>
          <p class="price">{{ plan.price }}</p>
          <ul>
            <li v-for="f in plan.features" :key="f">{{ f }}</li>
          </ul>
          <el-tag v-if="plan.level === currentLevel" size="small" type="success" style="margin-top: 8px">当前档位</el-tag>
        </el-card>
      </el-col>
    </el-row>

    <el-card shadow="never" class="sx-card" style="margin-top: 20px" v-loading="loading">
      <template #header>订阅与额度</template>
      <el-descriptions :column="2" border>
        <el-descriptions-item label="订阅档位">{{ subscriptionLevel || '—' }}</el-descriptions-item>
        <el-descriptions-item label="订阅到期">{{ subscriptionEnd || '—' }}</el-descriptions-item>
        <el-descriptions-item label="剩余免费额度">{{ freeQuota ?? '—' }}</el-descriptions-item>
        <el-descriptions-item label="已连接店铺">{{ connectedShops ?? '—' }}</el-descriptions-item>
      </el-descriptions>
    </el-card>

    <el-card shadow="never" class="sx-card" style="margin-top: 16px" v-loading="loading">
      <template #header>真实用量</template>
      <el-descriptions :column="2" border>
        <el-descriptions-item v-for="row in usageRows" :key="row.label" :label="row.label">
          {{ row.value }}
        </el-descriptions-item>
      </el-descriptions>
      <p class="hint">支付网关未接入：本页仅展示说明与计数，无法在线升级。</p>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getMerchantUsage } from '@/api/merchant'
import { getSellerProfile } from '@/api/seller'

const loading = ref(false)
const subscriptionLevel = ref<string>('')
const subscriptionEnd = ref<string>('')
const freeQuota = ref<number | string | null>(null)
const connectedShops = ref<number | null>(null)

const usageRows = ref<{ label: string; value: string }[]>([
  { label: '今日草稿', value: '—' },
  { label: '今日会话', value: '—' },
  { label: '本月消息', value: '—' },
  { label: '本月会话', value: '—' },
])

const plans = [
  {
    name: 'Trial',
    level: 'trial',
    price: '0（14 天）',
    features: ['1 店', '日消息上限低', '验证 Webhook / 查单'],
  },
  {
    name: 'Starter',
    level: 'starter',
    price: '$29–49 / 店·月',
    features: ['1 店', 'DraftFirst 人审', '订单查询', '转人工'],
  },
  {
    name: 'Pro',
    level: 'pro',
    price: '$79–129 / 店·月',
    features: ['更高消息包', '多客服 1–3', 'SLA 视图', '基础报表'],
  },
  {
    name: 'Agency',
    level: 'agency',
    price: '面议',
    features: ['多店折扣', '代运营控制台', '专属对接'],
  },
]

const currentLevel = computed(() => {
  const raw = (subscriptionLevel.value || '').toLowerCase()
  if (raw.includes('pro') || raw === 'basic') return 'pro'
  if (raw.includes('agency') || raw.includes('enterprise')) return 'agency'
  if (raw.includes('start') || raw.includes('free')) return 'starter'
  if (raw.includes('trial')) return 'trial'
  return raw || 'trial'
})

function pick(obj: Record<string, unknown>, ...keys: string[]) {
  for (const k of keys) {
    const v = obj[k]
    if (v !== undefined && v !== null && v !== '') return v
  }
  return undefined
}

function fmt(v: unknown) {
  if (v === undefined || v === null || v === '') return '—'
  if (typeof v === 'string' && /T\d{2}:/.test(v)) {
    try {
      return new Date(v).toLocaleString('zh-CN', { hour12: false })
    } catch {
      return String(v)
    }
  }
  return String(v)
}

onMounted(async () => {
  loading.value = true
  try {
    const [u, profile] = await Promise.all([
      getMerchantUsage().catch(() => null),
      getSellerProfile().catch(() => null),
    ])
    const usage = (u || {}) as Record<string, unknown>
    const prof = (profile || {}) as Record<string, unknown>

    subscriptionLevel.value = String(
      pick(usage, 'subscriptionLevel', 'subscription', 'SubscriptionLevel') ??
        pick(prof, 'subscriptionLevel', 'SubscriptionLevel') ??
        '',
    )
    const end =
      pick(usage, 'subscriptionEnd', 'SubscriptionEnd') ??
      pick(prof, 'subscriptionEnd', 'SubscriptionEnd')
    subscriptionEnd.value = end ? fmt(end) : '—'
    freeQuota.value = (pick(usage, 'freeQuota', 'quota', 'FreeQuota') ??
      pick(prof, 'freeQuota', 'FreeQuota') ??
      null) as number | string | null
    const shops = pick(usage, 'connectedShops', 'ConnectedShops')
    connectedShops.value = shops != null ? Number(shops) : null

    const numOrZero = (v: unknown) => {
      if (v === undefined || v === null || v === '') return '0'
      return String(v)
    }
    usageRows.value = [
      { label: '今日草稿', value: fmt(pick(usage, 'draftsToday', 'DraftsToday')) },
      { label: '今日会话', value: fmt(pick(usage, 'sessionsToday', 'SessionsToday')) },
      {
        label: '本月消息',
        value: fmt(pick(usage, 'messagesThisMonth', 'MessagesThisMonth', 'messageCount')),
      },
      {
        label: '本月会话',
        value: fmt(pick(usage, 'sessionsThisMonth', 'SessionsThisMonth', 'sessionCount')),
      },
      { label: '今日 AI Token', value: numOrZero(pick(usage, 'totalTokensToday', 'TotalTokensToday')) },
      {
        label: '今日估算费用(USD)',
        value: numOrZero(pick(usage, 'estimatedCostUsdToday', 'EstimatedCostUsdToday')),
      },
      {
        label: '本月 AI Token',
        value: numOrZero(pick(usage, 'totalTokensThisMonth', 'TotalTokensThisMonth')),
      },
      {
        label: '本月估算费用(USD)',
        value: numOrZero(pick(usage, 'estimatedCostUsdThisMonth', 'EstimatedCostUsdThisMonth')),
      },
      { label: '已连接店铺', value: fmt(pick(usage, 'connectedShops', 'ConnectedShops')) },
      { label: '统计截止', value: fmt(pick(usage, 'periodEnd', 'PeriodEnd')) },
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
  &.current {
    border-color: #2563eb;
  }
  h3 {
    margin: 0 0 8px;
  }
  .price {
    color: #2563eb;
    font-weight: 700;
    font-size: 16px;
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
.hint {
  margin: 12px 0 0;
  font-size: 12px;
  color: #94a3b8;
}
</style>
