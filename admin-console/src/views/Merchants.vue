<template>
  <div>
    <h2 class="page-title">商家</h2>
    <p class="page-desc">运营商家列表 · 启用/禁用、改订阅记审计；侧栏查看连接数与会话数</p>

    <div class="toolbar">
      <el-input
        v-model="q"
        clearable
        placeholder="搜索手机 / 昵称 / 邮箱"
        style="max-width: 280px"
        @keyup.enter="load(1)"
        @clear="load(1)"
      />
      <el-button type="primary" @click="load(1)">查询</el-button>
      <el-button :loading="loading" @click="load()">刷新</el-button>
    </div>

    <el-table :data="items" v-loading="loading" stripe empty-text="暂无商家。可先 seed-demo 或等待注册。">
      <el-table-column prop="nickname" label="昵称" min-width="120">
        <template #default="{ row }">
          <el-button text type="primary" @click="openDetail(row)">{{ row.nickname || '—' }}</el-button>
        </template>
      </el-table-column>
      <el-table-column prop="phone" label="手机" min-width="130">
        <template #default="{ row }">{{ row.phone || '—' }}</template>
      </el-table-column>
      <el-table-column prop="subscriptionLevel" label="订阅" width="100" />
      <el-table-column prop="freeQuota" label="额度" width="90">
        <template #default="{ row }">{{ row.freeQuota ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="connectionCount" label="连接数" width="90" />
      <el-table-column prop="sessionCount" label="会话数" width="90">
        <template #default="{ row }">{{ row.sessionCount ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="isActive" label="状态" width="90">
        <template #default="{ row }">
          <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
            {{ row.isActive ? '启用' : '停用' }}
          </el-tag>
        </template>
      </el-table-column>
      <el-table-column prop="createdAt" label="注册时间" min-width="160">
        <template #default="{ row }">{{ formatTime(row.createdAt) }}</template>
      </el-table-column>
      <el-table-column label="操作" width="320" fixed="right">
        <template #default="{ row }">
          <el-button text type="primary" size="small" @click="openDetail(row)">详情</el-button>
          <el-button text type="primary" size="small" @click="onToggleActive(row)">
            {{ row.isActive ? '禁用' : '启用' }}
          </el-button>
          <el-button text type="warning" size="small" @click="onChangeSub(row)">改订阅</el-button>
          <el-button text type="success" size="small" :loading="enterLoadingId === String(row.id)" @click="onEnterMerchant(row)">
            进入商户后台
          </el-button>
        </template>
      </el-table-column>
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

    <el-drawer v-model="drawer" title="商家详情" size="420px" destroy-on-close>
      <div v-loading="detailLoading">
        <template v-if="detail">
          <el-descriptions :column="1" border size="small">
            <el-descriptions-item label="昵称">{{ detail.nickname || '—' }}</el-descriptions-item>
            <el-descriptions-item label="手机">{{ detail.phone || '—' }}</el-descriptions-item>
            <el-descriptions-item label="邮箱">{{ detail.email || '—' }}</el-descriptions-item>
            <el-descriptions-item label="订阅">{{ detail.subscriptionLevel || '—' }}</el-descriptions-item>
            <el-descriptions-item label="额度">{{ detail.freeQuota ?? '—' }}</el-descriptions-item>
            <el-descriptions-item label="状态">
              <el-tag :type="detail.isActive ? 'success' : 'info'" size="small">
                {{ detail.isActive ? '启用' : '停用' }}
              </el-tag>
            </el-descriptions-item>
            <el-descriptions-item label="注册">{{ formatTime(detail.createdAt) }}</el-descriptions-item>
            <el-descriptions-item label="最近登录">{{ formatTime(detail.lastLoginAt) }}</el-descriptions-item>
          </el-descriptions>

          <el-row :gutter="12" class="stat-row">
            <el-col :span="12">
              <el-card shadow="never" class="mini">
                <div class="mini-label">活跃连接</div>
                <div class="mini-value">{{ detail.connectionCount ?? 0 }}</div>
              </el-card>
            </el-col>
            <el-col :span="12">
              <el-card shadow="never" class="mini">
                <div class="mini-label">总会话</div>
                <div class="mini-value">{{ detail.sessionCount ?? 0 }}</div>
              </el-card>
            </el-col>
            <el-col :span="12">
              <el-card shadow="never" class="mini">
                <div class="mini-label">未结束会话</div>
                <div class="mini-value">{{ detail.openSessions ?? 0 }}</div>
              </el-card>
            </el-col>
            <el-col :span="12">
              <el-card shadow="never" class="mini">
                <div class="mini-label">待审草稿</div>
                <div class="mini-value">{{ detail.pendingDrafts ?? 0 }}</div>
              </el-card>
            </el-col>
          </el-row>

          <h4 class="sub-title">店铺连接</h4>
          <el-table
            v-if="connections.length"
            :data="connections"
            size="small"
            stripe
            empty-text="无连接"
          >
            <el-table-column prop="platform" label="平台" width="80" />
            <el-table-column prop="nickname" label="店铺" min-width="100">
              <template #default="{ row }">{{ row.nickname || '—' }}</template>
            </el-table-column>
            <el-table-column label="状态" width="70">
              <template #default="{ row }">
                <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
                  {{ row.isActive ? '活' : '停' }}
                </el-tag>
              </template>
            </el-table-column>
          </el-table>
          <el-empty v-else description="该商家暂无店铺连接" :image-size="56" />

          <div class="drawer-actions">
            <el-button type="success" :loading="enterLoadingId === String(detail.id)" @click="onEnterMerchant(detail)">
              进入商户后台
            </el-button>
            <el-button type="primary" plain @click="onToggleActive(detail)">
              {{ detail.isActive ? '禁用商家' : '启用商家' }}
            </el-button>
            <el-button type="warning" plain @click="onChangeSub(detail)">改订阅</el-button>
          </div>
        </template>
        <el-empty v-else-if="!detailLoading" description="加载失败" />
      </div>
    </el-drawer>

    <el-dialog v-model="subDialog" title="修改订阅" width="360px" destroy-on-close>
      <el-select v-model="subLevel" style="width: 100%">
        <el-option label="Free" value="Free" />
        <el-option label="Basic" value="Basic" />
        <el-option label="Pro" value="Pro" />
      </el-select>
      <template #footer>
        <el-button @click="subDialog = false">取消</el-button>
        <el-button type="primary" :loading="subSaving" @click="confirmSub">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  getMerchants,
  getMerchant,
  setMerchantActive,
  setMerchantSubscription,
  enterMerchant,
} from '@/api/admin'

const loading = ref(false)
const items = ref<Record<string, unknown>[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const q = ref('')

const drawer = ref(false)
const detailLoading = ref(false)
const detail = ref<Record<string, unknown> | null>(null)
const connections = ref<Record<string, unknown>[]>([])

const subDialog = ref(false)
const subLevel = ref('Free')
const subSaving = ref(false)
const subTargetId = ref('')
const enterLoadingId = ref('')

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
    const res = await getMerchants(page.value, pageSize, q.value.trim() || undefined)
    items.value = res.items || []
    total.value = res.total || 0
  } catch {
    ElMessage.error('加载商家失败')
    items.value = []
    total.value = 0
  } finally {
    loading.value = false
  }
}

async function openDetail(row: Record<string, unknown>) {
  const id = String(row.id || '')
  if (!id) return
  drawer.value = true
  detailLoading.value = true
  detail.value = null
  connections.value = []
  try {
    const res = await getMerchant(id)
    detail.value = res
    connections.value = (res.connections as Record<string, unknown>[]) || []
  } catch {
    ElMessage.error('加载商家详情失败')
  } finally {
    detailLoading.value = false
  }
}

async function onToggleActive(row: Record<string, unknown>) {
  const id = String(row.id || '')
  if (!id) return
  const next = !row.isActive
  try {
    await ElMessageBox.confirm(
      next ? '确定启用该商家？' : '确定禁用该商家？',
      '确认',
      { type: 'warning' },
    )
    await setMerchantActive(id, next)
    ElMessage.success(next ? '已启用' : '已禁用')
    await load()
    if (drawer.value && detail.value && String(detail.value.id) === id) {
      await openDetail({ id })
    }
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('操作失败')
  }
}

function onChangeSub(row: Record<string, unknown>) {
  const id = String(row.id || '')
  if (!id) return
  subTargetId.value = id
  subLevel.value = String(row.subscriptionLevel || 'Free')
  subDialog.value = true
}

async function confirmSub() {
  if (!subTargetId.value) return
  subSaving.value = true
  try {
    await setMerchantSubscription(subTargetId.value, subLevel.value)
    ElMessage.success('订阅已更新')
    subDialog.value = false
    await load()
    if (drawer.value && detail.value && String(detail.value.id) === subTargetId.value) {
      await openDetail({ id: subTargetId.value })
    }
  } catch {
    ElMessage.error('更新失败')
  } finally {
    subSaving.value = false
  }
}


async function onEnterMerchant(row: Record<string, unknown>) {
  const id = String(row.id || '')
  if (!id) return
  enterLoadingId.value = id
  try {
    const res = await enterMerchant(id)
    const url = res.merchantWebUrl
    if (!url) {
      ElMessage.error('未返回商户后台地址')
      return
    }
    window.open(url, '_blank', 'noopener,noreferrer')
  } catch {
    ElMessage.error('进入商户后台失败')
  } finally {
    enterLoadingId.value = ''
  }
}

onMounted(() => load(1))
</script>

<style scoped lang="scss">
.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
  flex-wrap: wrap;
}
.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
.stat-row {
  margin-top: 16px;
}
.mini {
  margin-bottom: 12px;
  border-radius: 10px;
}
.mini-label {
  color: #64748b;
  font-size: 12px;
}
.mini-value {
  font-size: 22px;
  font-weight: 600;
  margin-top: 4px;
}
.sub-title {
  margin: 16px 0 8px;
  font-size: 14px;
}
.drawer-actions {
  margin-top: 20px;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
</style>
