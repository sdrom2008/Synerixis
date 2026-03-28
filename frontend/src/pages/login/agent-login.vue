<template>
  <view class="agent-login">
    <view class="header">
      <image src="/static/logo.png" mode="widthFix" class="logo" /><br/>
      <text class="title">{{ currentLang === 'zh-CN' ? '客服系统登录' : 'Agent Login' }}</text><br/>
      <text class="subtitle">{{ currentLang === 'zh-CN' ? '仅供客服和主管使用' : 'For Agents and Supervisors Only' }}</text>
      <view class="header-actions">
        <text class="lang-switch" @tap="switchLanguage">{{ currentLang === 'zh-CN' ? 'English' : '中文' }}</text>
        <text class="back" @tap="backToChoose">← {{ currentLang === 'zh-CN' ? '返回' : 'Back' }}</text>
      </view>
    </view>

    <view class="form">
      <input 
        v-model="email" 
        :placeholder="currentLang === 'zh-CN' ? '请输入邮箱账号' : 'Enter email address'" 
        type="text" 
        class="input" 
      />

      <input 
        v-model="password" 
        :placeholder="currentLang === 'zh-CN' ? '请输入密码' : 'Enter password'" 
        type="password" 
        class="input" 
      />

      <button 
        class="login-btn" 
        @tap="handleAgentLogin" 
        :loading="loginLoading"
        :disabled="loginLoading"
      >
        {{ currentLang === 'zh-CN' ? '立即登录' : 'Login' }}
      </button>
    </view>
  </view>
</template>

<script>
import { request } from '@/utils/request.js';
import { t, getLanguage, setLanguage } from '@/utils/i18n.js';

export default {
  data() {
    return {
      email: '',
      password: '',
      currentLang: getLanguage(),
      loginLoading: false
    };
  },

  methods: {
    t(key) {
      const _ = this.currentLang;
      return t(key);
    },

    backToChoose() {
      uni.navigateBack();
    },

    switchLanguage() {
      const newLang = this.currentLang === 'zh-CN' ? 'en-US' : 'zh-CN';
      setLanguage(newLang);
      this.currentLang = newLang;
    },

    async handleAgentLogin() {
      if (!this.email) {
        uni.showToast({ title: this.currentLang === 'zh-CN' ? '邮箱不能为空' : 'Email is required', icon: 'none' });
        return;
      }
      if (!this.password) {
        uni.showToast({ title: this.currentLang === 'zh-CN' ? '密码不能为空' : 'Password is required', icon: 'none' });
        return;
      }

      this.loginLoading = true;

      try {
        const res = await request({
          url: '/api/auth/agent-login',
          method: 'POST',
          data: {
            Email: this.email,
            Password: this.password
          }
        });

        // 存储相关信息
        uni.setStorageSync('token', res.token);
        uni.setStorageSync('userId', res.agentId);
        uni.setStorageSync('userType', res.role);
        uni.setStorageSync('nickname', res.name);
        uni.setStorageSync('shopId', res.shopId);

        uni.showToast({ title: this.currentLang === 'zh-CN' ? '登录成功' : 'Login successful', icon: 'success' });

        setTimeout(() => {
          uni.switchTab({ url: '/pages/support/workbench' });
        }, 1000);
      } catch (err) {
        // request.js 已经有错误提示
      } finally {
        this.loginLoading = false;
      }
    }
  }
};
</script>

<style>
.agent-login {
  height: 100vh;
  background: #0a0e1a;
  padding: 120rpx 40rpx;
}

.header {
  text-align: center;
  margin-bottom: 80rpx;
  position: relative;
  padding-top: 40rpx;
}

.logo {
  width: 160rpx;
  height: 160rpx;
}

.title {
  font-size: 64rpx;
  font-weight: bold;
  color: #22c55e;
  margin-top: 40rpx;
}

.subtitle {
  font-size: 28rpx;
  color: #64748b;
  margin-top: 10rpx;
}

.lang-switch, .back {
  position: absolute;
  top: 40rpx;
  font-size: 28rpx;
  color: #60a5fa;
}

.lang-switch {
  right: 40rpx;
}

.back {
  left: 40rpx;
}

.form {
  margin-top: 80rpx;
}

.input {
  background: #1e293b;
  border-radius: 24rpx;
  padding: 0 32rpx;
  height: 100rpx;
  line-height: 100rpx;
  font-size: 32rpx;
  color: #e2e8f0;
  margin-bottom: 40rpx;
}

.login-btn {
  width: 100%;
  height: 100rpx;
  line-height: 100rpx;
  background: linear-gradient(90deg, #3b82f6, #2563eb);
  color: white;
  border-radius: 50rpx;
  font-size: 36rpx;
  box-shadow: 0 8rpx 32rpx rgba(59, 130, 246, 0.3);
  margin-top: 20rpx;
}
</style>
