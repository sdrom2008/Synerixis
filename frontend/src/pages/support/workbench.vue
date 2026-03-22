<template>
    <view class="workbench">
        <!-- 左侧：会话列表 -->
        <view class="sidebar-left">
            <view class="header">
                <text class="title">待接入/进行中</text>
                <text class="count">{{ tickets.length }}</text>
            </view>
            <scroll-view scroll-y class="ticket-list">
                <view 
                    v-for="ticket in tickets" 
                    :key="ticket.id" 
                    class="ticket-item" 
                    :class="{ active: currentTicket?.id === ticket.id }"
                    @click="selectTicket(ticket)"
                >
                    <view class="customer-info">
                        <text class="customer-name">{{ ticket.customerName }}</text>
                        <text class="platform">{{ ticket.platform }}</text>
                    </view>
                    <text class="last-message">{{ ticket.lastMessage }}</text>
                    <view class="meta">
                        <text class="time">{{ formatTime(ticket.updatedAt) }}</text>
                        <text v-if="ticket.unread" class="badge">新</text>
                    </view>
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
                    <text class="session-id">会话ID: {{ currentTicket.sessionId }}</text>
                </view>
                <scroll-view scroll-y class="messages" :scroll-into-view="lastMessageId">
                    <view 
                        v-for="msg in messages" 
                        :key="msg.id" 
                        :id="'msg-'+msg.id"
                        class="message"
                        :class="{ 'from-user': msg.isFromUser, 'from-ai': !msg.isFromUser }"
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
                    <button class="send-btn" @click="sendMessage" size="mini">发送</button>
                </view>
            </template>
        </view>

        <!-- 右侧：客户信息 -->
        <view class="sidebar-right" v-if="currentTicket">
            <scroll-view scroll-y class="info-panel">
                <view class="section">
                    <text class="section-title">客户信息</text>
                    <view class="info-row">
                        <text class="label">平台:</text>
                        <text class="value">{{ currentTicket.platform }}</text>
                    </view>
                    <view class="info-row">
                        <text class="label">Customer ID:</text>
                        <text class="value">{{ currentTicket.customerId }}</text>
                    </view>
                    <view class="info-row">
                        <text class="label">会话状态:</text>
                        <text class="value">{{ currentTicket.status }}</text>
                    </view>
                </view>

                <view class="section">
                    <text class="section-title">订单历史</text>
                    <view v-if="customerOrders.length === 0" class="empty">
                        <text>暂无订单</text>
                    </view>
                    <view v-for="order in customerOrders" :key="order.orderNo" class="order-card">
                        <view class="order-row">
                            <text class="order-no">{{ order.orderNo }}</text>
                            <text class="order-status">{{ order.status }}</text>
                        </view>
                        <view class="order-row">
                            <text class="label">金额:</text>
                            <text class="value">¥{{ order.totalAmount }}</text>
                        </view>
                        <view class="order-row">
                            <text class="label">时间:</text>
                            <text class="value">{{ formatDate(order.orderTime) }}</text>
                        </view>
                    </view>
                </view>
            </scroll-view>
        </view>
    </view>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue';
import { request } from '@/utils/request.js';

// Reactive state
const tickets = ref([]);
const currentTicket = ref(null);
const messages = ref([]);
const newMessage = ref('');
const customerOrders = ref([]);
const lastMessageId = ref('');

// Lifecycle hooks
onMounted(() => {
    // 权限检查
    const role = uni.getStorageSync('role');
    if (role !== 'Agent' && role !== 'Supervisor' && role !== 'Admin') {
        uni.showToast({ title: '无权访问工作台', icon: 'none' });
        setTimeout(() => uni.switchTab({ url: '/pages/dashboard/dashboard' }), 1500);
        return;
    }
    loadTickets();
});

// API Calls
async function loadTickets() {
    try {
        const data = await request({ url: '/api/support/tickets' });
        tickets.value = data.map(t => ({
            ...t,
            customerName: t.customer?.name || '未知用户',
            lastMessage: t.lastMessage?.content || '...',
            updatedAt: t.lastMessage?.createdAt || t.createdAt,
            unread: t.unreadCount > 0
        }));
    } catch (error) {
        // Handled in request.js
    }
}

async function loadMessages(ticketId) {
    try {
        const data = await request({ url: `/api/support/tickets/${ticketId}/messages` });
        messages.value = data.map(m => ({
            id: m.id,
            content: m.content,
            isFromUser: m.senderType === 'Customer'
        }));
        scrollToBottom();
    } catch (error) {
        // Handled in request.js
    }
}

async function sendMessage() {
    if (!newMessage.value.trim() || !currentTicket.value) return;
    const content = newMessage.value;
    newMessage.value = '';

    try {
        await request({
            url: `/api/support/tickets/${currentTicket.value.id}/reply`,
            method: 'POST',
            data: { content }
        });
        await loadMessages(currentTicket.value.id); // Refresh messages
    } catch (error) {
        // Handled in request.js
    }
}

async function loadCustomerInfo(customerId) {
    // Placeholder for fetching customer details and order history
    // try {
    //     const data = await request({ url: `/api/customers/${customerId}/orders` });
    //     customerOrders.value = data;
    // } catch (error) {}
    customerOrders.value = [];
}

// UI Methods
async function selectTicket(ticket) {
    currentTicket.value = ticket;
    messages.value = [];
    customerOrders.value = [];
    await loadMessages(ticket.id);
    await loadCustomerInfo(ticket.customerId);
}

function scrollToBottom() {
    if (messages.value.length > 0) {
        const lastId = messages.value[messages.value.length - 1].id;
        setTimeout(() => {
            lastMessageId.value = 'msg-' + lastId;
        }, 100);
    }
}

// Formatters
const formatTime = (date) => {
    if (!date) return '';
    const d = new Date(date);
    return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`;
}

const formatDate = (date) => {
    if (!date) return '';
    const d = new Date(date);
    return `${d.getMonth() + 1}-${d.getDate()} ${d.getHours()}:${d.getMinutes().toString().padStart(2, '0')}`;
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
    background: #1890ff;
    color: white;
    padding: 4rpx 16rpx;
    border-radius: 20rpx;
    font-size: 24rpx;
}

.ticket-list {
    flex: 1;
}

.ticket-item {
    padding: 24rpx 30rpx;
    border-bottom: 1rpx solid #f5f5f5;
    position: relative;
}

.ticket-item.active {
    background: #e6f7ff;
}

.customer-info {
    display: flex;
    justify-content: space-between;
    margin-bottom: 12rpx;
}

.customer-name {
    font-size: 30rpx;
    font-weight: 600;
    color: #333;
}

.platform {
    font-size: 22rpx;
    color: #999;
    background: #f0f0f0;
    padding: 2rpx 8rpx;
    border-radius: 4rpx;
}

.last-message {
    font-size: 26rpx;
    color: #666;
    margin-bottom: 8rpx;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.meta {
    display: flex;
    justify-content: space-between;
    align-items: center;
}

.time {
    font-size: 22rpx;
    color: #bbb;
}

.badge {
    background: #ff4d4f;
    color: white;
    font-size: 20rpx;
    padding: 2rpx 8rpx;
    border-radius: 8rpx;
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
    padding: 24rpx 30rpx;
    background: white;
    border-bottom: 1rpx solid #e8e8e8;
}

.chat-header .customer-name {
    font-size: 32rpx;
    font-weight: bold;
    color: #333;
}

.session-id {
    font-size: 24rpx;
    color: #999;
    margin-left: 16rpx;
}

.messages {
    flex: 1;
    padding: 20rpx;
}

.message {
    margin-bottom: 24rpx;
    display: flex;
}

.message.from-user {
    justify-content: flex-end;
}

.message.from-ai {
    justify-content: flex-start;
}

.bubble {
    max-width: 70%;
    padding: 20rpx 28rpx;
    border-radius: 16rpx;
    font-size: 28rpx;
    line-height: 1.5;
}

.from-user .bubble {
    background: #1890ff;
    color: white;
    border-top-right-radius: 4rpx;
}

.from-ai .bubble {
    background: white;
    color: #333;
    border: 1rpx solid #e8e8e8;
    border-top-left-radius: 4rpx;
}

.input-bar {
    padding: 20rpx;
    background: white;
    border-top: 1rpx solid #e8e8e8;
    display: flex;
    align-items: center;
}

.input {
    flex: 1;
    border: 1rpx solid #d9d9d9;
    border-radius: 8rpx;
    padding: 16rpx;
    font-size: 28rpx;
    margin-right: 16rpx;
}

.send-btn {
    background: #1890ff;
    color: white;
    border: none;
}

.sidebar-right {
    width: 320rpx;
    background: white;
    border-left: 1rpx solid #e8e8e8;
}

.info-panel {
    height: 100%;
    padding: 20rpx;
}

.section {
    margin-bottom: 30rpx;
}

.section-title {
    font-size: 30rpx;
    font-weight: bold;
    color: #333;
    margin-bottom: 20rpx;
    display: block;
}

.info-row {
    display: flex;
    margin-bottom: 16rpx;
    font-size: 26rpx;
}

.label {
    color: #999;
    width: 160rpx;
}

.value {
    flex: 1;
    color: #333;
}

.order-card {
    border: 1rpx solid #f0f0f0;
    border-radius: 8rpx;
    padding: 20rpx;
    margin-bottom: 16rpx;
    background: #fafafa;
}

.order-row {
    display: flex;
    justify-content: space-between;
    margin-bottom: 8rpx;
    font-size: 26rpx;
}

.order-no {
    font-weight: 600;
    color: #333;
}

.order-status {
    color: #1890ff;
}

.empty {
    text-align: center;
    color: #999;
    padding: 40rpx 0;
}
</style>
