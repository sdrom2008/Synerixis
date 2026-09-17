<template>
  <div class="inbox" v-loading="listLoading">
    <!-- Col 1: session list -->
    <aside class="col list-col">
      <div class="list-head">
        <div class="list-title">
          <span>会话</span>
          <el-badge v-if="pendingDraftCount > 0" :value="pendingDraftCount" type="warning" />
        </div>
        <div class="list-head-actions">
          <el-button
            size="small"
            type="primary"
            plain
            :loading="injecting"
            @click="onInjectTest"
          >注入测试</el-button>
          <el-button text :icon="Refresh" :loading="listLoading" @click="refreshAll">刷新</el-button>
        </div>
      </div>

      <div v-if="pendingDraftCount > 0" class="draft-banner">
        待发送草稿 {{ pendingDraftCount }} 条 — 请尽快审发，以免超时影响店铺响应表现
      </div>
      <div v-if="alertCount > 0" class="alert-banner" @click="setFilter('alerts')">
        超时告警 {{ alertCount }} 条（已超时 {{ overdueCount }}）— 点击筛选
      </div>
      <div class="sla-notify-bar">
        <el-switch
          v-model="soundEnabled"
          size="small"
          inline-prompt
          active-text="声音"
          inactive-text="静音"
          @change="persistNotifyPrefs"
        />
        <el-button size="small" text type="primary" @click="enableBrowserNotify">
          {{ notifyPermission === 'granted' ? '浏览器提醒已开' : '开启提醒' }}
        </el-button>
      </div>

      <div class="filters">
        <el-select
          v-model="shopFilter"
          size="small"
          clearable
          placeholder="全部店铺"
          class="shop-filter"
          @change="onFilterChange"
        >
          <el-option label="全部店铺" value="" />
          <el-option
            v-for="opt in shopOptions"
            :key="opt.connectionId"
            :label="shopOptionLabel(opt)"
            :value="opt.connectionId"
          >
            <div class="shop-opt-row">
              <span>{{ shopOptionBase(opt) }}</span>
              <span class="shop-opt-badges">
                <el-tag v-if="(opt.pendingDraftCount || 0) > 0" size="small" type="warning" effect="plain">
                  草稿 {{ opt.pendingDraftCount }}
                </el-tag>
                <el-tag v-if="(opt.overdueCount || 0) > 0" size="small" type="danger" effect="plain">
                  超时 {{ opt.overdueCount }}
                </el-tag>
                <el-tag
                  v-else-if="(opt.soonCount || 0) > 0"
                  size="small"
                  class="tag-soon"
                  effect="plain"
                >即将 {{ opt.soonCount }}</el-tag>
              </span>
            </div>
          </el-option>
        </el-select>
        <el-radio-group v-model="statusFilter" size="small" @change="onFilterChange">
          <el-radio-button label="">全部</el-radio-button>
          <el-radio-button label="draft">待发草稿</el-radio-button>
          <el-radio-button label="alerts">
            超时告警
            <span v-if="alertCount > 0" class="chip-n">{{ alertCount }}</span>
          </el-radio-button>
          <el-radio-button label="handoff">待人工</el-radio-button>
          <el-radio-button label="Active">进行中</el-radio-button>
          <el-radio-button label="Pending">待处理</el-radio-button>
        </el-radio-group>
        <el-radio-group v-model="assignmentFilter" size="small" @change="onFilterChange" class="assign-filter">
          <el-radio-button label="">全部分配</el-radio-button>
          <el-radio-button label="unassigned">未分配</el-radio-button>
          <el-radio-button label="mine">分给我</el-radio-button>
        </el-radio-group>
      </div>

      <div v-if="displaySessions.length === 0 && !listLoading" class="list-empty">
        <EmptyState :title="listEmptyTitle" :desc="listEmptyDesc" />
        <div v-if="statusFilter || shopFilter || assignmentFilter" class="empty-actions">
          <el-button size="small" @click="clearFilters">清除筛选</el-button>
        </div>
      </div>
      <div v-else class="session-list">
        <button
          v-for="s in displaySessions"
          :key="s.id"
          type="button"
          :class="['session-item', { active: s.id === selectedId }, urgencyClass(s)]"
          @click="selectSession(s.id)"
        >
          <div class="row1">
            <strong>{{ s.customerName || '买家' }}</strong>
            <div class="badges">
              <el-tag v-if="s.hasPendingDraft" size="small" type="warning" effect="plain">草稿</el-tag>
              <el-tag v-if="isHandoff(s)" size="small" type="warning" effect="plain">待人工</el-tag>
              <el-tag v-if="slaLabel(s) === '已超时'" size="small" type="danger" effect="plain">已超时</el-tag>
              <el-tag v-else-if="slaLabel(s) === '即将超时'" size="small" class="tag-soon" effect="plain">即将超时</el-tag>
            </div>
          </div>
          <div class="row2">
            <span>{{ platformShopLabel(s) }}</span>
            <span v-if="s.assignedAgent" class="agent-chip">{{ s.assignedAgent.name }}</span>
            <span v-else class="agent-chip muted">未分配</span>
            <span v-if="s.unreadBuyerCount" class="unread">未读 {{ s.unreadBuyerCount }}</span>
          </div>
          <div class="row3">
            <span v-if="s.hoursSinceLastBuyerMsg != null">买家等待 {{ formatHours(s.hoursSinceLastBuyerMsg) }}</span>
            <span v-if="s.needsResponseBy" class="sla">截止 {{ formatTime(s.needsResponseBy) }}</span>
          </div>
        </button>
      </div>
    </aside>

    <!-- Col 2: timeline + draft -->
    <section class="col center-col">
      <template v-if="!selectedId">
        <EmptyState
          title="选择会话开始处理"
          desc="左侧选择买家会话后，可审发 AI 草稿或手动起草发送。"
          icon="ChatDotRound"
        />
      </template>
      <template v-else>
        <div class="center-head">
          <div class="head-top">
            <div class="head-title">
              <strong>{{ currentSession?.customerName || '会话' }}</strong>
              <el-tag size="small" effect="plain">
                {{ statusLabel(messagesMeta.sessionStatus || currentSession?.status) }}
              </el-tag>
              <el-tag v-if="messagesMeta.pendingHumanHandoff" size="small" type="warning" effect="plain">
                待人工 / 已停 AI 草稿
              </el-tag>
              <el-tag v-if="draft" size="small" type="warning" effect="plain">
                {{ isSupersededDraft ? '旧草稿可发送' : '待发送草稿' }}
              </el-tag>
              <el-tag v-if="messagesMeta.slaUrgency === 'overdue'" size="small" type="danger" effect="plain">
                已超时
              </el-tag>
              <el-tag
                v-else-if="messagesMeta.slaUrgency === 'soon'"
                size="small"
                class="tag-soon"
                effect="plain"
              >
                即将超时
              </el-tag>
            </div>
            <div class="assign-actions">
              <el-select
                v-if="canAssign"
                v-model="assignAgentId"
                size="small"
                clearable
                filterable
                placeholder="分配给坐席"
                style="width: 140px"
                :loading="agentsLoading"
              >
                <el-option
                  v-for="a in shopAgents"
                  :key="a.id"
                  :label="agentOptionLabel(a)"
                  :value="a.id"
                />
              </el-select>
              <el-button
                v-if="canAssign"
                size="small"
                type="primary"
                plain
                :loading="assigning"
                :disabled="!assignAgentId"
                @click="onAssign"
              >
                分配
              </el-button>
              <el-button
                v-if="canClaim"
                size="small"
                type="success"
                plain
                :loading="claiming"
                @click="onClaim"
              >
                认领给我
              </el-button>
              <el-button
                v-if="canTransfer"
                type="warning"
                plain
                size="small"
                :loading="transferring"
                @click="onTransfer"
              >
                转人工客服
              </el-button>
            </div>
          </div>
          <div class="assign-meta">
            当前坐席：
            <strong v-if="displayAssignedAgent">{{ displayAssignedAgent.name }}</strong>
            <span v-else class="muted">未分配</span>
            <span v-if="messagesMeta.assignedAt" class="muted"> · {{ formatTime(messagesMeta.assignedAt) }}</span>
          </div>
          <div v-if="messagesMeta.pendingHumanHandoff" class="handoff-hint">
            已转人工：入站消息不再生成新 AI 草稿，也不 AutoSend；下方旧草稿仍可编辑后手动发送。分配坐席与 handoff 可并存。
          </div>
          <div class="meta">
            <span v-if="messagesMeta.hoursSinceLastBuyerMsg != null">
              买家等待约 {{ formatHours(messagesMeta.hoursSinceLastBuyerMsg) }}
            </span>
            <span v-if="messagesMeta.needsResponseBy">
              建议回复截止 {{ formatTime(messagesMeta.needsResponseBy) }}
            </span>
            <span v-if="messagesMeta.responseSlaHours">SLA {{ messagesMeta.responseSlaHours }}h</span>
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
              <el-tag
                v-if="isMockOutbound(m)"
                size="small"
                type="info"
                effect="plain"
                class="mock-tag"
              >演示·模拟出站</el-tag>
              <span>{{ formatTime(m.createdAt) }}</span>
            </div>
            <div class="bubble-body">{{ m.content }}</div>
          </div>
        </div>

        <div class="draft-panel">
          <div class="draft-head">
            <strong>回复草稿（未发到平台）</strong>
            <el-tag v-if="draft && !isSupersededDraft" size="small" type="warning" effect="plain">待发送</el-tag>
            <el-tag v-else-if="isSupersededDraft" size="small" type="info" effect="plain">已停用但仍可发送</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">可手动起草</el-tag>
          </div>
          <el-alert
            v-if="lastSendHint"
            :title="lastSendHint"
            type="success"
            :closable="true"
            show-icon
            class="send-hint"
            @close="lastSendHint = ''"
          />
          <p class="draft-hint">
            可编辑 AI 草稿或<strong>手动起草</strong>后点「发送」。内容先落成待审草稿再出站（draft-first），不会开启 AutoSend。演示店为模拟出站，不调用真实 Shopee/TikTok。
          </p>
          <p v-if="!draft" class="draft-empty-hint">
            当前无 AI 草稿。可在下方直接撰写回复并「保存」或「发送」；也可点左上角「注入测试」生成样例进线。
          </p>
          <div v-if="quickReplies.length" class="qr-bar">
            <span class="qr-label">快捷回复</span>
            <el-button
              v-for="qr in quickReplies"
              :key="qr.id"
              size="small"
              @click="insertQuickReply(qr.content)"
            >
              {{ qr.title }}
            </el-button>
          </div>
          <el-input
            v-model="draftContent"
            type="textarea"
            :rows="4"
            placeholder="撰写回复内容，保存为草稿或直接发送（演示店模拟出站）"
          />
          <div class="draft-actions">
            <el-button :disabled="!canSaveDraft || saving" @click="onSave" :loading="saving">保存草稿</el-button>
            <el-button :disabled="!draft || sending" @click="onDiscard" :loading="discarding">丢弃</el-button>
            <el-button
              type="primary"
              :disabled="!canSendDraft || sending"
              :loading="sending"
              @click="onSend"
            >
              发送
            </el-button>
          </div>
        </div>
      </template>
    </section>

    <!-- Col 3: order / context -->
    <aside class="col side-col">
      <div class="side-head">详情</div>
      <EmptyState
        v-if="!currentSession"
        title="订单 / 买家画像"
        desc="选中会话后显示平台、SLA、关联订单。"
        icon="Document"
      />
      <template v-else>
        <el-descriptions :column="1" size="small" border class="ctx">
          <el-descriptions-item label="平台">{{ currentSession.platform || '—' }}</el-descriptions-item>
          <el-descriptions-item label="店铺">
            {{ currentSession.shopNickname || currentSession.platformShopOpenId || '—' }}
          </el-descriptions-item>
          <el-descriptions-item label="会话 ID">{{ currentSession.sessionId || currentSession.id }}</el-descriptions-item>
          <el-descriptions-item label="优先级">{{ currentSession.priority || '—' }}</el-descriptions-item>
          <el-descriptions-item label="消息数">{{ currentSession.messageCount ?? '—' }}</el-descriptions-item>
          <el-descriptions-item label="SLA 状态">
            <span :class="slaTextClass(currentSession)">{{ slaDetailLabel(currentSession) }}</span>
          </el-descriptions-item>
          <el-descriptions-item label="转人工">
            {{ isHandoff(currentSession) || messagesMeta.pendingHumanHandoff ? '是（已停 AI 新草稿）' : '否' }}
          </el-descriptions-item>
          <el-descriptions-item v-if="messagesMeta.handoffAt" label="转接时间">
            {{ formatTime(messagesMeta.handoffAt) }}
          </el-descriptions-item>
          <el-descriptions-item label="回复 SLA">
            {{ messagesMeta.responseSlaHours || responseSlaHours || 12 }} 小时
          </el-descriptions-item>
        </el-descriptions>

        <div class="orders-block">
          <div class="orders-title">
            关联订单
            <el-tag v-if="ordersSource" size="small" effect="plain" type="info" style="margin-left: 6px">
              {{ ordersSource === 'platform' ? '平台' : '本地' }}
            </el-tag>
          </div>
          <div v-loading="ordersLoading">
            <el-alert
              v-if="ordersWarning"
              type="warning"
              :closable="false"
              show-icon
              :title="ordersWarningLabel"
              style="margin-bottom: 8px"
            />
            <EmptyState
              v-if="!ordersLoading && orders.length === 0"
              title="暂无订单"
              desc="本地与平台均未查到该买家订单，或平台回源失败。"
              icon="Document"
            />
            <div v-for="o in orders" :key="o.id || o.orderNo || o.summary" class="order-card">
              <div class="order-row">
                <strong>{{ o.orderNo || '—' }}</strong>
                <div class="order-tags">
                  <el-tag size="small" effect="plain">{{ o.status || '—' }}</el-tag>
                  <el-tag size="small" :type="(o.source || ordersSource) === 'platform' ? 'warning' : 'success'" effect="plain">
                    {{ (o.source || ordersSource) === 'platform' ? '平台' : '本地' }}
                  </el-tag>
                </div>
              </div>
              <div class="order-meta">
                <span>金额 {{ formatAmount(o.totalAmount ?? o.paymentAmount) }}</span>
                <span>{{ formatTime(o.orderTime) }}</span>
              </div>
              <div v-if="o.logisticsNo || o.logistics?.trackingNumber" class="order-meta">
                {{ o.logisticsCompany || '物流' }} {{ o.logistics?.trackingNumber || o.logisticsNo }}
                <el-tag
                  v-if="o.logistics?.logisticsStatus"
                  size="small"
                  effect="plain"
                  style="margin-left: 6px"
                >{{ o.logistics.logisticsStatus }}</el-tag>
              </div>
              <div v-if="o.logistics?.available && o.logistics.checkpoints?.length" class="logistics-track">
                <div class="logistics-track-title">物流轨迹</div>
                <ul class="logistics-checkpoints">
                  <li v-for="(cp, idx) in o.logistics.checkpoints" :key="idx">
                    <span class="cp-time">{{ formatTime(cp.time) }}</span>
                    <span class="cp-desc">{{ cp.description || cp.status || '—' }}</span>
                  </li>
                </ul>
              </div>
              <div
                v-else-if="o.logisticsNo || o.logistics?.trackingNumber || o.logistics?.message"
                class="order-meta logistics-fallback"
              >
                {{ o.logistics?.message || '仅有运单号/订单状态，轨迹暂不可用' }}
              </div>
              <div v-if="o.summary && (o.source || ordersSource) === 'platform'" class="order-meta">
                {{ o.summary }}
              </div>
            </div>
          </div>
        </div>
      </template>
    </aside>
  </div>
</template>

<script setup lang="ts">
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { Refresh } from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import EmptyState from '@/components/EmptyState.vue'
import {
  approveDraft,
  discardDraft,
  editAndSendDraft,
  getMerchantAlerts,
  getSessionMessages,
  getSessionOrders,
  getSessions,
  getShopOptions,
  getShopAgents,
  assignSession,
  claimSession,
  transferSession,
  updateDraft,
  simulateInbound,
  listQuickReplies,
  type QuickReplyItem,
  type MessageItem,
  type SessionItem,
  type SessionOrderItem,
  type ShopOption,
  type ShopAgentItem,
  type DraftInfo,
  type DraftSendResult,
  type SlaUrgency,
} from '@/api/merchant'
import { useAuthStore } from '@/stores/auth'

const listLoading = ref(false)
const detailLoading = ref(false)
const saving = ref(false)
const sending = ref(false)
const discarding = ref(false)
const injecting = ref(false)
const transferring = ref(false)
const assigning = ref(false)
const claiming = ref(false)
const agentsLoading = ref(false)
const shopAgents = ref<ShopAgentItem[]>([])
const assignAgentId = ref<string>('')
const assignmentFilter = ref('')
const auth = useAuthStore()
const statusFilter = ref('')
const shopFilter = ref('')
const shopOptions = ref<ShopOption[]>([])
const orders = ref<SessionOrderItem[]>([])
const ordersLoading = ref(false)
const ordersSource = ref<string>('')
const ordersWarning = ref<string | null>(null)
const sessions = ref<SessionItem[]>([])
const pendingDraftCount = ref(0)
const alertCount = ref(0)
const overdueCount = ref(0)
const prevOverdueCount = ref(0)
const soundEnabled = ref(localStorage.getItem('sx.sla.sound') !== '0')
const notifyPermission = ref<NotificationPermission>(
  typeof Notification !== 'undefined' ? Notification.permission : 'denied',
)
let alertPollTimer: ReturnType<typeof setInterval> | null = null
const responseSlaHours = ref(12)
const selectedId = ref<string | null>(null)
const messages = ref<MessageItem[]>([])
const draft = ref<DraftInfo | null>(null)
const draftContent = ref('')
const lastSendHint = ref('')
const quickReplies = ref<QuickReplyItem[]>([])
const timelineEl = ref<HTMLElement | null>(null)
const messagesMeta = ref<{
  sessionStatus?: string
  pendingHumanHandoff?: boolean
  handoffAt?: string | null
  assignedAgent?: { id: string; name: string; role?: string } | null
  assignedAt?: string | null
  hoursSinceLastBuyerMsg?: number
  needsResponseBy?: string
  responseSlaHours?: number
  slaUrgency?: SlaUrgency
}>({})

const currentSession = computed(() => sessions.value.find((s) => s.id === selectedId.value) || null)

const isSupersededDraft = computed(() => {
  const st = draft.value?.status || ''
  return st === 'Superseded'
})

const canSaveDraft = computed(() => !!selectedId.value && !!draftContent.value.trim())
const canSendDraft = computed(() => !!selectedId.value && !!draftContent.value.trim())

const canTransfer = computed(() => {
  if (messagesMeta.value.pendingHumanHandoff) return false
  const st = messagesMeta.value.sessionStatus || currentSession.value?.status
  return st === 'Pending' || st === 'Active'
})

const canAssign = computed(() => {
  const t = auth.userType
  if (t !== 'Seller' && t !== 'Supervisor' && t !== 'Admin') return false
  const st = messagesMeta.value.sessionStatus || currentSession.value?.status
  return st === 'Pending' || st === 'Active'
})

const canClaim = computed(() => {
  const t = auth.userType
  if (t !== 'Agent' && t !== 'Supervisor' && t !== 'Admin') return false
  const st = messagesMeta.value.sessionStatus || currentSession.value?.status
  return st === 'Pending' || st === 'Active'
})

const displayAssignedAgent = computed(() => {
  return messagesMeta.value.assignedAgent || currentSession.value?.assignedAgent || null
})

const displaySessions = computed(() => {
  let list = sessions.value.slice()
  if (statusFilter.value === 'draft') {
    list = list.filter((s) => s.hasPendingDraft)
  } else if (statusFilter.value === 'handoff') {
    list = list.filter((s) => isHandoff(s))
  } else if (statusFilter.value === 'alerts') {
    list = list.filter((s) => {
      const u = computeUrgency(s)
      return u === 'soon' || u === 'overdue'
    })
  }
  if (assignmentFilter.value === 'unassigned') {
    list = list.filter((s) => !s.assignedAgent)
  } else if (assignmentFilter.value === 'mine') {
    const uid = auth.profile?.userId
    if (uid) list = list.filter((s) => s.assignedAgent?.id === uid)
    else list = []
  }
  // Sort: overdue first, then soon, then pending draft, then by needsResponseBy asc
  return list.sort((a, b) => {
    const rank = (s: SessionItem) => {
      const u = computeUrgency(s)
      if (u === 'overdue') return 0
      if (u === 'soon') return 1
      if (s.hasPendingDraft) return 2
      return 3
    }
    const ra = rank(a)
    const rb = rank(b)
    if (ra !== rb) return ra - rb
    const ta = a.needsResponseBy ? new Date(a.needsResponseBy).getTime() : Number.MAX_SAFE_INTEGER
    const tb = b.needsResponseBy ? new Date(b.needsResponseBy).getTime() : Number.MAX_SAFE_INTEGER
    return ta - tb
  })
})


const listEmptyTitle = computed(() => {
  if (statusFilter.value === 'draft') return '当前无待发草稿'
  if (statusFilter.value === 'alerts') return '当前无超时告警'
  if (statusFilter.value === 'handoff') return '当前无待人工会话'
  if (statusFilter.value === 'Active') return '当前无进行中会话'
  if (statusFilter.value === 'Pending') return '当前无待处理会话'
  if (assignmentFilter.value === 'unassigned') return '没有未分配会话'
  if (assignmentFilter.value === 'mine') return '没有分配给你的会话'
  if (shopFilter.value) return '该店铺暂无会话'
  return '暂无会话'
})

const listEmptyDesc = computed(() => {
  if (statusFilter.value || shopFilter.value || assignmentFilter.value) {
    return '试试清除筛选，或到登录页 / 上手指南「加载演示数据」注入样例会话。'
  }
  return '绑定店铺并有买家消息后，会话会出现在这里。AI 会生成草稿，需人工确认后才会发到平台。'
})

function clearFilters() {
  statusFilter.value = ''
  shopFilter.value = ''
  assignmentFilter.value = ''
  onFilterChange()
}

function isMockOutbound(m: MessageItem) {
  const id = (m.platformMsgId || '').toLowerCase()
  return id.startsWith('mock:') || id.startsWith('outbound:mock')
}

function slaDetailLabel(conv: SessionItem | null | undefined) {
  if (!conv) return '—'
  const u = computeUrgency(conv)
  const h = Number(conv.hoursSinceLastBuyerMsg)
  const sla = Number(conv.responseSlaHours || responseSlaHours.value || 12)
  if (u === 'overdue') {
    const over = Number.isNaN(h) ? '' : ` · 已超 ${formatHours(Math.max(0, h - sla))}`
    return `已超时${over}`
  }
  if (u === 'soon') {
    const left = Number.isNaN(h) ? '' : ` · 约剩 ${formatHours(Math.max(0, sla - h))}`
    return `即将超时${left}`
  }
  if (!Number.isNaN(h) && sla > 0) {
    return `正常 · 约剩 ${formatHours(Math.max(0, sla - h))}`
  }
  return '正常'
}

function slaTextClass(conv: SessionItem | null | undefined) {
  if (!conv) return ''
  const u = computeUrgency(conv)
  if (u === 'overdue') return 'sla-text-overdue'
  if (u === 'soon') return 'sla-text-soon'
  return 'sla-text-ok'
}

function formatTime(v?: string | null) {
  if (!v) return '—'
  try {
    return new Date(v).toLocaleString('zh-CN', { hour12: false })
  } catch {
    return v
  }
}

function formatHours(h?: number | null) {
  if (h == null || Number.isNaN(Number(h))) return '—'
  const n = Number(h)
  if (n < 1) return `${Math.round(n * 60)} 分钟`
  return `${n.toFixed(1)} 小时`
}

function statusLabel(status?: string) {
  const map: Record<string, string> = {
    Pending: '待处理',
    Active: '进行中',
    Resolved: '已解决',
    Closed: '已关闭',
  }
  return map[status || ''] || status || '—'
}

function senderClass(t: string) {
  const x = (t || '').toLowerCase()
  if (x === 'customer' || x === 'buyer' || x === '1') return 'from-buyer'
  if (x === 'agent' || x === 'seller' || x === '2') return 'from-agent'
  return 'from-system'
}

function senderLabel(t: string) {
  const x = (t || '').toLowerCase()
  if (x === 'customer' || x === 'buyer' || x === '1') return '买家'
  if (x === 'agent' || x === 'seller' || x === '2') return '坐席/店铺'
  return '系统'
}

function isHandoff(conv: SessionItem) {
  return !!(conv.pendingHumanHandoff)
}

function computeUrgency(conv: SessionItem): SlaUrgency {
  if (conv.slaUrgency) return conv.slaUrgency
  const h = Number(conv.hoursSinceLastBuyerMsg)
  const sla = Number(conv.responseSlaHours || responseSlaHours.value || 12)
  if (Number.isNaN(h)) return 'ok'
  if (h >= sla) return 'overdue'
  if (h >= Math.max(sla * 0.75, sla - 0.5)) return 'soon'
  return 'ok'
}

function slaLabel(conv: SessionItem) {
  const u = computeUrgency(conv)
  if (u === 'overdue') return '已超时'
  if (u === 'soon') return '即将超时'
  return ''
}

function urgencyClass(s: SessionItem) {
  const u = computeUrgency(s)
  if (u === 'overdue') return 'urgency-overdue'
  if (u === 'soon') return 'urgency-soon'
  return ''
}

function shopOptionBase(opt: ShopOption) {
  const nick = opt.nickname || opt.shopId || '店铺'
  return `${opt.platform || '—'} · ${nick}`
}

function shopOptionLabel(opt: ShopOption) {
  const base = shopOptionBase(opt)
  const bits: string[] = []
  if ((opt.pendingDraftCount || 0) > 0) bits.push(`草稿${opt.pendingDraftCount}`)
  if ((opt.overdueCount || 0) > 0) bits.push(`超时${opt.overdueCount}`)
  else if ((opt.soonCount || 0) > 0) bits.push(`即将${opt.soonCount}`)
  return bits.length ? `${base}（${bits.join(' · ')}）` : base
}

function platformShopLabel(s: SessionItem) {
  const p = s.platform || '—'
  const nick = s.shopNickname
  return nick ? `${p} · ${nick}` : p
}

function formatAmount(v?: number | null) {
  if (v == null || Number.isNaN(Number(v))) return '—'
  return Number(v).toLocaleString('zh-CN', { minimumFractionDigits: 2, maximumFractionDigits: 2 })
}

function setFilter(v: string) {
  statusFilter.value = v
  onFilterChange()
}

function onFilterChange() {
  loadSessions()
}

async function refreshAll() {
  void loadQuickReplies()
  void loadShopAgents()
  await Promise.all([loadSessions(), loadAlerts()])
  if (selectedId.value) await selectSession(selectedId.value)
}

const ordersWarningLabel = computed(() => {
  const w = ordersWarning.value
  if (w === 'platform_lookup_failed') return '平台查单失败，已返回空列表'
  if (w === 'platform_empty') return '平台未查到订单'
  if (w === 'platform_unsupported_or_missing_customer') return '无法回源平台（缺平台或买家 ID）'
  return w || ''
})

function persistNotifyPrefs() {
  localStorage.setItem('sx.sla.sound', soundEnabled.value ? '1' : '0')
}

async function enableBrowserNotify() {
  if (typeof Notification === 'undefined') {
    ElMessage.warning('当前浏览器不支持通知')
    return
  }
  const perm = await Notification.requestPermission()
  notifyPermission.value = perm
  localStorage.setItem('sx.sla.notify', perm === 'granted' ? '1' : '0')
  if (perm === 'granted') ElMessage.success('浏览器提醒已开启')
  else ElMessage.info('未授予通知权限')
}

function playSlaBeep() {
  try {
    const Ctx = window.AudioContext || (window as unknown as { webkitAudioContext: typeof AudioContext }).webkitAudioContext
    if (!Ctx) return
    const ctx = new Ctx()
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.type = 'sine'
    osc.frequency.value = 880
    gain.gain.value = 0.08
    osc.connect(gain)
    gain.connect(ctx.destination)
    osc.start()
    setTimeout(() => {
      osc.stop()
      ctx.close()
    }, 180)
  } catch {
    /* ignore */
  }
}

function maybeNotifySla(newOverdue: number) {
  if (newOverdue > prevOverdueCount.value && prevOverdueCount.value >= 0) {
    if (soundEnabled.value) playSlaBeep()
    if (
      notifyPermission.value === 'granted' &&
      localStorage.getItem('sx.sla.notify') === '1' &&
      typeof Notification !== 'undefined'
    ) {
      try {
        new Notification('Synerixis SLA', {
          body: `已超时会话增至 ${newOverdue} 条，请尽快处理`,
          silent: true,
        })
      } catch {
        /* ignore */
      }
    }
  }
  prevOverdueCount.value = newOverdue
}

async function loadAlerts() {
  try {
    const alerts = await getMerchantAlerts()
    alertCount.value = alerts?.total ?? alerts?.items?.length ?? 0
    const items = alerts?.items || []
    const overdue = items.filter((a) => a.slaUrgency === 'overdue').length
    overdueCount.value = overdue
    maybeNotifySla(overdue)
  } catch {
    alertCount.value = 0
    overdueCount.value = 0
  }
}

async function loadShopOptions() {
  try {
    const res = await getShopOptions()
    shopOptions.value = res.items || []
  } catch {
    shopOptions.value = []
  }
}

async function loadOrders(sessionId: string) {
  ordersLoading.value = true
  try {
    const res = await getSessionOrders(sessionId)
    orders.value = res.items || []
    ordersSource.value = res.source || (orders.value[0]?.source ?? '')
    ordersWarning.value = res.warning ?? null
  } catch {
    orders.value = []
    ordersSource.value = ''
    ordersWarning.value = null
  } finally {
    ordersLoading.value = false
  }
}

async function loadSessions() {
  listLoading.value = true
  try {
    const clientOnly = ['draft', 'alerts', 'handoff']
    const apiStatus =
      statusFilter.value && !clientOnly.includes(statusFilter.value)
        ? statusFilter.value
        : undefined
    const [res] = await Promise.all([
      getSessions({
        status: apiStatus,
        connectionId: shopFilter.value || undefined,
        assignment: assignmentFilter.value || undefined,
      }),
      loadAlerts(),
    ])
    sessions.value = res.items || []
    pendingDraftCount.value =
      res.pendingDraftCount ?? sessions.value.filter((s) => s.hasPendingDraft).length
    responseSlaHours.value = res.responseSlaHours || 12
    if (selectedId.value && !sessions.value.some((s) => s.id === selectedId.value)) {
      selectedId.value = null
      messages.value = []
      draft.value = null
      draftContent.value = ''
      messagesMeta.value = {}
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
      pendingHumanHandoff: !!res.pendingHumanHandoff,
      handoffAt: res.handoffAt,
      assignedAgent: res.assignedAgent || null,
      assignedAt: res.assignedAt || null,
      hoursSinceLastBuyerMsg: res.hoursSinceLastBuyerMsg,
      needsResponseBy: res.needsResponseBy,
      responseSlaHours: res.responseSlaHours,
      slaUrgency: res.slaUrgency,
    }
    assignAgentId.value = res.assignedAgent?.id || ''
    draft.value = res.pendingDraft || null
    draftContent.value = res.pendingDraft?.content || ''
    await loadOrders(id)
    await nextTick()
    if (timelineEl.value) {
      timelineEl.value.scrollTop = timelineEl.value.scrollHeight
    }
  } catch {
    messages.value = []
    draft.value = null
    draftContent.value = ''
    messagesMeta.value = {}
    ElMessage.error('加载消息失败')
  } finally {
    detailLoading.value = false
  }
}

async function onTransfer() {
  if (!selectedId.value || !canTransfer.value) return
  try {
    await ElMessageBox.confirm(
      '转人工后将停止 AI 新草稿与 AutoSend；旧草稿仍可手动发送。确认？',
      '转人工客服',
      { type: 'warning', confirmButtonText: '确认转接', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  transferring.value = true
  try {
    await transferSession(selectedId.value)
    ElMessage.success('已转人工，AI 草稿已停')
    await selectSession(selectedId.value)
    await loadSessions()
  } catch {
    ElMessage.error('转接失败')
  } finally {
    transferring.value = false
  }
}

function agentOptionLabel(a: ShopAgentItem) {
  const online = a.online ? '在线' : '离线'
  return `${a.name} · ${a.role || 'Agent'} · ${online}`
}

async function loadShopAgents() {
  agentsLoading.value = true
  try {
    const res = await getShopAgents()
    shopAgents.value = res.items || []
  } catch {
    shopAgents.value = []
  } finally {
    agentsLoading.value = false
  }
}

async function onAssign() {
  if (!selectedId.value || !assignAgentId.value || !canAssign.value) return
  assigning.value = true
  try {
    await assignSession(selectedId.value, assignAgentId.value)
    ElMessage.success('已分配坐席')
    await selectSession(selectedId.value)
    await loadSessions()
  } catch {
    ElMessage.error('分配失败')
  } finally {
    assigning.value = false
  }
}

async function onClaim() {
  if (!selectedId.value || !canClaim.value) return
  claiming.value = true
  try {
    await claimSession(selectedId.value)
    ElMessage.success('已认领')
    await selectSession(selectedId.value)
    await loadSessions()
  } catch {
    ElMessage.error('认领失败')
  } finally {
    claiming.value = false
  }
}

function insertQuickReply(content: string) {
  if (!selectedId.value) {
    ElMessage.warning('请先选择会话')
    return
  }
  const piece = (content || '').trim()
  if (!piece) return
  draftContent.value = draftContent.value
    ? `${draftContent.value.trimEnd()}\n${piece}`
    : piece
}

async function loadQuickReplies() {
  try {
    const res = await listQuickReplies()
    quickReplies.value = (res.items || []).filter((q) => q.isActive !== false).slice(0, 20)
  } catch {
    quickReplies.value = []
  }
}

async function onSave() {
  if (!selectedId.value) return
  const content = draftContent.value.trim()
  if (!content) {
    ElMessage.warning('内容不能为空')
    return
  }
  saving.value = true
  try {
    const res = await updateDraft(selectedId.value, content)
    ElMessage.success(res?.created ? '草稿已创建' : '草稿已保存')
    await selectSession(selectedId.value)
    await loadSessions()
    void loadShopOptions()
  } catch {
    ElMessage.error('保存失败')
  } finally {
    saving.value = false
  }
}

function applySendResult(res?: DraftSendResult | null, fallback = '已发送') {
  const mocked = !!res?.mocked
  const msg = res?.message || (mocked ? '已模拟发送（演示店，未调用真实平台）' : fallback)
  ElMessage.success(msg)
  lastSendHint.value = mocked
    ? '✓ 演示闭环完成：已模拟出站，时间线出现坐席消息，草稿标记为已发送。'
    : '✓ 已发送到平台，时间线已更新。'
}

/** 发送：有草稿则 approve/edit-send；无草稿则先落草稿再出站（仍 draft-first） */
async function onSend() {
  if (!selectedId.value) return
  const content = draftContent.value.trim()
  if (!content) {
    ElMessage.warning('内容不能为空')
    return
  }
  sending.value = true
  try {
    let res: DraftSendResult
    if (!draft.value) {
      res = await editAndSendDraft(selectedId.value, content)
    } else if (content !== (draft.value.content || '').trim()) {
      res = await editAndSendDraft(selectedId.value, content)
    } else {
      res = await approveDraft(selectedId.value)
    }
    applySendResult(res, '已发送')
    await selectSession(selectedId.value)
    await loadSessions()
    void loadShopOptions()
  } catch {
    ElMessage.error('发送失败')
  } finally {
    sending.value = false
  }
}

async function onInjectTest() {
  injecting.value = true
  try {
    const res = await simulateInbound({
      message: `【收件箱注入】买家咨询测试 ${new Date().toLocaleTimeString('zh-CN', { hour12: false })}`,
      customerName: '模拟买家·收件箱注入',
      platform: 'SHOPEE',
    })
    const hint = res.draft?.contentPreview
      ? `已注入，待审草稿：${res.draft.contentPreview}`
      : `已注入会话 ${res.sessionNo || res.sessionId || ''}`
    ElMessage.success(hint)
    await loadSessions()
    void loadShopOptions()
    const sid = res.sessionId
    if (sid) await selectSession(sid)
  } catch (e) {
    ElMessage.error(e instanceof Error ? e.message : '注入失败（仅 Development）')
  } finally {
    injecting.value = false
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
    void loadShopOptions()
  } catch {
    ElMessage.error('丢弃失败')
  } finally {
    discarding.value = false
  }
}

onMounted(() => {
  loadShopOptions()
  loadShopAgents()
  refreshAll()
  alertPollTimer = setInterval(() => {
    loadAlerts()
  }, 45000)
})

onUnmounted(() => {
  if (alertPollTimer) clearInterval(alertPollTimer)
})
</script>

<style scoped lang="scss">
.inbox {
  display: grid;
  grid-template-columns: 320px minmax(0, 1fr) 280px;
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
.draft-banner,
.alert-banner {
  margin: 8px 10px 0;
  padding: 10px 12px;
  border-radius: 8px;
  font-size: 12px;
  line-height: 1.45;
  flex-shrink: 0;
}
.draft-banner {
  background: #fffbeb;
  border: 1px solid #fde68a;
  color: #92400e;
}
.alert-banner {
  background: #fef2f2;
  border: 1px solid #fecaca;
  color: #991b1b;
  cursor: pointer;
  &:hover {
    background: #fee2e2;
  }
}
.filters {
  padding: 8px 10px;
  border-bottom: 1px solid var(--sx-border);
  display: flex;
  flex-direction: column;
  gap: 8px;
  .shop-filter {
    width: 100%;
  }
  :deep(.el-radio-button__inner) {
    padding: 6px 10px;
    font-size: 12px;
  }
}
.orders-block {
  margin: 0 12px 16px;
}
.sla-notify-bar {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 12px 8px;
}
.order-tags {
  display: flex;
  gap: 4px;
  align-items: center;
}
.orders-title {
  font-weight: 600;
  font-size: 13px;
  margin-bottom: 8px;
  color: #334155;
}
.order-card {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 8px;
  padding: 10px 12px;
  margin-bottom: 8px;
}
.order-row {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
  font-size: 13px;
}
.order-meta {
  margin-top: 4px;
  font-size: 12px;
  color: #64748b;
  display: flex;
  justify-content: space-between;
  gap: 8px;
}
.chip-n {
  display: inline-block;
  margin-left: 4px;
  min-width: 16px;
  padding: 0 4px;
  border-radius: 999px;
  background: #ef4444;
  color: #fff;
  font-size: 10px;
  line-height: 1.4;
  text-align: center;
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
  &.urgency-overdue {
    background: #fef2f2;
  }
  &.urgency-soon {
    background: #fff7ed;
  }
  &.urgency-overdue.active,
  &.urgency-soon.active {
    border-left-color: #dc2626;
  }
}
.row1 {
  display: flex;
  justify-content: space-between;
  align-items: center;
  gap: 8px;
}
.badges {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  justify-content: flex-end;
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
.tag-soon {
  --el-tag-text-color: #c2410c;
  --el-tag-border-color: #fdba74;
  --el-tag-bg-color: #ffedd5;
  color: #c2410c !important;
  border-color: #fdba74 !important;
  background: #ffedd5 !important;
}
.center-head {
  flex-direction: column;
  align-items: stretch;
  .head-top {
    display: flex;
    justify-content: space-between;
    align-items: flex-start;
    gap: 12px;
    width: 100%;
  }
  .head-title {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
  }
  .meta {
    display: flex;
    flex-wrap: wrap;
    gap: 12px;
    font-size: 12px;
    color: #64748b;
    margin-top: 6px;
  }
}
.handoff-hint {
  margin-top: 8px;
  font-size: 12px;
  color: #92400e;
  background: #fffbeb;
  padding: 8px 10px;
  border-radius: 6px;
  line-height: 1.45;
  width: 100%;
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
.draft-hint {
  margin: 0 0 8px;
  font-size: 12px;
  color: #64748b;
  line-height: 1.45;
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
    grid-template-columns: 280px minmax(0, 1fr);
  }
  .side-col {
    display: none;
  }
}
.qr-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
  margin-bottom: 8px;
}
.qr-label {
  font-size: 12px;
  color: #64748b;
  margin-right: 4px;
}

.assign-filter {
  margin-top: 6px;
  display: flex;
  flex-wrap: wrap;
}
.assign-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  align-items: center;
}
.assign-meta {
  margin-top: 6px;
  font-size: 12px;
  color: var(--el-text-color-secondary);
}
.assign-meta .muted,
.agent-chip.muted {
  color: var(--el-text-color-placeholder);
}
.agent-chip {
  font-size: 11px;
  color: var(--el-color-primary);
}

.logistics-track {
  margin-top: 6px;
  padding: 6px 8px;
  background: var(--el-fill-color-light, #f5f7fa);
  border-radius: 6px;
  font-size: 12px;
}
.logistics-track-title {
  font-weight: 600;
  margin-bottom: 4px;
  color: var(--el-text-color-secondary);
}
.logistics-checkpoints {
  list-style: none;
  margin: 0;
  padding: 0;
  max-height: 160px;
  overflow-y: auto;
}
.logistics-checkpoints li {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 4px 0;
  border-bottom: 1px dashed var(--el-border-color-lighter, #ebeef5);
}
.logistics-checkpoints li:last-child {
  border-bottom: none;
}
.cp-time {
  color: var(--el-text-color-secondary);
  font-size: 11px;
}
.cp-desc {
  color: var(--el-text-color-primary);
  line-height: 1.4;
}
.logistics-fallback {
  color: var(--el-color-warning);
}

.empty-actions {
  display: flex;
  justify-content: center;
  margin-top: -8px;
  padding-bottom: 12px;
}
.send-hint {
  margin: 0 12px 8px;
}
.draft-empty-hint {
  margin: 0 14px 8px;
  font-size: 12px;
  color: #64748b;
  line-height: 1.45;
}
.mock-tag {
  margin: 0 4px;
}
.sla-text-overdue {
  color: #b91c1c;
  font-weight: 600;
}
.sla-text-soon {
  color: #b45309;
  font-weight: 600;
}
.sla-text-ok {
  color: #047857;
}
.list-head-actions {
  display: flex;
  align-items: center;
  gap: 4px;
}
.shop-opt-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  width: 100%;
}
.shop-opt-badges {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  justify-content: flex-end;
}
</style>
