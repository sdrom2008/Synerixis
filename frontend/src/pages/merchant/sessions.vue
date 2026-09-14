<template>
  <AppShell active="inbox">
    <view class="sx-page sx-page-wide inbox-page">
      <view class="sx-page-header">
        <view>
          <view class="sx-page-title">收件箱</view>
          <view class="sx-page-sub">
            AI 草稿需人工确认后发送 · 默认不自动出站到 Shopee/TikTok
          </view>
        </view>
        <view class="filters">
          <view
            v-for="f in filters"
            :key="f.value"
            class="filter-chip"
            :class="{ active: statusFilter === f.value }"
            @tap="setFilter(f.value)"
          >
            {{ f.label }}
            <text v-if="f.value === 'draft' && pendingDraftCount > 0" class="chip-badge">
              {{ pendingDraftCount }}
            </text>
          </view>
        </view>
      </view>

      <view v-if="pendingDraftCount > 0" class="draft-banner">
        <text class="draft-banner-text">待发送草稿 {{ pendingDraftCount }} 条 — 请尽快审核发送，以免超时影响店铺响应表现</text>
      </view>

      <view v-if="loading" class="loading">加载中…</view>

      <EmptyState
        v-else-if="!displaySessions.length"
        title="暂无会话"
        description="绑定店铺并有买家进线后，会话将出现在这里。AI 会生成草稿，需您确认后才会发到平台。"
        cta-text="查看店铺绑定"
        icon="信"
        @cta="goShops"
      />

      <view v-else class="session-list">
        <view
          v-for="conv in displaySessions"
          :key="conv.id"
          class="session-item"
          @tap="viewSession(conv.id)"
        >
          <view class="row-top">
            <text class="customer">{{ conv.customerName || '匿名买家' }}</text>
            <text class="time">{{ formatTime(conv.lastActiveAt || conv.createdAt) }}</text>
          </view>
          <view class="row-mid">
            <text class="platform">{{ conv.platform || '—' }}</text>
            <view class="sx-badge" :class="statusBadgeClass(conv.status)">
              {{ statusLabel(conv.status) }}
            </view>
            <view v-if="conv.hasPendingDraft" class="sx-badge sx-badge-warning">待发送草稿</view>
            <view v-if="isHandoff(conv)" class="sx-badge sx-badge-warning">待人工</view>
            <view v-if="isOverdue(conv)" class="sx-badge sx-badge-danger">即将超时</view>
          </view>
          <view class="row-bottom">
            <text class="agent">
              {{ conv.assignedAgent?.name || conv.assignedAgent?.Name || '未分配坐席' }}
            </text>
            <text class="counts">
              买家等待 {{ formatHours(conv.hoursSinceLastBuyerMsg) }} · 消息 {{ conv.messageCount ?? '—' }}
            </text>
          </view>
        </view>
      </view>
    </view>
  </AppShell>
</template>

<script>
import AppShell from '@/components/merchant/AppShell.vue';
import EmptyState from '@/components/merchant/EmptyState.vue';
import { getSessions } from '@/api/merchant.js';

export default {
  components: { AppShell, EmptyState },
  data() {
    return {
      sessions: [],
      pendingDraftCount: 0,
      loading: false,
      statusFilter: '',
      filters: [
        { label: '全部', value: '' },
        { label: '待发送草稿', value: 'draft' },
        { label: '待人工', value: 'Pending' },
        { label: '进行中', value: 'Active' },
        { label: '已解决', value: 'Resolved' }
      ]
    };
  },
  computed: {
    displaySessions() {
      if (this.statusFilter === 'draft') {
        return this.sessions.filter((s) => s.hasPendingDraft);
      }
      return this.sessions;
    }
  },
  onShow() {
    this.loadSessions();
  },
  methods: {
    setFilter(v) {
      this.statusFilter = v;
      this.loadSessions();
    },
    async loadSessions() {
      const token = uni.getStorageSync('token');
      if (!token) {
        uni.showToast({ title: '请先登录', icon: 'none' });
        uni.reLaunch({ url: '/pages/login/choose-login' });
        return;
      }
      this.loading = true;
      try {
        const apiStatus =
          this.statusFilter && this.statusFilter !== 'draft' ? this.statusFilter : undefined;
        const data = await getSessions({ status: apiStatus });
        this.sessions = data?.items || [];
        this.pendingDraftCount =
          data?.pendingDraftCount ?? this.sessions.filter((s) => s.hasPendingDraft).length;
      } catch (e) {
        this.sessions = [];
        this.pendingDraftCount = 0;
      } finally {
        this.loading = false;
      }
    },
    viewSession(id) {
      uni.navigateTo({ url: `/pages/merchant/session-detail?id=${id}` });
    },
    goShops() {
      uni.switchTab({ url: '/pages/merchant/shops' });
    },
    formatTime(dateStr) {
      if (!dateStr) return '';
      const d = new Date(dateStr);
      if (Number.isNaN(d.getTime())) return '';
      const pad = (n) => String(n).padStart(2, '0');
      return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    },
    formatHours(h) {
      if (h == null || Number.isNaN(Number(h))) return '—';
      const n = Number(h);
      if (n < 1) return `${Math.round(n * 60)} 分钟`;
      return `${n.toFixed(1)} 小时`;
    },
    statusLabel(status) {
      const map = {
        Pending: '待处理',
        Active: '进行中',
        Resolved: '已解决',
        Closed: '已关闭'
      };
      return map[status] || status || '—';
    },
    statusBadgeClass(status) {
      if (status === 'Pending') return 'sx-badge-warning';
      if (status === 'Active') return 'sx-badge-info';
      if (status === 'Resolved') return 'sx-badge-success';
      return 'sx-badge-muted';
    },
    isHandoff(conv) {
      return conv.status === 'Pending' || (!conv.assignedAgent && conv.status === 'Active');
    },
    isOverdue(conv) {
      const h = Number(conv.hoursSinceLastBuyerMsg);
      return !Number.isNaN(h) && h >= 8;
    }
  }
};
</script>

<style lang="scss">
@import '../../styles/merchant.scss';

.filters {
  display: flex;
  flex-wrap: wrap;
  gap: 12rpx;
}

.filter-chip {
  padding: 10rpx 22rpx;
  border-radius: 999rpx;
  font-size: 24rpx;
  color: #64748B;
  background: #fff;
  border: 1rpx solid #E2E8F0;
  display: flex;
  align-items: center;
  gap: 8rpx;
}

.filter-chip.active {
  background: #EFF6FF;
  color: #2563EB;
  border-color: #BFDBFE;
  font-weight: 600;
}

.chip-badge {
  min-width: 28rpx;
  padding: 0 8rpx;
  border-radius: 999rpx;
  background: #F59E0B;
  color: #fff;
  font-size: 20rpx;
  text-align: center;
  line-height: 1.4;
}

.draft-banner {
  margin-bottom: 20rpx;
  padding: 20rpx 24rpx;
  background: #FFFBEB;
  border: 1rpx solid #FDE68A;
  border-radius: 12rpx;
}

.draft-banner-text {
  font-size: 24rpx;
  color: #92400E;
  line-height: 1.5;
}

.loading {
  text-align: center;
  color: #94A3B8;
  padding: 80rpx 0;
}

.session-list {
  display: flex;
  flex-direction: column;
  gap: 20rpx;
}

.session-item {
  background: #fff;
  border: 1rpx solid #E2E8F0;
  border-radius: 16rpx;
  padding: 28rpx 24rpx;
  box-shadow: 0 2rpx 8rpx rgba(15, 23, 42, 0.04);
}

.row-top,
.row-mid,
.row-bottom {
  display: flex;
  align-items: center;
  gap: 12rpx;
}

.row-top {
  justify-content: space-between;
  margin-bottom: 12rpx;
}

.customer {
  font-size: 32rpx;
  font-weight: 650;
  color: #0F172A;
}

.time {
  font-size: 22rpx;
  color: #94A3B8;
}

.row-mid {
  margin-bottom: 12rpx;
  flex-wrap: wrap;
}

.platform {
  font-size: 22rpx;
  color: #64748B;
  background: #F1F5F9;
  padding: 4rpx 12rpx;
  border-radius: 8rpx;
}

.row-bottom {
  justify-content: space-between;
}

.agent,
.counts {
  font-size: 24rpx;
  color: #64748B;
}

.sx-badge-danger {
  background: #FEE2E2;
  color: #B91C1C;
}
</style>
