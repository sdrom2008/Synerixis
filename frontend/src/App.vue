<script setup lang="ts">
import { onLaunch, onShow } from '@dcloudio/uni-app';
import { getLanguage } from '@/utils/i18n.js';

onLaunch(() => {
  console.log('App Launch');
  getLanguage();
});

onShow(() => {
  console.log('App Show');
  updateTabBar();
});

/**
 * Tab 索引（pages.json）：
 * 0 仪表盘 · 1 收件箱 · 2 店铺 · 3 工作台 · 4 我的
 */
function updateTabBar() {
  const userType = uni.getStorageSync('userType') || '';
  const totalTabs = 5;

  for (let i = 0; i < totalTabs; i++) {
    try {
      uni.hideTabBarItem({ index: i });
    } catch (e) {
      /* ignore */
    }
  }

  if (!userType) return;

  let indexesToShow: number[] = [];
  if (userType === 'Seller') {
    indexesToShow = [0, 1, 2, 4];
  } else if (userType === 'Agent') {
    indexesToShow = [3, 4];
  } else if (userType === 'Supervisor') {
    indexesToShow = [0, 3, 4];
  }

  indexesToShow.forEach((idx) => {
    try {
      uni.showTabBarItem({ index: idx });
    } catch (e) {
      /* ignore */
    }
  });
}
</script>

<template>
  <view class="app-root">
    <slot />
  </view>
</template>

<style lang="scss">
.app-root {
  min-height: 100vh;
  background: #F8FAFC;
  box-sizing: border-box;
}
</style>
