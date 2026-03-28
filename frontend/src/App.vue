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
  // 动态 TabBar：根据 userType 显示不同菜单
  updateTabBar();
});

function updateTabBar() {
  const userType = uni.getStorageSync('userType') || '';
  const totalTabs = 5; // 定义了5个Tab

  // 先隐藏所有 Tab
  for (let i = 0; i < totalTabs; i++) {
    try {
      uni.hideTabBarItem({ index: i });
    } catch (e) {
      // ignore errors (e.g., item already hidden or not ready)
    }
  }

  if (!userType) {
    // 未登录，全部隐藏
    return;
  }

  let indexesToShow: number[] = [];
  if (userType === 'Seller') {
    indexesToShow = [0, 1, 2, 4]; // 会话、商品、仪表盘、我的
  } else if (userType === 'Agent') {
    indexesToShow = [3, 4]; // 工作台、我的
  } else if (userType === 'Supervisor') {
    indexesToShow = [2, 3, 4]; // 仪表盘、工作台、我的
  }

  // 显示对应 Tab
  indexesToShow.forEach(idx => {
    try {
      uni.showTabBarItem({ index: idx });
    } catch (e) {
      // ignore
    }
  });
}
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
