<template>
  <view class="shop-setting-page">
    <!-- 导航栏 -->
    <view class="navbar">
      <view class="back" @tap="goBack">← {{ currentLang === 'zh-CN' ? '返回' : 'Back' }}</view>
      <view class="title">{{ t('merchant.dashboard') }}</view>
      <view class="placeholder"></view>
    </view>

    <!-- 表单 -->
    <view class="form-container">
      <view class="form-group">
        <view class="label required">{{ t('merchant.shopName') }}</view>
        <input 
          v-model="form.shopName" 
          :placeholder="t('merchant.placeholderShopName')" 
          class="input" 
          maxlength="30" 
          focus 
        />
        <view class="tip">{{ t('merchant.shopName') }}{{ currentLang === 'zh-CN' ? '用于 AI 生成文案时自动带入店铺名' : 'Used to auto-include shop name in AI-generated copy' }}</view>
      </view>

      <view class="form-group">
        <view class="label">{{ t('merchant.shopLogo') }}</view>
        <view class="logo-preview" v-if="form.shopLogo">
          <image :src="form.shopLogo" mode="aspectFill" class="logo-img" @tap="previewLogo" />
        </view>
        <input 
          v-model="form.shopLogo" 
          :placeholder="t('merchant.shopLogo')" 
          class="input" 
        />
        <button class="upload-btn" @tap="chooseLogo" :loading="uploading">
          {{ t('merchant.uploadLogo') }}
        </button>
        <view class="tip">{{ currentLang === 'zh-CN' ? '建议正方形 1:1，尺寸 500x500 以上，PNG/JPG 格式' : 'Recommended square 1:1, at least 500x500, PNG/JPG format' }}</view>
      </view>

      <view class="form-group">
        <view class="label">{{ t('merchant.mainCategory') }}</view>
        <input 
          v-model="form.mainCategory" 
          :placeholder="t('merchant.placeholderCategory')" 
          class="input" 
          maxlength="100" 
        />
        <view class="tip">{{ currentLang === 'zh-CN' ? '帮助 AI 理解店铺定位，提升回复和营销方案精准度' : 'Help AI understand your shop positioning for better responses and marketing' }}</view>
      </view>

      <view class="form-group">
        <view class="label">{{ t('merchant.targetCustomer') }}</view>
        <textarea 
          v-model="form.targetCustomerDesc" 
          :placeholder="t('merchant.placeholderTarget')" 
          class="textarea" 
          maxlength="300" 
          show-confirm-bar 
        />
        <view class="tip">{{ currentLang === 'zh-CN' ? '越详细越好，AI 会据此生成更匹配的客服回复和营销内容' : 'More details help AI generate better responses and marketing content' }}</view>
      </view>
    </view>

    <!-- 保存按钮 -->
    <button class="save-btn" :loading="saving" @tap="save">
      {{ t('common.save') }}
    </button>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';
import { t, getLanguage } from '@/utils/i18n.js';

export default {
  data() {
    return {
      form: {
        shopName: '',
        shopLogo: '',
        mainCategory: '',
        targetCustomerDesc: ''
      },
      saving: false,
      uploading: false,
      currentLang: getLanguage()
    };
  },

  onLoad() {
    this.loadConfig();
  },

  methods: {
    t(key) {
      return t(key);
    },

    async loadConfig() {
      const token = uni.getStorageSync('token');
      if (!token) return uni.navigateTo({ url: '/pages/login/login' });

      const res = await uni.request({
        url: `${testbase}/api/seller/profile`,
        header: { Authorization: `Bearer ${token}` }
      });

      if (res.statusCode === 200 && res.data.config) {
        const config = res.data.config;
        this.form = {
          shopName: config.shopName || '',
          shopLogo: config.shopLogo || '',
          mainCategory: config.mainCategory || '',
          targetCustomerDesc: config.targetCustomerDesc || ''
        };
      }
    },

    async chooseLogo() {
      uni.chooseImage({
        count: 1,
        sizeType: ['compressed'],
        sourceType: ['album', 'camera'],
        success: res => {
          const tempFile = res.tempFilePaths[0];
          this.uploadLogo(tempFile);
        }
      });
    },

    async uploadLogo(filePath) {
      this.uploading = true;
      const token = uni.getStorageSync('token');

      uni.uploadFile({
        url: `${testbase}/api/upload/logo`,
        filePath,
        name: 'file',
        header: { Authorization: `Bearer ${token}` },
        success: res => {
          const data = JSON.parse(res.data);
          if (data.url) {
            const fullUrl = `${testbase}${data.url}`;
            this.form.shopLogo = fullUrl;
            uni.showToast({ title: 'LOGO上传成功', icon: 'success' });
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

    previewLogo() {
      uni.previewImage({ urls: [this.form.shopLogo] });
    },

    async save() {
      if (!this.form.shopName.trim()) {
        uni.showToast({ title: '店铺名称不能为空', icon: 'none' });
        return;
      }

      this.saving = true;
      const token = uni.getStorageSync('token');

      const res = await uni.request({
        url: `${testbase}/api/seller/config`,
        method: 'PUT',
        header: { 'content-type': 'application/json', Authorization: `Bearer ${token}` },
        data: this.form
      });

      this.saving = false;

      if (res.statusCode === 200) {
        uni.showToast({ title: t('common.success'), icon: 'success' });
        uni.navigateBack();
      } else {
        uni.showToast({ title: res.data?.msg || '保存失败，请重试', icon: 'none' });
      }
    },

    goBack() {
      uni.navigateBack();
    }
  }
};
</script>

<style>
.shop-setting-page { background: #f5f5f5; min-height: 100vh; padding-bottom: 120rpx; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.back { font-size: 48rpx; }
.title { font-size: 36rpx; font-weight: bold; }
.form-container { margin: 88rpx 30rpx 30rpx; background: white; border-radius: 24rpx; padding: 40rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.form-group { margin-bottom: 48rpx; }
.label { font-size: 30rpx; color: #333; margin-bottom: 20rpx; display: block; }
.required::after { content: '*'; color: red; margin-left: 8rpx; }
.input, .textarea { width: 100%; padding: 24rpx 30rpx; background: #f8f8f8; border-radius: 16rpx; font-size: 30rpx; border: 1rpx solid #eee; }
.textarea { height: 240rpx; }
.logo-preview { width: 240rpx; height: 240rpx; margin-bottom: 20rpx; border: 1rpx solid #eee; border-radius: 16rpx; overflow: hidden; }
.logo-img { width: 100%; height: 100%; }
.upload-btn { background: #f0f0f0; color: #333; border-radius: 16rpx; margin-top: 16rpx; height: 80rpx; line-height: 80rpx; font-size: 30rpx; }
.save-btn { position: fixed; bottom: 40rpx; left: 30rpx; right: 30rpx; background: #007aff; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; text-align: center; box-shadow: 0 8rpx 24rpx rgba(0,122,255,0.3); }
.tip { font-size: 26rpx; color: #999; margin-top: 16rpx; }
</style>
