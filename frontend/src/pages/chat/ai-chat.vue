<template>
  <view class="chat-container">
    <scroll-view scroll-y class="message-list" :scroll-into-view="lastMsgId" scroll-with-animation>
      <view v-for="(msg, idx) in messages" :key="msg.id" :id="'msg-'+msg.id" class="message-item" :class="{ 'is-user': msg.isFromUser }">
        <view class="avatar">
          <image :src="msg.isFromUser ? userAvatar : aiAvatar" mode="aspectFill" />
        </view>
        <view class="content">
          <text class="text">{{ msg.content }}</text>
        </view>
      </view>
      <view v-if="loading" class="message-item">
        <view class="avatar"><image :src="aiAvatar" /></view>
        <view class="content"><text class="text">思考中...</text></view>
      </view>
    </scroll-view>

    <view class="input-bar">
      <input v-model="inputText" placeholder="输入消息..." class="input" @confirm="sendMessage" />
      <button type="primary" size="mini" :loading="loading" @click="sendMessage">发送</button>
    </view>
  </view>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'

interface Message {
  id: string
  isFromUser: boolean
  content: string
  timestamp: number
}

const messages = ref<Message[]>([])
const inputText = ref('')
const loading = ref(false)
const userAvatar = '/static/avatar-user.png'
const aiAvatar = '/static/avatar-ai.png'
const conversationId = ref<string>('')  // 保存当前会话ID

const lastMsgId = computed(() => {
  if (messages.value.length > 0) {
    return 'msg-' + messages.value[messages.value.length - 1].id
  }
  return ''
})

const generateId = () => Date.now().toString(36) + Math.random().toString(36).substr(2)

const sendMessage = async () => {
  const text = inputText.value.trim()
  if (!text) return
  inputText.value = ''

  // 添加用户消息（前端临时ID）
  messages.value.push({
    id: generateId(),
    isFromUser: true,
    content: text,
    timestamp: Date.now()
  })

  loading.value = true
  try {
    const token = uni.getStorageSync('token')
    const headers: UniApp.RequestOptions['header'] = { 'Content-Type': 'application/json' }
    if (token) headers.Authorization = `Bearer ${token}`

    // 构造请求体：conversationId 可为空字符串（新建会话）
    const payload: any = { message: text }
    if (conversationId.value) {
      payload.conversationId = conversationId.value
    }

    const apiRes = await uni.request<any>({
      url: 'http://localhost:7092/api/chat/send',
      method: 'POST',
      header: headers,
      data: payload
    })
    if (apiRes.statusCode === 200) {
      const data = apiRes.data
      // 保存/更新会话ID
      if (data.conversationId && (!conversationId.value || conversationId.value !== data.conversationId)) {
        conversationId.value = data.conversationId
        uni.setStorageSync('conversationId', data.conversationId)
      }
      // 添加AI回复
      messages.value.push({
        id: data.messageId || generateId(),
        isFromUser: false,
        content: data.content,
        timestamp: Date.now()
      })
    } else {
      throw new Error(data?.message || '发送失败')
    }
  } catch (err: any) {
    uni.showToast({ title: err.message || '请求失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.chat-container {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #f5f5f5;
}
.message-list {
  flex: 1;
  padding: 20rpx;
}
.message-item {
  display: flex;
  margin-bottom: 30rpx;
  align-items: flex-start;
}
.message-item.is-user {
  flex-direction: row-reverse;
}
.avatar {
  width: 80rpx;
  height: 80rpx;
  border-radius: 50%;
  overflow: hidden;
  background: #ddd;
}
.avatar image {
  width: 100%;
  height: 100%;
}
.content {
  max-width: 70%;
  margin: 0 20rpx;
  padding: 20rpx 24rpx;
  border-radius: 16rpx;
  background: #fff;
}
.message-item.is-user .content {
  background: #95ec69;
}
.text {
  font-size: 30rpx;
  line-height: 1.5;
  word-wrap: break-word;
}
.input-bar {
  display: flex;
  padding: 20rpx;
  background: #fff;
  border-top: 1px solid #e0e0e0;
}
.input {
  flex: 1;
  padding: 16rpx 24rpx;
  border: 1px solid #ddd;
  border-radius: 8rpx;
  margin-right: 20rpx;
  font-size: 30rpx;
}
</style>
