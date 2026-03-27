<template>
  <view class="optimize-page">
    <!-- 导航栏 -->
    <view class="navbar">
      <view class="back" @tap="goBack">← 返回</view>
      <view class="title">AI 优化商品</view>
      <view class="placeholder"></view>
    </view>

    <!-- 商品原信息 -->
    <view class="section">
      <view class="section-title">原商品信息</view>
      <view class="item">
        <text class="label">标题</text>
        <text class="value">{{ original.title }}</text>
      </view>
      <view class="item">
        <text class="label">描述</text>
        <text class="value">{{ original.description }}</text>
      </view>
      <view class="item">
        <text class="label">标签</text>
        <view class="tags">
          <text v-for="(tag, idx) in original.tags" :key="idx" class="tag">{{ tag }}</text>
        </view>
      </view>
    </view>

    <!-- 优化后信息 -->
    <view class="section">
      <view class="section-title">AI 优化后（预计流量提升 25%）</view>
      <view class="item">
        <text class="label">标题</text>
        <text class="value optimized">{{ optimized.title }}</text>
      </view>
      <view class="item">
        <text class="label">描述</text>
        <text class="value optimized">{{ optimized.description }}</text>
      </view>
      <view class="item">
        <text class="label">标签</text>
        <view class="tags">
          <text v-for="(tag, idx) in optimized.tags" :key="idx" class="tag optimized">{{ tag }}</text>
        </view>
      </view>
      <view class="tip">优化关键词已匹配当前热销趋势，如“爆款”“高性价比”，预计搜索排名提升</view>
    </view>

    <!-- 操作按钮 -->
    <button class="save-btn" :loading="saving" @tap="saveOptimized">
      保存优化结果
    </button>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      productId: '',
      original: {
        title: '',
        description: '',
        tags: []
      },
      optimized: {
        title: '',
        description: '',
        tags: []
      },
      saving: false
    };
  },

  onLoad(options) {
    this.productId = options.id;
    this.loadProduct();
  },

  methods: {
    async loadProduct() {
      try {
        uni.showLoading({ title: 'AI 优化中...' });
        const token = uni.getStorageSync('token');
        
        // 先获取商品详情
        const productRes = await uni.request({
          url: `${testbase}/api/seller/products?page=1&pageSize=1`,
          header: { Authorization: `Bearer ${token}` }
        });

        if (productRes.statusCode !== 200 || !productRes.data.items || productRes.data.items.length === 0) {
          uni.hideLoading();
          uni.showToast({ title: '商品加载失败', icon: 'none' });
          return;
        }

        const product = productRes.data.items.find(p => p.id === this.productId);
        if (!product) {
          uni.hideLoading();
          uni.showToast({ title: '商品不存在', icon: 'none' });
          return;
        }

        this.original = {
          title: product.title,
          description: product.description || '',
          tags: this.parseTags(product.tagsJson)
        };

        // 调用后端 AI 优化接口
        const optimizeRes = await uni.request({
          url: `${testbase}/api/seller/products/${this.productId}/optimize-suggestion`,
          method: 'GET',
          header: { Authorization: `Bearer ${token}` }
        });

        uni.hideLoading();

        if (optimizeRes.statusCode === 200 && optimizeRes.data) {
          this.optimized = {
            title: optimizeRes.data.optimizedTitle,
            description: optimizeRes.data.optimizedDescription,
            tags: optimizeRes.data.optimizedTags || []
          };
          uni.showToast({ title: 'AI 优化完成', icon: 'success' });
        } else {
          // 降级处理：使用简单的本地优化
          this.generateLocalOptimization();
          uni.showToast({ title: '使用本地优化方案', icon: 'none' });
        }
      } catch (error) {
        uni.hideLoading();
        console.error('[ERROR]', error);
        this.generateLocalOptimization();
        uni.showToast({ title: '使用本地优化方案', icon: 'none' });
      }
    },

    parseTags(tagsJson) {
      try {
        return JSON.parse(tagsJson || '[]');
      } catch {
        return [];
      }
    },

    generateLocalOptimization() {
      // 本地优化方案（无 AI 时的降级）
      this.optimized = {
        title: this.original.title + ' [爆款高性价比]',
        description: this.original.description + ' 匹配热销趋势，品质优选',
        tags: [...this.original.tags, '爆款', '高性价比', '2026新款']
      };
    },

    async saveOptimized() {
      try {
        this.saving = true;
        const token = uni.getStorageSync('token');

        const sendData = {
          optimizedTitle: this.optimized.title,
          optimizedDescription: this.optimized.description,
          optimizedTagsJson: JSON.stringify(this.optimized.tags || [])
        };

        const res = await uni.request({
          url: `${testbase}/api/seller/products/${this.productId}/optimize`,
          method: 'PUT',
          header: { 'content-type': 'application/json', Authorization: `Bearer ${token}` },
          data: sendData
        });

        this.saving = false;

        if (res.statusCode === 200) {
          uni.showToast({ title: '优化结果已保存', icon: 'success' });
          setTimeout(() => {
            uni.navigateBack();
          }, 500);
        } else {
          uni.showToast({ title: res.data?.message || '保存失败', icon: 'none' });
        }
      } catch (error) {
        this.saving = false;
        console.error('[ERROR]', error);
        uni.showToast({ title: '网络错误', icon: 'none' });
      }
    },

    goBack() {
      uni.navigateBack();
    }
  }
};
</script>

<style>
.optimize-page { background: #f5f5f5; min-height: 100vh; padding-bottom: 120rpx; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.back { font-size: 48rpx; }
.title { font-size: 36rpx; font-weight: bold; }
.section { background: white; margin: 88rpx 30rpx 30rpx; padding: 40rpx; border-radius: 24rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.section-title { font-size: 36rpx; font-weight: bold; margin-bottom: 40rpx; }
.item { margin-bottom: 32rpx; }
.label { font-size: 30rpx; color: #666; margin-bottom: 16rpx; display: block; }
.value { font-size: 32rpx; color: #333; line-height: 48rpx; }
.optimized { color: #007aff; font-weight: bold; }
.tags { display: flex; flex-wrap: wrap; }
.tag { background: #f0f0f0; color: #666; padding: 8rpx 16rpx; border-radius: 8rpx; margin: 8rpx 16rpx 8rpx 0; font-size: 28rpx; }
.tag.optimized { background: #e6f7ff; color: #007aff; }
.tip { font-size: 28rpx; color: #999; margin-top: 32rpx; line-height: 48rpx; }
.save-btn { position: fixed; bottom: 40rpx; left: 30rpx; right: 30rpx; background: #007aff; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; text-align: center; box-shadow: 0 8rpx 24rpx rgba(0,122,255,0.3); }
</style>