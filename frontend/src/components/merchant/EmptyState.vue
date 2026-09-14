<template>
  <view class="empty-state">
    <view class="empty-icon-wrap">
      <text class="empty-icon">{{ icon }}</text>
    </view>
    <text class="empty-title">{{ title }}</text>
    <text v-if="description" class="empty-desc">{{ description }}</text>
    <button
      v-if="ctaText"
      class="sx-btn sx-btn-primary empty-cta"
      @tap="$emit('cta')"
    >
      {{ ctaText }}
    </button>
  </view>
</template>

<script>
export default {
  name: 'EmptyState',
  props: {
    title: { type: String, default: '暂无数据' },
    description: { type: String, default: '' },
    ctaText: { type: String, default: '' },
    icon: { type: String, default: '○' }
  },
  emits: ['cta']
};
</script>

<style scoped lang="scss">
@import '../../styles/tokens.scss';

.empty-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding: 80rpx $sx-space-4;
  background: $sx-surface;
  border: 1rpx dashed $sx-border;
  border-radius: $sx-radius-lg;
}

.empty-icon-wrap {
  width: 96rpx;
  height: 96rpx;
  border-radius: 50%;
  background: $sx-primary-soft;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: $sx-space-3;
}

.empty-icon {
  font-size: 40rpx;
  color: $sx-primary;
}

.empty-title {
  font-size: $sx-font-lg;
  font-weight: 650;
  color: $sx-ink;
}

.empty-desc {
  margin-top: $sx-space-2;
  font-size: $sx-font-sm;
  color: $sx-muted;
  line-height: 1.5;
  max-width: 520rpx;
}

.empty-cta {
  margin-top: $sx-space-4;
  min-width: 280rpx;
}
</style>
