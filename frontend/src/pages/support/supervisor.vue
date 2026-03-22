<template>
    <view class="supervisor">
        <!-- 顶部统计卡片 -->
        <view class="stats-overview">
            <view class="stat-card">
                <text class="label">总会话数</text>
                <text class="value">{{ dashboard.totalSessions }}</text>
            </view>
            <view class="stat-card">
                <text class="label">待处理</text>
                <text class="value pending">{{ dashboard.pendingSessions }}</text>
            </view>
            <view class="stat-card">
                <text class="label">进行中</text>
                <text class="value active">{{ dashboard.activeSessions }}</text>
            </view>
            <view class="stat-card">
                <text class="label">已解决</text>
                <text class="value resolved">{{ dashboard.resolvedSessions }}</text>
            </view>
        </view>

        <view class="main-content">
            <!-- 左侧：客服绩效列表 -->
            <view class="agent-stats-panel">
                <view class="panel-header">
                    <text class="title">客服绩效</text>
                    <text class="subtitle">总计: {{ agentStats.length }} 位客服</text>
                </view>
                <scroll-view scroll-y class="stats-list">
                    <view v-for="agent in agentStats" :key="agent.agentId" class="agent-card">
                        <view class="agent-header">
                            <text class="agent-name">{{ agent.agentName }}</text>
                            <text class="status-tag" :class="{ online: agent.online }">在线</text>
                        </view>
                        <view class="metrics">
                            <view class="metric">
                                <text class="label">总会话</text>
                                <text class="value">{{ agent.totalSessions }}</text>
                            </view>
                            <view class="metric">
                                <text class="label">解决数</text>
                                <text class="value">{{ agent.resolvedSessions }}</text>
                            </view>
                            <view class="metric">
                                <text class="label">平均响应</text>
                                <text class="value">{{ formatSeconds(agent.avgResponseTimeSeconds) }}</text>
                            </view>
                            <view class="metric">
                                <text class="label">解决时长</text>
                                <text class="value">{{ formatMinutes(agent.avgResolutionTimeMinutes) }}</text>
                            </view>
                        </view>
                    </view>
                </scroll-view>
            </view>

            <!-- 右侧：系统指标详情 -->
            <view class="system-metrics-panel">
                <view class="panel-header">
                    <text class="title">系统指标</text>
                </view>
                <scroll-view scroll-y class="metrics-detail">
                    <view class="metric-group">
                        <text class="group-title">客服状态</text>
                        <view class="metric-row">
                            <text class="label">总客服数</text>
                            <text class="value">{{ dashboard.totalAgents }}</text>
                        </view>
                        <view class="metric-row">
                            <text class="label">在线客服</text>
                            <text class="value online">{{ dashboard.onlineAgents }}</text>
                        </view>
                    </view>

                    <view class="metric-group">
                        <text class="group-title">会话效率</text>
                        <view class="metric-row">
                            <text class="label">平均响应时间</text>
                            <text class="value">{{ formatSeconds(dashboard.avgResponseTimeSeconds) }}</text>
                        </view>
                        <view class="metric-row">
                            <text class="label">平均解决时长</text>
                            <text class="value">{{ formatMinutes(dashboard.avgResolutionTimeMinutes) }}</text>
                        </view>
                        <view class="metric-row">
                            <text class="label">满意度</text>
                            <text class="value">{{ dashboard.overallSatisfaction?.toFixed(1) }}</text>
                        </view>
                    </view>

                    <view class="metric-group">
                        <text class="group-title">更新时间</text>
                        <view class="metric-row">
                            <text class="value">{{ formatDateTime(dashboard.lastUpdated) }}</text>
                        </view>
                    </view>
                </scroll-view>
            </view>
        </view>
    </view>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { request } from '@/utils/request.js';

const dashboard = ref({
    totalSessions: 0,
    pendingSessions: 0,
    activeSessions: 0,
    resolvedSessions: 0,
    totalAgents: 0,
    onlineAgents: 0,
    avgResponseTimeSeconds: 0,
    avgResolutionTimeMinutes: 0,
    overallSatisfaction: 0,
    lastUpdated: null
});

const agentStats = ref([]);

onMounted(async () => {
    const role = uni.getStorageSync('role');
    if (role !== 'Supervisor' && role !== 'Admin') {
        uni.showToast({ title: '无访问权限', icon: 'none' });
        setTimeout(() => uni.switchTab({ url: '/pages/dashboard/dashboard' }), 1500);
        return;
    }

    await loadDashboard();
    await loadAgentStats();
});

async function loadDashboard() {
    try {
        const data = await request({ url: '/api/support/dashboard' });
        dashboard.value = {
            totalSessions: data.totalSessions || 0,
            pendingSessions: data.pendingSessions || 0,
            activeSessions: data.activeSessions || 0,
            resolvedSessions: data.resolvedSessions || 0,
            totalAgents: data.totalAgents || 0,
            onlineAgents: data.onlineAgents || 0,
            avgResponseTimeSeconds: data.avgResponseTimeSeconds || 0,
            avgResolutionTimeMinutes: data.avgResolutionTimeMinutes || 0,
            overallSatisfaction: data.overallSatisfaction || 0,
            lastUpdated: data.lastUpdated || new Date()
        };
    } catch (error) {
        // Handled in request.js
    }
}

async function loadAgentStats() {
    try {
        const data = await request({ url: '/api/support/agents/stats' });
        agentStats.value = data.map(agent => ({
            agentId: agent.id,
            agentName: agent.name,
            online: agent.isOnline,
            totalSessions: agent.totalSessions || 0,
            resolvedSessions: agent.resolvedSessions || 0,
            avgResponseTimeSeconds: agent.avgResponseTimeSeconds || 0,
            avgResolutionTimeMinutes: agent.avgResolutionTimeMinutes || 0
        }));
    } catch (error) {
        // Handled in request.js
    }
}

const formatSeconds = (seconds) => {
    if (!seconds) return '0s';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    return m > 0 ? `${m}m ${s}s` : `${s}s`;
};

const formatMinutes = (minutes) => {
    if (!minutes) return '0m';
    return `${minutes.toFixed(1)}m`;
};

const formatDateTime = (date) => {
    if (!date) return '';
    const d = new Date(date);
    return `${d.getFullYear()}-${d.getMonth()+1}-${d.getDate()} ${d.getHours()}:${d.getMinutes().toString().padStart(2, '0')}`;
};
</script>

<style scoped>
.supervisor {
    display: flex;
    flex-direction: column;
    height: 100vh;
    background: #f5f5f5;
}

.stats-overview {
    display: flex;
    padding: 20rpx;
    gap: 20rpx;
    background: white;
    border-bottom: 1rpx solid #e8e8e8;
}

.stat-card {
    flex: 1;
    display: flex;
    flex-direction: column;
    align-items: center;
    padding: 20rpx;
    border-radius: 12rpx;
    background: #fafafa;
}

.stat-card .label {
    font-size: 24rpx;
    color: #999;
    margin-bottom: 8rpx;
}

.stat-card .value {
    font-size: 36rpx;
    font-weight: bold;
    color: #333;
}

.stat-card .value.pending {
    color: #faad14;
}

.stat-card .value.active {
    color: #1890ff;
}

.stat-card .value.resolved {
    color: #52c41a;
}

.main-content {
    flex: 1;
    display: flex;
    overflow: hidden;
}

.agent-stats-panel,
.system-metrics-panel {
    flex: 1;
    display: flex;
    flex-direction: column;
    background: white;
    margin: 20rpx;
    border-radius: 12rpx;
    border: 1rpx solid #e8e8e8;
}

.agent-stats-panel {
    margin-right: 10rpx;
}

.system-metrics-panel {
    margin-left: 10rpx;
}

.panel-header {
    padding: 24rpx;
    border-bottom: 1rpx solid #f0f0f0;
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.panel-header .title {
    font-size: 32rpx;
    font-weight: bold;
    color: #333;
}

.panel-header .subtitle {
    font-size: 24rpx;
    color: #999;
}

.stats-list,
.metrics-detail {
    flex: 1;
    padding: 20rpx;
}

.agent-card {
    border: 1rpx solid #f0f0f0;
    border-radius: 8rpx;
    padding: 20rpx;
    margin-bottom: 16rpx;
    background: #fafafa;
}

.agent-header {
    display: flex;
    justify-content: space-between;
    align-items: center;
    margin-bottom: 16rpx;
}

.agent-name {
    font-size: 30rpx;
    font-weight: 600;
    color: #333;
}

.status-tag {
    background: #d9d9d9;
    color: #666;
    font-size: 22rpx;
    padding: 4rpx 12rpx;
    border-radius: 12rpx;
}

.status-tag.online {
    background: #b7eb8f;
    color: #389e0d;
}

.metrics {
    display: grid;
    grid-template-columns: repeat(2, 1fr);
    gap: 12rpx;
}

.metric {
    display: flex;
    justify-content: space-between;
    align-items: center;
    background: white;
    padding: 12rpx;
    border-radius: 6rpx;
}

.metric .label {
    font-size: 24rpx;
    color: #999;
}

.metric .value {
    font-size: 28rpx;
    font-weight: 600;
    color: #333;
}

.metric-group {
    margin-bottom: 30rpx;
    background: #fafafa;
    border-radius: 8rpx;
    padding: 20rpx;
}

.group-title {
    font-size: 28rpx;
    font-weight: bold;
    color: #333;
    margin-bottom: 16rpx;
    display: block;
}

.metric-row {
    display: flex;
    justify-content: space-between;
    margin-bottom: 12rpx;
    font-size: 26rpx;
}

.metric-row .label {
    color: #666;
}

.metric-row .value {
    color: #333;
    font-weight: 600;
}

.metric-row .value.online {
    color: #52c41a;
}
</style>
