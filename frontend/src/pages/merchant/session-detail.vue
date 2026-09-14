<template>
  <view class="detail-page">
    <view class="detail-header">
      <view class="back" @tap="goBack">← 返回</view>
      <view class="title-wrap">
        <text class="title">会话详情</text>
        <view v-if="handoff" class="sx-badge sx-badge-warning">待人工 / 已停 AI 草稿</view>
        <view v-if="draft" class="sx-badge sx-badge-warning">
          {{ draft.status === 'Superseded' || draft.Status === 'Superseded' ? '旧草稿可发送' : '待发送草稿' }}
        </view>
        <view v-if="slaUrgency === 'overdue'" class="sx-badge sx-badge-danger">已超时</view>
        <view v-else-if="slaUrgency === 'soon'" class="sx-badge sx-badge-soon">即将超时</view>
      </view>
      <view v-if="handoff" class="handoff-hint">
        已转人工：入站消息不再生成新 AI 草稿，也不 AutoSend；下方旧草稿仍可编辑后手动发送。
      </view>
      <view v-if="hoursSince != null" class="meta">
        买家等待约 {{ formatHours(hoursSince) }}
        <text v-if="needsResponseBy"> · 建议回复截止 {{ formatTime(needsResponseBy) }}</text>
        <text v-if="responseSlaHours"> · SLA {{ responseSlaHours }}h</text>
      </view>
    </view>

    <SessionTimeline :messages="messages" :handoff="handoff" />

    <view v-if="draft" class="draft-panel">
      <view class="draft-title">AI 草稿（未发到平台）</view>
      <text class="draft-hint">
        确认无误后再发送。自动生成的草稿不计入平台「真人坐席响应率」；请勿宣称无人值守自动回信。
      </text>
      <textarea
        v-if="editing"
        class="draft-editor"
        v-model="editContent"
        :maxlength="-1"
        auto-height
      />
      <view v-else class="draft-body">{{ draft.content || draft.Content }}</view>
      <view class="draft-actions">
        <button class="sx-btn sx-btn-ghost" size="mini" @tap="toggleEdit">
          {{ editing ? '取消编辑' : '编辑' }}
        </button>
        <button class="sx-btn sx-btn-ghost danger" size="mini" :loading="busy" @tap="onDiscard">
          丢弃
        </button>
        <button class="sx-btn sx-btn-primary" size="mini" :loading="busy" @tap="onSend">
          {{ editing ? '保存并发送' : '发送到平台' }}
        </button>
      </view>
    </view>

    <view class="action-bar" v-if="canTransfer">
      <button class="sx-btn sx-btn-primary transfer" :loading="transferring" @tap="transferToAgent">
        转人工客服
      </button>
    </view>
  </view>
</template>

<script>
import SessionTimeline from '@/components/merchant/SessionTimeline.vue';
import {
  getSessionMessages,
  transferSession,
  approveDraft,
  editAndSendDraft,
  discardDraft,
  updateDraft
} from '@/api/merchant.js';

export default {
  components: { SessionTimeline },
  data() {
    return {
      messages: [],
      sessionId: '',
      sessionStatus: '',
      transferring: false,
      draft: null,
      editing: false,
      editContent: '',
      busy: false,
      hoursSince: null,
      needsResponseBy: null,
      pendingHumanHandoff: false,
      slaUrgency: 'ok',
      responseSlaHours: 12
    };
  },
  computed: {
    handoff() {
      return !!this.pendingHumanHandoff;
    },
    canTransfer() {
      return (
        !this.pendingHumanHandoff &&
        (this.sessionStatus === 'Pending' || this.sessionStatus === 'Active')
      );
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
        if (Array.isArray(data)) {
          this.messages = data;
        } else {
          this.messages = data?.items || [];
          this.sessionStatus = data?.sessionStatus || this.sessionStatus || 'Active';
          this.pendingHumanHandoff = !!(
            data?.pendingHumanHandoff ?? data?.PendingHumanHandoff
          );
          this.draft = data?.pendingDraft || null;
          this.hoursSince = data?.hoursSinceLastBuyerMsg ?? null;
          this.needsResponseBy = data?.needsResponseBy || null;
          this.slaUrgency = data?.slaUrgency || 'ok';
          this.responseSlaHours = data?.responseSlaHours || 12;
        }
        if (this.draft) {
          this.editContent = this.draft.content || this.draft.Content || '';
        }
      } catch (e) {
        this.messages = [];
      }
    },
    toggleEdit() {
      if (!this.editing && this.draft) {
        this.editContent = this.draft.content || this.draft.Content || '';
      }
      this.editing = !this.editing;
    },
    async onSend() {
      this.busy = true;
      try {
        if (this.editing) {
          await editAndSendDraft(this.sessionId, this.editContent);
        } else {
          await approveDraft(this.sessionId);
        }
        uni.showToast({ title: '已发送到平台', icon: 'success' });
        this.draft = null;
        this.editing = false;
        await this.loadMessages();
      } catch (e) {
        uni.showToast({ title: '发送失败', icon: 'none' });
      } finally {
        this.busy = false;
      }
    },
    async onDiscard() {
      this.busy = true;
      try {
        await discardDraft(this.sessionId);
        uni.showToast({ title: '草稿已丢弃', icon: 'none' });
        this.draft = null;
        this.editing = false;
      } catch (e) {
        /* handled */
      } finally {
        this.busy = false;
      }
    },
    async transferToAgent() {
      this.transferring = true;
      try {
        await transferSession(this.sessionId);
        uni.showToast({ title: '已转人工，AI 草稿已停', icon: 'success' });
        this.sessionStatus = 'Pending';
        this.pendingHumanHandoff = true;
        await this.loadMessages();
      } catch (e) {
        /* handled */
      } finally {
        this.transferring = false;
      }
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
  flex-wrap: wrap;
}

.title {
  font-size: 34rpx;
  font-weight: 700;
  color: #0F172A;
}

.meta {
  margin-top: 12rpx;
  font-size: 22rpx;
  color: #64748B;
}

.handoff-hint {
  margin-top: 12rpx;
  font-size: 22rpx;
  color: #92400E;
  background: #FFFBEB;
  padding: 12rpx 16rpx;
  border-radius: 8rpx;
  line-height: 1.45;
}

.sx-badge-danger {
  background: #FEE2E2;
  color: #B91C1C;
}

.sx-badge-soon {
  background: #FFEDD5;
  color: #C2410C;
}

.draft-panel {
  margin: 16rpx 24rpx;
  padding: 24rpx;
  background: #FFFBEB;
  border: 1rpx solid #FDE68A;
  border-radius: 16rpx;
}

.draft-title {
  font-size: 28rpx;
  font-weight: 650;
  color: #92400E;
  margin-bottom: 8rpx;
}

.draft-hint {
  display: block;
  font-size: 22rpx;
  color: #A16207;
  line-height: 1.5;
  margin-bottom: 16rpx;
}

.draft-body,
.draft-editor {
  width: 100%;
  min-height: 120rpx;
  padding: 16rpx;
  background: #fff;
  border-radius: 12rpx;
  border: 1rpx solid #FDE68A;
  font-size: 28rpx;
  color: #0F172A;
  line-height: 1.55;
  box-sizing: border-box;
}

.draft-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 12rpx;
  margin-top: 20rpx;
  justify-content: flex-end;
}

.danger {
  color: #B91C1C !important;
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
