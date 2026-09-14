<template>
  <view class="app-shell">
    <view class="sx-desktop-nav" v-if="showNav">
      <text class="brand">Synerixis</text>
      <view
        v-for="item in navItems"
        :key="item.path"
        class="nav-item"
        :class="{ active: item.active }"
        @tap="go(item)"
      >
        {{ item.label }}
      </view>
    </view>
    <view class="shell-body">
      <slot />
    </view>
  </view>
</template>

<script>
export default {
  name: 'AppShell',
  props: {
    active: { type: String, default: '' },
    showNav: { type: Boolean, default: true }
  },
  computed: {
    navItems() {
      const a = this.active;
      return [
        { label: '仪表盘', path: '/pages/dashboard/dashboard', tab: true, key: 'dashboard', active: a === 'dashboard' },
        { label: '收件箱', path: '/pages/merchant/sessions', tab: true, key: 'inbox', active: a === 'inbox' },
        { label: '店铺', path: '/pages/merchant/shops', tab: true, key: 'shops', active: a === 'shops' },
        { label: 'AI 设置', path: '/pages/merchant/ai-settings', tab: false, key: 'ai', active: a === 'ai' },
        { label: '计费', path: '/pages/merchant/billing', tab: false, key: 'billing', active: a === 'billing' },
        { label: '我的', path: '/pages/profile/index', tab: true, key: 'profile', active: a === 'profile' }
      ];
    }
  },
  methods: {
    go(item) {
      if (item.tab) {
        uni.switchTab({ url: item.path });
      } else {
        uni.navigateTo({ url: item.path });
      }
    }
  }
};
</script>

<style scoped lang="scss">
@import '../../styles/tokens.scss';
@import '../../styles/merchant.scss';

.app-shell {
  min-height: 100vh;
  background: $sx-bg;
}

.shell-body {
  min-height: 100vh;
}
</style>
