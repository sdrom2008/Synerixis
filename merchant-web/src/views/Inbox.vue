<template>
  <div class="inbox" v-loading="listLoading">
    <!-- Col 1: session list -->
    <aside class="col list-col">
      <div class="list-head">
        <div class="list-title">
          <span>会话</span>
          <el-badge v-if="pendingDraftCount > 0" :value="pendingDraftCount" type="warning" />
        </div>
        <el-button text :icon="Refresh" :loading="listLoading" @click="loadSessions">刷新</el-button>
      </div>
      <div class="filters">
        <el-radio-group v-model="statusFilter" size="small" @change="loadSessions">
          <el-radio-button label="">全部</el-radio-button>
          <el-radio-button label="Active">进行中</el-radio-button>
          <el-radio-button label="Pending">待处理</el-radio-button>
        </el-radio-group>
      </div>
      <div v-if="sessions.length === 0 && !listLoading" class="list-empty">
        <EmptyState title="暂无会话" desc="绑定店铺并有买家消息后，会话会出现在这里。" />
      </div>
      <div v-else class="session-list">
        <button
          v-for="s in sessions"
          :key="s.id"
          type="button"
          :class="['session-item', { active: s.id === selectedId }]"
          @click="selectSession(s.id)"
        >
          <div class="row1">
            <strong>{{ s.customerName || '买家' }}</strong>
            <el-tag v-if="s.hasPendingDraft" size="small" type="warning" effect="plain">草稿</el-tag>
          </div>
          <div class="row2">
            <span>{{ s.platform || '—' }}</span>
            <span v-if="s.unreadBuyerCount" class="unread">未读 {{ s.unreadBuyerCount }}</span>
          </div>
          <div class="row3">
            <span v-if="s.hoursSinceLastBuyerMsg != null">距买家 {{ s.hoursSinceLastBuyerMsg }}h</span>
            <span v-if="s.needsResponseBy" class="sla">SLA {{ formatTime(s.needsResponseBy) }}</span>
          </div>
        </button>
      </div>
    </aside>

    <!-- Col 2: timeline + draft -->
    <section class="col center-col">
      <template v-if="!selectedId">
        <EmptyState
          title="选择会话开始处理"
          desc="左侧选择买家会话后，可查看消息并审发 AI 草稿。"
          icon="ChatDotRound"
        />
      </template>
      <template v-else>
        <div class="center-head">
          <div>
            <strong>{{ currentSession?.customerName || '会话' }}</strong>
            <el-tag size="small" effect="plain" style="margin-left: 8px">
              {{ messagesMeta.sessionStatus || currentSession?.status || '—' }}
            </el-tag>
          </div>
          <div class="meta">
            <span v-if="messagesMeta.hoursSinceLastBuyerMsg != null">
              距买家消息 {{ messagesMeta.hoursSinceLastBuyerMsg }} 小时
            </span>
            <span v-if="messagesMeta.needsResponseBy">
              建议回复截止 {{ formatTime(messagesMeta.needsResponseBy) }}
            </span>
          </div>
        </div>

        <div class="timeline" ref="timelineEl" v-loading="detailLoading">
          <div v-if="messages.length === 0 && !detailLoading" class="timeline-empty">
            <EmptyState title="暂无消息" desc="该会话尚无消息记录。" />
          </div>
          <div
            v-for="m in messages"
            :key="m.id"
            :class="['bubble', senderClass(m.senderType)]"
          >
            <div class="bubble-meta">
              <span>{{ senderLabel(m.senderType) }}</span>
              <span>{{ formatTime(m.createdAt) }}</span>
            </div>
            <div class="bubble-body">{{ m.content }}</div>
          </div>
        </div>

        <div class="draft-panel">
          <div class="draft-head">
            <strong>AI 草稿</strong>
            <el-tag v-if="draft" size="small" type="warning" effect="plain">待发送</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">无草稿</el-tag>
          </div>
          <el-input
            v-model="draftContent"
            type="textarea"
            :rows="4"
            :disabled="!draft"
            placeholder="选中会话后，若有待发草稿可在此编辑"
          />
          <div class="draft-actions">
            <el-button :disabled="!draft || saving" @click="onSave" :loading="saving">保存</el-button>
            <el-button :disabled="!draft || sending" @click="onDiscard" :loading="discarding">丢弃</el-button>
            <el-button type="primary" :disabled="!draft || sending" :loading="sending" @click="onApprove">
              批准发送
            </el-button>
            <el-button type="success" :disabled="!draft || sending" :loading="sending" @click="onEditSend">
              编辑并发送
            </el-button>
          </div>
        </div>
      </template>
    </section>

    <!-- Col 3: order / context placeholder -->
    <aside class="col side-col">
      <div class="side-head">订单 / 上下文</div>
      <EmptyState
        title="上下文侧栏占位"
        desc="订单、物流与买家画像将在此展示。Handoff / SLA 深化可与后端并行落地。"
        icon="Document"
      />
      <el-descriptions v-if="currentSession" :column="1" size="small" border class="ctx">
        <el-descriptions-item label="平台">{{ currentSession.platform || '—' }}</el-descriptions-item>
        <el-descriptions-item label="会话 ID">{{ currentSession.sessionId || currentSession.id }}</el-descriptions-item>
        <el-descriptions-item label="优先级">{{ currentSession.priority || '—' }}</el-descriptions-item>
        <el-descriptions-item label="消息数">{{ currentSession.messageCount ?? '—' }}</el-descriptions-item>
      </el-descriptions>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, ref } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import EmptyState from '@/components/EmptyState.vue'
import {
  approveDraft,
  discardDraft,
  editAndSendDraft,
  getSessionMessages,
  getSessions,
  updateDraft,
  type MessageItem,
  type SessionItem,
  type DraftInfo,
} from '@/api/merchant'

const listLoading = ref(false)
const detailLoading = ref(false)
const saving = ref(false)
const sending = ref(false)
const discarding = ref(false)
const statusFilter = ref('')
const sessions = ref<SessionItem[]>([])
const pendingDraftCount = ref(0)
const selectedId = ref<string | null>(null)
const messages = ref<MessageItem[]>([])
const draft = ref<DraftInfo | null>(null)
const draftContent = ref('')
const timelineEl = ref<HTMLElement | null>(null)
const messagesMeta = ref<{
  sessionStatus?: string
  hoursSinceLastBuyerMsg?: number
  needsResponseBy?: string
}>({})

const currentSession = computed(() => sessions.value.find((s) => s.id === selectedId.value) || null)

function formatTime(v?: string | null) {
  if (!v) return '—'
  try {
    return new Date(v).toLocaleString('zh-CN', { hour12: false })
  } catch {
    return v
  }
}

function senderClass(t: string) {
  const x = (t || '').toLowerCase()
  if (x === 'customer' || x === 'buyer') return 'from-buyer'
  if (x === 'agent' || x === 'seller') return 'from-agent'
  return 'from-system'
}

function senderLabel(t: string) {
  const x = (t || '').toLowerCase()
  if (x === 'customer' || x === 'buyer') return '买家'
  if (x === 'agent' || x === 'seller') return '坐席/店铺'
  return '系统'
}

async function loadSessions() {
  listLoading.value = true
  try {
    const res = await getSessions(statusFilter.value || undefined)
    sessions.value = res.items || []
    pendingDraftCount.value = res.pendingDraftCount ?? sessions.value.filter((s) => s.hasPendingDraft).length
    if (selectedId.value && !sessions.value.some((s) => s.id === selectedId.value)) {
      selectedId.value = null
      messages.value = []
      draft.value = null
      draftContent.value = ''
    }
  } catch {
    sessions.value = []
    pendingDraftCount.value = 0
    ElMessage.warning('加载会话失败（请确认 API 与登录态）')
  } finally {
    listLoading.value = false
  }
}

async function selectSession(id: string) {
  selectedId.value = id
  detailLoading.value = true
  try {
    const res = await getSessionMessages(id)
    messages.value = res.items || []
    messagesMeta.value = {
      sessionStatus: res.sessionStatus,
      hoursSinceLastBuyerMsg: res.hoursSinceLastBuyerMsg,
      needsResponseBy: res.needsResponseBy,
    }
    draft.value = res.pendingDraft || null
    draftContent.value = res.pendingDraft?.content || ''
    await nextTick()
    if (timelineEl.value) {
      timelineEl.value.scrollTop = timelineEl.value.scrollHeight
    }
  } catch {
    messages.value = []
    draft.value = null
    draftContent.value = ''
    ElMessage.error('加载消息失败')
  } finally {
    detailLoading.value = false
  }
}

async function onSave() {
  if (!selectedId.value || !draft.value) return
  saving.value = true
  try {
    await updateDraft(selectedId.value, draftContent.value)
    ElMessage.success('草稿已保存')
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

async function onApprove() {
  if (!selectedId.value || !draft.value) return
  sending.value = true
  try {
    // If content changed, use edit-send; else approve
    if (draftContent.value.trim() !== (draft.value.content || '').trim()) {
      await editAndSendDraft(selectedId.value, draftContent.value)
    } else {
      await approveDraft(selectedId.value)
    }
    ElMessage.success('已发送')
    await selectSession(selectedId.value)
    await loadSessions()
  } catch {
    ElMessage.error('发送失败')
  } finally {
    sending.value = false
  }
}

async function onEditSend() {
  if (!selectedId.value || !draft.value) return
  if (!draftContent.value.trim()) {
    ElMessage.warning('内容不能为空')
    return
  }
  sending.value = true
  try {
    await editAndSendDraft(selectedId.value, draftContent.value)
    ElMessage.success('已编辑并发送')
    await selectSession(selectedId.value)
    await loadSessions()
  } catch {
    ElMessage.error('发送失败')
  } finally {
    sending.value = false
  }
}

async function onDiscard() {
  if (!selectedId.value || !draft.value) return
  try {
    await ElMessageBox.confirm('确定丢弃该待发送草稿？', '丢弃草稿', {
      type: 'warning',
      confirmButtonText: '丢弃',
      cancelButtonText: '取消',
    })
  } catch {
    return
  }
  discarding.value = true
  try {
    await discardDraft(selectedId.value)
    ElMessage.success('草稿已丢弃')
    await selectSession(selectedId.value)
    await loadSessions()
  } catch {
    ElMessage.error('丢弃失败')
  } finally {
    discarding.value = false
  }
}

onMounted(loadSessions)
</script>

<style scoped lang="scss">
.inbox {
  display: grid;
  grid-template-columns: 300px minmax(0, 1fr) 280px;
  height: calc(100vh - 56px);
  background: #fff;
}
.col {
  min-height: 0;
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--sx-border);
}
.side-col {
  border-right: none;
  background: #f8fafc;
}
.list-head,
.center-head,
.side-head,
.draft-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 12px 14px;
  border-bottom: 1px solid var(--sx-border);
  flex-shrink: 0;
}
.list-title {
  display: flex;
  align-items: center;
  gap: 8px;
  font-weight: 600;
}
.filters {
  padding: 8px 12px;
  border-bottom: 1px solid var(--sx-border);
}
.session-list {
  overflow: auto;
  flex: 1;
}
.session-item {
  display: block;
  width: 100%;
  text-align: left;
  border: none;
  background: transparent;
  border-bottom: 1px solid #f1f5f9;
  padding: 12px 14px;
  cursor: pointer;
  font: inherit;
  color: inherit;
  &:hover {
    background: #f8fafc;
  }
  &.active {
    background: #eff6ff;
    border-left: 3px solid #2563eb;
  }
}
.row1 {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}
.row2,
.row3 {
  margin-top: 4px;
  font-size: 12px;
  color: #64748b;
  display: flex;
  justify-content: space-between;
  gap: 8px;
}
.unread {
  color: #2563eb;
  font-weight: 600;
}
.sla {
  color: #b45309;
}
.center-head {
  flex-direction: column;
  align-items: flex-start;
  .meta {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
    font-size: 12px;
    color: #64748b;
    margin-top: 4px;
  }
}
.timeline {
  flex: 1;
  overflow: auto;
  padding: 16px;
  background: #f8fafc;
}
.bubble {
  max-width: 78%;
  margin-bottom: 12px;
  &.from-buyer {
    margin-right: auto;
  }
  &.from-agent {
    margin-left: auto;
  }
  &.from-system {
    margin: 0 auto 12px;
    max-width: 90%;
    opacity: 0.85;
  }
}
.bubble-meta {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  font-size: 11px;
  color: #94a3b8;
  margin-bottom: 4px;
}
.bubble-body {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 10px 12px;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.5;
  font-size: 14px;
}
.from-agent .bubble-body {
  background: #2563eb;
  color: #fff;
  border-color: #2563eb;
}
.from-system .bubble-body {
  background: #f1f5f9;
  text-align: center;
  font-size: 13px;
}
.draft-panel {
  border-top: 1px solid var(--sx-border);
  padding: 12px 14px 14px;
  background: #fff;
  flex-shrink: 0;
}
.draft-actions {
  margin-top: 10px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: flex-end;
}
.side-head {
  font-weight: 600;
  justify-content: flex-start;
}
.ctx {
  margin: 0 12px 16px;
}
.list-empty,
.timeline-empty {
  flex: 1;
}
@media (max-width: 1100px) {
  .inbox {
    grid-template-columns: 260px minmax(0, 1fr);
  }
  .side-col {
    display: none;
  }
}
</style>
