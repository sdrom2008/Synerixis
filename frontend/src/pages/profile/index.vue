<template>
  <AppShell active="profile">
    <view class="sx-page sx-page-wide">
      <view class="sx-card user-card" @tap="toPersonalInfo">
        <image class="avatar" :src="user.avatarUrl || '/static/default-avatar.png'" mode="aspectFill" />
        <view class="info">
          <view class="nickname">{{ user.nickname || '商家账号' }}</view>
          <view class="phone">{{ user.phone ? '手机 ' + user.phone : '未绑定手机号' }}</view>
          <view class="level">
            <text class="sx-badge sx-badge-info">{{ user.subscriptionLevel || '—' }}</text>
          </view>
        </view>
        <text class="chev">›</text>
      </view>

      <view class="menu sx-card">
        <view class="menu-item" @tap="toShops">
          <text>店铺绑定</text>
          <text class="chev">›</text>
        </view>
        <view class="menu-item" @tap="toAi">
          <text>AI 设置</text>
          <text class="chev">›</text>
        </view>
        <view class="menu-item" @tap="toBilling">
          <text>套餐与计费</text>
          <text class="chev">›</text>
        </view>
        <view class="menu-item" @tap="toShopSetting">
          <text>店铺资料</text>
          <text class="chev">›</text>
        </view>
        <view class="menu-item" @tap="toTeam">
          <text>团队管理</text>
          <text class="chev">›</text>
        </view>
        <view class="menu-item" @tap="toAccount">
          <text>账号与安全</text>
          <text class="chev">›</text>
        </view>
      </view>

      <view class="menu sx-card">
        <view class="menu-item" @tap="contactUs">
          <text>联系支持</text>
          <text class="chev">›</text>
        </view>
        <view class="menu-item danger" @tap="logout">
          <text>退出登录</text>
          <text class="chev">›</text>
        </view>
      </view>

      <view class="version">Synerixis 商家控制台 · v1.1</view>
    </view>
  </AppShell>
</template>

<script>
import AppShell from '@/components/merchant/AppShell.vue';
import { getSellerProfile } from '@/api/merchant.js';

export default {
  components: { AppShell },
  data() {
    return {
      user: {
        nickname: '',
        avatarUrl: '',
        phone: '',
        subscriptionLevel: ''
      }
    };
  },
  onShow() {
    this.loadUser();
  },
  methods: {
    async loadUser() {
      const token = uni.getStorageSync('token');
      if (!token) {
        uni.navigateTo({ url: '/pages/login/choose-login' });
        return;
      }
      try {
        const data = await getSellerProfile();
        this.user = {
          nickname: data.nickname || data.Nickname || '',
          avatarUrl: data.avatarUrl || data.AvatarUrl || '',
          phone: data.phone || data.Phone || '',
          subscriptionLevel: data.subscriptionLevel || data.SubscriptionLevel || '—'
        };
      } catch (e) {
        /* handled */
      }
    },
    toPersonalInfo() {
      uni.navigateTo({ url: '/pages/profile/profile-info' });
    },
    toShops() {
      uni.switchTab({ url: '/pages/merchant/shops' });
    },
    toAi() {
      uni.navigateTo({ url: '/pages/merchant/ai-settings' });
    },
    toBilling() {
      uni.navigateTo({ url: '/pages/merchant/billing' });
    },
    toShopSetting() {
      uni.navigateTo({ url: '/pages/profile/shop-setting' });
    },
    toTeam() {
      uni.navigateTo({ url: '/pages/merchant/team' });
    },
    toAccount() {
      uni.navigateTo({ url: '/pages/profile/account-security' });
    },
    logout() {
      uni.showModal({
        title: '退出登录',
        content: '确定要退出当前账号吗？',
        success: (res) => {
          if (res.confirm) {
            uni.removeStorageSync('token');
            uni.removeStorageSync('sellerId');
            uni.removeStorageSync('userType');
            uni.reLaunch({ url: '/pages/login/choose-login' });
          }
        }
      });
    },
    contactUs() {
      uni.showModal({
        title: '联系支持',
        content: '邮箱：support@synerixis.com',
        showCancel: false
      });
    }
  }
};
</script>

<style lang="scss">
@import '../../styles/merchant.scss';

.user-card {
  display: flex;
  align-items: center;
  margin-bottom: 24rpx;
}

.avatar {
  width: 112rpx;
  height: 112rpx;
  border-radius: 50%;
  background: #E2E8F0;
}

.info {
  flex: 1;
  margin-left: 24rpx;
}

.nickname {
  font-size: 34rpx;
  font-weight: 700;
  color: #0F172A;
}

.phone {
  font-size: 24rpx;
  color: #64748B;
  margin-top: 8rpx;
}

.level {
  margin-top: 12rpx;
}

.chev {
  color: #94A3B8;
  font-size: 36rpx;
}

.menu {
  padding: 0;
  margin-bottom: 24rpx;
  overflow: hidden;
}

.menu-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 32rpx 28rpx;
  font-size: 30rpx;
  color: #0F172A;
  border-bottom: 1rpx solid #F1F5F9;
}

.menu-item:last-child {
  border-bottom: none;
}

.menu-item.danger {
  color: #DC2626;
}

.version {
  text-align: center;
  padding: 24rpx 0 48rpx;
  color: #94A3B8;
  font-size: 22rpx;
}
</style>
