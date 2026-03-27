<template>
  <view class="merchant-sessions">
    <view class="header">
      <text class="title">{{ t('merchant.sessions') }}</text>
      <button class="new-btn" @tap="createNew">+ {{ currentLang === 'zh-CN' ? '转人工' : 'Transfer' }}</button>
    </view>

    <scroll-view scroll-y class="list">
      <view v-for="conv in sessions" :key="conv.id" class="conv-item" @tap="viewSession(conv.id)">
        <view class="title-row">
          <text class="customer">{{ conv.customerName || (currentLang === 'zh-CN' ? '匿名客户' : 'Anonymous') }}</text>
          <text class="platform">{{ conv.platform }}</text>
        </view>
        <view class="info-row">
          <text class="status" :class="conv.status">{{ getStatusLabel(conv.status) }}</text>
          <text class="agent" v-if="conv.assignedAgent">{{ conv.assignedAgent.name }}</text>
          <text class="agent" v-else>{{ currentLang === 'zh-CN' ? '未分配' : 'Unassigned' }}</text>
        </view>
        <view class="time">{{ formatTime(conv.lastActiveAt) }}</view>
      </view>

      <view v-if="!sessions.length" class="empty">
        <text>{{ currentLang === 'zh-CN' ? '暂无会话' : 'No sessions' }}</text>
      </view>
    </scroll-view>
  </view>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { getLanguage, t } from '@/utils/i18n'

const sessions = ref([])
const BASE_URL = 'http://192.168.1.254:7092'
const currentLang = getLanguage()

onMounted(() => {
  loadSessions()
})

const loadSessions = async () => {
  const token = uni.getStorageSync('token')
  if (!token) {
    uni.showToast({ title: currentLang === 'zh-CN' ? '请先登录' : 'Please login', icon: 'error' })
    uni.reLaunch({ url: '/pages/login/login' })
    return
  }

  try {
    const res = await uni.request({
      url: `${BASE_URL}/api/merchant/sessions`,
      header: { Authorization: `Bearer ${token}` }
    })

    if (res.statusCode === 200 && res.data.items) {
      sessions.value = res.data.items
    } else {
      uni.showToast({ title: currentLang === 'zh-CN' ? '加载失败' : 'Load failed', icon: 'none' })
    }
  } catch (e) {
    uni.showToast({ title: currentLang === 'zh-CN' ? '网络错误' : 'Network error', icon: 'none' })
  }
}

const viewSession = (id) => {
  uni.navigateTo({ url: `/pages/merchant/session-detail?id=${id}` })
}

const createNew = () => {
  uni.showToast({ 
    title: currentLang === 'zh-CN' 
      ? '请点击会话列表中的会话进行转人工' 
      : 'Please click on a session to transfer', 
    icon: 'none' 
  })
}

const formatTime = (dateStr) => {
  if (!dateStr) return currentLang === 'zh-CN' ? '刚刚' : 'Just now'
  const d = new Date(dateStr)
  return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`
}

const getStatusLabel = (status) => {
  const zhMap = { Pending: '待处理', Active: '进行中', Resolved: '已解决', Closed: '已关闭' }
  const enMap = { Pending: 'Pending', Active: 'Active', Resolved: 'Resolved', Closed: 'Closed' }
  const map = currentLang === 'zh-CN' ? zhMap : enMap
  return map[status] || status
}
</script>

<style scoped>
.merchant-sessions {
  height: 100vh;
  display: flex;
  flex-direction: column;
  background: #f5f5f5;
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
  width: 120rpx;
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
.title-row {
  display: flex;
  justify-content: space-between;
  margin-bottom: 10rpx;
}
.customer {
  font-size: 36rpx;
  font-weight: bold;
  color: #333;
}
.platform {
  font-size: 24rpx;
  color: #999;
  background: #f0f0f0;
  padding: 4rpx 12rpx;
  border-radius: 8rpx;
}
.info-row {
  display: flex;
  justify-content: space-between;
  margin-bottom: 10rpx;
}
.status {
  font-size: 28rpx;
  padding: 4rpx 12rpx;
  border-radius: 8rpx;
}
.status.Pending { background: #fff3e0; color: #f57c00; }
.status.Active { background: #e3f2fd; color: #1976d2; }
.status.Resolved { background: #e8f5e9; color: #388e3c; }
.status.Closed { background: #f5f5f5; color: #999; }
.agent {
  font-size: 26rpx;
  color: #666;
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
