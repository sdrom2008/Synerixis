<template>
  <el-container class="admin-shell">
    <el-aside :width="collapsed ? '64px' : '220px'" class="aside">
      <div class="brand">
        <span class="logo">S</span>
        <span v-if="!collapsed" class="brand-text">Synerixis</span>
      </div>
      <el-menu
        :default-active="active"
        :collapse="collapsed"
        router
        background-color="#0f172a"
        text-color="#94a3b8"
        active-text-color="#ffffff"
        class="menu"
      >
        <el-menu-item index="/dashboard">
          <el-icon><Odometer /></el-icon>
          <span>概览</span>
        </el-menu-item>
        <el-menu-item index="/merchants">
          <el-icon><User /></el-icon>
          <span>商家</span>
        </el-menu-item>
        <el-menu-item index="/shops">
          <el-icon><Shop /></el-icon>
          <span>店铺连接</span>
        </el-menu-item>
        <el-menu-item index="/sessions">
          <el-icon><ChatDotRound /></el-icon>
          <span>会话监控</span>
        </el-menu-item>
        <el-menu-item index="/usage">
          <el-icon><Coin /></el-icon>
          <span>用量计费</span>
        </el-menu-item>
        <el-menu-item index="/audit">
          <el-icon><Document /></el-icon>
          <span>审计日志</span>
        </el-menu-item>
        <el-menu-item index="/settings">
          <el-icon><Setting /></el-icon>
          <span>系统设置</span>
        </el-menu-item>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="header">
        <div class="left">
          <el-button text @click="collapsed = !collapsed">
            <el-icon><Fold v-if="!collapsed" /><Expand v-else /></el-icon>
          </el-button>
          <span class="header-title">{{ title }}</span>
        </div>
        <div class="right">
          <el-switch
            v-model="theme.isDark"
            inline-prompt
            active-text="暗"
            inactive-text="亮"
          />
          <el-button text type="danger" @click="logout">退出</el-button>
        </div>
      </el-header>
      <el-main class="main">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useThemeStore } from '@/stores/theme'

const route = useRoute()
const router = useRouter()
const theme = useThemeStore()
const collapsed = ref(false)

const active = computed(() => route.path)
const title = computed(() => (route.meta.title as string) || '运营后台')

function logout() {
  localStorage.removeItem('sx_admin_token')
  router.push({ name: 'login' })
}
</script>

<style scoped lang="scss">
.admin-shell {
  height: 100%;
}
.aside {
  background: #0f172a;
  transition: width 0.2s ease;
  overflow: hidden;
}
.brand {
  display: flex;
  align-items: center;
  gap: 10px;
  height: 56px;
  padding: 0 16px;
  color: #fff;
}
.logo {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  background: #2563eb;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 14px;
}
.brand-text {
  font-weight: 600;
  letter-spacing: 0.02em;
}
.menu {
  border-right: none;
}
.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 56px;
  border-bottom: 1px solid var(--el-border-color-lighter);
  background: var(--el-bg-color);
}
.left,
.right {
  display: flex;
  align-items: center;
  gap: 12px;
}
.header-title {
  font-weight: 600;
}
.main {
  background: var(--sx-bg);
  padding: 20px 24px;
}
</style>
