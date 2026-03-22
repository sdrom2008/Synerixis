<template>
  <view class="workbench">
    <!-- 左侧：会话列表 -->
    <view class="sidebar-left">
      <view class="header">
        <text class="title">会话列表</text>
        <text class="count">{{ tickets.length }}</text>
      </view>
      <scroll-view scroll-y class="ticket-list">
        <view 
          v-for="ticket in tickets" 
          :key="ticket.sessionId" 
          class="ticket-item" 
          :class="{ active: currentTicket?.sessionId === ticket.sessionId }"
          @click="selectTicket(ticket)"
        >
          <view class="customer-info">
            <text class="customer-name">{{ ticket.customerName || '未知' }}</text>
            <text class="platform">{{ ticket.platform }}</text>
          </view>
          <view class="info-row">
            <text class="status" :class="ticket.status">{{ ticket.status }}</text>
            <text class="agent" v-if="ticket.assignedAgent">{{ ticket.assignedAgent.name }}</text>
            <text class="agent" v-else>未分配</text>
          </view>
          <view class="time">{{ formatTime(ticket.createdAt) }}</view>
        </view>
      </scroll-view>
    </view>

    <!-- 中间：聊天窗口 -->
    <view class="chat-panel">
      <view v-if="!currentTicket" class="empty-state">
        <text>选择一个会话开始聊天</text>
      </view>
      <template v-else>
        <view class="chat-header">
          <text class="customer-name">{{ currentTicket.customerName }}</text>
          <text class="session-id">{{ currentTicket.sessionId }}</text>
          <button v-if="currentTicket.status === 'Pending'" class="take-btn" @click="takeTicket">接管</button>
        </view>
        <scroll-view scroll-y class="messages" :scroll-into-view="lastMessageId">
          <view 
            v-for="msg in messages" 
            :key="msg.id" 
            :id="'msg-'+msg.id"
            class="message"
            :class="{ 'from-user': msg.senderType === 'Customer', 'from-agent': msg.senderType === 'Agent' }"
          >
            <view class="bubble">
              <text>{{ msg.content }}</text>
            </view>
          </view>
        </scroll-view>
        <view class="input-bar">
          <input 
            v-model="newMessage" 
            class="input" 
            placeholder="输入回复..." 
            confirm-type="send"
            @confirm="sendMessage"
          />
          <button class="send-btn" @click="sendMessage" size="mini" :loading="sending">发送</button>
        </view>
      </template>
    </view>
  </view>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { request } from '@/utils/request.js'

const tickets = ref([])
const currentTicket = ref(null)
const messages = ref([])
const newMessage = ref('')
const sending = ref(false)
const lastMessageId = ref('')
const BASE_URL = 'http://192.168.1.254:7092'

onMounted(() => {
  loadTickets()
})

const loadTickets = async () => {
  const token = uni.getStorageSync('token')
  try {
    const res = await uni.request({
      url: `${BASE_URL}/api/support/tickets`,
      header: { Authorization: `Bearer ${token}` }
    })
    if (res.statusCode === 200 && res.data.items) {
      tickets.value = res.data.items
    } else {
      uni.showToast({ title: '加载失败', icon: 'none' })
    }
  } catch (e) {
    uni.showToast({ title: '网络错误', icon: 'none' })
  }
}

const selectTicket = async (ticket: any) => {
  currentTicket.value = ticket
  messages.value = []
  await loadMessages(ticket.sessionId)
}

const loadMessages = async (sessionId: string) => {
  const token = uni.getStorageSync('token')
  try {
    const res = await uni.request({
      url: `${BASE_URL}/api/support/tickets/${sessionId}/messages`,
      header: { Authorization: `Bearer ${token}` }
    })
    if (res.statusCode === 200) {
      messages.value = res.data
      scrollToBottom()
    } else {
      uni.showToast({ title: '加载消息失败', icon: 'none' })
    }
  } catch (e) {
    uni.showToast({ title: '网络错误', icon: 'none' })
  }
}

const takeTicket = async () => {
  if (!currentTicket.value) return
  const token = uni.getStorageSync('token')
  try {
    const res = await uni.request({
      url: `${BASE_URL}/api/support/tickets/${currentTicket.value.sessionId}/take`,
      method: 'POST',
      header: { Authorization: `Bearer ${token}` }
    })
    if (res.statusCode === 200) {
      uni.showToast({ title: '已接管', icon: 'success' })
      currentTicket.value.status = 'Active'
      await loadTickets()
    } else {
      uni.showToast({ title: res.data?.message || '接管失败', icon: 'none' })
    }
  } catch (e) {
    uni.showToast({ title: '网络错误', icon: 'none' })
  }
}

const sendMessage = async () => {
  if (!newMessage.value.trim() || !currentTicket.value) return
  sending.value = true
  const token = uni.getStorageSync('token')
  try {
    const res = await uni.request({
      url: `${BASE_URL}/api/support/tickets/${currentTicket.value.sessionId}/reply`,
      method: 'POST',
      header: { Authorization: `Bearer ${token}` },
      data: { content: newMessage.value }
    })
    if (res.statusCode === 200) {
      newMessage.value = ''
      await loadMessages(currentTicket.value.sessionId)
    } else {
      uni.showToast({ title: res.data?.message || '发送失败', icon: 'none' })
    }
  } catch (e) {
    uni.showToast({ title: '网络错误', icon: 'none' })
  } finally {
    sending.value = false
  }
}

const scrollToBottom = () => {
  if (messages.value.length > 0) {
    lastMessageId.value = 'msg-' + messages.value[messages.value.length - 1].id
  }
}

const formatTime = (dateStr) => {
  if (!dateStr) return ''
  const d = new Date(dateStr)
  return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`
}
</script>

<style scoped>
.workbench {
  display: flex;
  height: 100vh;
  background: #f5f5f5;
}
.sidebar-left {
  width: 280rpx;
  background: white;
  border-right: 1rpx solid #e8e8e8;
  display: flex;
  flex-direction: column;
}
.header {
  padding: 30rpx;
  border-bottom: 1rpx solid #f0f0f0;
  display: flex;
  justify-content: space-between;
  align-items: center;
}
.title {
  font-size: 32rpx;
  font-weight: bold;
  color: #333;
}
.count {
  background: #ff5722;
  color: white;
  border-radius: 50%;
  width: 40rpx;
  height: 40rpx;
  font-size: 24rpx;
  line-height: 40rpx;
  text-align: center;
}
.ticket-list {
  flex: 1;
}
.ticket-item {
  padding: 30rpx;
  border-bottom: 1rpx solid #f0f0f0;
}
.ticket-item.active {
  background: #e3f2fd;
}
.customer-info {
  display: flex;
  justify-content: space-between;
  margin-bottom: 10rpx;
}
.customer-name {
  font-size: 32rpx;
  font-weight: bold;
}
.platform {
  font-size: 22rpx;
  color: #999;
  background: #f0f0f0;
  padding: 4rpx 12rpx;
  border-radius: 8rpx;
}
.info-row {
  display: flex;
  justify-content: space-between;
  margin-bottom: 6rpx;
}
.status {
  font-size: 24rpx;
  padding: 2rpx 8rpx;
  border-radius: 4rpx;
}
.status.Pending { color: #f57c00; background: #fff3e0; }
.status.Active { color: #1976d2; background: #e3f2fd; }
.status.Resolved { color: #388e3c; background: #e8f5e9; }
.status.Closed { color: #999; background: #f5f5f5; }
.agent {
  font-size: 24rpx;
  color: #666;
}
.time {
  font-size: 22rpx;
  color: #999;
  text-align: right;
}
.chat-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #fafafa;
}
.empty-state {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #999;
}
.chat-header {
  padding: 20rpx 30rpx;
  background: white;
  border-bottom: 1rpx solid #e8e8e8;
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.customer-name {
  font-size: 36rpx;
  font-weight: bold;
}
.session-id {
  font-size: 24rpx;
  color: #999;
  margin-left: 20rpx;
}
.take-btn {
  background: #ff5722;
  color: white;
  font-size: 24rpx;
  padding: 10rpx 24rpx;
  border-radius: 30rpx;
}
.messages {
  flex: 1;
  padding: 20rpx;
}
.message {
  margin: 20rpx 0;
  display: flex;
}
.from-user {
  justify-content: flex-start;
}
.from-agent {
  justify-content: flex-end;
}
.bubble {
  max-width: 70%;
  padding: 20rpx 24rpx;
  border-radius: 20rpx;
  background: white;
  box-shadow: 0 2rpx 8rpx rgba(0,0,0,0.1);
}
.from-user .bubble {
  background: #fff;
}
.from-agent .bubble {
  background: #95ec69;
}
.input-bar {
  padding: 20rpx;
  background: white;
  border-top: 1rpx solid #eee;
  display: flex;
}
.input {
  flex: 1;
  border: 1rpx solid #ddd;
  border-radius: 40rpx;
  padding: 16rpx 24rpx;
  margin-right: 20rpx;
}
.send-btn {
  background: #07c160;
  color: white;
  border-radius: 40rpx;
}
</style>
