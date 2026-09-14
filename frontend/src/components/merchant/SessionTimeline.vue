<template>
  <scroll-view scroll-y class="timeline" :scroll-into-view="scrollInto">
    <view v-if="!messages || !messages.length" class="timeline-empty">
      <text>暂无消息</text>
    </view>
    <view
      v-for="(msg, idx) in messages"
      :id="'tl-' + (msg.id || idx)"
      :key="msg.id || idx"
      class="msg-row"
      :class="rowClass(msg)"
    >
      <view class="meta">
        <text class="sender">{{ senderLabel(msg) }}</text>
        <text class="time">{{ formatTime(msg.createdAt) }}</text>
      </view>
      <view class="bubble">
        <text class="content">{{ msg.content }}</text>
      </view>
    </view>
  </scroll-view>
</template>

<script>
export default {
  name: 'SessionTimeline',
  props: {
    messages: { type: Array, default: () => [] },
    handoff: { type: Boolean, default: false }
  },
  computed: {
    scrollInto() {
      if (!this.messages || !this.messages.length) return '';
      const last = this.messages[this.messages.length - 1];
      return 'tl-' + (last.id || this.messages.length - 1);
    }
  },
  methods: {
    rowClass(msg) {
      const t = (msg.senderType || '').toLowerCase();
      if (t === 'customer') return 'from-customer';
      if (t === 'agent') return 'from-agent';
      return 'from-system';
    },
    senderLabel(msg) {
      const t = msg.senderType;
      if (t === 'Customer') return '买家';
      if (t === 'Agent') return '客服 / AI';
      if (t === 'System') return '系统';
      return t || '消息';
    },
    formatTime(dateStr) {
      if (!dateStr) return '';
      const d = new Date(dateStr);
      if (Number.isNaN(d.getTime())) return '';
      const pad = (n) => String(n).padStart(2, '0');
      return `${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
    }
  }
};
</script>

<style scoped lang="scss">
@import '../../styles/tokens.scss';

.timeline {
  flex: 1;
  height: 100%;
  padding: $sx-space-3;
  box-sizing: border-box;
}

.timeline-empty {
  text-align: center;
  color: $sx-muted;
  padding: 120rpx 0;
  font-size: $sx-font-sm;
}

.msg-row {
  margin-bottom: $sx-space-3;
  display: flex;
  flex-direction: column;
  max-width: 85%;
}

.msg-row.from-customer {
  align-items: flex-start;
  align-self: flex-start;
}

.msg-row.from-agent {
  align-items: flex-end;
  margin-left: auto;
}

.msg-row.from-system {
  align-items: center;
  margin-left: auto;
  margin-right: auto;
  max-width: 90%;
}

.meta {
  display: flex;
  gap: $sx-space-2;
  margin-bottom: 6rpx;
  font-size: $sx-font-xs;
  color: $sx-muted;
}

.bubble {
  padding: 20rpx 24rpx;
  border-radius: $sx-radius-md;
  border: 1rpx solid $sx-border;
  background: $sx-surface;
}

.from-customer .bubble {
  background: $sx-surface;
  border-color: $sx-border;
}

.from-agent .bubble {
  background: $sx-primary-soft;
  border-color: #BFDBFE;
}

.from-system .bubble {
  background: #F1F5F9;
  border-style: dashed;
}

.content {
  font-size: $sx-font-base;
  color: $sx-ink;
  line-height: 1.55;
  word-break: break-word;
}
</style>
