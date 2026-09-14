<template>
  <view class="detail-page">
    <view class="detail-header">
      <view class="back" @tap="goBack">← 返回</view>
      <view class="title-wrap">
        <text class="title">会话详情</text>
        <view v-if="handoff" class="sx-badge sx-badge-warning">待人工 / 已转接</view>
      </view>
    </view>

    <SessionTimeline :messages="messages" :handoff="handoff" />

    <view class="action-bar" v-if="canTransfer">
      <button class="sx-btn sx-btn-primary transfer" :loading="transferring" @tap="transferToAgent">
        转人工客服
      </button>
    </view>
  </view>
</template>

<script>
import SessionTimeline from '@/components/merchant/SessionTimeline.vue';
import { getSessionMessages, transferSession } from '@/api/merchant.js';

export default {
  components: { SessionTimeline },
  data() {
    return {
      messages: [],
      sessionId: '',
      sessionStatus: '',
      transferring: false
    };
  },
  computed: {
    handoff() {
      return this.sessionStatus === 'Pending';
    },
    canTransfer() {
      return this.sessionStatus === 'Pending' || this.sessionStatus === 'Active';
    }
  },
  onLoad(query) {
    this.sessionId = query?.id || '';
    if (this.sessionId) this.loadMessages();
  },
  methods: {
    goBack() {
      uni.navigateBack({ fail: () => uni.switchTab({ url: '/pages/merchant/sessions' }) });
    },
    async loadMessages() {
      try {
        const data = await getSessionMessages(this.sessionId);
        this.messages = Array.isArray(data) ? data : data?.items || [];
        // Prefer explicit status if API adds it later; fallback from list context
        this.sessionStatus = data?.sessionStatus || this.sessionStatus || 'Active';
      } catch (e) {
        this.messages = [];
      }
    },
    async transferToAgent() {
      this.transferring = true;
      try {
        await transferSession(this.sessionId);
        uni.showToast({ title: '已转人工', icon: 'success' });
        this.sessionStatus = 'Pending';
      } catch (e) {
        /* handled */
      } finally {
        this.transferring = false;
      }
    }
  }
};
</script>

<style lang="scss">
@import '../../styles/merchant.scss';

.detail-page {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #F8FAFC;
}

.detail-header {
  padding: 24rpx 32rpx;
  background: #fff;
  border-bottom: 1rpx solid #E2E8F0;
}

.back {
  font-size: 26rpx;
  color: #2563EB;
  margin-bottom: 12rpx;
}

.title-wrap {
  display: flex;
  align-items: center;
  gap: 16rpx;
}

.title {
  font-size: 34rpx;
  font-weight: 700;
  color: #0F172A;
}

.action-bar {
  padding: 20rpx 32rpx calc(20rpx + env(safe-area-inset-bottom));
  background: #fff;
  border-top: 1rpx solid #E2E8F0;
}

.transfer {
  width: 100%;
}
</style>
