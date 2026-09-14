<template>
  <AppShell active="billing">
    <view class="sx-page sx-page-wide">
      <view class="sx-page-header">
        <view>
          <view class="sx-page-title">套餐与计费</view>
          <view class="sx-page-sub">按店铺订阅 · 对齐定价草案（示意价，非正式报价）</view>
        </view>
      </view>

      <view class="sx-card current">
        <text class="muted">当前套餐</text>
        <view class="current-row">
          <text class="plan-name">{{ currentPlan }}</text>
          <text class="expiry">{{ expiryText }}</text>
        </view>
        <view class="usage-row" v-if="messagesThisMonth !== null">
          <text class="muted">本月消息（DB 计数）</text>
          <text class="usage-val">{{ messagesThisMonth }}</text>
        </view>
        <text v-else class="muted tip">本月用量加载中或暂无数据时显示 —</text>
      </view>

      <view class="plans">
        <view
          v-for="plan in plans"
          :key="plan.id"
          class="plan-card"
          :class="{ featured: plan.featured }"
        >
          <view class="plan-top">
            <text class="name">{{ plan.name }}</text>
            <text v-if="plan.badge" class="sx-badge sx-badge-info">{{ plan.badge }}</text>
          </view>
          <view class="price-row">
            <text class="price">{{ plan.price }}</text>
            <text class="period">{{ plan.period }}</text>
          </view>
          <text class="fit">{{ plan.fit }}</text>
          <view class="features">
            <text v-for="(f, i) in plan.features" :key="i" class="feature">· {{ f }}</text>
          </view>
          <button
            class="sx-btn"
            :class="plan.featured ? 'sx-btn-primary' : 'sx-btn-outline'"
            @tap="selectPlan(plan)"
          >
            {{ plan.cta }}
          </button>
        </view>
      </view>

      <view class="sx-card note">
        <text class="note-title">说明</text>
        <text class="note-body">
          价格来自 docs/PRICING_DRAFT.md，试点期可调整。季付约 9 折、年付约 8 折。超额与发票以正式合同为准。
        </text>
      </view>
    </view>
  </AppShell>
</template>

<script>
import AppShell from '@/components/merchant/AppShell.vue';
import { getSellerProfile, getMerchantUsage } from '@/api/merchant.js';

export default {
  components: { AppShell },
  data() {
    return {
      currentPlan: '—',
      subscriptionEnd: null,
      messagesThisMonth: null,
      plans: [
        {
          id: 'trial',
          name: 'Trial',
          price: '$0',
          period: '/ 14 天',
          fit: '验证 Webhook 与查单',
          badge: '',
          featured: false,
          cta: '开始试用',
          features: ['1 店', '日消息上限较低', '额度提示']
        },
        {
          id: 'starter',
          name: 'Starter',
          price: '$29–49',
          period: '/ 店 / 月',
          fit: '单店卖家',
          badge: '推荐入门',
          featured: true,
          cta: '选择 Starter',
          features: ['1 店', '基础自动回复', '订单查询', '转人工 API', '~3,000 入站消息 / 月']
        },
        {
          id: 'pro',
          name: 'Pro',
          price: '$79–129',
          period: '/ 店 / 月',
          fit: '日咨询量大的店铺',
          badge: '',
          featured: false,
          cta: '选择 Pro',
          features: ['更高消息包', '优先队列', '1–3 客服席位', '基础报表', '~15,000 入站消息 / 月']
        },
        {
          id: 'agency',
          name: 'Agency',
          price: '面议',
          period: '',
          fit: '工作室 / 服务商',
          badge: '',
          featured: false,
          cta: '联系销售',
          features: ['多店折扣', '代运营控制台', '专属对接']
        }
      ]
    };
  },
  computed: {
    expiryText() {
      if (!this.subscriptionEnd) return '到期时间：—';
      const d = new Date(this.subscriptionEnd);
      if (Number.isNaN(d.getTime())) return '到期时间：—';
      return `到期：${d.toLocaleDateString('zh-CN')}`;
    }
  },
  onShow() {
    this.load();
  },
  methods: {
    async load() {
      try {
        const p = await getSellerProfile();
        this.currentPlan = p?.subscriptionLevel || p?.SubscriptionLevel || '—';
        this.subscriptionEnd = p?.subscriptionEnd || p?.SubscriptionEnd || null;
      } catch (e) {
        this.currentPlan = '—';
      }
      try {
        const u = await getMerchantUsage();
        const n = u?.messagesThisMonth ?? u?.MessagesThisMonth;
        this.messagesThisMonth = typeof n === 'number' ? n : null;
      } catch (e) {
        this.messagesThisMonth = null;
      }
    },
    selectPlan(plan) {
      if (plan.id === 'agency') {
        uni.showModal({
          title: 'Agency 方案',
          content: '请联系团队获取多店报价。支付闭环可沿用现有 /pages/pay/subscribe 入口。',
          showCancel: false
        });
        return;
      }
      // Keep existing pay page as checkout stub
      uni.navigateTo({ url: '/pages/pay/subscribe' });
    }
  }
};
</script>

<style lang="scss">
@import '../../styles/merchant.scss';

.current {
  margin-bottom: 32rpx;
}

.muted {
  font-size: 24rpx;
  color: #64748B;
}

.current-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin: 12rpx 0 16rpx;
}

.plan-name {
  font-size: 40rpx;
  font-weight: 700;
  color: #0F172A;
}

.expiry {
  font-size: 24rpx;
  color: #64748B;
}

.tip {
  line-height: 1.5;
}

.usage-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  margin-top: 16rpx;
  padding-top: 16rpx;
  border-top: 1rpx solid #E2E8F0;
}

.usage-val {
  font-size: 34rpx;
  font-weight: 700;
  color: #0F172A;
}

.plans {
  display: grid;
  grid-template-columns: 1fr;
  gap: 24rpx;
}

@media (min-width: 768px) {
  .plans {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1100px) {
  .plans {
    grid-template-columns: repeat(4, 1fr);
  }
}

.plan-card {
  background: #fff;
  border: 1rpx solid #E2E8F0;
  border-radius: 24rpx;
  padding: 32rpx 28rpx;
  display: flex;
  flex-direction: column;
  box-shadow: 0 2rpx 8rpx rgba(15, 23, 42, 0.04);
}

.plan-card.featured {
  border-color: #2563EB;
  box-shadow: 0 8rpx 24rpx rgba(37, 99, 235, 0.12);
}

.plan-top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16rpx;
}

.name {
  font-size: 34rpx;
  font-weight: 700;
  color: #0F172A;
}

.price-row {
  display: flex;
  align-items: baseline;
  gap: 8rpx;
  margin-bottom: 8rpx;
}

.price {
  font-size: 44rpx;
  font-weight: 700;
  color: #0F172A;
}

.period {
  font-size: 22rpx;
  color: #64748B;
}

.fit {
  font-size: 24rpx;
  color: #64748B;
  margin-bottom: 20rpx;
}

.features {
  flex: 1;
  margin-bottom: 28rpx;
}

.feature {
  display: block;
  font-size: 26rpx;
  color: #334155;
  line-height: 1.7;
}

.note {
  margin-top: 32rpx;
}

.note-title {
  display: block;
  font-weight: 650;
  margin-bottom: 8rpx;
  color: #0F172A;
}

.note-body {
  font-size: 24rpx;
  color: #64748B;
  line-height: 1.55;
}
</style>
