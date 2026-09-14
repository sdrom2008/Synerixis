<template>
  <view class="kpi-card">
    <text class="kpi-label">{{ label }}</text>
    <view class="kpi-value-row">
      <text class="kpi-value">{{ displayValue }}</text>
      <text v-if="unit && hasValue" class="kpi-unit">{{ unit }}</text>
    </view>
    <text v-if="hint" class="kpi-hint">{{ hint }}</text>
  </view>
</template>

<script>
export default {
  name: 'KpiCard',
  props: {
    label: { type: String, required: true },
    value: { type: [String, Number], default: null },
    unit: { type: String, default: '' },
    hint: { type: String, default: '' }
  },
  computed: {
    hasValue() {
      return this.value !== null && this.value !== undefined && this.value !== '';
    },
    displayValue() {
      // Honest empty: never fake production metrics
      return this.hasValue ? String(this.value) : '—';
    }
  }
};
</script>

<style scoped lang="scss">
@import '../../styles/tokens.scss';

.kpi-card {
  background: $sx-surface;
  border: 1rpx solid $sx-border;
  border-radius: $sx-radius-lg;
  padding: $sx-space-4;
  box-shadow: $sx-shadow-sm;
  min-height: 160rpx;
  box-sizing: border-box;
}

.kpi-label {
  display: block;
  font-size: $sx-font-sm;
  color: $sx-muted;
  margin-bottom: $sx-space-2;
}

.kpi-value-row {
  display: flex;
  align-items: baseline;
  gap: 8rpx;
}

.kpi-value {
  font-size: $sx-font-kpi;
  font-weight: 700;
  color: $sx-ink;
  letter-spacing: -1rpx;
  line-height: 1.1;
}

.kpi-unit {
  font-size: $sx-font-sm;
  color: $sx-muted;
}

.kpi-hint {
  display: block;
  margin-top: $sx-space-2;
  font-size: $sx-font-xs;
  color: $sx-muted;
}
</style>
