<template>
  <view class="account-security-page">
    <!-- 导航栏 -->
    <view class="navbar">
      <view class="back" @tap="goBack">← 返回</view>
      <view class="title">账号与安全</view>
      <view class="placeholder"></view>
    </view>

    <!-- 账号信息 -->
    <view class="section">
      <view class="section-title">账号信息</view>
      <view class="info-item">
        <text class="label">登录方式</text>
        <text class="value">{{ loginMethods }}</text>
      </view>
      <view class="info-item">
        <text class="label">注册时间</text>
        <text class="value">{{ formatDate(profile.createdAt) || '未知' }}</text>
      </view>
      <view class="info-item">
        <text class="label">最后登录</text>
        <text class="value">{{ profile.lastLoginAt ? formatDate(profile.lastLoginAt) : '未知' }}</text>
      </view>
    </view>

    <!-- 安全设置 -->
    <view class="section">
      <view class="section-title">安全设置</view>
      <view class="menu-item" @tap="changePassword">
        <text>修改登录密码</text>
        <text class="arrow">></text>
      </view>
      <view class="menu-item" @tap="toBindPhone" v-if="!profile.phone">
        <text>绑定手机号</text>
        <text class="arrow">></text>
      </view>
      <view class="menu-item disabled" v-else>
        <text>手机号已绑定</text>
        <text class="status">已绑定</text>
      </view>
      <view class="menu-item" @tap="toWechatBind" v-if="!profile.openId">
        <text>绑定微信</text>
        <text class="arrow">></text>
      </view>
      <view class="menu-item disabled" v-else>
        <text>微信已绑定</text>
        <text class="status">已绑定</text>
      </view>
    </view>

    <!-- 隐私与协议 -->
    <view class="section">
      <view class="section-title">隐私与协议</view>
      <view class="menu-item" @tap="openPrivacy">
        <text>隐私政策</text>
        <text class="arrow">></text>
      </view>
      <view class="menu-item" @tap="openTerms">
        <text>用户协议</text>
        <text class="arrow">></text>
      </view>
      <view class="menu-item" @tap="openDataDelete">
        <text>账号注销</text>
        <text class="arrow">></text>
      </view>
    </view>

    <!-- 退出登录 -->
    <button class="logout-btn" @tap="logout">
      退出登录
    </button>

    <!-- 版本信息 -->
    <view class="version">
      Synerixis v1.0.0 | © 2026
    </view>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      profile: {
        nickname: '',
        phone: '',
        openId: '',
        createdAt: null,
        lastLoginAt: null,
        subscriptionLevel: '免费版'
      }
    };
  },

  onLoad() {
    this.loadProfile();
  },

  computed: {
    loginMethods() {
      const methods = [];
      if (this.profile.phone) methods.push('手机号');
      if (this.profile.openId) methods.push('微信');
      return methods.length ? methods.join(' / ') : '未设置';
    }
  },

  methods: {
    async loadProfile() {
      const token = uni.getStorageSync('token');
      if (!token) return uni.navigateTo({ url: '/pages/login/login' });

      const res = await uni.request({
        url: `${testbase}/api/seller/profile`,
        header: { Authorization: `Bearer ${token}` }
      });

      if (res.statusCode === 200) {
        this.profile = res.data;
      }
    },

    formatDate(dateStr) {
      if (!dateStr) return '未知';
      const date = new Date(dateStr);
      return date.toLocaleString('zh-CN', { year: 'numeric', month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' });
    },

    changePassword() {
      uni.showModal({
        title: '修改密码',
        content: '当前暂不支持修改密码，请联系客服协助',
        showCancel: false
      });
    },

    toBindPhone() {
      uni.navigateTo({ url: '/pages/login/login' });
    },

    toWechatBind() {
      uni.showToast({ title: '请从微信登录页重新绑定', icon: 'none' });
    },

    openPrivacy() {
      uni.showModal({
        title: '隐私政策',
        content: '请访问 https://synerixis.com/privacy 查看完整隐私政策',
        showCancel: false
      });
    },

    openTerms() {
      uni.showModal({
        title: '用户协议',
        content: '请访问 https://synerixis.com/terms 查看完整用户协议',
        showCancel: false
      });
    },

    openDataDelete() {
      uni.showModal({
        title: '账号注销',
        content: '账号注销将清除所有数据且不可恢复，确定申请吗？\n\n申请后将在 7 天内处理。',
        success: res => {
          if (res.confirm) {
            uni.showToast({ title: '已提交注销申请', icon: 'success' });
            // 后续可调用注销接口
          }
        }
      });
    },

    logout() {
      uni.showModal({
        title: '退出登录',
        content: '确定要退出吗？',
        success: res => {
          if (res.confirm) {
            uni.removeStorageSync('token');
            uni.removeStorageSync('sellerId');
            uni.switchTab({ url: '/pages/login/login' });
          }
        }
      });
    },

    goBack() {
      uni.navigateBack();
    }
  }
};
</script>

<style>
.account-security-page { background: #f5f5f5; min-height: 100vh; padding-bottom: 120rpx; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.back { font-size: 48rpx; }
.title { font-size: 36rpx; font-weight: bold; }
.section { background: white; margin: 88rpx 30rpx 30rpx; padding: 40rpx; border-radius: 24rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.section-title { font-size: 36rpx; font-weight: bold; margin-bottom: 40rpx; }
.info-item, .menu-item { display: flex; justify-content: space-between; font-size: 32rpx; margin: 32rpx 0; padding-bottom: 32rpx; border-bottom: 1rpx solid #eee; }
.info-item:last-child, .menu-item:last-child { border-bottom: none; }
.label { color: #666; }
.value { font-weight: bold; }
.menu-item { cursor: pointer; }
.arrow { color: #999; }
.disabled { color: #999; opacity: 0.6; }
.logout-btn { position: fixed; bottom: 40rpx; left: 30rpx; right: 30rpx; background: #ff4d4f; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; text-align: center; box-shadow: 0 8rpx 24rpx rgba(255,77,79,0.3); }
</style>