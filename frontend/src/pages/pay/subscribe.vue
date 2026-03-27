<template>
  <view class="subscribe-page">
    <view class="header">
      <text class="title">升级订阅，畅享无限使用</text>
    </view>

    <view class="benefit-list">
      <view class="benefit-item">+1000 条额外额度</view>
      <view class="benefit-item">30 天无限智能客服 & 商品优化</view>
      <view class="benefit-item">优先使用高级营销模板</view>
      <view class="benefit-item">专属 AI 偏好记忆</view>
    </view>

    <view class="price-card">
      <view class="price">¥99</view>
      <view class="period">/月</view>
      <view class="original">原价 ¥199</view>
    </view>

    <!-- 支付方式选择 -->
    <view class="payment-method">
      <text class="method-title">支付方式</text>
      <view class="method-list">
        <view 
          class="method-item" 
          :class="{ active: selectedChannel === 'wechat' }" 
          @tap="selectChannel('wechat')"
        >
          <image src="/static/wechat-pay.png" class="method-icon" mode="aspectFit"></image>
          <text>微信支付</text>
        </view>
        <view 
          class="method-item" 
          :class="{ active: selectedChannel === 'alipay' }" 
          @tap="selectChannel('alipay')"
        >
          <image src="/static/alipay.png" class="method-icon" mode="aspectFit"></image>
          <text>支付宝支付</text>
        </view>
      </view>
    </view>

    <button 
      class="pay-btn" 
      :loading="paying" 
      :disabled="paying"
      @tap="createOrder"
    >
      立即支付 99 元
    </button>

    <view class="tip">
      支付后立即生效，支持微信/支付宝支付\n额度可累积，随时查看
    </view>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      paying: false,
      selectedChannel: 'wechat'
    };
  },

  methods: {
    selectChannel(channel) {
      this.selectedChannel = channel;
    },

    async createOrder() {
      this.paying = true;
      try {
        const token = uni.getStorageSync('token');
        if (!token) {
          uni.showToast({ title: '请先登录', icon: 'none' });
          uni.navigateTo({ url: '/pages/login/login' });
          return;
        }

        console.log('[支付] 开始创建订单，渠道：', this.selectedChannel);

        const res = await uni.request({
          url: `${testbase}/api/pay/create`,
          method: 'POST',
          header: { 
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          },
          data: {
            channel: this.selectedChannel,
            amount: 99,
            description: '升级订阅 99元/月',
            notifyUrl: 'https://your-domain.com/api/pay/notify/' + this.selectedChannel,
            returnUrl: 'pages/profile/profile'
          }
        });

        console.log('[支付] 创建订单完整响应：', JSON.stringify(res, null, 2));

        if (res.statusCode !== 200) {
          uni.showToast({ title: '创建订单失败 ' + res.statusCode, icon: 'none' });
          return;
        }

        const data = res.data || {};
        if (!data.success) {
          uni.showToast({ title: data.message || '创建订单失败', icon: 'none' });
          return;
        }

        const payData = data.payParams || data.payData || data;
        console.log('[支付] 提取的支付参数：', JSON.stringify(payData, null, 2));

        let provider = this.selectedChannel === 'wechat' ? 'wxpay' : 'alipay';

        uni.requestPayment({
          provider: provider,
          orderInfo: payData.orderInfo || payData,  // 支付宝用
          timeStamp: payData.timeStamp ? payData.timeStamp.toString() : '',
          nonceStr: payData.nonceStr || '',
          package: payData.package || '',
          signType: payData.signType || 'MD5',
          paySign: payData.paySign || '',
          success: (payRes) => {
            console.log('[支付] 成功：', payRes);
            uni.showToast({ title: '支付成功！权益已到账', icon: 'success' });

            // 轮询订单状态（防止回调延迟）
            const outTradeNo = payData.outTradeNo || '未知订单号';
            const checkInterval = setInterval(async () => {
              const queryRes = await uni.request({
                url: `${testbase}/api/pay/query?outTradeNo=${outTradeNo}`,
                method: 'GET',
                header: { 'Authorization': `Bearer ${token}` }
              });

              console.log('[轮询] 订单状态：', queryRes.data);

              if (queryRes.statusCode === 200 && queryRes.data?.status === 'paid') {
                clearInterval(checkInterval);
                uni.showToast({ title: '权益已同步，请查看个人中心', icon: 'success' });
                uni.navigateTo({ url: '/pages/profile/profile' });
              }
            }, 3000);  // 每3秒查一次

            // 超时停止轮询
            setTimeout(() => clearInterval(checkInterval), 60000);
          },
          fail: (err) => {
            console.error('[支付] 失败：', err);
            uni.showToast({ title: '支付失败：' + (err.errMsg || '未知错误'), icon: 'none', duration: 5000 });
          },
          complete: () => {
            console.log('[支付] complete');
            this.paying = false;
          }
        });

      } catch (error) {
        console.error('[支付] 异常：', error);
        uni.showToast({ title: '网络错误，请重试', icon: 'none' });
      } finally {
        this.paying = false;
      }
    }
  }
};
</script>

<style>
.payment-method {
  background: white;
  border-radius: 24rpx;
  padding: 40rpx 30rpx;
  margin-bottom: 60rpx;
  box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08);
}

.method-title {
  font-size: 36rpx;
  font-weight: bold;
  color: #333;
  margin-bottom: 30rpx;
}

.method-list {
  display: flex;
  justify-content: space-around;
}

.method-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 20rpx;
  border-radius: 16rpx;
  border: 2rpx solid #eee;
  width: 280rpx;
  transition: all 0.3s;
}

.method-item.active {
  border-color: #ff4d4f;
  background: #fff1f0;
}

.method-icon {
  width: 80rpx;
  height: 80rpx;
  margin-bottom: 20rpx;
}

.method-item text {
  font-size: 32rpx;
  color: #333;
}

.pay-btn {
  /* 原有样式 */
  margin-top: 20rpx;
}
.subscribe-page { background: #f5f5f5; min-height: 100vh; padding: 60rpx 30rpx; }
.header { text-align: center; margin-bottom: 60rpx; }
.title { font-size: 48rpx; font-weight: bold; color: #333; }
.benefit-list { background: white; border-radius: 24rpx; padding: 40rpx; margin-bottom: 40rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.benefit-item { font-size: 32rpx; margin: 20rpx 0; padding-left: 40rpx; position: relative; }
.benefit-item::before { content: '✓'; color: #52c41a; position: absolute; left: 0; font-size: 36rpx; }
.price-card { text-align: center; background: white; border-radius: 24rpx; padding: 60rpx 30rpx; margin-bottom: 60rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.price { font-size: 96rpx; font-weight: bold; color: #ff4d4f; }
.period { font-size: 48rpx; color: #666; }
.original { font-size: 32rpx; color: #999; text-decoration: line-through; margin-top: 16rpx; }
.pay-btn { background: #ff4d4f; color: white; border-radius: 50rpx; height: 120rpx; line-height: 120rpx; font-size: 40rpx; box-shadow: 0 12rpx 32rpx rgba(255,77,79,0.3); }
.tip { text-align: center; font-size: 28rpx; color: #666; margin-top: 40rpx; line-height: 48rpx; }
</style>