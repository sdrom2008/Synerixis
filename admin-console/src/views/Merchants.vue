<template>
  <div>
    <h2 class="page-title">商家</h2>
    <p class="page-desc">运营商家列表 · 写操作记审计（禁用 / 改订阅）</p>

    <div class="toolbar">
      <el-input
        v-model="q"
        clearable
        placeholder="搜索手机 / 昵称 / 邮箱"
        style="max-width: 280px"
        @keyup.enter="load(1)"
      />
      <el-button type="primary" @click="load(1)">查询</el-button>
    </div>

    <el-table :data="items" v-loading="loading" stripe empty-text="暂无数据">
      <el-table-column prop="nickname" label="昵称" min-width="120">
        <template #default="{ row }">{{ row.nickname || '—' }}</template>
      </el-table-column>
      <el-table-column prop="phone" label="手机" min-width="130">
        <template #default="{ row }">{{ row.phone || '—' }}</template>
      </el-table-column>
      <el-table-column prop="subscriptionLevel" label="订阅" width="100" />
      <el-table-column prop="freeQuota" label="额度" width="90">
        <template #default="{ row }">{{ row.freeQuota ?? '—' }}</template>
      </el-table-column>
      <el-table-column prop="connectionCount" label="连接数" width="90" />
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
      <el-table-column label="操作" width="200" fixed="right">
        <template #default="{ row }">
          <el-button text type="primary" size="small" @click="onToggleActive(row)">
            {{ row.isActive ? '禁用' : '启用' }}
          </el-button>
          <el-button text type="warning" size="small" @click="onChangeSub(row)">改订阅</el-button>
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
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getMerchants, setMerchantActive, setMerchantSubscription } from '@/api/admin'

const loading = ref(false)
const items = ref<Record<string, unknown>[]>([])
const total = ref(0)
const page = ref(1)
const pageSize = 20
const q = ref('')

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
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('操作失败')
  }
}

async function onChangeSub(row: Record<string, unknown>) {
  const id = String(row.id || '')
  if (!id) return
  try {
    const { value } = await ElMessageBox.prompt('订阅等级：Free / Basic / Pro', '修改订阅', {
      inputValue: String(row.subscriptionLevel || 'Free'),
      confirmButtonText: '保存',
      cancelButtonText: '取消',
    })
    const level = String(value || '').trim()
    if (!level) return
    await setMerchantSubscription(id, level)
    ElMessage.success('订阅已更新')
    await load()
  } catch (e) {
    if (e !== 'cancel') ElMessage.error('更新失败')
  }
}

onMounted(() => load(1))
</script>

<style scoped lang="scss">
.toolbar {
  display: flex;
  gap: 12px;
  margin-bottom: 16px;
}
.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
