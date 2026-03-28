<template>
  <view class="choose-login">
    <!-- 语言切换悬浮按钮 -->
    <view class="lang-switch" @tap="switchLanguage">
      <text class="lang-text">{{ currentLang === 'zh-CN' ? 'EN' : '中' }}</text>
    </view>

    <!-- 品牌区 -->
    <view class="brand">
      <image src="/static/logo.png" mode="widthFix" class="logo" />
      <br/>
      <text class="title">Synerixis</text>
      <br/>
      <text class="slogan">{{ t('login.title', currentLang) }}</text>
    </view>

    <!-- 按钮区 -->
    <view class="options">
      <!-- 微信登录：只跳转页面 -->
      <!-- #ifdef MP-WEIXIN -->
      <button class="option-btn wechat" hover-class="btn-hover" @tap="goToWechatLogin">
        <text>{{ t('login.wechatLogin', currentLang) }}</text>
        <text class="tag">推荐</text>
      </button>
      <!-- #endif -->

      <button class="option-btn phone" hover-class="btn-hover" @tap="loginPhone">
        <text>{{ t('login.phoneLogin', currentLang) }}</text>
      </button>
    </view>

    <!-- 协议 -->
    <view class="protocol">
      <checkbox size="24" :checked="agree" @change="toggleAgree" color="#3b82f6" />
      <text>{{ t('common.agree', currentLang) }} </text>
      <text class="link" @tap="openUserProtocol">《{{ t('common.terms', currentLang) }}》</text>
      <text>{{ t('common.and', currentLang) }} </text>
      <text class="link" @tap="openPrivacy">《{{ t('common.privacy', currentLang) }}》</text>
    </view>

    <!-- 页脚备案信息 (H5和微信小程序都显示) -->
    <view class="footer">
      <text class="icp">湘ICP备2026009564号</text>
      <text class="copyright">© 2024-2026 Synerixis All Rights Reserved</text>
    </view>
  </view>
</template>

<script>
import { t, getLanguage, setLanguage } from '@/utils/i18n.js';

export default {
  data() {
    return {
      agree: true,
      currentLang: getLanguage()
    };
  },

  onShow() {
    // 返回到此页面时，同步当前语言
    this.currentLang = getLanguage();
  },

  methods: {
    // 封装一层以确保 currentLang 作为 Vue 的响应式依赖被追踪
    t(key) {
      const _lang = this.currentLang;
      return t(key);
    },

    toggleAgree(e) {
      this.agree = e.detail.value;
    },

    openUserProtocol() {
      uni.navigateTo({ url: '/pages/protocol/user' });
    },

    openPrivacy() {
      uni.navigateTo({ url: '/pages/protocol/privacy' });
    },

    goToWechatLogin() {
      if (!this.agree) {
        uni.showToast({ title: this.t('common.agree') + '?', icon: 'none' });
        return;
      }
      uni.navigateTo({
        url: '/pages/login/wechat-login'
      });
    },

    loginPhone() {
      if (!this.agree) {
        uni.showToast({ title: this.t('common.agree') + '?', icon: 'none' });
        return;
      }
      uni.navigateTo({ url: '/pages/login/login' });
    },

    switchLanguage() {
      const newLang = this.currentLang === 'zh-CN' ? 'en-US' : 'zh-CN';
      setLanguage(newLang); // 存入本地存储并修改内部状态
      this.currentLang = newLang; // 触发视图重新渲染，且后续所有页面加载时会取到最新语言
    }
  }
};
</script>

<style>
.choose-login {
  min-height: 100vh;
  background: #0a0e1a;
  padding-top: 140rpx;
  padding-bottom: 120rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;
  position: relative;
  box-sizing: border-box;
}

.lang-switch {
  position: absolute;
  top: 80rpx;
  right: 40rpx;
  width: 64rpx;
  height: 64rpx;
  border-radius: 32rpx;
  background: rgba(255, 255, 255, 0.1);
  display: flex;
  align-items: center;
  justify-content: center;
  border: 1px solid rgba(255, 255, 255, 0.2);
  z-index: 10;
}

.lang-text {
  font-size: 24rpx;
  color: #94a3b8;
  font-weight: bold;
}

.brand {
  text-align: center;
  opacity: 0;
  transform: translateY(180rpx);
  animation: brandFloat 1.2s ease-out forwards;
}

.logo {
  width: 220rpx;
  height: 220rpx;
  margin-bottom: 0;
}

.title {
  font-size: 64rpx;
  font-weight: bold;
  color: #22c55e;
  margin: 0;
}

.slogan {
  font-size: 32rpx;
  color: #64748b;
  margin: 0;
}

.options {
  width: 80%;
  margin-top: 240rpx;
  opacity: 0;
  animation: fadeIn 0.8s ease-out 1.2s forwards;
}

.option-btn {
  width: 100%;
  height: 88rpx;
  line-height: 88rpx;
  border-radius: 44rpx;
  font-size: 32rpx;
  margin-bottom: 40rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 6rpx 24rpx rgba(0,0,0,0.4);
  transition: all 0.25s;
  position: relative;
}

.wechat {
  background: linear-gradient(90deg, #22c55e, #16a34a);
  color: white;
}

.phone {
  background: #3b82f6;
  color: white;
}

.tag {
  position: absolute;
  top: 6rpx;
  right: 16rpx;
  background: #fbbf24;
  color: #854d0e;
  font-size: 20rpx;
  padding: 2rpx 12rpx;
  border-radius: 20rpx;
}

.protocol {
  margin-top: 40rpx;
  font-size: 24rpx;
  color: #94a3b8;
  text-align: center;
}

.footer {
  position: absolute;
  bottom: 40rpx;
  width: 100%;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  opacity: 0.5;
}

.footer .icp, .footer .copyright {
  font-size: 20rpx;
  color: #64748b;
  margin-top: 8rpx;
}

@keyframes brandFloat {
  0% { opacity: 0; transform: translateY(180rpx); }
  100% { opacity: 1; transform: translateY(0); }
}

@keyframes fadeIn {
  0% { opacity: 0; transform: translateY(40rpx); }
  100% { opacity: 1; transform: translateY(0); }
}
</style>
