<template>
  <div>
    <h2 class="page-title">AI 设置</h2>
    <p class="page-desc">
      语气、营业时段、自动 handoff、出站模式与回复 SLA。默认 DraftFirst：AI 只写草稿，人工批准后发送。
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
          title="合规风险：AutoSend 可能触发平台对 chatbot / 促销广播的限制，请确认业务确需自动出站。"
          style="margin-bottom: 16px"
        />

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
          <div class="field-hint">逗号分隔；命中则转人工，不生成新 AI 草稿</div>
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
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getSellerProfile, updateSellerConfig } from '@/api/seller'

const loading = ref(false)
const saving = ref(false)
const form = reactive({
  defaultReplyTone: 'professional',
  outboundMode: 'DraftFirst',
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
})

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
    const mode = pick(cfg, 'OutboundMode', 'outboundMode')
    if (mode) form.outboundMode = String(mode) === 'AutoSend' ? 'AutoSend' : 'DraftFirst'
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
  } catch {
    ElMessage.warning('加载配置失败，显示默认值')
  } finally {
    loading.value = false
  }
})

async function save() {
  saving.value = true
  try {
    await updateSellerConfig({
      DefaultReplyTone: form.defaultReplyTone,
      defaultReplyTone: form.defaultReplyTone,
      OutboundMode: form.outboundMode,
      outboundMode: form.outboundMode,
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
    })
    ElMessage.success('已保存')
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
