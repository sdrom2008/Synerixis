<template>
  <view class="site-footer">
    <text class="icp">{{ t('common.icp') }}</text>
    <!-- 公安备案号出来后添加：<text class="police">{{ t('common.police') }} 123456789号</text> -->
  </view>
</template>

<script setup>
import { t } from '@/utils/i18n.js';
</script>

<style scoped>
.site-footer {
  padding: 20rpx 0;
  text-align: center;
  font-size: 24rpx;
  color: #94a3b8;
  background: #0a0e1a;
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  z-index: 999;
}

.icp, .police {
  margin: 0 10rpx;
}
</style>
