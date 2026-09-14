<template>
  <AppShell active="dashboard">
    <view class="sx-page sx-page-wide">
      <view class="sx-page-header">
        <view>
          <view class="sx-page-title">仪表盘</view>
          <view class="sx-page-sub">{{ greeting }} · {{ currentDate }}</view>
        </view>
        <button class="sx-btn sx-btn-ghost" @tap="goShops">店铺绑定</button>
      </view>

      <view class="sx-kpi-grid">
        <KpiCard label="今日会话" :value="kpis.todaySessions" hint="按 UTC 日切分的进线会话" />
        <KpiCard label="自动解决率" :value="autoRateDisplay" :hint="autoRateHint" />
        <KpiCard label="待人工" :value="kpis.pendingHandoff" unit="条" />
        <KpiCard label="店铺状态" :value="shopStatusLabel" :hint="shopHint" />
      </view>

      <view class="sx-section sx-card chart-card">
        <view class="chart-head">
          <text class="sx-section-title" style="margin:0">会话趋势</text>
          <text class="chart-tag">占位</text>
        </view>
        <view class="chart-placeholder">
          <view class="bars">
            <view v-for="n in 7" :key="n" class="bar" :style="{ height: barHeight(n) }" />
          </view>
          <text class="chart-note">趋势图仍为占位；上方 KPI 已接 /api/merchant/dashboard 真实聚合</text>
        </view>
      </view>

      <view class="sx-section">
        <view class="sx-section-title">快捷入口</view>
        <view class="quick-grid">
          <view class="quick-item" @tap="goInbox">
            <text class="q-title">收件箱</text>
            <text class="q-desc">会话列表与转人工</text>
          </view>
          <view class="quick-item" @tap="goShops">
            <text class="q-title">绑定 Shopee</text>
            <text class="q-desc">OAuth 授权与连接状态</text>
          </view>
          <view class="quick-item" @tap="goAi">
            <text class="q-title">AI 设置</text>
            <text class="q-desc">语气、自动回复、营业时段</text>
          </view>
          <view class="quick-item" @tap="goBilling">
            <text class="q-title">套餐计费</text>
            <text class="q-desc">Trial / Starter / Pro</text>
          </view>
        </view>
      </view>

      <EmptyState
        v-if="showEmptyShop"
        class="sx-section"
        title="尚未绑定店铺"
        description="绑定 Shopee 店铺后，Webhook 消息才会进入收件箱并由 AI 自动回复。"
        cta-text="去绑定 Shopee"
        icon="店"
        @cta="goShops"
      />
    </view>
  </AppShell>
</template>

<script>
import AppShell from '@/components/merchant/AppShell.vue';
import KpiCard from '@/components/merchant/KpiCard.vue';
import EmptyState from '@/components/merchant/EmptyState.vue';
import { getDashboardKpis, getConnections, getSellerProfile } from '@/api/merchant.js';

export default {
  components: { AppShell, KpiCard, EmptyState },
  data() {
    return {
      nickname: '',
      currentDate: '',
      kpis: {
        todaySessions: null,
        autoResolveRate: null,
        pendingHandoff: null,
        connectedShops: null,
        sessionsTotal: null
      },
      connections: [],
      loaded: false
    };
  },
  computed: {
    greeting() {
      return this.nickname ? `你好，${this.nickname}` : '商家控制台';
    },
    autoRateDisplay() {
      if (this.kpis.autoResolveRate === null || this.kpis.autoResolveRate === undefined) return null;
      return `${this.kpis.autoResolveRate}%`;
    },
    autoRateHint() {
      if (this.kpis.autoResolveRate === null || this.kpis.autoResolveRate === undefined) {
        return '今日尚无已结束会话时显示 —';
      }
      return '今日已结束会话中纯 AI 占比';
    },
    shopConnected() {
      if (typeof this.kpis.connectedShops === 'number') {
        return this.kpis.connectedShops > 0;
      }
      return (this.connections || []).some((c) => c.isActive || c.IsActive);
    },
    shopStatusLabel() {
      if (!this.loaded) return null;
      if (typeof this.kpis.connectedShops === 'number') {
        return this.kpis.connectedShops > 0 ? `${this.kpis.connectedShops} 店` : '未绑定';
      }
      return this.shopConnected ? '已连接' : '未绑定';
    },
    shopHint() {
      if (!this.loaded) return '';
      return this.shopConnected ? '已绑定活跃店铺' : '请完成 OAuth 授权';
    },
    showEmptyShop() {
      return this.loaded && !this.shopConnected;
    }
  },
  onShow() {
    this.updateDate();
    this.load();
  },
  methods: {
    updateDate() {
      const now = new Date();
      this.currentDate = now.toLocaleDateString('zh-CN', {
        weekday: 'long',
        month: 'long',
        day: 'numeric'
      });
    },
    async load() {
      try {
        const profile = await getSellerProfile();
        this.nickname = profile?.nickname || profile?.Nickname || '';
      } catch (e) {
        /* request.js handles toast */
      }
      try {
        const conn = await getConnections();
        this.connections = conn?.items || [];
      } catch (e) {
        this.connections = [];
      }
      const kpis = await getDashboardKpis();
      this.kpis = {
        todaySessions: kpis.todaySessions,
        autoResolveRate: kpis.autoResolveRate,
        pendingHandoff: kpis.pendingHandoff,
        connectedShops: kpis.connectedShops,
        sessionsTotal: kpis.sessionsTotal
      };
      this.loaded = true;
    },
    barHeight(n) {
      // Decorative placeholder only — heights are fixed UI chrome, not metrics
      const heights = [36, 52, 44, 68, 40, 60, 48];
      return heights[(n - 1) % heights.length] + 'rpx';
    },
    goInbox() {
      uni.switchTab({ url: '/pages/merchant/sessions' });
    },
    goShops() {
      uni.switchTab({ url: '/pages/merchant/shops' });
    },
    goAi() {
      uni.navigateTo({ url: '/pages/merchant/ai-settings' });
    },
    goBilling() {
      uni.navigateTo({ url: '/pages/merchant/billing' });
    }
  }
};
</script>

<style lang="scss">
@import '../../styles/merchant.scss';

.chart-card {
  margin-top: 32rpx;
}

.chart-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 24rpx;
}

.chart-tag {
  font-size: 22rpx;
  color: #64748B;
  background: #F1F5F9;
  padding: 4rpx 14rpx;
  border-radius: 999rpx;
}

.chart-placeholder {
  background: #F8FAFC;
  border-radius: 16rpx;
  padding: 40rpx 24rpx 28rpx;
  border: 1rpx solid #E2E8F0;
}

.bars {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  height: 120rpx;
  gap: 12rpx;
  margin-bottom: 20rpx;
}

.bar {
  flex: 1;
  background: #BFDBFE;
  border-radius: 8rpx 8rpx 4rpx 4rpx;
  min-height: 24rpx;
}

.chart-note {
  display: block;
  text-align: center;
  font-size: 22rpx;
  color: #94A3B8;
  line-height: 1.5;
}

.quick-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 24rpx;
}

@media (min-width: 768px) {
  .quick-grid {
    grid-template-columns: repeat(4, 1fr);
  }
}

.quick-item {
  background: #fff;
  border: 1rpx solid #E2E8F0;
  border-radius: 16rpx;
  padding: 28rpx 24rpx;
  box-shadow: 0 2rpx 8rpx rgba(15, 23, 42, 0.04);
}

.q-title {
  display: block;
  font-size: 30rpx;
  font-weight: 650;
  color: #0F172A;
  margin-bottom: 8rpx;
}

.q-desc {
  font-size: 24rpx;
  color: #64748B;
  line-height: 1.4;
}
</style>
