<template>
  <view class="wechat-login">
    <view class="header">
      <image src="/static/logo.png" mode="widthFix" class="logo" /><br/>
      <text class="title">微信登录</text><br/>
      <text class="back" @tap="backToChoose">返回</text>
    </view>

    <view class="content">
      <text class="tip">使用微信登录，快速体验</text>

      <button class="wechat-btn" open-type="getUserInfo" @getuserinfo="handleWechatLogin" :loading="wechatLoading">
        <image src="/static/wechat-icon.png" class="icon" />
        <text>微信一键登录</text>
      </button>

      <!-- 绑定手机号引导 -->
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

    <view class="protocol">
      <checkbox size="22" :checked="agree" @change="toggleAgree" color="#22c55e" />
      <text>同意《用户协议》和《隐私政策》</text>
    </view>

    <!-- 页脚备案信息 -->
    <view class="footer">
      <text class="icp">蜀ICP备2026123456号-1</text>
      <text class="copyright">© 2024-2026 Synerixis All Rights Reserved</text>
    </view>
  </view>
</template>

<script>
const testbase = 'http://192.168.1.254:7092';

export default {
  data() {
    return {
      wechatLoading: false,
      bindLoading: false,
      needBind: false,
      tempOpenid: '',
      agree: true
    };
  },

  methods: {
    toggleAgree(e) {
      this.agree = e.detail.value;
    },

    backToChoose() {
      uni.navigateBack();
    },

    handleWechatLogin(e) {
      if (e.detail.errMsg && e.detail.errMsg.includes('deny')) {
        uni.showToast({ title: '用户拒绝授权', icon: 'none' });
        return;
      }

      if (!this.agree) {
        uni.showToast({ title: '请先同意协议', icon: 'none' });
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
            uni.setStorageSync('token', data.token);
            uni.setStorageSync('sellerId', data.sellerId);
            uni.showToast({ title: '登录成功', icon: 'success' });
            uni.switchTab({ url: '/pages/dashboard/dashboard' });
          } else if (data.needBind === true) {
            this.needBind = true;
            this.tempOpenid = data.openid;
            uni.setStorageSync('openId', this.tempOpenid);
            console.log('[DEBUG] 需要绑定，tempOpenid 已保存:', this.tempOpenid);
            uni.showToast({ title: '请绑定手机号', icon: 'none' });
          } else {
            uni.showToast({ title: data.msg || data.message || '登录失败，请重试', icon: 'none' });
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

    async handleBindPhone(e) {
      console.log('[DEBUG] handleBindPhone 被触发，完整 detail:', JSON.stringify(e.detail || {}));

      const detail = e.detail || {};

      if (detail.errMsg && detail.errMsg.includes('deny')) {
        uni.showToast({ title: '用户拒绝授权', icon: 'none' });
        return;
      }

      if (detail.errMsg !== 'getPhoneNumber:ok' || !detail.encryptedData || !detail.iv) {
        uni.showToast({ title: '获取手机号失败：' + (detail.errMsg || '未知错误'), icon: 'none' });
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
          uni.showToast({ title: res.data?.msg || res.data?.message || '绑定失败，请重试', icon: 'none' });
        }
      } catch (err) {
        console.error('[ERROR] 绑定异常:', err);
        uni.showToast({ title: '网络错误，请检查控制台', icon: 'none', duration: 4000 });
      } finally {
        this.bindLoading = false;
      }
    }
  }
};
</script>

<style>
.wechat-login {
  height: 100vh;
  background: #0a0e1a;
  padding: 120rpx 40rpx;
  display: flex;
  flex-direction: column;
  align-items: center;
}

.header {
  text-align: center;
  margin-bottom: 140rpx;
}

.logo {
  width: 180rpx;
  height: 180rpx;
}

.title {
  font-size: 72rpx;
  font-weight: bold;
  color: #22c55e;
  margin-top: 40rpx;
}

.content {
  text-align: center;
  margin-top: 80rpx;
}

.tip {
  font-size: 32rpx;
  color: #94a3b8;
  margin-bottom: 60rpx;
}

.wechat-btn {
  width: 100%;
  height: 100rpx;
  line-height: 100rpx;
  background: linear-gradient(90deg, #22c55e, #16a34a);
  color: white;
  font-size: 36rpx;
  border-radius: 50rpx;
  display: flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 8rpx 32rpx rgba(34, 197, 94, 0.3);
  margin-bottom: 40rpx;
}

.icon {
  width: 48rpx;
  height: 48rpx;
  margin-right: 20rpx;
}

.back {
  font-size: 28rpx;
  color: #60a5fa;
  margin-top: 20rpx;
}

.protocol {
  margin-top: auto;
  font-size: 24rpx;
  color: #94a3b8;
  text-align: center;
  padding-bottom: 40rpx;
}

.bind-section {
  margin-top: 40rpx;
  text-align: center;
}

.bind-tip {
  font-size: 28rpx;
  color: #94a3b8;
  margin-bottom: 20rpx;
}

.bind-btn {
  width: 100%;
  height: 100rpx;
  line-height: 100rpx;
  background: linear-gradient(90deg, #3b82f6, #2563eb);
  color: white;
  font-size: 36rpx;
  border-radius: 50rpx;
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