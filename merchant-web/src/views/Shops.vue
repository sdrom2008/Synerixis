<template>
  <div>
    <h2 class="page-title">店铺绑定</h2>
    <p class="page-desc">
      对接 GET /api/merchant/connections 与 bind/unbind。Seller / Supervisor / Admin 可管理；Agent 无权限。
    </p>

    <el-alert
      v-if="!canManage"
      type="warning"
      :closable="false"
      show-icon
      title="当前账号为普通坐席（Agent），无法绑定或解绑店铺。请使用商家或主管账号。"
      style="margin-bottom: 16px"
    />

    <el-card v-else shadow="never" class="sx-card" v-loading="loading">
      <template #header>
        <div class="head">
          <span>已连接店铺</span>
          <div class="head-actions">
            <el-button size="small" :loading="loading" @click="load">刷新</el-button>
            <el-button type="primary" size="small" :loading="binding" @click="startBind('shopee')">
              绑定 Shopee
            </el-button>
          </div>
        </div>
      </template>

      <el-table v-if="connections.length" :data="connections" stripe>
        <el-table-column prop="platform" label="平台" min-width="100">
          <template #default="{ row }">{{ row.platform || row.Platform || '—' }}</template>
        </el-table-column>
        <el-table-column label="店铺昵称" min-width="140">
          <template #default="{ row }">
            {{ row.nickname || row.Nickname || row.shopName || '—' }}
          </template>
        </el-table-column>
        <el-table-column label="平台店铺 ID" min-width="140">
          <template #default="{ row }">{{ row.shopId || row.ShopId || '—' }}</template>
        </el-table-column>
        <el-table-column label="Token" width="120">
          <template #default="{ row }">
            <el-tag :type="tokenTagType(row)" size="small">
              {{ row.tokenHint || tokenLabel(row) }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="isActive" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isActive || row.IsActive ? 'success' : 'info'" size="small">
              {{ row.isActive || row.IsActive ? '已连接' : '未激活' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="180">
          <template #default="{ row }">
            <el-button
              text
              type="primary"
              size="small"
              :loading="refreshingId === (row.id || row.Id)"
              @click="onRefresh(row)"
            >
              刷新 Token
            </el-button>
            <el-button
              text
              type="danger"
              size="small"
              @click="onUnbind(row.platform || row.Platform)"
            >
              解绑
            </el-button>
          </template>
        </el-table-column>
      </el-table>

      <EmptyState
        v-else
        title="尚未绑定店铺"
        desc="点击「绑定 Shopee」跳转 OAuth。完成后回到此页将自动刷新。"
      />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import EmptyState from '@/components/EmptyState.vue'
import { useAuthStore } from '@/stores/auth'
import { getBindUrl, getConnections, refreshConnection, unbindPlatform } from '@/api/merchant'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()
const canManage = computed(() => !!auth.permissions.canManageShops)

const loading = ref(false)
const binding = ref(false)
const refreshingId = ref<string | null>(null)
const connections = ref<Record<string, unknown>[]>([])
let focusHandler: (() => void) | null = null

function tokenLabel(row: Record<string, unknown>) {
  const st = String(row.tokenStatus || '')
  if (st === 'expired') return '已过期'
  if (st === 'expiring') return '即将过期'
  if (st === 'inactive') return '停用'
  return '有效'
}

function tokenTagType(row: Record<string, unknown>) {
  const st = String(row.tokenStatus || '')
  if (st === 'expired') return 'danger'
  if (st === 'expiring') return 'warning'
  if (st === 'inactive') return 'info'
  return 'success'
}

async function load() {
  if (!canManage.value) {
    connections.value = []
    return
  }
  loading.value = true
  try {
    const res = await getConnections()
    const raw = res as
      | { items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] }
      | Record<string, unknown>[]
    if (Array.isArray(raw)) connections.value = raw
    else connections.value = raw.items || raw.Items || []
  } catch (e: unknown) {
    connections.value = []
    const status = (e as { response?: { status?: number } })?.response?.status
    if (status === 401 || status === 403) {
      ElMessage.warning('无权限查看店铺连接（需要商家或主管）')
    } else {
      ElMessage.warning('加载连接失败')
    }
  } finally {
    loading.value = false
  }
}

async function startBind(platform: string) {
  binding.value = true
  try {
    const res = await getBindUrl(platform)
    const url = res.url || res.authorizeUrl
    if (!url) {
      ElMessage.warning('未返回授权 URL')
      return
    }
    window.open(url, '_blank')
    ElMessage.info('请在新窗口完成授权，完成后此页会自动刷新')
  } catch {
    ElMessage.error('获取授权链接失败')
  } finally {
    binding.value = false
  }
}

async function onRefresh(row: Record<string, unknown>) {
  const id = String(row.id || row.Id || '')
  if (!id) return
  refreshingId.value = id
  try {
    await refreshConnection(id)
    ElMessage.success('Token 已刷新')
    await load()
  } catch {
    ElMessage.error('刷新失败')
  } finally {
    refreshingId.value = null
  }
}

async function onUnbind(platform?: string) {
  if (!platform) return
  try {
    await ElMessageBox.confirm(`确定解绑 ${platform}？`, '解绑店铺', { type: 'warning' })
    await unbindPlatform(String(platform))
    ElMessage.success('已解绑')
    await load()
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('解绑失败')
  }
}

async function handleBoundQuery() {
  if (route.query.bound === '1') {
    ElMessage.success('店铺绑定成功')
    await load()
    const q = { ...route.query }
    delete q.bound
    delete q.error
    router.replace({ path: '/shops', query: q })
  } else if (route.query.bound === '0') {
    const err = String(route.query.error || '绑定失败')
    ElMessage.error(`绑定未完成：${err}`)
    const q = { ...route.query }
    delete q.bound
    delete q.error
    router.replace({ path: '/shops', query: q })
  }
}

onMounted(() => {
  load()
  handleBoundQuery()
  focusHandler = () => {
    if (document.visibilityState === 'visible' && canManage.value) load()
  }
  document.addEventListener('visibilitychange', focusHandler)
  window.addEventListener('focus', focusHandler)
})

onUnmounted(() => {
  if (focusHandler) {
    document.removeEventListener('visibilitychange', focusHandler)
    window.removeEventListener('focus', focusHandler)
  }
})
</script>

<style scoped lang="scss">
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.head-actions {
  display: flex;
  gap: 8px;
}
</style>
