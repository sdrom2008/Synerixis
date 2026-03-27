<template>
  <view class="session-detail">
    <scroll-view scroll-y class="msg-list" :scroll-into-view="lastMsgId">
      <view v-for="msg in messages" :key="msg.id" class="msg" :class="msg.senderType">
        <view class="bubble">{{ msg.content }}</view>
      </view>
    </scroll-view>

    <view class="action-bar" v-if="sessionStatus === 'Pending' || sessionStatus === 'Active'">
      <button class="transfer-btn" @tap="transferToAgent">转人工客服</button>
    </view>
  </view>
</template>

<script setup>
import { ref, onMounted } from 'vue'

const messages = ref([])
const sessionId = ref('')
const sessionStatus = ref('')
const lastMsgId = ref('')
import { BASE_URL } from '@/utils/config.js';

onMounted(() => {
  const pages = getCurrentPages()
  const currentPage = pages[pages.length - 1]
  sessionId.value = currentPage.options.id
  if (sessionId.value) {
    loadMessages()
  }
})

const loadMessages = async () => {
  const token = uni.getStorageSync('token')
  try {
    const res = await uni.request({
      url: `${BASE_URL}/api/merchant/sessions/${sessionId.value}/messages`,
      header: { Authorization: `Bearer ${token}` }
    })
    if (res.statusCode === 200) {
      messages.value = res.data
      sessionStatus.value = res.data[0]?.sessionStatus || ''
      scrollToBottom()
    } else {
      uni.showToast({ title: '加载失败', icon: 'none' })
    }
  } catch (e) {
    uni.showToast({ title: '网络错误', icon: 'none' })
  }
}

const transferToAgent = async () => {
  const token = uni.getStorageSync('token')
  try {
    const res = await uni.request({
      url: `${BASE_URL}/api/merchant/sessions/${sessionId.value}/transfer`,
      method: 'POST',
      header: { Authorization: `Bearer ${token}` }
    })
    if (res.statusCode === 200) {
      uni.showToast({ title: '已转人工', icon: 'success' })
      sessionStatus.value = 'Pending'
    } else {
      uni.showToast({ title: res.data?.message || '操作失败', icon: 'none' })
    }
  } catch (e) {
    uni.showToast({ title: '网络错误', icon: 'none' })
  }
}

const scrollToBottom = () => {
  if (messages.value.length > 0) {
    lastMsgId.value = 'msg-' + messages.value[messages.value.length - 1].id
  }
}
</script>

<style scoped>
.session-detail {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #f5f5f5;
}
.msg-list {
  flex: 1;
  padding: 20rpx;
}
.msg {
  margin: 20rpx 0;
  display: flex;
}
.msg.Customer {
  justify-content: flex-start;
}
.msg.Agent, .msg.System {
  justify-content: flex-end;
}
.bubble {
  max-width: 70%;
  padding: 20rpx 24rpx;
  border-radius: 20rpx;
  background: white;
  box-shadow: 0 2rpx 8rpx rgba(0,0,0,0.1);
}
.msg.Customer .bubble {
  background: #fff;
}
.msg.Agent .bubble {
  background: #95ec69;
}
.action-bar {
  padding: 20rpx;
  background: white;
  border-top: 1rpx solid #eee;
}
.transfer-btn {
  background: #ff9800;
  color: white;
  border-radius: 40rpx;
}
</style>
