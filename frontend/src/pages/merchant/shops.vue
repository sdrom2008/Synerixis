<template>
  <AppShell active="shops">
    <view class="sx-page sx-page-wide">
      <view class="sx-page-header">
        <view>
          <view class="sx-page-title">店铺绑定</view>
          <view class="sx-page-sub">连接 Shopee 后即可接收 Webhook 并自动回复</view>
        </view>
      </view>

      <view class="sx-card platform-card">
        <view class="platform-row">
          <view class="platform-meta">
            <view class="platform-name">Shopee</view>
            <view class="platform-desc">首发平台 · OAuth 授权</view>
          </view>
          <view
            class="sx-badge"
            :class="shopeeConnected ? 'sx-badge-success' : 'sx-badge-warning'"
          >
            {{ shopeeConnected ? '已连接' : '未连接' }}
          </view>
        </view>

        <view v-if="shopeeConn" class="conn-detail">
          <view class="detail-row">
            <text class="label">店铺名称</text>
            <text class="value">{{ shopeeConn.nickname || shopeeConn.Nickname || '—' }}</text>
          </view>
          <view class="detail-row">
            <text class="label">Shop ID</text>
            <text class="value">{{ shopeeConn.shopId || shopeeConn.ShopId || '—' }}</text>
          </view>
          <view class="detail-row">
            <text class="label">状态</text>
            <text class="value">{{ (shopeeConn.isActive ?? shopeeConn.IsActive) ? '启用中' : '已停用' }}</text>
          </view>
        </view>

        <view class="token-hint">
          <text class="hint-title">Token 有效期提示</text>
          <text class="hint-body">
            平台 Access Token 有时效。若自动回复突然失败，请重新授权。完整刷新与过期告警将在 P2 提供；当前请定期检查连接状态。
          </text>
        </view>

        <view class="actions">
          <button class="sx-btn sx-btn-primary" :loading="binding" @tap="connectShopee">
            {{ shopeeConnected ? '重新授权 Shopee' : '连接 Shopee' }}
          </button>
          <button
            v-if="shopeeConnected"
            class="sx-btn sx-btn-outline"
            :loading="unbinding"
            @tap="unbindShopee"
          >
            解绑
          </button>
        </view>
      </view>

      <EmptyState
        v-if="loaded && !shopeeConnected"
        class="sx-section"
        title="还没有绑定任何店铺"
        description="点击上方「连接 Shopee」完成 OAuth。授权成功后，买家消息将进入收件箱。"
        cta-text="开始授权"
        icon="链"
        @cta="connectShopee"
      />

      <view class="sx-section sx-card">
        <view class="sx-section-title">店铺资料（可选）</view>
        <text class="linkish" @tap="goShopProfile">编辑店铺名称 / 类目 →</text>
      </view>
    </view>
  </AppShell>
</template>

<script>
import AppShell from '@/components/merchant/AppShell.vue';
import EmptyState from '@/components/merchant/EmptyState.vue';
import { getConnections, getBindUrl, unbindPlatform } from '@/api/merchant.js';

export default {
  components: { AppShell, EmptyState },
  data() {
    return {
      connections: [],
      loaded: false,
      binding: false,
      unbinding: false
    };
  },
  computed: {
    shopeeConn() {
      return (this.connections || []).find((c) => {
        const p = (c.platform || c.Platform || '').toUpperCase();
        return p === 'SHOPEE';
      });
    },
    shopeeConnected() {
      if (!this.shopeeConn) return false;
      return !!(this.shopeeConn.isActive ?? this.shopeeConn.IsActive);
    }
  },
  onShow() {
    this.load();
  },
  methods: {
    async load() {
      try {
        const data = await getConnections();
        this.connections = data?.items || [];
      } catch (e) {
        this.connections = [];
      }
      this.loaded = true;
    },
    async connectShopee() {
      this.binding = true;
      try {
        const data = await getBindUrl('shopee');
        const url = data?.url;
        if (!url) {
          uni.showToast({ title: '未获取到授权链接', icon: 'none' });
          return;
        }
        // H5: open OAuth URL; App/小程序 may need plus.runtime or web-view
        // #ifdef H5
        window.location.href = url;
        // #endif
        // #ifndef H5
        uni.setClipboardData({
          data: url,
          success: () => {
            uni.showModal({
              title: '授权链接已复制',
              content: '请在浏览器中打开完成 Shopee OAuth 授权。',
              showCancel: false
            });
          }
        });
        // #endif
      } catch (e) {
        /* handled */
      } finally {
        this.binding = false;
      }
    },
    async unbindShopee() {
      uni.showModal({
        title: '确认解绑',
        content: '解绑后将无法自动回复该店消息，确定继续？',
        success: async (res) => {
          if (!res.confirm) return;
          this.unbinding = true;
          try {
            await unbindPlatform('shopee');
            uni.showToast({ title: '已解绑', icon: 'success' });
            this.load();
          } catch (e) {
            /* handled */
          } finally {
            this.unbinding = false;
          }
        }
      });
    },
    goShopProfile() {
      uni.navigateTo({ url: '/pages/profile/shop-setting' });
    }
  }
};
</script>

<style lang="scss">
@import '../../styles/merchant.scss';

.platform-card {
  margin-bottom: 24rpx;
}

.platform-row {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 24rpx;
}

.platform-name {
  font-size: 36rpx;
  font-weight: 700;
  color: #0F172A;
}

.platform-desc {
  margin-top: 8rpx;
  font-size: 24rpx;
  color: #64748B;
}

.conn-detail {
  background: #F8FAFC;
  border-radius: 16rpx;
  padding: 20rpx 24rpx;
  margin-bottom: 24rpx;
}

.detail-row {
  display: flex;
  justify-content: space-between;
  padding: 12rpx 0;
  font-size: 28rpx;
}

.detail-row .label {
  color: #64748B;
}

.detail-row .value {
  color: #0F172A;
  font-weight: 500;
  max-width: 60%;
  text-align: right;
}

.token-hint {
  border: 1rpx solid #FDE68A;
  background: #FFFBEB;
  border-radius: 16rpx;
  padding: 20rpx 24rpx;
  margin-bottom: 32rpx;
}

.hint-title {
  display: block;
  font-size: 26rpx;
  font-weight: 650;
  color: #D97706;
  margin-bottom: 8rpx;
}

.hint-body {
  font-size: 24rpx;
  color: #92400E;
  line-height: 1.55;
}

.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 20rpx;
}

.linkish {
  color: #2563EB;
  font-size: 28rpx;
}
</style>
