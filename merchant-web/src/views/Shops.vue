<template>
  <div>
    <h2 class="page-title">店铺绑定</h2>
    <p class="page-desc">对接 GET /api/merchant/connections 与 bind/unbind。首发平台 Shopee。</p>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <template #header>
        <div class="head">
          <span>已连接店铺</span>
          <el-button type="primary" size="small" :loading="binding" @click="startBind('shopee')">
            绑定 Shopee
          </el-button>
        </div>
      </template>

      <el-table v-if="connections.length" :data="connections" stripe>
        <el-table-column prop="platform" label="平台" min-width="100" />
        <el-table-column prop="shopName" label="店铺名" min-width="140">
          <template #default="{ row }">{{ row.shopName || row.ShopName || '—' }}</template>
        </el-table-column>
        <el-table-column prop="isActive" label="状态" width="100">
          <template #default="{ row }">
            <el-tag :type="row.isActive || row.IsActive ? 'success' : 'info'" size="small">
              {{ row.isActive || row.IsActive ? '已连接' : '未激活' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="操作" width="120">
          <template #default="{ row }">
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
        desc="点击「绑定 Shopee」跳转 OAuth。完成后回到此页刷新。"
      />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import EmptyState from '@/components/EmptyState.vue'
import { getBindUrl, getConnections, unbindPlatform } from '@/api/merchant'

const loading = ref(false)
const binding = ref(false)
const connections = ref<Record<string, unknown>[]>([])

async function load() {
  loading.value = true
  try {
    const res = await getConnections()
    const raw = res as { items?: Record<string, unknown>[]; Items?: Record<string, unknown>[] } | Record<string, unknown>[]
    if (Array.isArray(raw)) connections.value = raw
    else connections.value = raw.items || raw.Items || []
  } catch {
    connections.value = []
    ElMessage.warning('加载连接失败')
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
  } catch {
    ElMessage.error('获取授权链接失败')
  } finally {
    binding.value = false
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

onMounted(load)
</script>

<style scoped lang="scss">
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
</style>
