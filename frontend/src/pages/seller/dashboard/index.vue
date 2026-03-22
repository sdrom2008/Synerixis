<template>
    <view class="dashboard">
        <view v-if="loading" class="loading">加载中...</view>
        <view v-else-if="error" class="error">{{ error }}</view>
        <view v-else>
            <view class="header">
                <image v-if="seller.avatarUrl" :src="seller.avatarUrl" class="avatar" mode="aspectFill" />
                <view class="welcome">
                    <text class="title">欢迎, {{ seller.nickname || '商家' }}</text>
                    <text class="subtitle">{{ seller.phone || '未绑定手机' }}</text>
                </view>
            </view>

            <view class="stats-grid">
                <view class="stat-card">
                    <text class="stat-value">{{ stats.totalAgents }}</text>
                    <text class="stat-label">客服总数</text>
                </view>
                <view class="stat-card">
                    <text class="stat-value">{{ stats.onlineAgents }}</text>
                    <text class="stat-label">在线客服</text>
                </view>
                <view class="stat-card">
                    <text class="stat-value">{{ seller.freeQuota || 0 }}</text>
                    <text class="stat-label">免费额度</text>
                </view>
                <view class="stat-card">
                    <text class="stat-value">{{ subscriptionText }}</text>
                    <text class="stat-label">订阅状态</text>
                </view>
            </view>

            <view class="menu-list">
                <navigator url="/pages/products/products" class="menu-item">
                    <text class="icon">📦</text>
                    <text class="label">商品管理</text>
                    <text class="arrow">></text>
                </navigator>
                <navigator url="/pages/marketing/generate" class="menu-item">
                    <text class="icon">✍️</text>
                    <text class="label">营销文案生成</text>
                    <text class="arrow">></text>
                </navigator>
                <navigator url="/pages/product/optimize" class="menu-item">
                    <text class="icon">🔍</text>
                    <text class="label">商品优化</text>
                    <text class="arrow">></text>
                </navigator>
                <navigator url="/pages/competitor/analyze" class="menu-item">
                    <text class="icon">📊</text>
                    <text class="label">竞品分析</text>
                    <text class="arrow">></text>
                </navigator>
            </view>

            <button class="logout-btn" @click="logout">退出登录</button>
        </view>
    </view>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { BASE_URL, getToken } from '@/utils/config'

interface SellerProfile {
    id: string
    nickname: string
    avatarUrl: string
    phone: string
    freeQuota: number
    subscriptionLevel: number
    subscriptionEnd: string
    TeamStats: {
        TotalAgents: number
        OnlineAgents: number
        ActiveAgents: number
    }
}

const loading = ref(true)
const error = ref('')
const seller = ref<SellerProfile>({
    id: '', nickname: '', avatarUrl: '', phone: '',
    freeQuota: 0, subscriptionLevel: 0, subscriptionEnd: '',
    TeamStats: { TotalAgents: 0, OnlineAgents: 0, ActiveAgents: 0 }
})

const stats = computed(() => seller.value.TeamStats)

const subscriptionText = computed(() => {
    if (seller.value.subscriptionLevel === 0) return '免费版'
    if (seller.value.subscriptionEnd) {
        const end = new Date(seller.value.subscriptionEnd)
        return `付费至 ${end.toLocaleDateString()}`
    }
    return '付费版'
})

const fetchProfile = async () => {
    loading.value = true
    error.value = ''
    try {
        const token = getToken()
        if (!token) {
            uni.reLaunch({ url: '/pages/login/index' })
            return
        }

        const res = await uni.request({
            url: `${BASE_URL}/api/seller/profile`,
            method: 'GET',
            header: { Authorization: `Bearer ${token}` }
        })

        if (res.statusCode === 200) {
            seller.value = res.data as SellerProfile
        } else if (res.statusCode === 401) {
            uni.removeStorageSync('token')
            uni.removeStorageSync('sellerId')
            uni.reLaunch({ url: '/pages/login/index' })
        } else {
            error.value = res.data || '加载失败'
        }
    } catch (e: any) {
        error.value = e.errMsg || '网络错误'
    } finally {
        loading.value = false
    }
}

const logout = () => {
    uni.removeStorageSync('token')
    uni.removeStorageSync('sellerId')
    uni.reLaunch({ url: '/pages/login/index' })
}

onMounted(() => {
    fetchProfile()
})
</script>

<style scoped>
.dashboard {
    padding: 30rpx;
    background: #f5f5f5;
    min-height: 100vh;
}

.loading, .error {
    text-align: center;
    padding: 100rpx 0;
    color: #999;
}

.header {
    display: flex;
    align-items: center;
    background: white;
    padding: 40rpx;
    border-radius: 20rpx;
    margin-bottom: 30rpx;
    box-shadow: 0 4rpx 12rpx rgba(0,0,0,0.05);
}

.avatar {
    width: 120rpx;
    height: 120rpx;
    border-radius: 60rpx;
    margin-right: 30rpx;
    background: #eee;
}

.welcome {
    display: flex;
    flex-direction: column;
}

.title {
    font-size: 40rpx;
    font-weight: bold;
    color: #333;
    margin-bottom: 10rpx;
}

.subtitle {
    font-size: 28rpx;
    color: #666;
}

.stats-grid {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 20rpx;
    margin-bottom: 30rpx;
}

.stat-card {
    background: white;
    padding: 30rpx;
    border-radius: 16rpx;
    text-align: center;
    box-shadow: 0 2rpx 8rpx rgba(0,0,0,0.05);
}

.stat-value {
    display: block;
    font-size: 48rpx;
    font-weight: bold;
    color: #1890ff;
    margin-bottom: 8rpx;
}

.stat-label {
    display: block;
    font-size: 26rpx;
    color: #999;
}

.menu-list {
    background: white;
    border-radius: 16rpx;
    overflow: hidden;
    margin-bottom: 30rpx;
}

.menu-item {
    display: flex;
    align-items: center;
    padding: 32rpx 30rpx;
    border-bottom: 1rpx solid #f0f0f0;
}

.menu-item:last-child {
    border-bottom: none;
}

.icon {
    font-size: 48rpx;
    margin-right: 20rpx;
}

.label {
    flex: 1;
    font-size: 32rpx;
    color: #333;
}

.arrow {
    font-size: 32rpx;
    color: #ccc;
}

.logout-btn {
    width: 100%;
    background: #ff4d4f;
    color: white;
    border: none;
    border-radius: 12rpx;
    font-size: 32rpx;
    margin-top: 20rpx;
}
</style>