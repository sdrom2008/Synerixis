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
        const shopId = uni.getStorageSync('shopId');
        const data = await request({ 
          url: '/api/support/dashboard', 
          params: shopId ? { shopId } : {} 
        });
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
        const shopId = uni.getStorageSync('shopId');
        const data = await request({ 
          url: '/api/support/agents/stats', 
          params: shopId ? { shopId } : {} 
        });
        agentStats.value = data.map(agent => ({
            agentId: agent.agentId,
            agentName: agent.agentName,
            totalSessions: agent.totalSessions || 0,
            activeSessions: agent.activeSessions || 0,
            resolvedSessions: agent.resolvedSessions || 0,
            avgResponseTimeSeconds: agent.avgResponseTimeSeconds || 0,
            avgResolutionTimeMinutes: agent.avgResolutionTimeMinutes || 0,
            avgSatisfaction: agent.avgSatisfaction || 0,
            online: agent.online || false
        }));
    } catch (error) {
        // Handled in request.js
    }
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
