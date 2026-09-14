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
      <el-form label-width="160px" style="max-width: 640px">
        <el-form-item label="维护模式">
          <div class="switch-row">
            <el-switch v-model="form.maintenanceMode" />
            <span class="hint">开启后商家 API 返回 503 MAINTENANCE；Admin / health / Webhook 仍可用</span>
          </div>
        </el-form-item>
        <el-form-item label="默认出站模式">
          <el-select v-model="form.defaultOutboundMode" style="width: 240px">
            <el-option label="DraftFirst（人审，推荐）" value="DraftFirst" />
            <el-option label="AutoSend（慎用）" value="AutoSend" />
          </el-select>
        </el-form-item>
        <el-form-item label="允许新注册">
          <div class="switch-row">
            <el-switch v-model="form.allowNewRegistration" />
            <span class="hint">关闭后新商家注册返回 REGISTRATION_CLOSED</span>
          </div>
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

    <el-card
      v-if="isDevEnv"
      shadow="never"
      class="dev-card"
      style="margin-bottom: 16px"
    >
      <template #header>
        <div class="dev-head">
          <span>开发工具</span>
          <el-tag type="warning" size="small" effect="dark">Development</el-tag>
        </div>
      </template>
      <p class="dev-desc">
        可调用 <code>POST /api/dev/seed-demo</code> 写入本地演示商家 / 模拟店 / 会话 / Admin 账号。
        生产环境该接口返回 404。按钮会附带当前 Admin JWT（接口本身亦允许匿名）。
      </p>
      <el-button type="warning" :loading="seeding" @click="onSeed">运行 seed-demo</el-button>
      <el-button plain @click="fillHint">查看演示账号</el-button>
      <p v-if="seedResult" class="seed-result">{{ seedResult }}</p>
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
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { getSettings, updateSettings, seedDemo } from '@/api/admin'

const loading = ref(false)
const saving = ref(false)
const seeding = ref(false)
const healthLoading = ref(false)
const data = ref<Record<string, unknown> | null>(null)
const healthLive = ref('')
const healthReady = ref('')
const seedResult = ref('')
const form = reactive({
  maintenanceMode: false,
  defaultOutboundMode: 'DraftFirst',
  allowNewRegistration: true,
})

const isDevEnv = computed(() => {
  if (import.meta.env.DEV) return true
  const env = String(data.value?.environment || '')
  return env.toLowerCase() === 'development'
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

async function onSeed() {
  try {
    await ElMessageBox.confirm(
      '将写入/刷新本地演示数据（商家、模拟店、会话、Admin）。仅 Development 有效。',
      '运行 seed-demo',
      { type: 'warning' },
    )
  } catch {
    return
  }
  seeding.value = true
  seedResult.value = ''
  try {
    const res = await seedDemo()
    const parts = [
      res.message || 'seed-demo 完成',
      res.created?.length ? `created=${res.created.join(',')}` : '',
      res.updated?.length ? `updated=${res.updated.join(',')}` : '',
      res.skipped?.length ? `skipped=${res.skipped.length}项` : '',
    ].filter(Boolean)
    seedResult.value = parts.join(' · ')
    ElMessage.success('演示数据已就绪')
  } catch (e: unknown) {
    const data = (e as { response?: { data?: { message?: string } | string } })?.response?.data
    const text =
      typeof data === 'string'
        ? data
        : (data as { message?: string })?.message || (e as Error)?.message || 'seed 失败'
    ElMessage.error(text)
    seedResult.value = text
  } finally {
    seeding.value = false
  }
}

function fillHint() {
  ElMessage.info('Admin：admin@test.com / Agent123! · 商家手机 13800138000 / 码 123456')
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
.switch-row {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
}
.dev-card {
  border: 1px dashed #f59e0b;
}
.dev-head {
  display: flex;
  align-items: center;
  gap: 10px;
}
.dev-desc {
  margin: 0 0 12px;
  color: #64748b;
  font-size: 13px;
  line-height: 1.6;
}
.seed-result {
  margin-top: 12px;
  font-size: 12px;
  color: #94a3b8;
  word-break: break-all;
}
</style>
