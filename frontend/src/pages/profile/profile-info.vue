<template>
  <view class="profile-info-page">
    <!-- 导航栏 -->
    <view class="navbar">
      <view class="back" @tap="goBack">←</view>
      <view class="title">个人信息</view>
      <view class="placeholder"></view>
    </view>

    <!-- 头像与昵称 -->
    <view class="avatar-section">
      <view class="avatar-wrapper" @tap="chooseAvatar">
        <image :src="form.avatarUrl || '/static/default-avatar.png'" mode="aspectFill" class="avatar" />
        <view class="edit-overlay">
          <text>更换</text>
        </view>
      </view>
      <view class="nickname-group">
        <input 
          v-model="form.nickname" 
          placeholder="点击设置昵称" 
          class="nickname-input" 
          maxlength="20" 
        />
        <text class="tip">用于显示给客户，建议使用店铺名或真实昵称</text>
      </view>
    </view>

    <!-- 手机号信息 -->
    <view class="section">
      <view class="section-title">手机号</view>
      <view class="phone-display">
        <text class="phone">{{ form.phone || '未绑定' }}</text>
        <button class="bind-btn" v-if="!form.phone" @tap="toBindPhone">
          立即绑定
        </button>
        <text class="tip" v-if="form.phone">已绑定，可在登录页更换</text>
      </view>
    </view>

    <!-- 会员信息 -->
    <view class="section">
      <view class="section-title">会员信息</view>
      <view class="member-info">
        <view class="item">
          <text class="label">当前等级</text>
          <text class="value" :class="profile.subscriptionLevel === 'Pro' ? 'pro' : 'free'">
            {{ profile.subscriptionLevel || '免费版' }}
          </text>
        </view>
        <view class="item">
          <text class="label">剩余额度</text>
          <text class="value">{{ profile.freeQuota || 0 }} 条</text>
        </view>
        <view class="item">
          <text class="label">订阅到期</text>
          <text class="value">{{ profile.subscriptionEnd ? formatDate(profile.subscriptionEnd) : '未订阅' }}</text>
        </view>
      </view>
      <button class="upgrade-btn" @tap="toSubscribe">
        升级订阅享更多权益
      </button>
    </view>

    <!-- 保存按钮 -->
    <button class="save-btn" :loading="saving" @tap="saveInfo">
      保存个人信息
    </button>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      profile: {},
      form: {
        nickname: '',
        avatarUrl: '',
        phone: ''
      },
      saving: false,
      uploading: false
    };
  },

  onLoad() {
    this.loadInfo();
  },

  methods: {
    async loadInfo() {
      const token = uni.getStorageSync('token');
      if (!token) return uni.navigateTo({ url: '/pages/login/login' });

      const res = await uni.request({
        url: `${testbase}/api/seller/profile`,
        header: { Authorization: `Bearer ${token}` }
      });

      if (res.statusCode === 200) {
        this.profile = res.data;
        this.form = {
          nickname: res.data.nickname || '',
          avatarUrl: res.data.avatarUrl || '',
          phone: res.data.phone || ''
        };
      }
    },

    async chooseAvatar() {
      uni.chooseImage({
        count: 1,
        sizeType: ['compressed'],
        sourceType: ['album', 'camera'],
        success: res => {
          const tempFile = res.tempFilePaths[0];
          this.uploadFile(tempFile, 'avatar');
        }
      });
    },

    async uploadFile(filePath, type) {
      this.uploading = true;
      const token = uni.getStorageSync('token');

      uni.uploadFile({
        url: `${testbase}/api/upload/avatar`,
        filePath,
        name: 'file',
        header: { Authorization: `Bearer ${token}` },
        success: res => {
          const data = JSON.parse(res.data);
          if (data.url) {
            this.form.avatarUrl = data.url;
            uni.showToast({ title: '头像上传成功', icon: 'success' });
          } else {
            uni.showToast({ title: data.msg || '上传失败', icon: 'none' });
          }
        },
        fail: err => {
          uni.showToast({ title: '上传失败', icon: 'none' });
          console.error('上传错误:', err);
        },
        complete: () => {
          this.uploading = false;
        }
      });
    },

    async saveInfo() {
      if (!this.form.nickname.trim()) {
        uni.showToast({ title: '昵称不能为空', icon: 'none' });
        return;
      }

      this.saving = true;
      const token = uni.getStorageSync('token');

      const res = await uni.request({
        url: `${testbase}/api/seller/profile`,
        method: 'PUT',
        header: { 'content-type': 'application/json', Authorization: `Bearer ${token}` },
        data: {
          nickname: this.form.nickname,
          avatarUrl: this.form.avatarUrl
        }
      });

      this.saving = false;

      if (res.statusCode === 200) {
        uni.showToast({ title: '保存成功', icon: 'success' });
        uni.navigateBack();
      } else {
        uni.showToast({ title: res.data?.msg || '保存失败', icon: 'none' });
      }
    },

    toBindPhone() {
      uni.navigateTo({ url: '/pages/login/login' });  // 回到登录页重新绑定
    },

    toSubscribe() {
      uni.navigateTo({ url: '/pages/pay/subscribe' });
    },

    goBack() {
      uni.navigateBack();
    }
  }
};
</script>

<style>
.profile-info-page { background: #f5f5f5; min-height: 100vh; padding-bottom: 120rpx; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.back { font-size: 48rpx; }
.title { font-size: 36rpx; font-weight: bold; }
.avatar-section { margin: 88rpx 30rpx 40rpx; text-align: center; }
.avatar-wrapper { position: relative; width: 200rpx; height: 200rpx; margin: 0 auto; border-radius: 50%; overflow: hidden; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.1); }
.avatar { width: 100%; height: 100%; }
.edit-overlay { position: absolute; bottom: 0; left: 0; right: 0; background: rgba(0,0,0,0.6); color: white; font-size: 28rpx; line-height: 80rpx; }
.nickname-group { margin-top: 30rpx; }
.nickname-input { font-size: 48rpx; font-weight: bold; text-align: center; border-bottom: 2rpx solid #ddd; padding: 20rpx 0; }
.tip { font-size: 28rpx; color: #999; margin-top: 12rpx; }
.section { background: white; margin: 0 30rpx 30rpx; padding: 40rpx; border-radius: 24rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.section-title { font-size: 36rpx; font-weight: bold; margin-bottom: 40rpx; }
.phone-display { font-size: 32rpx; color: #333; margin-bottom: 20rpx; }
.bind-btn { background: #007aff; color: white; border-radius: 50rpx; height: 80rpx; line-height: 80rpx; font-size: 30rpx; margin-top: 20rpx; }
.member-info { margin-bottom: 40rpx; }
.item { display: flex; justify-content: space-between; font-size: 32rpx; margin: 20rpx 0; }
.label { color: #666; }
.value { font-weight: bold; }
.upgrade-btn { background: #ff4d4f; color: white; border-radius: 50rpx; margin-top: 40rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; }
.save-btn { position: fixed; bottom: 40rpx; left: 30rpx; right: 30rpx; background: #007aff; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; box-shadow: 0 8rpx 24rpx rgba(0,122,255,0.3); }
</style>