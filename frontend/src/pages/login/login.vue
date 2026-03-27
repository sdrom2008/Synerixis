<template>
  <view class="phone-login">
    <view class="header">
      <image src="/static/logo.png" mode="widthFix" class="logo" /><br/>
      <text class="title">{{ title }}</text><br/>
      <view class="header-actions">
        <text class="lang-switch" @tap="switchLanguage">{{ currentLang === 'zh-CN' ? 'English' : '中文' }}</text>
        <text class="back" @tap="backToChoose">← {{ currentLang === 'zh-CN' ? '返回' : 'Back' }}</text>
      </view>
    </view>

    <view class="form">
      <!-- 国家/区号选择 -->
      <picker class="country-picker" mode="selector" :value="countryIndex" :range="countries" range-key="name" @change="onCountryChange">
        <view class="picker-content">
          <text class="picker-flag">{{ countries[countryIndex].flag }}</text>
          <text class="picker-text">+{{ countries[countryIndex].code }}</text>
          <text class="picker-arrow">▼</text>
        </view>
      </picker>

      <!-- 手机号输入框 -->
      <input 
        v-model="phone" 
        :placeholder="placeholderPhone" 
        type="number" 
        class="input" 
      />

      <view class="code-row">
        <input 
          v-model="code" 
          :placeholder="placeholderCode" 
          type="number" 
          maxlength="6" 
          class="input" 
        />
        <button 
          class="code-btn" 
          :disabled="countdown > 0 || sendCodeLoading" 
          @tap="sendCode"
          :loading="sendCodeLoading"
        >
          {{ countdown > 0 ? countdown + '秒' : t('common.sendCode') }}
        </button>
      </view>

      <button 
        class="login-btn" 
        @tap="handlePhoneLogin" 
        :loading="loginLoading"
        :disabled="loginLoading || !agree"
      >
        {{ loginBtnText }}
      </button>
    </view>

    <view class="protocol">
      <checkbox size="22" :checked="agree" @change="toggleAgree" color="#22c55e" />
      <text>{{ t('common.agree') }} 《{{ t('common.terms') }}》 {{ t('common.and') }} 《{{ t('common.privacy') }}》</text>
    </view>

    <!-- 页脚备案信息 -->
    <view class="footer">
      <text class="icp">湘ICP备2026009564号</text>
      <text class="copyright">© 2024-2026 Synerixis All Rights Reserved</text>
    </view>
  </view>
</template>

<script>
import { request } from '@/utils/request.js';
import { t, getLanguage, setLanguage } from '@/utils/i18n.js';

export default {
  data() {
    return {
      phone: '',
      code: '',
      countryCode: '86',
      countryIndex: 0,
      currentLang: getLanguage(),
      countries: [
        { name: '中国', code: '86', flag: '🇨🇳' },
        { name: '美国', code: '1', flag: '🇺🇸' },
        { name: '香港', code: '852', flag: '🇭🇰' },
        { name: '新加坡', code: '65', flag: '🇸🇬' },
        { name: '英国', code: '44', flag: '🇬🇧' },
        { name: '日本', code: '81', flag: '🇯🇵' },
        { name: '韩国', code: '82', flag: '🇰🇷' },
        { name: '澳大利亚', code: '61', flag: '🇦🇺' },
        { name: '加拿大', code: '1', flag: '🇨🇦' },
        { name: '德国', code: '49', flag: '🇩🇪' },
        { name: '法国', code: '33', flag: '🇫🇷' }
      ],
      countdown: 0,
      sendCodeLoading: false,
      loginLoading: false,
      agree: true
    };
  },

  computed: {
    title() {
      const _ = this.currentLang; return t('login.title');
    },
    placeholderPhone() {
      const _ = this.currentLang; return t('login.placeholderPhone');
    },
    placeholderCode() {
      const _ = this.currentLang; return t('login.placeholderCode');
    },
    loginBtnText() {
      const _ = this.currentLang; return t('login.loginBtn');
    }
  },

  methods: {
    t(key) {
      const _ = this.currentLang;
      return t(key);
    },
    toggleAgree(e) {
      this.agree = e.detail.value;
    },

    backToChoose() {
      uni.navigateBack();
    },

    switchLanguage() {
      const newLang = this.currentLang === 'zh-CN' ? 'en-US' : 'zh-CN';
      setLanguage(newLang);
      this.currentLang = newLang;
    },

    onCountryChange(e) {
      this.countryIndex = parseInt(e.detail.value);
      this.countryCode = this.countries[this.countryIndex].code;
    },

    async sendCode() {
      if (!this.agree) {
        uni.showToast({ title: t('common.agree') + '?', icon: 'none' });
        return;
      }

      if (!this.phone) {
        uni.showToast({ title: t('login.placeholderPhone'), icon: 'none' });
        return;
      }

      this.sendCodeLoading = true;

      try {
        const res = await request({
          url: '/api/auth/send-code',
          method: 'POST',
          data: { 
            Phone: this.phone,
            CountryCode: this.countryCode
          }
        });
        uni.showToast({ title: res.message || t('common.sendCode'), icon: 'success' });
        this.countdown = 60;
        const timer = setInterval(() => {
          this.countdown--;
          if (this.countdown <= 0) clearInterval(timer);
        }, 1000);
      } catch (err) {
        // 错误已在 request 中处理
      } finally {
        this.sendCodeLoading = false;
      }
    },

    async handlePhoneLogin() {
      if (!this.agree) {
        uni.showToast({ title: t('common.agree') + '?', icon: 'none' });
        return;
      }

      if (!this.phone) {
        uni.showToast({ title: t('login.placeholderPhone'), icon: 'none' });
        return;
      }

      if (!this.code) {
        uni.showToast({ title: t('login.placeholderCode'), icon: 'none' });
        return;
      }

      this.loginLoading = true;

      try {
        const res = await request({
          url: '/api/auth/phone-login',
          method: 'POST',
          data: {
            Phone: this.phone,
            Code: this.code,
            CountryCode: this.countryCode
          }
        });

        // 保存 token 和用户信息
        uni.setStorageSync('token', res.token);
        uni.setStorageSync('userId', res.userId);
        uni.setStorageSync('userType', res.userType);

        if (res.userType === 'Seller') {
          uni.setStorageSync('sellerId', res.sellerId);
          uni.setStorageSync('nickname', res.nickname);
        } else if (res.userType === 'Agent' || res.userType === 'Supervisor') {
          uni.setStorageSync('agentId', res.userId);
          uni.setStorageSync('shopId', res.shopId);
          uni.setStorageSync('agentName', res.name);
        }

        uni.showToast({ title: t('common.login'), icon: 'success' });

        setTimeout(() => {
          if (res.userType === 'Seller') {
            uni.switchTab({ url: '/pages/merchant/sessions' });
          } else if (res.userType === 'Agent' || res.userType === 'Supervisor') {
            uni.switchTab({ url: '/pages/support/workbench' });
          } else {
            uni.switchTab({ url: '/pages/conversations/conversations' });
          }
        }, 1000);
      } catch (err) {
        // 错误已在 request 中处理
      } finally {
        this.loginLoading = false;
      }
    }
  }
};
</script>

<style>
.phone-login {
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
  margin-top: 60rpx;
}

.country-picker {
  background: #1e293b;
  border-radius: 24rpx;
  padding: 0 32rpx;
  height: 100rpx;
  line-height: 100rpx;
  margin-bottom: 32rpx;
  display: flex;
  align-items: center;
}

.picker-content {
  display: flex;
  align-items: center;
  width: 100%;
}

.picker-flag {
  font-size: 40rpx;
  margin-right: 16rpx;
}

.picker-text {
  font-size: 36rpx;
  color: #e2e8f0;
  flex: 1;
}

.picker-arrow {
  font-size: 24rpx;
  color: #94a3b8;
}

.input {
  background: #1e293b;
  border-radius: 24rpx;
  padding: 0 32rpx;
  height: 100rpx;
  line-height: 100rpx;
  font-size: 36rpx;
  color: #e2e8f0;
  margin-bottom: 32rpx;
}

.code-row {
  display: flex;
  align-items: center;
  margin-bottom: 40rpx;
}

.code-input {
  flex: 1;
  background: #1e293b;
  border-radius: 24rpx;
  padding: 0 32rpx;
  height: 100rpx;
  line-height: 100rpx;
  font-size: 36rpx;
  color: #e2e8f0;
}

.code-btn {
  width: 280rpx;
  height: 100rpx;
  line-height: 100rpx;
  background: #3b82f6;
  color: white;
  border-radius: 50rpx;
  font-size: 30rpx;
  margin-left: 20rpx;
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
  left: 0;
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
</style>
