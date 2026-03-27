<template>
  <view class="import-page">
    <!-- 导航栏 -->
    <view class="navbar">
      <view class="back" @tap="goBack">← 返回</view>
      <view class="title">导入商品</view>
      <view class="placeholder"></view>
    </view>

    <!-- 导入方式切换 -->
    <view class="method-tabs">
      <view :class="['tab', importMethod === 'manual' ? 'active' : '']" @tap="importMethod = 'manual'">
        手动输入
      </view>
      <view :class="['tab', importMethod === 'url' ? 'active' : '']" @tap="importMethod = 'url'">
        链接抓取
      </view>
      <view class="tab disabled">Excel 批量（开发中）</view>
    </view>

    <!-- 手动输入表单 -->
    <view v-if="importMethod === 'manual'" class="form-section">
      <view class="form-group">
        <view class="label required">商品标题</view>
        <input v-model="manualForm.title" placeholder="请输入商品标题" class="input" maxlength="60" />
      </view>
      <view class="form-group">
        <view class="label">商品描述</view>
        <textarea v-model="manualForm.description" placeholder="详细描述商品特点、卖点、材质等" class="textarea" maxlength="500" />
      </view>
      <view class="form-group">
        <view class="label">售价（元）</view>
        <input v-model="manualForm.price" type="digit" placeholder="请输入售价" class="input" />
      </view>
      <view class="form-group">
        <view class="label">主图链接（多个用逗号分隔）</view>
        <input v-model="manualForm.images" placeholder="https://... , https://..." class="input" />
      </view>
      <view class="form-group">
        <view class="label">类目</view>
        <input v-model="manualForm.category" placeholder="如：女装,连衣裙" class="input" maxlength="100" />
      </view>
      <view class="form-group">
        <view class="label">标签（用逗号分隔）</view>
        <input v-model="manualForm.tags" placeholder="爆款,高性价比,网红同款" class="input" />
      </view>
    </view>

    <!-- 链接抓取 -->
    <view v-if="importMethod === 'url'" class="form-section">
      <view class="form-group">
        <view class="label required">商品链接</view>
        <input v-model="urlForm.url" placeholder="粘贴淘宝/京东/拼多多/抖店商品链接" class="input" />
      </view>
      <button class="fetch-btn" :loading="fetching" @tap="fetchFromUrl">
        自动抓取商品信息
      </button>

      <!-- 抓取预览 -->
      <view v-if="previewItem" class="preview-card">
        <view class="preview-title">抓取预览</view>
        <image :src="previewItem.images[0]" mode="aspectFill" class="preview-img" />
        <view class="preview-info">
          <view class="title">{{ previewItem.title }}</view>
          <view class="price">￥{{ previewItem.price || '--' }}</view>
          <view class="category">{{ previewItem.category || '未分类' }}</view>
          <view class="tags">
            <text v-for="(tag, idx) in previewItem.tags" :key="idx" class="tag">{{ tag }}</text>
          </view>
        </view>
      </view>
    </view>

    <!-- 保存按钮 -->
    <button class="save-btn" :loading="saving" @tap="saveProduct" v-if="importMethod === 'manual' || previewItem">
      确认导入商品
    </button>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      importMethod: 'manual',
      manualForm: {
        title: '',
        description: '',
        price: '',
        images: '',
        category: '',
        tags: ''
      },
      urlForm: {
        url: ''
      },
      previewItem: null,
      fetching: false,
      saving: false
    };
  },

  methods: {
    async fetchFromUrl() {
      if (!this.urlForm.url.trim()) {
        uni.showToast({ title: '请输入商品链接', icon: 'none' });
        return;
      }

      this.fetching = true;

      const res = await uni.request({
        url: `${testbase}/api/seller/products/fetch-url`,
        method: 'POST',
        data: { url: this.urlForm.url },
        header: { Authorization: `Bearer ${uni.getStorageSync('token')}` }
      });

      this.fetching = false;

      if (res.statusCode === 200 && res.data) {
        this.previewItem = res.data;
        uni.showToast({ title: '抓取成功，可直接导入', icon: 'success' });
      } else {
        uni.showToast({ title: res.data?.msg || '抓取失败，请检查链接', icon: 'none' });
      }
    },

    async saveProduct() {
      const formData = this.importMethod === 'manual' ? this.manualForm : this.previewItem;

      // 校验
      if (!formData.title || !formData.title.trim()) {
        uni.showToast({ title: '商品标题不能为空', icon: 'none' });
        return;
      }

      this.saving = true;
      const token = uni.getStorageSync('token');

      try {
        const sendData = {
          title: formData.title.trim(),
          description: formData.description?.trim() || '',
          price: formData.price ? String(formData.price).trim() : null,  // 转为字符串
          imagesJson: JSON.stringify(formData.images ? formData.images.split(',').map(s => s.trim()).filter(s => s) : []),
          category: formData.category?.trim() || '',
          tagsJson: JSON.stringify(formData.tags ? formData.tags.split(',').map(s => s.trim()).filter(s => s) : []),
          source: this.importMethod === 'url' ? 'url' : 'manual'
        };

        console.log('[DEBUG] 发送到 /import 的数据:', JSON.stringify(sendData));

        const res = await uni.request({
          url: `${testbase}/api/seller/products/import`,
          method: 'POST',
          header: { 'content-type': 'application/json', Authorization: `Bearer ${token}` },
          data: sendData
        });

        this.saving = false;

        console.log('[DEBUG] 后端返回:', res);

        if (res.statusCode === 200) {
          uni.showToast({ title: '导入成功！', icon: 'success' });
          // 返回列表页面
          setTimeout(() => {
            uni.navigateBack();
          }, 500);
        } else {
          const errorMsg = res.data?.message || res.data?.error || '导入失败，请检查输入';
          uni.showToast({ title: errorMsg, icon: 'none', duration: 3000 });
        }
      } catch (error) {
        this.saving = false;
        console.error('[ERROR]', error);
        uni.showToast({ title: '网络错误，请重试', icon: 'none' });
      }
    },

    goBack() {
      uni.navigateBack();
    }
  }
};
</script>

<style>
/* 保持原有样式不变 */
.import-page { background: #f5f5f5; min-height: 100vh; padding-bottom: 120rpx; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.back { font-size: 48rpx; }
.title { font-size: 36rpx; font-weight: bold; }
.method-tabs { display: flex; margin: 88rpx 30rpx 30rpx; background: white; border-radius: 24rpx; overflow: hidden; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.tab { flex: 1; padding: 36rpx 0; text-align: center; font-size: 32rpx; color: #666; border-right: 1rpx solid #eee; }
.tab:last-child { border-right: none; }
.tab.active { background: #007aff; color: white; }
.form-section { margin: 0 30rpx 30rpx; background: white; border-radius: 24rpx; padding: 40rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.form-group { margin-bottom: 48rpx; }
.label { font-size: 30rpx; color: #333; margin-bottom: 20rpx; display: block; }
.required::after { content: '*'; color: red; margin-left: 8rpx; }
.input, .textarea { width: 100%; padding: 24rpx 30rpx; background: #f8f8f8; border-radius: 16rpx; font-size: 30rpx; border: 1rpx solid #eee; }
.textarea { height: 240rpx; }
.fetch-btn { background: #007aff; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; margin-top: 20rpx; }
.preview-card { margin: 30rpx 0; background: #fff7e6; border-radius: 16rpx; padding: 40rpx; }
.preview-title { font-size: 36rpx; font-weight: bold; margin-bottom: 30rpx; }
.preview-img { width: 100%; height: 400rpx; border-radius: 16rpx; margin-bottom: 30rpx; background: #eee; }
.preview-info .title { font-size: 36rpx; font-weight: bold; margin-bottom: 20rpx; }
.preview-info .price { font-size: 48rpx; color: #ff4d4f; margin-bottom: 20rpx; }
.preview-info .category { font-size: 32rpx; color: #666; }
.save-btn { position: fixed; bottom: 40rpx; left: 30rpx; right: 30rpx; background: #007aff; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; text-align: center; box-shadow: 0 8rpx 24rpx rgba(0,122,255,0.3); }
</style>