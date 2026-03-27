<script setup lang="ts">
import { onLaunch, onShow, onHide } from '@dcloudio/uni-app';
import { getLanguage } from '@/utils/i18n.js';
import Footer from '@/components/Footer.vue';

onLaunch(() => {
  console.log('App Launch');
  // 初始化语言
  getLanguage();
});
onShow(() => {
  console.log('App Show');
});
onHide(() => {
  console.log('App Hide');
});
// 这里写全局逻辑
</script>

<template>
  <view class="app-container">
    <!-- 页面内容 -->
    <slot />
    <!-- 全局页脚 -->
    <Footer />
  </view>
</template>

<style>
.app-container {
  min-height: 100vh;
  padding-bottom: 120rpx; /* 为固定页脚留出空间 */
  box-sizing: border-box;
}
</style>