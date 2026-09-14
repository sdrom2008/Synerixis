<template>
  <view class="splash">
    <view class="mark">S</view>
    <text class="title">Synerixis</text>
    <text class="slogan">跨境电商 AI 客服</text>
  </view>
</template>

<script>
export default {
  onLoad() {
    const token = uni.getStorageSync('token');
    const userType = uni.getStorageSync('userType');
    setTimeout(() => {
      if (token && userType === 'Seller') {
        uni.reLaunch({ url: '/pages/dashboard/dashboard' });
      } else if (token && (userType === 'Agent' || userType === 'Supervisor')) {
        uni.reLaunch({ url: '/pages/support/workbench' });
      } else {
        uni.reLaunch({ url: '/pages/login/choose-login' });
      }
    }, 900);
  }
};
</script>

<style>
.splash {
  height: 100vh;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  background: #F8FAFC;
}

.mark {
  width: 112rpx;
  height: 112rpx;
  border-radius: 28rpx;
  background: #2563EB;
  color: #fff;
  font-size: 56rpx;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
}

.title {
  margin-top: 32rpx;
  font-size: 44rpx;
  font-weight: 700;
  color: #0F172A;
}

.slogan {
  margin-top: 12rpx;
  font-size: 26rpx;
  color: #64748B;
}
</style>
