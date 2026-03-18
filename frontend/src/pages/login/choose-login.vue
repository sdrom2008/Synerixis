<template>
  <view class="choose-login">
    <!-- 品牌区 -->
    <view class="brand">
      <image src="/static/logo.png" mode="widthFix" class="logo" />
      <br/>
      <text class="title">Synerixis</text>
      <br/>
      <text class="slogan">AI 智能伙伴</text>
    </view>

    <!-- 按钮区 -->
    <view class="options">
      <!-- 微信登录：只跳转页面 -->
      <button class="option-btn wechat" hover-class="btn-hover" @tap="goToWechatLogin">
        <text>微信一键登录</text>
        <text class="tag">推荐</text>
      </button>

      <button class="option-btn phone" hover-class="btn-hover" @tap="loginPhone">
        <text>手机号验证码登录</text>
      </button>
    </view>

    <!-- 协议 -->
    <view class="protocol">
      <checkbox size="24" :checked="agree" @change="toggleAgree" color="#3b82f6" />
      <text>我已阅读并同意</text>
      <text class="link" @tap="openUserProtocol">《用户协议》</text>
      <text>和</text>
      <text class="link" @tap="openPrivacy">《隐私政策》</text>
    </view>
  </view>
</template>

<script>
export default {
  data() {
    return {
      agree: true
    };
  },

  methods: {
    toggleAgree(e) {
      this.agree = e.detail.value;
    },

    openUserProtocol() {
      uni.navigateTo({ url: '/pages/protocol/user' });
    },

    openPrivacy() {
      uni.navigateTo({ url: '/pages/protocol/privacy' });
    },

    // 点击微信登录 → 跳转到微信登录专用页面
    goToWechatLogin() {
      if (!this.agree) {
        uni.showToast({ title: '请先同意协议', icon: 'none' });
        return;
      }
      
      uni.navigateTo({
        url: '/pages/login/wechat-login'   // ← 这里改成你实际的微信登录页面路径
      });
    },

    loginPhone() {
      if (!this.agree) {
        uni.showToast({ title: '请先同意协议', icon: 'none' });
        return;
      }
      uni.navigateTo({ url: '/pages/login/login' });   // 手机号登录页面路径
    }
  }
};
</script>

<style>
.choose-login {
  height: 100vh;
  background: #0a0e1a;
  padding-top: 140rpx;
  padding-bottom: 80rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: flex-start;
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

/* 动画 */
@keyframes brandFloat {
  0% { opacity: 0; transform: translateY(180rpx); }
  100% { opacity: 1; transform: translateY(0); }
}

@keyframes fadeIn {
  0% { opacity: 0; transform: translateY(40rpx); }
  100% { opacity: 1; transform: translateY(0); }
}
</style>