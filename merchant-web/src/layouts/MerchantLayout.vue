<template>
  <el-container class="merchant-shell">
    <el-aside :width="collapsed ? '64px' : '220px'" class="aside">
      <div class="brand">
        <span class="logo">S</span>
        <div v-if="!collapsed" class="brand-text">
          <strong>Synerixis</strong>
          <span>桌面工作台</span>
        </div>
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
        <el-menu-item v-if="showOverview" index="/overview">
          <el-icon><Odometer /></el-icon>
          <span>概览</span>
        </el-menu-item>
        <el-menu-item :index="inboxMenuIndex">
          <el-icon><ChatDotRound /></el-icon>
          <span>收件箱</span>
          <el-badge
            v-if="overdueBadge > 0"
            :value="overdueBadge"
            type="danger"
            class="inbox-overdue-badge"
            title="已超时 · 点击筛选叫醒"
          />
        </el-menu-item>
        <el-menu-item index="/onboarding">
          <el-icon><Guide /></el-icon>
          <span>上手指南</span>
        </el-menu-item>
        <template v-if="perms.fullMenu">
          <el-menu-item index="/shops">
            <el-icon><Shop /></el-icon>
            <span>店铺绑定</span>
          </el-menu-item>
          <el-menu-item index="/ai-settings">
            <el-icon><Cpu /></el-icon>
            <span>AI 设置</span>
          </el-menu-item>
          <el-menu-item index="/quick-replies">
            <el-icon><ChatLineSquare /></el-icon>
            <span>快捷回复</span>
          </el-menu-item>
          <el-menu-item v-if="perms.canViewBilling" index="/billing">
            <el-icon><Coin /></el-icon>
            <span>计费</span>
          </el-menu-item>
          <el-menu-item index="/team">
            <el-icon><UserFilled /></el-icon>
            <span>团队</span>
          </el-menu-item>
          <el-menu-item index="/audit">
            <el-icon><Document /></el-icon>
            <span>操作日志</span>
          </el-menu-item>
        </template>
      </el-menu>
    </el-aside>

    <el-container>
      <el-header class="header">
        <div class="left">
          <el-button text @click="collapsed = !collapsed">
            <el-icon><Fold v-if="!collapsed" /><Expand v-else /></el-icon>
          </el-button>
          <span class="header-title">{{ title }}</span>
          <el-tag size="small" effect="plain" type="info">桌面工作台 · draft-first</el-tag>
        </div>
        <div class="right">
          <el-badge
            v-if="tokenBadge.count > 0"
            :value="tokenBadge.count"
            :type="tokenBadge.hasExpired ? 'danger' : 'warning'"
            class="token-badge"
          >
            <el-button size="small" :type="tokenBadge.hasExpired ? 'danger' : 'warning'" plain @click="router.push('/shops')">
              {{
                tokenBadge.needsRebind
                  ? '需重新授权'
                  : tokenBadge.hasExpired
                    ? 'Token 已过期'
                    : 'Token 即将过期'
              }}
            </el-button>
          </el-badge>
          <el-tag size="small" type="primary" effect="light">{{ auth.identityLabel }}</el-tag>
          <span class="user-name">{{ auth.displayName }}</span>
          <el-button text type="danger" @click="logout">退出登录</el-button>
        </div>
      </el-header>
      <el-main :class="['main', { flush: !!route.meta.flush }]">
        <router-view />
      </el-main>
    </el-container>
  </el-container>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { getConnections, getMerchantAlerts } from '@/api/merchant'

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()
const collapsed = ref(false)

const active = computed(() => route.path)
const title = computed(() => (route.meta.title as string) || '桌面工作台')
const perms = computed(() => auth.permissions)
const showOverview = computed(() => perms.value.canViewOverview)

const tokenBadge = ref<{ count: number; hasExpired: boolean; needsRebind: boolean }>({
  count: 0,
  hasExpired: false,
  needsRebind: false,
})
const overdueBadge = ref(0)
let badgeTimer: ReturnType<typeof setInterval> | null = null

async function refreshTokenBadge() {
  if (!perms.value.canManageShops) {
    tokenBadge.value = { count: 0, hasExpired: false, needsRebind: false }
    return
  }
  try {
    const res = await getConnections()
    const raw = res as { items?: Record<string, unknown>[] } | Record<string, unknown>[]
    const items = Array.isArray(raw) ? raw : raw.items || []
    let expired = 0
    let expiring = 0
    let needsRebind = false
    for (const c of items) {
      const st = String(c.status || c.tokenStatus || '')
      if (st === 'expired') expired++
      else if (st === 'expiring') expiring++
      if (c.needsRebind === true || (st === 'expired' && c.lastRefreshError)) needsRebind = true
    }
    tokenBadge.value = {
      count: expired + expiring,
      hasExpired: expired > 0,
      needsRebind,
    }
  } catch {
    /* ignore badge errors */
  }
}

async function refreshOverdueBadge() {
  if (!auth.isAuthenticated) {
    overdueBadge.value = 0
    return
  }
  try {
    const alerts = await getMerchantAlerts()
    if (typeof alerts?.overdueCount === 'number') {
      overdueBadge.value = alerts.overdueCount
      return
    }
    const items = alerts?.items || []
    overdueBadge.value = items.filter(
      (a) => a.type !== 'connection_token' && a.slaUrgency === 'overdue',
    ).length
  } catch {
    overdueBadge.value = 0
  }
}

function logout() {
  auth.clear()
  router.push({ name: 'login' })
}

/** 有超时红点时点收件箱直达「已超时」筛选（对内叫醒，非自动回复买家） */
const inboxMenuIndex = computed(() =>
  overdueBadge.value > 0 ? '/inbox?filter=overdue' : '/inbox',
)

function onAlertsChanged() {
  void refreshOverdueBadge()
}

onMounted(() => {
  refreshTokenBadge()
  refreshOverdueBadge()
  badgeTimer = setInterval(() => {
    refreshTokenBadge()
    refreshOverdueBadge()
  }, 60_000)
  window.addEventListener('sx-alerts-changed', onAlertsChanged)
})
onUnmounted(() => {
  if (badgeTimer) clearInterval(badgeTimer)
  window.removeEventListener('sx-alerts-changed', onAlertsChanged)
})
</script>

<style scoped lang="scss">
.merchant-shell {
  height: 100%;
}
.aside {
  background: #0f172a;
  transition: width 0.2s ease;
  overflow: hidden;
  border-right: 1px solid #1e293b;
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
  flex-shrink: 0;
}
.brand-text {
  display: flex;
  flex-direction: column;
  line-height: 1.2;
  strong {
    font-size: 14px;
  }
  span {
    font-size: 11px;
    color: #94a3b8;
  }
}
.menu {
  border-right: none;
}
.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  height: 56px;
  border-bottom: 1px solid var(--sx-border);
  background: var(--sx-surface);
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
.user-name {
  font-size: 13px;
  color: var(--sx-muted);
}
.token-badge {
  margin-right: 4px;
}
.inbox-overdue-badge {
  margin-left: 8px;
  :deep(.el-badge__content) {
    position: static;
    transform: none;
  }
}
.main {
  background: var(--sx-bg);
  padding: 20px 24px;
  &.flush {
    padding: 0;
    overflow: hidden;
  }
}
</style>
