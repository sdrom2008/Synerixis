<template>
  <div>
    <h2 class="page-title">系统设置</h2>
    <p class="page-desc">
      可写运营开关存 <code>system_settings</code>；只暴露安全项，密钥不可读写。
      <code>GET/PUT /api/admin/settings</code>
    </p>

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

    <el-card shadow="never" v-loading="loading" style="margin-bottom: 16px">
      <template #header>可写运营开关</template>
      <el-form label-width="160px" style="max-width: 560px">
        <el-form-item label="维护模式">
          <el-switch v-model="form.maintenanceMode" />
          <span class="hint">开启后商家 API 返回 503 MAINTENANCE；Admin / health / Webhook 仍可用（Webhook 跳过 AI）</span>
        </el-form-item>
        <el-form-item label="默认出站模式">
          <el-select v-model="form.defaultOutboundMode" style="width: 220px">
            <el-option label="DraftFirst（人审）" value="DraftFirst" />
            <el-option label="AutoSend（慎用）" value="AutoSend" />
          </el-select>
        </el-form-item>
        <el-form-item label="允许新注册">
          <el-switch v-model="form.allowNewRegistration" />
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="saving" @click="onSave">保存</el-button>
          <el-button :disabled="loading" @click="reload">重置</el-button>
        </el-form-item>
      </el-form>
      <el-alert
        type="info"
        :closable="false"
        show-icon
        title="不会通过本页读写 AppKey / AppSecret / Token 等密钥。"
        style="margin-top: 8px"
      />
    </el-card>

    <el-card shadow="never" v-loading="loading">
      <template #header>只读说明</template>
      <el-descriptions v-if="data" :column="1" border>
        <el-descriptions-item label="环境">{{ data.environment || '—' }}</el-descriptions-item>
        <el-descriptions-item label="产品定位">{{ data.productPositioning || '—' }}</el-descriptions-item>
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
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getSettings, updateSettings } from '@/api/admin'

const loading = ref(false)
const saving = ref(false)
const healthLoading = ref(false)
const data = ref<Record<string, unknown> | null>(null)
const healthLive = ref('')
const healthReady = ref('')
const form = reactive({
  maintenanceMode: false,
  defaultOutboundMode: 'DraftFirst',
  allowNewRegistration: true,
})

function applyForm(src: Record<string, unknown> | null) {
  if (!src) return
  form.maintenanceMode = !!src.maintenanceMode
  form.defaultOutboundMode = String(src.defaultOutboundMode || 'DraftFirst')
  form.allowNewRegistration = src.allowNewRegistration !== false
}

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

async function reload() {
  loading.value = true
  try {
    data.value = await getSettings()
    applyForm(data.value)
  } catch {
    ElMessage.error('加载设置失败')
    data.value = null
  } finally {
    loading.value = false
  }
}

async function onSave() {
  saving.value = true
  try {
    const res = await updateSettings({
      maintenanceMode: form.maintenanceMode,
      defaultOutboundMode: form.defaultOutboundMode,
      allowNewRegistration: form.allowNewRegistration,
    })
    ElMessage.success(res.message || '已保存')
    await reload()
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  await reload()
  void fetchHealth()
})
</script>

<style scoped>
.feat {
  margin: 0;
  font-size: 12px;
  white-space: pre-wrap;
}
.hint {
  margin-left: 10px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
</style>
