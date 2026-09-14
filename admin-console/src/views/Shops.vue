<template>
  <div>
    <div class="page-head">
      <div>
        <h2 class="page-title">店铺连接</h2>
        <p class="page-desc">平台连接健康 · <code>GET /api/admin/shops</code></p>
      </div>
      <el-button :loading="loading" @click="load()">刷新</el-button>
    </div>

    <el-table :data="items" v-loading="loading" stripe>
      <el-table-column prop="platform" label="平台" width="100" />
      <el-table-column prop="nickname" label="店铺名" min-width="140">
        <template #default="{ row }">{{ row.nickname || '—' }}</template>
      </el-table-column>
      <el-table-column prop="shopId" label="ShopId" min-width="120">
        <template #default="{ row }">{{ row.shopId || '—' }}</template>
      </el-table-column>
      <el-table-column label="商家" min-width="140">
        <template #default="{ row }">
          {{ row.sellerNickname || row.sellerPhone || String(row.sellerId || '').slice(0, 8) || '—' }}
        </template>
      </el-table-column>
      <el-table-column prop="isActive" label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '活跃' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="tokenExpiresAt" label="Token 过期" min-width="160">
        <template #default="{ row }">{{ formatTime(row.tokenExpiresAt) }}</template>
      </el-table-column>
      <el-table-column prop="updatedAt" label="更新" min-width="160">
        <template #default="{ row }">{{ formatTime(row.updatedAt || row.createdAt) }}</template>
      </el-table-column>
      <template #empty>
        <el-empty
          description="暂无店铺连接。本地演示可先 seed-demo（会出现「模拟 Shopee 店」）。"
          :image-size="72"
        />
      </template>
    </el-table>

    <div class="pager">
      <el-pagination
        background
        layout="total, prev, pager, next"
        :total="total"
        :page-size="pageSize"
        :current-page="page"
        @current-change="load"
      />
    </div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getShops } from '@/api/admin'

const loading = ref(false)
const items = ref<Record<string, unknown>[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 50

function formatTime(v: unknown) {
  if (!v) return '—'
  try {
    return new Date(String(v)).toLocaleString('zh-CN', { hour12: false })
  } catch {
    return String(v)
  }
}

async function load(p = page.value) {
  page.value = p
  loading.value = true
  try {
    const res = await getShops(page.value, pageSize)
    items.value = res.items || []
    total.value = res.total || 0
  } catch {
    ElMessage.error('加载店铺连接失败')
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

onMounted(() => load(1))
</script>

<style scoped lang="scss">
.page-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}
.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
