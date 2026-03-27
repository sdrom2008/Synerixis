<template>
  <view class="profile-page">
    <!-- 顶部导航 -->
    <view class="navbar">
      <view class="title">我的</view>
      <view class="right-menu" @tap="showMenu">
        <text class="icon">•••</text>
      </view>
    </view>

    <!-- 用户信息卡片 -->
    <view class="user-card">
      <view class="avatar-wrapper" @tap="chooseAvatar">
        <image :src="form.avatarUrl || '/static/default-avatar.png'" mode="aspectFill" class="avatar" />
        <view class="edit-overlay">
          <text>更换头像</text>
        </view>
      </view>
      <view class="user-info">
        <view class="nickname">{{ form.nickname || '商户' }}</view>
        <view class="phone">手机号：{{ form.phone || '未绑定' }}</view>
        <view class="level">
          <text class="badge" :class="profile.subscriptionLevel === 'Pro' ? 'pro' : 'free'">
            {{ profile.subscriptionLevel || '免费版' }}
          </text>
          <text class="quota">剩余 {{ profile.freeQuota || 0 }} 条</text>
        </view>
      </view>
    </view>

    <!-- 店铺信息 -->
    <view class="section">
      <view class="section-title">店铺信息</view>
      <view class="form-group">
        <view class="label required">店铺名称</view>
        <input v-model="form.shopName" placeholder="请输入店铺名称" class="input" maxlength="30" />
      </view>
      <view class="form-group">
        <view class="label">店铺LOGO</view>
        <view class="logo-preview" v-if="form.shopLogo">
          <image :src="form.shopLogo" mode="aspectFill" class="logo-img" @tap="previewLogo" />
        </view>
        <input v-model="form.shopLogo" placeholder="LOGO URL 或上传" class="input" />
        <button class="upload-btn" @tap="chooseLogo" :loading="uploading">上传图片</button>
      </view>
      <view class="form-group">
        <view class="label">主营类目</view>
        <input v-model="form.mainCategory" placeholder="如：女装,数码（用逗号分隔，最多5个）" class="input" maxlength="100" />
      </view>
      <view class="form-group">
        <view class="label">目标客户描述</view>
        <textarea v-model="form.targetCustomerDesc" placeholder="描述你的目标客户群体，帮助 AI 更精准回复" class="textarea" maxlength="300" show-confirm-bar />
      </view>
    </view>

    <!-- AI 偏好设置 -->
    <view class="section">
      <view class="section-title">AI 偏好设置</view>
      <view class="form-group">
        <view class="label">回复语气</view>
        <view class="segmented">
          <view v-for="(item, index) in toneOptions" :key="index" 
                :class="['segment', toneIndex === index ? 'active' : '']" 
                @tap="toneIndex = index; form.defaultReplyTone = item.value">
            {{ item.label }}
          </view>
        </view>
      </view>
      <view class="form-group">
        <view class="label">偏好语言</view>
        <view class="segmented">
          <view v-for="(item, index) in langOptions" :key="index" 
                :class="['segment', langIndex === index ? 'active' : '']" 
                @tap="langIndex = index; form.preferredLanguage = item.value">
            {{ item.label }}
          </view>
        </view>
      </view>
      <view class="form-group switch">
        <view class="label">开启主动营销提醒</view>
        <switch :checked="form.enableAutoMarketingReminder" @change="form.enableAutoMarketingReminder = $event.detail.value" color="#007aff" />
      </view>
    </view>

    <!-- 账号与安全 -->
    <view class="section">
      <view class="section-title">账号与安全</view>
      <view class="menu-item" @tap="logout">
        <text>退出登录</text>
        <text class="arrow">></text>
      </view>
      <view class="menu-item" @tap="contactSupport">
        <text>联系客服</text>
        <text class="arrow">></text>
      </view>
      <view class="version">Synerixis v1.0.0 | © 2026</view>
    </view>

    <!-- 保存按钮 -->
    <button class="save-btn" :loading="saving" @tap="saveConfig">保存设置</button>
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
        phone: '',
        shopName: '',
        shopLogo: '',
        mainCategory: '',
        targetCustomerDesc: '',
        defaultReplyTone: 'professional',
        preferredLanguage: 'zh',
        enableAutoMarketingReminder: true
      },
      toneOptions: [
        { label: '专业', value: 'professional' },
        { label: '亲切', value: 'friendly' },
        { label: '幽默', value: 'humorous' }
      ],
      toneIndex: 0,
      langOptions: [
        { label: '中文', value: 'zh' },
        { label: '英文', value: 'en' },
        { label: '双语', value: 'bilingual' }
      ],
      langIndex: 0,
      saving: false,
      uploading: false
    };
  },

  onLoad() {
    this.loadProfile();
  },

  methods: {
    async loadProfile() {
      try {
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
            phone: res.data.phone || '',
            shopName: res.data.config?.shopName || '',
            shopLogo: res.data.config?.shopLogo || '',
            mainCategory: res.data.config?.mainCategory || '',
            targetCustomerDesc: res.data.config?.targetCustomerDesc || '',
            defaultReplyTone: res.data.config?.defaultReplyTone || 'professional',
            preferredLanguage: res.data.config?.preferredLanguage || 'zh',
            enableAutoMarketingReminder: res.data.config?.enableAutoMarketingReminder ?? true
          };
          this.toneIndex = this.toneOptions.findIndex(t => t.value === this.form.defaultReplyTone);
          this.langIndex = this.langOptions.findIndex(l => l.value === this.form.preferredLanguage);
        } else {
          uni.showToast({ title: res.data?.message || '加载失败', icon: 'none' });
        }
      } catch (error) {
        console.error('[ERROR]', error);
        uni.showToast({ title: '网络错误，请重试', icon: 'none' });
      }
    },

    toneChange(e) {
      this.form.defaultReplyTone = this.toneOptions[e.detail.value].value;
    },

    langChange(e) {
      this.form.preferredLanguage = this.langOptions[e.detail.value].value;
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

    async chooseLogo() {
      uni.chooseImage({
        count: 1,
        sizeType: ['compressed'],
        sourceType: ['album', 'camera'],
        success: res => {
          const tempFile = res.tempFilePaths[0];
          this.uploadFile(tempFile, 'logo');
        }
      });
    },

    async uploadFile(filePath, type) {
      try {
        this.uploading = true;
        const token = uni.getStorageSync('token');

        uni.uploadFile({
          url: `${testbase}/api/seller/upload/${type}`,
          filePath,
          name: 'file',
          header: { Authorization: `Bearer ${token}` },
          success: res => {
            try {
              const data = JSON.parse(res.data);
              if (data.url) {
                if (type === 'avatar') this.form.avatarUrl = data.url;
                else this.form.shopLogo = data.url;
                uni.showToast({ title: '上传成功', icon: 'success' });
              } else {
                uni.showToast({ title: data.message || '上传失败', icon: 'none' });
              }
            } catch (e) {
              uni.showToast({ title: '响应解析失败', icon: 'none' });
            }
          },
          fail: err => {
            uni.showToast({ title: '上传失败', icon: 'none' });
            console.error('[ERROR]', err);
          },
          complete: () => {
            this.uploading = false;
          }
        });
      } catch (error) {
        this.uploading = false;
        console.error('[ERROR]', error);
      }
    },

    async saveConfig() {
      try {
        if (!this.form.shopName || !this.form.shopName.trim()) {
          uni.showToast({ title: '店铺名称不能为空', icon: 'none' });
          return;
        }

        this.saving = true;
        const token = uni.getStorageSync('token');

        // 分别更新个人信息和配置
        const configUpdateRes = await uni.request({
          url: `${testbase}/api/seller/config`,
          method: 'PUT',
          header: { 'content-type': 'application/json', Authorization: `Bearer ${token}` },
          data: {
            shopName: this.form.shopName,
            shopLogo: this.form.shopLogo,
            mainCategory: this.form.mainCategory,
            targetCustomerDesc: this.form.targetCustomerDesc,
            defaultReplyTone: this.form.defaultReplyTone,
            preferredLanguage: this.form.preferredLanguage,
            enableAutoMarketingReminder: this.form.enableAutoMarketingReminder
          }
        });

        if (configUpdateRes.statusCode === 200) {
          // 如果个人信息有变化，也更新
          if (this.form.nickname !== this.profile.nickname || this.form.avatarUrl !== this.profile.avatarUrl) {
            await uni.request({
              url: `${testbase}/api/seller/profile`,
              method: 'PUT',
              header: { 'content-type': 'application/json', Authorization: `Bearer ${token}` },
              data: {
                nickname: this.form.nickname,
                avatarUrl: this.form.avatarUrl
              }
            });
          }

          uni.showToast({ title: '保存成功', icon: 'success' });
          this.loadProfile();
        } else {
          uni.showToast({ title: configUpdateRes.data?.message || '保存失败', icon: 'none' });
        }
      } catch (error) {
        console.error('[ERROR]', error);
        uni.showToast({ title: '网络错误', icon: 'none' });
      } finally {
        this.saving = false;
      }
    },

    logout() {
      uni.showModal({
        title: '提示',
        content: '确定退出登录吗？',
        success: res => {
          if (res.confirm) {
            uni.removeStorageSync('token');
            uni.removeStorageSync('sellerId');
            uni.navigateTo({ url: '/pages/login/login' });
          }
        }
      });
    },

    contactSupport() {
      uni.showModal({
        title: '联系客服',
        content: '微信：synerixis_support\n邮箱：support@synerixis.com',
        showCancel: false
      });
    },

    previewLogo() {
      if (this.form.shopLogo) {
        uni.previewImage({
          urls: [this.form.shopLogo]
        });
      }
    }
  }
};
</script>

<style>
.profile-page { background: #f5f5f5; min-height: 100vh; padding-bottom: 120rpx; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.title { font-size: 36rpx; font-weight: bold; }
.right-menu { font-size: 48rpx; }
.user-card { background: white; margin: 88rpx 30rpx 30rpx; padding: 40rpx; border-radius: 24rpx; display: flex; align-items: center; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.avatar-wrapper { position: relative; width: 140rpx; height: 140rpx; border-radius: 50%; overflow: hidden; }
.avatar { width: 100%; height: 100%; }
.edit-overlay { position: absolute; bottom: 0; left: 0; right: 0; background: rgba(0,0,0,0.5); color: white; text-align: center; font-size: 24rpx; line-height: 60rpx; }
.user-info { margin-left: 30rpx; flex: 1; }
.nickname { font-size: 40rpx; font-weight: bold; }
.phone { font-size: 28rpx; color: #666; margin-top: 8rpx; }
.level { margin-top: 12rpx; display: flex; align-items: center; }
.badge { padding: 4rpx 16rpx; border-radius: 20rpx; font-size: 24rpx; }
.pro { background: #007aff; color: white; }
.free { background: #999; color: white; }
.quota { font-size: 28rpx; color: #666; margin-left: 12rpx; }
.section { background: white; margin: 0 30rpx 30rpx; padding: 40rpx; border-radius: 24rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.section-title { font-size: 36rpx; font-weight: bold; margin-bottom: 40rpx; }
.form-group { margin-bottom: 48rpx; }
.label { font-size: 30rpx; color: #333; margin-bottom: 20rpx; display: block; }
.required::after { content: '*'; color: red; margin-left: 8rpx; }
.input, .textarea { width: 100%; padding: 24rpx 30rpx; background: #f8f8f8; border-radius: 16rpx; font-size: 30rpx; border: 1rpx solid #eee; }
.textarea { height: 200rpx; }
.logo-preview { width: 200rpx; height: 200rpx; margin-bottom: 20rpx; border: 1rpx solid #eee; border-radius: 16rpx; overflow: hidden; }
.logo-img { width: 100%; height: 100%; }
.upload-btn { background: #f0f0f0; color: #333; border-radius: 16rpx; margin-top: 16rpx; height: 80rpx; line-height: 80rpx; font-size: 30rpx; }
.segmented { display: flex; background: #f8f8f8; border-radius: 16rpx; overflow: hidden; margin-top: 20rpx; }
.segment { flex: 1; text-align: center; padding: 24rpx 0; font-size: 30rpx; color: #666; border-right: 1rpx solid #ddd; }
.segment:last-child { border-right: none; }
.segment.active { background: #007aff; color: white; }
.switch { display: flex; justify-content: space-between; align-items: center; font-size: 32rpx; margin-top: 20rpx; }
.save-btn { position: fixed; bottom: 40rpx; left: 30rpx; right: 30rpx; background: #007aff; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; text-align: center; box-shadow: 0 8rpx 24rpx rgba(0,122,255,0.3); }
</style>