<template>
  <div>
    <h2 class="page-title">系统设置</h2>
    <p class="page-desc">只读配置说明 · <code>GET /api/admin/settings</code>；健康检查可选拉取 <code>/health/ready</code></p>

    <el-card shadow="never" v-loading="loading" style="margin-bottom: 16px">
      <template #header>API Health</template>
      <el-descriptions :column="2" border>
        <el-descriptions-item label="/health">{{ healthLive || '—' }}</el-descriptions-item>
        <el-descriptions-item label="/health/ready">{{ healthReady || '—' }}</el-descriptions-item>
      </el-descriptions>
      <el-button size="small" style="margin-top: 12px" :loading="healthLoading" @click="fetchHealth">
        重新检查
      </el-button>
    </el-card>

    <el-card shadow="never" v-loading="loading">
      <el-descriptions v-if="data" :column="1" border>
        <el-descriptions-item label="环境">{{ data.environment || '—' }}</el-descriptions-item>
        <el-descriptions-item label="产品定位">{{ data.productPositioning || '—' }}</el-descriptions-item>
        <el-descriptions-item label="默认出站">{{ data.defaultOutboundMode || '—' }}</el-descriptions-item>
        <el-descriptions-item label="Admin 鉴权">{{ data.adminAuth || '—' }}</el-descriptions-item>
        <el-descriptions-item label="Admin 前端">
          {{ (data.frontends as Record<string, string>)?.adminConsole || '—' }}
        </el-descriptions-item>
        <el-descriptions-item label="商家前端">
          {{ (data.frontends as Record<string, string>)?.merchantWeb || '—' }}
        </el-descriptions-item>
        <el-descriptions-item label="特性">
          <pre class="feat">{{ JSON.stringify(data.features, null, 2) }}</pre>
        </el-descriptions-item>
        <el-descriptions-item v-if="data.health" label="健康端点">
          <pre class="feat">{{ JSON.stringify(data.health, null, 2) }}</pre>
        </el-descriptions-item>
      </el-descriptions>
      <el-empty v-else-if="!loading" description="暂无配置说明" />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getSettings } from '@/api/admin'

const loading = ref(false)
const healthLoading = ref(false)
const data = ref<Record<string, unknown> | null>(null)
const healthLive = ref('')
const healthReady = ref('')

async function fetchHealth() {
  healthLoading.value = true
  try {
    const base = (import.meta as ImportMeta & { env?: { VITE_API_BASE_URL?: string } }).env?.VITE_API_BASE_URL || ''
    const liveUrl = `${base}/health`
    const readyUrl = `${base}/health/ready`
    const [live, ready] = await Promise.all([
      fetch(liveUrl).then(async (r) => ({ ok: r.ok, status: r.status, text: await r.text() })).catch((e) => ({ ok: false, status: 0, text: String(e) })),
      fetch(readyUrl).then(async (r) => ({ ok: r.ok, status: r.status, text: await r.text() })).catch((e) => ({ ok: false, status: 0, text: String(e) })),
    ])
    healthLive.value = live.ok ? `Healthy (${live.status})` : `Unhealthy (${live.status}) ${live.text.slice(0, 80)}`
    healthReady.value = ready.ok ? `Ready (${ready.status})` : `Not ready (${ready.status}) ${ready.text.slice(0, 80)}`
  } catch {
    healthLive.value = '检查失败'
    healthReady.value = '检查失败'
  } finally {
    healthLoading.value = false
  }
}

onMounted(async () => {
  loading.value = true
  try {
    data.value = await getSettings()
  } catch {
    ElMessage.error('加载设置失败')
    data.value = null
  } finally {
    loading.value = false
  }
  void fetchHealth()
})
</script>

<style scoped>
.feat {
  margin: 0;
  font-size: 12px;
  white-space: pre-wrap;
}
</style>
