<template>
  <div>
    <h2 class="page-title">系统设置</h2>
    <p class="page-desc">
      可写运营开关存 <code>system_settings</code>；LLM Provider 见下方（Key 掩码）。
      <code>GET/PUT /api/admin/settings</code> · <code>/api/admin/llm-provider</code>
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

    <el-card shadow="never" v-loading="llmLoading" style="margin-bottom: 16px">
      <template #header>
        <div class="dev-head">
          <span>LLM Provider（OpenAI-compatible）</span>
          <el-tag v-if="llmForm.active" type="success" size="small">Active</el-tag>
          <el-tag v-else type="info" size="small">Inactive → appsettings</el-tag>
        </div>
      </template>
      <p class="dev-desc">
        Admin 配置全局 BaseUrl / Model / API Key；激活后优先于 <code>Llm:*</code> 与环境变量。
        商家仍可用「AI 设置」覆盖本店 Key。无 Key / 本地不可达 → 规则草稿降级。
      </p>
      <div class="preset-row">
        <span class="preset-label">预设：</span>
        <el-button
          v-for="p in presets"
          :key="p.id"
          size="small"
          @click="applyPreset(p)"
        >
          {{ p.name }}
        </el-button>
      </div>
      <el-form label-width="120px" style="max-width: 720px; margin-top: 12px">
        <el-form-item label="显示名">
          <el-input v-model="llmForm.name" placeholder="可选，如 DashScope / Ollama" clearable />
        </el-form-item>
        <el-form-item label="Base URL" required>
          <el-input v-model="llmForm.baseUrl" placeholder="https://.../v1 或 http://127.0.0.1:11434/v1" />
        </el-form-item>
        <el-form-item label="Model" required>
          <el-input v-model="llmForm.model" placeholder="qwen-plus / gpt-4o-mini / llama3.2" />
        </el-form-item>
        <el-form-item label="API Key">
          <el-input
            v-model="llmForm.apiKey"
            type="password"
            show-password
            :placeholder="apiKeyPlaceholder"
            clearable
          />
          <div class="hint" style="margin-left: 0; margin-top: 6px">
            留空不改；本地 Ollama/LM Studio 可不填。勾选清除将删除已存 Key。
          </div>
          <el-checkbox v-model="llmForm.clearApiKey" style="margin-top: 6px">清除已存 Key</el-checkbox>
        </el-form-item>
        <el-form-item label="状态">
          <div class="switch-row">
            <el-switch v-model="llmForm.active" active-text="激活" inactive-text="停用" />
            <span class="hint">停用后回退 appsettings / 环境变量</span>
          </div>
        </el-form-item>
        <el-form-item>
          <el-button type="primary" :loading="llmSaving" @click="onSaveLlm(false)">保存</el-button>
          <el-button type="success" :loading="llmSaving" @click="onSaveLlm(true)">保存并激活</el-button>
          <el-button :loading="llmSaving" @click="onActivateOnly(false)">仅停用</el-button>
          <el-button :disabled="llmLoading" @click="reloadLlm">刷新</el-button>
        </el-form-item>
      </el-form>
      <el-alert
        v-if="llmEffective"
        type="info"
        :closable="false"
        show-icon
        style="margin-top: 8px"
        :title="`生效：source=${llmEffective.source || '—'} · configured=${llmEffective.configured} · baseUrl=${llmEffective.baseUrl || '—'} · model=${llmEffective.model || '—'}`"
      />
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
        title="运营开关不读写 AppKey / Token。LLM Key 仅在上方 Provider 区（掩码）。"
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
import {
  getSettings,
  updateSettings,
  seedDemo,
  getLlmProvider,
  updateLlmProvider,
  activateLlmProvider,
  type LlmProviderPreset,
} from '@/api/admin'

const loading = ref(false)
const saving = ref(false)
const seeding = ref(false)
const healthLoading = ref(false)
const llmLoading = ref(false)
const llmSaving = ref(false)
const data = ref<Record<string, unknown> | null>(null)
const healthLive = ref('')
const healthReady = ref('')
const seedResult = ref('')
const form = reactive({
  maintenanceMode: false,
  defaultOutboundMode: 'DraftFirst',
  allowNewRegistration: true,
})

const llmForm = reactive({
  name: '',
  baseUrl: '',
  model: '',
  apiKey: '',
  active: false,
  clearApiKey: false,
})
const presets = ref<LlmProviderPreset[]>([])
const apiKeyHint = ref<string | null>(null)
const apiKeyConfigured = ref(false)
const llmEffective = ref<Record<string, unknown> | null>(null)

const apiKeyPlaceholder = computed(() => {
  if (apiKeyConfigured.value && apiKeyHint.value) return `已配置 ${apiKeyHint.value}（留空不改）`
  return 'sk-... 或本地留空'
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

function applyPreset(p: LlmProviderPreset) {
  llmForm.name = p.name
  llmForm.baseUrl = p.baseUrl
  llmForm.model = p.model
  ElMessage.info(`已套用预设：${p.name}`)
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

async function reloadLlm() {
  llmLoading.value = true
  try {
    const res = await getLlmProvider()
    llmForm.name = String(res.name || '')
    llmForm.baseUrl = String(res.baseUrl || '')
    llmForm.model = String(res.model || '')
    llmForm.active = !!res.active
    llmForm.apiKey = ''
    llmForm.clearApiKey = false
    apiKeyConfigured.value = !!res.apiKeyConfigured
    apiKeyHint.value = res.apiKeyHint || null
    presets.value = res.presets || []
    llmEffective.value = {
      ...(res.effective || {}),
      configured: res.effective?.configured ?? res.configured,
      source: (res.effective as Record<string, unknown> | undefined)?.llmSource || res.effective?.source || res.source,
      baseUrl: (res.effective as Record<string, unknown> | undefined)?.baseUrl || res.baseUrl,
      model: (res.effective as Record<string, unknown> | undefined)?.model || res.model,
    }
  } catch {
    ElMessage.error('加载 LLM Provider 失败')
  } finally {
    llmLoading.value = false
  }
}

async function onSaveLlm(forceActivate: boolean) {
  if (!llmForm.baseUrl.trim() || !llmForm.model.trim()) {
    ElMessage.warning('请填写 Base URL 与 Model')
    return
  }
  llmSaving.value = true
  try {
    const res = await updateLlmProvider({
      name: llmForm.name.trim() || undefined,
      baseUrl: llmForm.baseUrl.trim(),
      model: llmForm.model.trim(),
      apiKey: llmForm.clearApiKey ? undefined : (llmForm.apiKey.trim() || undefined),
      clearApiKey: llmForm.clearApiKey || undefined,
      active: forceActivate ? true : llmForm.active,
    })
    ElMessage.success(res.message || '已保存')
    await reloadLlm()
  } catch {
    ElMessage.error('保存 LLM Provider 失败')
  } finally {
    llmSaving.value = false
  }
}

async function onActivateOnly(active: boolean) {
  llmSaving.value = true
  try {
    const res = await activateLlmProvider(active)
    ElMessage.success(res.message || (active ? '已激活' : '已停用'))
    await reloadLlm()
  } catch {
    ElMessage.error('切换失败（请先保存 baseUrl/model）')
  } finally {
    llmSaving.value = false
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
  await Promise.all([reload(), reloadLlm()])
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
.preset-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
}
.preset-label {
  font-size: 13px;
  color: #64748b;
}
</style>
