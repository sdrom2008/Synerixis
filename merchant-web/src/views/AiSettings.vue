<template>
  <div>
    <h2 class="page-title">AI 设置</h2>
    <p class="page-desc">语气、营业时段与出站模式。默认 DraftFirst：AI 只写草稿，人工批准后发送。</p>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <el-form label-width="140px" style="max-width: 560px">
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
        <el-form-item label="启用自动回复">
          <el-switch v-model="form.enableAutoReply" />
        </el-form-item>
        <el-form-item label="营业开始">
          <el-input v-model="form.businessHoursStart" placeholder="如 09:00" />
        </el-form-item>
        <el-form-item label="营业结束">
          <el-input v-model="form.businessHoursEnd" placeholder="如 22:00" />
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
})

onMounted(async () => {
  loading.value = true
  try {
    const profile = await getSellerProfile()
    const cfg = (profile.Config || profile.config || {}) as Record<string, unknown>
    if (cfg.DefaultReplyTone || cfg.defaultReplyTone) {
      form.defaultReplyTone = String(cfg.DefaultReplyTone || cfg.defaultReplyTone)
    }
    if (cfg.OutboundMode || cfg.outboundMode) {
      form.outboundMode = String(cfg.OutboundMode || cfg.outboundMode)
    }
    if (typeof cfg.EnableAutoReply === 'boolean') form.enableAutoReply = cfg.EnableAutoReply
    if (typeof cfg.enableAutoReply === 'boolean') form.enableAutoReply = cfg.enableAutoReply
    if (cfg.BusinessHoursStart || cfg.businessHoursStart) {
      form.businessHoursStart = String(cfg.BusinessHoursStart || cfg.businessHoursStart)
    }
    if (cfg.BusinessHoursEnd || cfg.businessHoursEnd) {
      form.businessHoursEnd = String(cfg.BusinessHoursEnd || cfg.businessHoursEnd)
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
    await updateSellerConfig({
      DefaultReplyTone: form.defaultReplyTone,
      OutboundMode: form.outboundMode,
      EnableAutoReply: form.enableAutoReply,
      BusinessHoursStart: form.businessHoursStart,
      BusinessHoursEnd: form.businessHoursEnd,
    })
    ElMessage.success('已保存')
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}
</script>
