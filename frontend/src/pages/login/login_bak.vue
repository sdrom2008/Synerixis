<template>
  <view class="login-container">
    <image src="/static/logo.png" mode="widthFix" class="logo" />
    <view class="title">Synerixis - AI 智能伙伴</view>
    <view class="desc">一键接入，AI 帮你 24h 客服 & 增长</view>

<view>
  <button open-type="getUserInfo" @getuserinfo="handleWechatLogin">微信登录</button>
	<!-- 新增：绑定手机号引导（当 needBind 时显示） -->
	<view v-if="needBind" class="bind-section">
	  <text class="bind-tip">微信登录成功，请绑定手机号以完成注册</text>
	  <button 
		class="bind-btn" 
		open-type="getPhoneNumber" 
		@getphonenumber="handleBindPhone"
		:loading="bindLoading"
	  >
		一键绑定手机号
	  </button>
	</view>
</view>

    <view class="divider">或</view>

    <!-- 手机号登录 / 注册 -->
    <view class="phone-section">
      <input 
        v-model="phone" 
        placeholder="请输入手机号" 
        type="tel" 
        maxlength="11" 
        class="input" 
        focus 
      />
      <view class="code-row">
        <input 
          v-model="code" 
          placeholder="验证码" 
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
          {{ countdown > 0 ? countdown + '秒' : '获取验证码' }}
        </button>
      </view>
      <button 
        class="login-btn" 
        @tap="handlePhoneLogin" 
        :loading="loginLoading"
        :disabled="loginLoading"
      >
        登录 / 注册
      </button>
    </view>

    <view class="tip">登录即同意《用户协议》和《隐私政策》</view>
  </view>
</template>
<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      phone: '',
      code: '',
      countdown: 0,
      wechatLoading: false,      // 微信登录 loading
      bindLoading: false,        // 绑定手机号 loading
      sendCodeLoading: false,
      loginLoading: false,
      needBind: false,
      tempOpenid: ''             // 临时存 openid 用于绑定
    };
  },

  methods: {
    // 微信登录入口（唯一方法，删除 handleWechatUserInfo 重复代码）
    handleWechatLogin(e) {
      if (e.detail.errMsg && e.detail.errMsg.includes('deny')) {
        uni.showToast({ title: '用户拒绝授权', icon: 'none' });
        return;
      }

      this.wechatLoading = true;

      uni.login({
        success: async res => {
          if (!res.code) {
            uni.showToast({ title: '登录凭证获取失败', icon: 'none' });
            this.wechatLoading = false;
            return;
          }

          console.log('[DEBUG] 拿到微信 code:', res.code);

          const loginRes = await uni.request({
            url: `${testbase}/api/auth/wechat`,
            method: 'POST',
            data: { code: res.code }
          });

          console.log('[DEBUG] /wechat 完整响应数据:', JSON.stringify(loginRes.data || {}));

          const data = loginRes.data || {};

          if (data.token) {
            // 已绑定，直接登录成功
            uni.setStorageSync('token', data.token);
            uni.setStorageSync('sellerId', data.sellerId);
            uni.showToast({ title: '登录成功', icon: 'success' });
            uni.switchTab({ url: '/pages/dashboard/dashboard' });
          } else if (data.needBind === true) {
            // 需要绑定手机号
            this.needBind = true;
            this.tempOpenid = data.openid;
			uni.setStorageSync('openId', this.tempOpenid);  // 使用临时存储的 openid
            console.log('[DEBUG] 需要绑定，tempOpenid 已保存:', this.tempOpenid);
            uni.showToast({ title: '请绑定手机号', icon: 'none' });
          } else {
            uni.showToast({ 
              title: data.msg || data.message || '登录失败，请重试', 
              icon: 'none' 
            });
          }
        },
        fail: err => {
          console.error('[ERROR] uni.login 失败:', err);
          uni.showToast({ title: '微信登录失败', icon: 'none' });
        },
        complete: () => {
          this.wechatLoading = false;
        }
      });
    },

    // 绑定手机号
    async handleBindPhone(e) {
      console.log('[DEBUG] handleBindPhone 被触发，完整 detail:', JSON.stringify(e.detail || {}));

      const detail = e.detail || {};

      if (detail.errMsg && detail.errMsg.includes('deny')) {
        uni.showToast({ title: '用户拒绝授权', icon: 'none' });
        return;
      }

      if (detail.errMsg !== 'getPhoneNumber:ok' || !detail.encryptedData || !detail.iv) {
        uni.showToast({ 
          title: '获取手机号失败：' + (detail.errMsg || '未知错误'), 
          icon: 'none' 
        });
        console.error('getPhoneNumber 失败:', detail);
        return;
      }

      console.log('[DEBUG] 拿到数据成功：', {
        encryptedData: detail.encryptedData.substring(0, 30) + '...',
        iv: detail.iv,
        code: detail.code || '(无新 code)'
      });

      this.bindLoading = true;

      try {
        const loginRes = await uni.login({ provider: 'weixin' });
        const code = loginRes.code;

        const res = await uni.request({
          url: `${testbase}/api/auth/decrypt-phone`,
          method: 'POST',
          data: {
            code,
            encryptedData: detail.encryptedData,
            iv: detail.iv,
            openId: this.tempOpenid
          },
          header: { 'content-type': 'application/json' }
        });

        console.log('[DEBUG] /decrypt-phone 完整返回:', res);

        if (res.statusCode === 200 && res.data?.token) {
          uni.setStorageSync('token', res.data.token);
          uni.setStorageSync('sellerId', res.data.sellerId || '');
          uni.showToast({ title: '绑定并登录成功', icon: 'success' });
          uni.switchTab({ url: '/pages/dashboard/dashboard' });
        } else {
          uni.showToast({ 
            title: res.data?.msg || res.data?.message || '绑定失败，请重试', 
            icon: 'none' 
          });
        }
      } catch (err) {
        console.error('[ERROR] 绑定异常:', err);
        uni.showToast({ title: '网络错误，请检查控制台', icon: 'none', duration: 4000 });
      } finally {
        this.bindLoading = false;
      }
    },

    // 发送验证码（保持不变）
    async sendCode() {
      if (!this.phone || this.phone.length !== 11) {
        uni.showToast({ title: '手机号格式错误', icon: 'none' });
        return;
      }

      this.sendCodeLoading = true;

      try {
        const res = await uni.request({
          url: `${testbase}/api/auth/send-code`,
          method: 'POST',
          data: { phone: this.phone }
        });

        if (res.statusCode === 200) {
          uni.showToast({ title: '验证码已发送', icon: 'success' });
          this.countdown = 60;
          const timer = setInterval(() => {
            this.countdown--;
            if (this.countdown <= 0) clearInterval(timer);
          }, 1000);
        } else {
          uni.showToast({ title: res.data?.message || '发送失败', icon: 'none' });
        }
      } catch (err) {
        uni.showToast({ title: '网络错误', icon: 'none' });
      } finally {
        this.sendCodeLoading = false;
      }
    },

    // 手机号登录 / 注册（保持不变）
    async handlePhoneLogin() {
      if (!this.phone || !this.code) {
        uni.showToast({ title: '请输入完整信息', icon: 'none' });
        return;
      }

      this.loginLoading = true;

      try {
        const res = await uni.request({
          url: `${testbase}/api/auth/phone-login`,
          method: 'POST',
          data: { phone: this.phone, code: this.code }
        });

        console.log('后端响应:', res);

        if (res.statusCode === 200 && res.data?.token) {
          uni.setStorageSync('token', res.data.token);
          uni.setStorageSync('sellerId', res.data.sellerId);
		  uni.setStorageSync('openId', res.data.openId);
          uni.showToast({ title: '登录成功', icon: 'success' });
          uni.switchTab({ url: '/pages/dashboard/dashboard' });
        } else {
          uni.showToast({ title: res.data?.message || '登录失败', icon: 'none' });
        }
      } catch (err) {
        uni.showToast({ title: '网络错误', icon: 'none' });
      } finally {
        this.loginLoading = false;
      }
    }
  }
};
</script>

<style>
.login-container { height: 100vh; display: flex; flex-direction: column; align-items: center; justify-content: center; background: linear-gradient(to bottom, #f0f4ff, #fff); padding: 0 60rpx; }
.logo { width: 300rpx; margin-bottom: 40rpx; }
.title { font-size: 52rpx; font-weight: bold; margin-bottom: 20rpx; }
.desc { font-size: 32rpx; color: #666; margin-bottom: 80rpx; text-align: center; }
.wechat-btn { width: 100%; height: 100rpx; line-height: 100rpx; background: #07c160; color: white; border-radius: 50rpx; font-size: 36rpx; margin: 40rpx 0; }
.divider { color: #999; margin: 40rpx 0; font-size: 28rpx; }
.phone-section { width: 100%; }
.input { width: 100%; height: 100rpx; padding: 0 30rpx; background: white; border-radius: 50rpx; margin: 20rpx 0; border: 1rpx solid #eee; box-sizing: border-box; font-size: 32rpx; }
.code-row { display: flex; align-items: center; margin: 20rpx 0; }
.code-btn { width: 280rpx; height: 100rpx; line-height: 100rpx; background: #007aff; color: white; border-radius: 50rpx; font-size: 32rpx; margin-left: 20rpx; }
.login-btn { width: 100%; height: 100rpx; line-height: 100rpx; background: #007aff; color: white; border-radius: 50rpx; font-size: 36rpx; margin-top: 40rpx; }
.tip { font-size: 28rpx; color: #999; margin-top: 40rpx; text-align: center; }
</style>