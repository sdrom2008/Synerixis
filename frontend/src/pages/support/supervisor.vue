<template>
  <view class="supervisor">
    <view class="stats-overview">
      <view class="stat-card"><text class="label">总会话数</text><text class="value">{{ dashboard.totalSessions }}</text></view>
      <view class="stat-card"><text class="label">待处理</text><text class="value pending">{{ dashboard.pendingSessions }}</text></view>
      <view class="stat-card"><text class="label">进行中</text><text class="value active">{{ dashboard.activeSessions }}</text></view>
      <view class="stat-card"><text class="label">已解决</text><text class="value resolved">{{ dashboard.resolvedSessions }}</text></view>
    </view>

    <view class="main-content">
      <view class="agent-stats-panel">
        <view class="panel-header"><text class="title">客服绩效</text><text class="subtitle">总计: {{ agentStats.length }} 位客服</text></view>
        <scroll-view scroll-y class="stats-list">
          <view v-for="agent in agentStats" :key="agent.agentId" class="agent-card">
            <view class="agent-header"><text class="agent-name">{{ agent.agentName }}</text><text class="status-tag" :class="{ online: agent.online }">在线</text></view>
            <view class="metrics">
              <view class="metric"><text class="label">总会话</text><text class="value">{{ agent.totalSessions }}</text></view>
              <view class="metric"><text class="label">解决数</text><text class="value">{{ agent.resolvedSessions }}</text></view>
              <view class="metric"><text class="label">平均响应</text><text class="value">{{ formatSeconds(agent.avgResponseTimeSeconds) }}</text></view>
              <view class="metric"><text class="label">解决时长</text><text class="value">{{ formatMinutes(agent.avgResolutionTimeMinutes) }}</text></view>
            </view>
          </view>
        </scroll-view>
      </view>

      <view class="system-metrics-panel">
        <view class="panel-header"><text class="title">系统指标</text></view>
        <scroll-view scroll-y class="metrics-detail">
          <view class="metric-group">
            <text class="group-title">客服状态</text>
            <view class="metric-row"><text class="label">总客服数</text><text class="value">{{ dashboard.totalAgents }}</text></view>
            <view class="metric-row"><text class="label">在线客服</text><text class="value online">{{ dashboard.onlineAgents }}</text></view>
          </view>
          <view class="metric-group">
            <text class="group-title">会话效率</text>
            <view class="metric-row"><text class="label">平均响应时间</text><text class="value">{{ formatSeconds(dashboard.avgResponseTimeSeconds) }}</text></view>
            <view class="metric-row"><text class="label">平均解决时长</text><text class="value">{{ formatMinutes(dashboard.avgResolutionTimeMinutes) }}</text></view>
            <view class="metric-row"><text class="label">满意度</text><text class="value">{{ dashboard.overallSatisfaction?.toFixed(1) }}</text></view>
          </view>
          <view class="metric-group">
            <text class="group-title">更新时间</text>
            <view class="metric-row"><text class="value">{{ formatDateTime(dashboard.lastUpdated) }}</text></view>
          </view>
        </scroll-view>
      </view>
    </view>
  </view>
</template>

<script setup>
import { ref, onMounted } from 'vue';
import { request } from '@/utils/request.js';

const dashboard = ref({ totalSessions: 0, pendingSessions: 0, activeSessions: 0, resolvedSessions: 0, totalAgents: 0, onlineAgents: 0, avgResponseTimeSeconds: 0, avgResolutionTimeMinutes: 0, overallSatisfaction: 0, lastUpdated: null });
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
        const shopId = uni.getStorageSync('shopId');
        const data = await request({ url: '/api/support/dashboard', params: shopId ? { shopId } : {} });
        dashboard.value = { totalSessions: data.totalSessions || 0, pendingSessions: data.pendingSessions || 0, activeSessions: data.activeSessions || 0, resolvedSessions: data.resolvedSessions || 0, totalAgents: data.totalAgents || 0, onlineAgents: data.onlineAgents || 0, avgResponseTimeSeconds: data.avgResponseTimeSeconds || 0, avgResolutionTimeMinutes: data.avgResolutionTimeMinutes || 0, overallSatisfaction: data.overallSatisfaction || 0, lastUpdated: data.lastUpdated || new Date() };
    } catch (error) {}
}

async function loadAgentStats() {
    try {
        const shopId = uni.getStorageSync('shopId');
        const data = await request({ url: '/api/support/agents/stats', params: shopId ? { shopId } : {} });
        agentStats.value = data.map(agent => ({ agentId: agent.id || agent.agentId, agentName: agent.name || agent.agentName, totalSessions: agent.totalSessions || 0, activeSessions: agent.activeSessions || 0, resolvedSessions: agent.resolvedSessions || 0, avgResponseTimeSeconds: agent.avgResponseTimeSeconds || 0, avgResolutionTimeMinutes: agent.avgResolutionTimeMinutes || 0, avgSatisfaction: agent.avgSatisfaction || 0, online: agent.online || false }));
    } catch (error) {}
}

function formatSeconds(seconds) {
    if (!seconds) return '0s';
    const m = Math.floor(seconds / 60);
    const s = Math.floor(seconds % 60);
    if (m > 0) return `${m}m ${s}s`;
    return `${s}s`;
}
function formatMinutes(minutes) {
    if (!minutes) return '0m';
    return `${minutes.toFixed(1)}m`;
}
function formatDateTime(dateStr) {
    if (!dateStr) return '-';
    const d = new Date(dateStr);
    return `${d.getFullYear()}-${d.getMonth()+1}-${d.getDate()} ${d.getHours()}:${d.getMinutes().toString().padStart(2,'0')}`;
}
</script>

<style scoped>
.supervisor { display: flex; flex-direction: column; height: 100vh; background: #f5f5f5; }
.stats-overview { display: flex; padding: 30rpx; background: white; border-bottom: 1rpx solid #e8e8e8; }
.stat-card { flex: 1; text-align: center; margin: 0 10rpx; padding: 20rpx 0; background: #f8f9fa; border-radius: 16rpx; }
.stat-card .label { display: block; font-size: 28rpx; color: #666; margin-bottom: 10rpx; }
.stat-card .value { display: block; font-size: 40rpx; font-weight: bold; color: #333; }
.stat-card .value.pending { color: #f57c00; }
.stat-card .value.active { color: #1976d2; }
.stat-card .value.resolved { color: #388e3c; }

.main-content { display: flex; flex: 1; overflow: hidden; }
.agent-stats-panel { width: 400rpx; background: white; border-right: 1rpx solid #e8e8e8; display: flex; flex-direction: column; }
.panel-header { padding: 30rpx; border-bottom: 1rpx solid #f0f0f0; display: flex; justify-content: space-between; align-items: center; }
.panel-header .title { font-size: 32rpx; font-weight: bold; color: #333; }
.panel-header .subtitle { font-size: 24rpx; color: #999; }
.stats-list { flex: 1; }
.agent-card { padding: 30rpx; border-bottom: 1rpx solid #f0f0f0; }
.agent-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: 20rpx; }
.agent-name { font-size: 32rpx; font-weight: bold; }
.status-tag { font-size: 22rpx; color: #999; background: #f0f0f0; padding: 4rpx 12rpx; border-radius: 8rpx; }
.status-tag.online { background: #e8f5e9; color: #388e3c; }
.metrics { display: flex; flex-wrap: wrap; }
.metric { width: 50%; margin-bottom: 16rpx; }
.metric .label { display: block; font-size: 24rpx; color: #999; }
.metric .value { display: block; font-size: 28rpx; color: #333; font-weight: 500; }

.system-metrics-panel { flex: 1; background: white; display: flex; flex-direction: column; }
.metrics-detail { flex: 1; padding: 30rpx; }
.metric-group { margin-bottom: 40rpx; }
.group-title { display: block; font-size: 32rpx; font-weight: bold; margin-bottom: 20rpx; color: #333; }
.metric-row { display: flex; justify-content: space-between; margin-bottom: 16rpx; }
.metric-row .label { font-size: 28rpx; color: #666; }
.metric-row .value { font-size: 28rpx; color: #333; font-weight: 500; }
.metric-row .value.online { color: #388e3c; }
</style>
