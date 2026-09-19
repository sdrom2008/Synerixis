<template>
  <div>
    <h2 class="page-title">AI 设置</h2>
    <p class="page-desc">
      语气、多语工作语、LLM Key、营业时段、转人工、出站模式与 SLA。默认 DraftFirst：AI/规则只写草稿，由人工「人审发送」。
      无 Key 时自动降级为规则草稿（不做假流利翻译），演示不依赖真实模型。
    </p>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <el-form label-width="160px" style="max-width: 640px">
        <el-form-item label="回复语气">
          <el-select v-model="form.defaultReplyTone" placeholder="选择语气" style="width: 100%">
            <el-option label="专业礼貌" value="professional" />
            <el-option label="亲切友好" value="friendly" />
            <el-option label="简洁直接" value="concise" />
          </el-select>
        </el-form-item>

        <el-divider content-position="left">多语（CBEC）</el-divider>
        <el-form-item label="坐席工作语">
          <el-select v-model="form.workingLanguage" placeholder="默认 ZH" style="width: 100%">
            <el-option label="中文 ZH" value="ZH" />
            <el-option label="英语 EN" value="EN" />
            <el-option label="印尼语 ID" value="ID" />
            <el-option label="泰语 TH" value="TH" />
            <el-option label="越南语 VN" value="VN" />
          </el-select>
          <div class="field-hint">入站一键翻译目标语；默认中文。对应 PreferredLanguage / workingLanguage。</div>
        </el-form-item>
        <el-form-item label="支持语种">
          <el-checkbox-group v-model="form.supportedLanguages">
            <el-checkbox label="ID">ID</el-checkbox>
            <el-checkbox label="TH">TH</el-checkbox>
            <el-checkbox label="VN">VN</el-checkbox>
            <el-checkbox label="EN">EN</el-checkbox>
            <el-checkbox label="ZH">ZH</el-checkbox>
          </el-checkbox-group>
          <div class="field-hint">买家语优先级 ID → TH → VN → EN → ZH；草稿可切换目标语重写。</div>
        </el-form-item>

        <el-form-item label="出站模式">
          <el-radio-group v-model="form.outboundMode">
            <el-radio label="DraftFirst">草稿优先（推荐）</el-radio>
            <el-radio label="AutoSend">自动发送</el-radio>
          </el-radio-group>
        </el-form-item>
        <el-alert
          v-if="form.outboundMode === 'AutoSend'"
          type="warning"
          :closable="false"
          show-icon
          title="合规风险：AutoSend 可能触发平台对 chatbot / 促销广播的限制（Shopee 等禁全自动 chatbot），请确认业务确需自动出站。默认请保持 DraftFirst 人审。"
          style="margin-bottom: 16px"
        />

        <el-form-item label="入站分配">
          <el-radio-group v-model="form.assignmentMode">
            <el-radio label="Unassigned">未分配队列（默认）</el-radio>
            <el-radio label="LeastLoaded">最少负载自动分给 Agent</el-radio>
          </el-radio-group>
          <div class="field-hint">
            规则分流（非 AI）。默认进未分配，由主管/商家点「分配」或「按规则分配」；LeastLoaded 新会话自动分给本店负载最低的有效 Agent（在线优先）。敏感词命中可升 Supervisor。不自动回复买家。
          </div>
        </el-form-item>

        <el-form-item label="启用 AI 草稿">
          <el-switch v-model="form.enableAutoReply" />
          <div class="field-hint">关闭后入站只落库，不生成草稿；出站仍由「出站模式」控制，默认 DraftFirst 不会自动发给买家</div>
        </el-form-item>

        <el-form-item label="营业开始">
          <el-input v-model="form.businessHoursStart" placeholder="如 09:00" />
        </el-form-item>
        <el-form-item label="营业结束">
          <el-input v-model="form.businessHoursEnd" placeholder="如 22:00" />
        </el-form-item>
        <el-form-item label="时区">
          <el-input v-model="form.timeZoneId" placeholder="Asia/Shanghai" />
          <div class="field-hint">店铺本地时区（IANA）；营业时段按此时区判断</div>
        </el-form-item>
        <el-form-item label="营业外转人工">
          <el-switch v-model="form.handoffOutsideBusinessHours" />
          <div class="field-hint">开启后非营业时间直接 PendingHumanHandoff，不 AutoSend</div>
        </el-form-item>

        <el-divider content-position="left">自动转人工</el-divider>

        <el-form-item label="低置信度转人工">
          <el-switch v-model="form.autoHandoffOnLowConfidence" />
        </el-form-item>
        <el-form-item label="置信度阈值">
          <el-input-number
            v-model="form.handoffConfidenceThreshold"
            :min="0"
            :max="1"
            :step="0.05"
            :precision="2"
          />
          <div class="field-hint">默认 0.45；分类置信度低于此值则转人工且不生成新草稿</div>
        </el-form-item>
        <el-form-item label="敏感词">
          <el-input
            v-model="form.sensitiveKeywords"
            type="textarea"
            :rows="2"
            placeholder="退款,律师,投诉,police,..."
          />
          <div class="field-hint">逗号分隔；命中则转人工且不生成新 AI 草稿；新会话还可规则升 Supervisor</div>
        </el-form-item>

        <el-divider content-position="left">LLM API Key</el-divider>

        <el-alert
          :type="llm.configured ? 'success' : 'info'"
          :closable="false"
          show-icon
          style="margin-bottom: 16px"
        >
          <template #title>
            <span v-if="llm.configured">
              AI 已配置（来源：{{ sourceLabel }}<span v-if="llm.keyHint">，{{ llm.keyHint }}</span>）— 入站将走真实 LLM 起草
            </span>
            <span v-else>
              <strong>未配置 AI</strong>（规则草稿模式）— 入站 / 注入会生成「未配置 AI·规则草稿」，
              seed、人审发送、SIM 模拟出站仍可演示，不会报错。要启用真实 AI 起草，请在下方填写本店 Key 并保存。
            </span>
          </template>
          <div v-if="!llm.configured && llm.degradeHint" class="field-hint" style="margin-top: 6px">
            {{ llm.degradeHint }}
          </div>
        </el-alert>

        <el-form-item label="API Key">
          <el-input
            v-model="form.llmApiKey"
            type="password"
            @input="llmKeyTouched = true"
            show-password
            clearable
            placeholder="粘贴 DashScope / 通义兼容 Key；留空保存可清除商家 Key"
            autocomplete="off"
          />
          <div class="field-hint">
            优先使用本店 Key；未填则回退平台 <code>Llm:ApiKey</code> / 环境变量 <code>LLM_API_KEY</code>。
            <strong>没有 Key 也能演示</strong>：规则草稿 + 人审发送即可跑通闭环。
          </div>
        </el-form-item>

        <el-divider content-position="left">回复 SLA</el-divider>

        <el-form-item label="回复 SLA（小时）">
          <el-radio-group v-model="form.responseSlaHours">
            <el-radio-button :label="3">3h</el-radio-button>
            <el-radio-button :label="6">6h</el-radio-button>
            <el-radio-button :label="12">12h</el-radio-button>
            <el-radio-button :label="24">24h</el-radio-button>
          </el-radio-group>
          <div class="field-hint">
            用于收件箱「即将超时 / 已超时」与告警列表；默认 12 小时（非平台硬 SLA）
          </div>
        </el-form-item>

        <el-form-item label="告警阈值（小时）">
          <el-input
            v-model="form.alertThresholdHours"
            placeholder="1,3,12"
          />
          <div class="field-hint">逗号分隔；供 GET /api/merchant/alerts 与超时告警条使用</div>
        </el-form-item>

        <el-form-item>
          <el-button type="primary" :loading="saving" @click="save">保存设置</el-button>
        </el-form-item>
      </el-form>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getSellerProfile, updateSellerConfig } from '@/api/seller'

const loading = ref(false)
const saving = ref(false)
const form = reactive({
  defaultReplyTone: 'professional',
  workingLanguage: 'ZH',
  supportedLanguages: ['ID', 'TH', 'VN', 'EN', 'ZH'] as string[],
  outboundMode: 'DraftFirst',
  assignmentMode: 'Unassigned',
  enableAutoReply: true,
  businessHoursStart: '09:00',
  businessHoursEnd: '22:00',
  timeZoneId: 'Asia/Shanghai',
  handoffOutsideBusinessHours: true,
  autoHandoffOnLowConfidence: true,
  handoffConfidenceThreshold: 0.45,
  sensitiveKeywords: '退款,律师,投诉,police,lawyer,refund,lawsuit,举报,报警,法院,诉讼',
  responseSlaHours: 12 as number,
  alertThresholdHours: '1,3,12',
  llmApiKey: '',
})

const llm = reactive({
  configured: false,
  sellerKeyConfigured: false,
  platformKeyConfigured: false,
  keyHint: '' as string | null,
  source: 'none',
  degradeHint: '' as string | null,
})

const sourceLabel = computed(() => {
  if (llm.source === 'seller') return '本店 Key'
  if (llm.source === 'platform') return '平台配置'
  return '无'
})

/** 加载时若已有 Key，用掩码占位；用户未改则保存时不覆盖 */
const llmKeyTouched = ref(false)

function pick(cfg: Record<string, unknown>, ...keys: string[]) {
  for (const k of keys) {
    if (cfg[k] !== undefined && cfg[k] !== null && cfg[k] !== '') return cfg[k]
  }
  return undefined
}

onMounted(async () => {
  loading.value = true
  try {
    const profile = await getSellerProfile()
    const cfg = (profile.Config || profile.config || profile) as Record<string, unknown>
    const tone = pick(cfg, 'DefaultReplyTone', 'defaultReplyTone')
    if (tone) form.defaultReplyTone = String(tone)
    const wl = pick(cfg, 'WorkingLanguage', 'workingLanguage', 'PreferredLanguage', 'preferredLanguage')
    if (wl) form.workingLanguage = String(wl).toUpperCase()
    const sl = pick(cfg, 'SupportedLanguageList', 'supportedLanguageList')
    if (Array.isArray(sl) && sl.length) form.supportedLanguages = sl.map((x: unknown) => String(x).toUpperCase())
    else {
      const csv = pick(cfg, 'SupportedLanguages', 'supportedLanguages')
      if (csv) form.supportedLanguages = String(csv).split(',').map((s) => s.trim().toUpperCase()).filter(Boolean)
    }
    const mode = pick(cfg, 'OutboundMode', 'outboundMode')
    if (mode) form.outboundMode = String(mode) === 'AutoSend' ? 'AutoSend' : 'DraftFirst'
    const am = pick(cfg, 'AssignmentMode', 'assignmentMode')
    if (am) form.assignmentMode = String(am) === 'LeastLoaded' ? 'LeastLoaded' : 'Unassigned'
    const auto = pick(cfg, 'EnableAutoReply', 'enableAutoReply', 'AutoReplyEnabled', 'autoReplyEnabled')
    if (typeof auto === 'boolean') form.enableAutoReply = auto
    const start = pick(cfg, 'BusinessHoursStart', 'businessHoursStart')
    if (start) form.businessHoursStart = String(start)
    const end = pick(cfg, 'BusinessHoursEnd', 'businessHoursEnd')
    if (end) form.businessHoursEnd = String(end)
    const tz = pick(cfg, 'TimeZoneId', 'timeZoneId')
    if (tz) form.timeZoneId = String(tz)
    const ho = pick(cfg, 'HandoffOutsideBusinessHours', 'handoffOutsideBusinessHours')
    if (typeof ho === 'boolean') form.handoffOutsideBusinessHours = ho
    const ah = pick(cfg, 'AutoHandoffOnLowConfidence', 'autoHandoffOnLowConfidence')
    if (typeof ah === 'boolean') form.autoHandoffOnLowConfidence = ah
    const thr = pick(cfg, 'HandoffConfidenceThreshold', 'handoffConfidenceThreshold')
    if (thr != null) form.handoffConfidenceThreshold = Number(thr)
    const sk = pick(cfg, 'SensitiveKeywords', 'sensitiveKeywords')
    if (sk) form.sensitiveKeywords = String(sk)
    const sla = pick(cfg, 'ResponseSlaHours', 'responseSlaHours')
    if (sla != null) form.responseSlaHours = Number(sla) || 12
    const th = pick(cfg, 'AlertThresholdHours', 'alertThresholdHours')
    if (th) form.alertThresholdHours = String(th)

    const llmInfo = (profile.Llm || profile.llm || {}) as Record<string, unknown>
    llm.configured = !!llmInfo.configured
    llm.sellerKeyConfigured = !!llmInfo.sellerKeyConfigured
    llm.platformKeyConfigured = !!llmInfo.platformKeyConfigured
    llm.keyHint = (llmInfo.keyHint as string) || null
    llm.source = String(llmInfo.source || 'none')
    llm.degradeHint = (llmInfo.degradeHint as string) || null
    const hint = pick(cfg, 'LlmKeyHint', 'llmKeyHint')
    if (llm.sellerKeyConfigured) {
      form.llmApiKey = hint ? String(hint) : (llm.keyHint || '********')
      llmKeyTouched.value = false
    } else {
      form.llmApiKey = ''
      llmKeyTouched.value = false
    }
  } catch {
    ElMessage.warning('加载配置失败，显示默认值')
  } finally {
    loading.value = false
  }
})

async function save() {
  saving.value = true
  try {
    const payload: Record<string, unknown> = {
      DefaultReplyTone: form.defaultReplyTone,
      defaultReplyTone: form.defaultReplyTone,
      WorkingLanguage: form.workingLanguage,
      workingLanguage: form.workingLanguage,
      PreferredLanguage: form.workingLanguage,
      preferredLanguage: form.workingLanguage,
      SupportedLanguages: form.supportedLanguages.join(','),
      supportedLanguages: form.supportedLanguages.join(','),
      OutboundMode: form.outboundMode,
      outboundMode: form.outboundMode,
      AssignmentMode: form.assignmentMode,
      assignmentMode: form.assignmentMode,
      EnableAutoReply: form.enableAutoReply,
      enableAutoReply: form.enableAutoReply,
      BusinessHoursStart: form.businessHoursStart,
      businessHoursStart: form.businessHoursStart,
      BusinessHoursEnd: form.businessHoursEnd,
      businessHoursEnd: form.businessHoursEnd,
      TimeZoneId: form.timeZoneId,
      timeZoneId: form.timeZoneId,
      HandoffOutsideBusinessHours: form.handoffOutsideBusinessHours,
      handoffOutsideBusinessHours: form.handoffOutsideBusinessHours,
      AutoHandoffOnLowConfidence: form.autoHandoffOnLowConfidence,
      autoHandoffOnLowConfidence: form.autoHandoffOnLowConfidence,
      HandoffConfidenceThreshold: form.handoffConfidenceThreshold,
      handoffConfidenceThreshold: form.handoffConfidenceThreshold,
      SensitiveKeywords: form.sensitiveKeywords,
      sensitiveKeywords: form.sensitiveKeywords,
      ResponseSlaHours: form.responseSlaHours,
      responseSlaHours: form.responseSlaHours,
      AlertThresholdHours: form.alertThresholdHours,
      alertThresholdHours: form.alertThresholdHours,
    }
    const keyVal = form.llmApiKey.trim()
    if (llmKeyTouched.value || keyVal === '') {
      // 空串 = 清除商家 Key；明文 = 更新；掩码未触碰则不传
      if (!keyVal.startsWith('****')) {
        payload.LlmApiKey = keyVal
        payload.llmApiKey = keyVal
      }
    } else if (keyVal && !keyVal.startsWith('****')) {
      payload.LlmApiKey = keyVal
      payload.llmApiKey = keyVal
    }
    await updateSellerConfig(payload)
    ElMessage.success('已保存')
    // 重新拉状态
    const profile = await getSellerProfile()
    const llmInfo = (profile.Llm || profile.llm || {}) as Record<string, unknown>
    llm.configured = !!llmInfo.configured
    llm.sellerKeyConfigured = !!llmInfo.sellerKeyConfigured
    llm.platformKeyConfigured = !!llmInfo.platformKeyConfigured
    llm.keyHint = (llmInfo.keyHint as string) || null
    llm.source = String(llmInfo.source || 'none')
    llm.degradeHint = (llmInfo.degradeHint as string) || null
    if (llm.sellerKeyConfigured && llm.keyHint) {
      form.llmApiKey = String(llm.keyHint)
      llmKeyTouched.value = false
    }
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped lang="scss">
.field-hint {
  margin-top: 6px;
  font-size: 12px;
  color: #64748b;
  line-height: 1.45;
  width: 100%;
}
</style>
