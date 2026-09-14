<template>
  <div class="inbox" v-loading="listLoading">
    <!-- Col 1: session list -->
    <aside class="col list-col">
      <div class="list-head">
        <div class="list-title">
          <span>会话</span>
          <el-badge v-if="pendingDraftCount > 0" :value="pendingDraftCount" type="warning" />
        </div>
        <el-button text :icon="Refresh" :loading="listLoading" @click="refreshAll">刷新</el-button>
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
          />
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
      </div>

      <div v-if="displaySessions.length === 0 && !listLoading" class="list-empty">
        <EmptyState title="暂无会话" desc="绑定店铺并有买家消息后，会话会出现在这里。AI 会生成草稿，需人工确认后才会发到平台。" />
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
          desc="左侧选择买家会话后，可查看消息并审发 AI 草稿。"
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
          <div v-if="messagesMeta.pendingHumanHandoff" class="handoff-hint">
            已转人工：入站消息不再生成新 AI 草稿，也不 AutoSend；下方旧草稿仍可编辑后手动发送。
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
              <span>{{ formatTime(m.createdAt) }}</span>
            </div>
            <div class="bubble-body">{{ m.content }}</div>
          </div>
        </div>

        <div class="draft-panel">
          <div class="draft-head">
            <strong>AI 草稿（未发到平台）</strong>
            <el-tag v-if="draft && !isSupersededDraft" size="small" type="warning" effect="plain">待发送</el-tag>
            <el-tag v-else-if="isSupersededDraft" size="small" type="info" effect="plain">已停用但仍可发送</el-tag>
            <el-tag v-else size="small" type="info" effect="plain">无草稿</el-tag>
          </div>
          <p class="draft-hint">
            确认无误后再发送。自动生成的草稿不计入平台「真人坐席响应率」；请勿宣称无人值守自动回信。
          </p>
          <div v-if="quickReplies.length" class="qr-bar">
            <span class="qr-label">快捷回复</span>
            <el-button
              v-for="qr in quickReplies"
              :key="qr.id"
              size="small"
              :disabled="!draft"
              @click="insertQuickReply(qr.content)"
            >
              {{ qr.title }}
            </el-button>
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
            {{ slaLabel(currentSession) || '正常' }}
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
              <div v-if="o.logisticsNo" class="order-meta">
                {{ o.logisticsCompany || '物流' }} {{ o.logisticsNo }}
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
  transferSession,
  updateDraft,
  listQuickReplies,
  type QuickReplyItem,
  type MessageItem,
  type SessionItem,
  type SessionOrderItem,
  type ShopOption,
  type DraftInfo,
  type SlaUrgency,
} from '@/api/merchant'

const listLoading = ref(false)
const detailLoading = ref(false)
const saving = ref(false)
const sending = ref(false)
const discarding = ref(false)
const transferring = ref(false)
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
const quickReplies = ref<QuickReplyItem[]>([])
const timelineEl = ref<HTMLElement | null>(null)
const messagesMeta = ref<{
  sessionStatus?: string
  pendingHumanHandoff?: boolean
  handoffAt?: string | null
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

const canTransfer = computed(() => {
  if (messagesMeta.value.pendingHumanHandoff) return false
  const st = messagesMeta.value.sessionStatus || currentSession.value?.status
  return st === 'Pending' || st === 'Active'
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

function shopOptionLabel(opt: ShopOption) {
  const nick = opt.nickname || opt.shopId || '店铺'
  return `${opt.platform || '—'} · ${nick}`
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
      hoursSinceLastBuyerMsg: res.hoursSinceLastBuyerMsg,
      needsResponseBy: res.needsResponseBy,
      responseSlaHours: res.responseSlaHours,
      slaUrgency: res.slaUrgency,
    }
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

function insertQuickReply(content: string) {
  if (!draft.value) {
    ElMessage.warning('请先选中有草稿的会话')
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

onMounted(() => {
  loadShopOptions()
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
</style>
