<template>
  <view class="list-page">
    <view class="header">
      <text>我的会话</text>
      <button class="new-btn" @tap="createNew">+ 新建</button>
    </view>

    <scroll-view scroll-y class="list">
      <view v-for="conv in conversations" :key="conv.id" class="conv-item" @tap="openConversation(conv.id)">
        <view class="title">{{ conv.title || '新对话' }}</view>
        <view class="last-msg">{{ conv.lastMessage || '暂无消息' }}</view>
        <view class="time">{{ conv.lastActiveAt || '刚刚' }}</view>
      </view>

      <view v-if="!conversations.length" class="empty">
        <text>暂无会话，快去创建一个吧～</text>
      </view>
    </scroll-view>
  </view>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

const conversations = ref<any[]>([])
const BASE_URL = 'http://192.168.1.254:7092'  // ← 替换成你的后端地址

onMounted(async () => {
  await loadConversations()
})

const loadConversations = async () => {
  const token = uni.getStorageSync('token')
  if (!token) {
    uni.showToast({ title: '请先登录', icon: 'error' })
    uni.reLaunch({ url: '/pages/login/index' })
    return
  }

  const res = await uni.request({
    url: `${BASE_URL}/api/chat/conversations`,
    header: { 'Authorization': `Bearer ${token}` }
  }) as any

  if (res.statusCode === 200 && res.data.items) {
    conversations.value = res.data.items
  } else {
    uni.showToast({ title: res.data?.message || '加载会话失败', icon: 'none' })
  }
}

const openConversation = (id: string) => {
  uni.setStorageSync('currentConversationId', id)
  uni.navigateTo({ url: '/pages/chat/chat' })  // 跳转到 chat.vue
}

const createNew = () => {
  uni.removeStorageSync('currentConversationId')
  uni.navigateTo({ url: '/pages/chat/index' })
}
</script>

<style>
.list-page {
  height: 100vh;
  display: flex;
  flex-direction: column;
}

.header {
  padding: 30rpx;
  background: #07c160;
  color: white;
  font-size: 40rpx;
  text-align: center;
  position: relative;
}

.new-btn {
  position: absolute;
  right: 30rpx;
  top: 30rpx;
  width: 100rpx;
  height: 60rpx;
  line-height: 60rpx;
  font-size: 28rpx;
  background: white;
  color: #07c160;
  border-radius: 30rpx;
}

.list {
  flex: 1;
  padding: 20rpx;
}

.conv-item {
  background: white;
  border-radius: 16rpx;
  padding: 30rpx;
  margin-bottom: 20rpx;
  box-shadow: 0 4rpx 12rpx rgba(0,0,0,0.08);
}

.title {
  font-size: 36rpx;
  font-weight: bold;
  margin-bottom: 10rpx;
}

.last-msg {
  font-size: 28rpx;
  color: #666;
  margin-bottom: 10rpx;
}

.time {
  font-size: 24rpx;
  color: #999;
  text-align: right;
}

.empty {
  text-align: center;
  padding: 300rpx 0;
  color: #999;
  font-size: 32rpx;
}
</style>