<template>
  <view class="bind-page">
    <view class="title">绑定手机号</view>
    <view class="desc">绑定后即可用微信一键登录</view>

    <input v-model="phone" placeholder="请输入手机号" type="number" maxlength="11" />
    <view class="code-row">
      <input v-model="code" placeholder="验证码" type="number" maxlength="6" />
      <button @tap="sendCode" :disabled="countdown > 0">
        {{ countdown > 0 ? countdown + '秒' : '获取验证码' }}
      </button>
    </view>

    <button class="bind-btn" @tap="bindPhone">绑定并登录</button>
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
      openid: ''
    };
  },

  onLoad(options) {
    this.openid = options.openid;
  },
 
  methods: {
    async sendCode() {
      if (!this.phone || this.phone.length !== 11) {
        uni.showToast({ title: '手机号格式错误', icon: 'none' });
        return;
      }

      await uni.request({
        url: `${testbase}/api/auth/send-code`,
        method: 'POST',
        data: { phone: this.phone }
      });

      this.countdown = 60;
      const timer = setInterval(() => {
        this.countdown--;
        if (this.countdown <= 0) clearInterval(timer);
      }, 1000);
    },

    async bindPhone() {
      if (!this.phone || !this.code) return;

      const res = await uni.request({
        url: `${testbase}/api/auth/bind-phone`,
        method: 'POST',
        data: { openid: this.openid, phone: this.phone, code: this.code }
      });

      if (res.data.token) {
        uni.setStorageSync('token', res.data.token);
        uni.switchTab({ url: '/pages/conversations/conversations' });
      } else {
        uni.showToast({ title: res.data.message || '绑定失败', icon: 'none' });
      }
    }
  }
};
</script>