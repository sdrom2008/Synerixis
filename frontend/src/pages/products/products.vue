<template>
  <view class="products-page">
    <!-- 导航栏 -->
    <view class="navbar">
      <view class="title">我的商品</view>
      <button class="add-btn" @tap="toImport">+ 导入商品</button>
    </view>

    <!-- 搜索栏 -->
    <view class="search-bar">
      <input v-model="searchKeyword" placeholder="搜索商品标题或类目" class="search-input" />
      <button class="search-btn" @tap="search">搜索</button>
    </view>

    <!-- 商品列表 -->
    <scroll-view scroll-y class="list-container">
      <view v-if="products.length === 0" class="empty">
        暂无商品，点击上方“+ 导入商品”开始添加
      </view>
      <view v-else v-for="item in products" :key="item.id" class="product-item">
        <image :src="getFirstImage(item.imagesJson)" mode="aspectFill" class="thumb" />
        <view class="info">
          <view class="title">{{ item.title || '未命名商品' }}</view>
          <view class="price">￥{{ item.price || '--' }}</view>
          <view class="category">{{ item.category || '未分类' }}</view>
          <view class="tags">
            <text v-for="(tag, idx) in getTags(item.tagsJson)" :key="idx" class="tag">{{ tag }}</text>
          </view>
        </view>
        <view class="actions">
          <button class="opt-btn" @tap="optimizeProduct(item.id)">AI 优化</button>
          <button class="delete-btn" @tap="deleteProduct(item.id)">删除</button>
        </view>
      </view>

      <!-- 加载更多 -->
      <view v-if="hasMore" class="load-more" @tap="loadMore">
        {{ loading ? '加载中...' : '加载更多' }}
      </view>
    </scroll-view>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      products: [],
      searchKeyword: '',
      page: 1,
      pageSize: 10,
      total: 0,
      loading: false,
      hasMore: true
    };
  },

  onLoad() {
    this.loadProducts();
  },

  onReachBottom() {
    if (this.hasMore && !this.loading) {
      this.loadMore();
    }
  },

  methods: {
    async loadProducts(reset = false) {
      if (this.loading) return;
      this.loading = true;

      if (reset) {
        this.page = 1;
        this.products = [];
        this.hasMore = true;
      }

      const token = uni.getStorageSync('token');
      if (!token) return uni.navigateTo({ url: '/pages/login/login' });

      const res = await uni.request({
        url: `${testbase}/api/seller/products`,
        method: 'GET',
        data: {
          page: this.page,
          pageSize: this.pageSize,
          keyword: this.searchKeyword
        },
        header: { Authorization: `Bearer ${token}` }
      });

      this.loading = false;

      if (res.statusCode === 200) {
        const data = res.data;
        this.total = data.total;
        this.products = reset ? data.items : [...this.products, ...data.items];
        this.hasMore = this.products.length < this.total;
      } else {
        uni.showToast({ title: '加载失败', icon: 'none' });
      }
    },

    loadMore() {
      this.page++;
      this.loadProducts();
    },

    search() {
      this.loadProducts(true);
    },

    getFirstImage(imagesJson) {
      try {
        const images = JSON.parse(imagesJson || '[]');
        return images[0] || '/static/default-product.png';
      } catch {
        return '/static/default-product.png';
      }
    },

    getTags(tagsJson) {
      try {
        return JSON.parse(tagsJson || '[]');
      } catch {
        return [];
      }
    },

    toImport() {
      uni.navigateTo({ url: '/pages/products/import' });  // 导入页后续做
    },

    optimizeProduct(id) {
      uni.navigateTo({ url: `/pages/products/optimize?id=${id}` });
    },

    async deleteProduct(id) {
      uni.showModal({
        title: '删除商品',
        content: '确定删除该商品吗？删除后不可恢复',
        success: async res => {
          if (res.confirm) {
            try {
              const token = uni.getStorageSync('token');
              const delRes = await uni.request({
                url: `${testbase}/api/seller/products/${id}`,
                method: 'DELETE',
                header: { Authorization: `Bearer ${token}` }
              });

              if (delRes.statusCode === 200) {
                uni.showToast({ title: '删除成功', icon: 'success' });
                this.products = this.products.filter(p => p.id !== id);
              } else {
                uni.showToast({ title: delRes.data?.message || '删除失败', icon: 'none' });
              }
            } catch (error) {
              console.error('[ERROR]', error);
              uni.showToast({ title: '网络错误', icon: 'none' });
            }
          }
        }
      });
    }
  }
};
</script>

<style>
.products-page { background: #f5f5f5; min-height: 100vh; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.title { font-size: 36rpx; font-weight: bold; }
.add-btn { background: #007aff; color: white; border-radius: 50rpx; height: 80rpx; line-height: 80rpx; font-size: 30rpx; padding: 0 30rpx; }
.search-bar { padding: 88rpx 30rpx 20rpx; display: flex; background: white; }
.search-input { flex: 1; padding: 20rpx 30rpx; background: #f8f8f8; border-radius: 16rpx 0 0 16rpx; font-size: 30rpx; border: 1rpx solid #eee; border-right: none; }
.search-btn { background: #007aff; color: white; border-radius: 0 16rpx 16rpx 0; padding: 0 40rpx; font-size: 30rpx; }
.list-container { height: calc(100vh - 200rpx); }
.product-item { background: white; border-radius: 16rpx; margin: 20rpx 30rpx; padding: 30rpx; display: flex; box-shadow: 0 4rpx 16rpx rgba(0,0,0,0.06); }
.thumb { width: 160rpx; height: 160rpx; border-radius: 12rpx; margin-right: 30rpx; background: #eee; }
.info { flex: 1; }
.title { font-size: 32rpx; font-weight: bold; margin-bottom: 12rpx; }
.price { font-size: 36rpx; color: #ff4d4f; margin-bottom: 8rpx; }
.category { font-size: 28rpx; color: #666; margin-bottom: 8rpx; }
.tags { display: flex; flex-wrap: wrap; }
.tag { background: #f0f0f0; color: #666; padding: 4rpx 12rpx; border-radius: 8rpx; margin: 4rpx 8rpx 4rpx 0; font-size: 24rpx; }
.actions { display: flex; flex-direction: column; justify-content: center; }
.opt-btn, .delete-btn { margin: 10rpx 0; border-radius: 12rpx; font-size: 28rpx; height: 80rpx; line-height: 80rpx; }
.opt-btn { background: #fff7e6; color: #fa8c16; }
.delete-btn { background: #fff1f0; color: #ff4d4f; }
.empty { text-align: center; padding: 200rpx 0; color: #999; font-size: 36rpx; }
</style>