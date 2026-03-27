<template>
  <view class="conversations-page">
    <view class="header">
      <text class="title">我的会话</text>
      <button class="new-chat-btn" @tap="startNewChat">+ 新会话</button>
    </view>

    <scroll-view 
      scroll-y 
      style="height: calc(100vh - 120rpx);" 
      @scrolltolower="loadMore"
      refresher-enabled
      refresher-triggered="onPullDownRefresh"
      refresher-background="#f8f8f8"
      refresher-default-style="black"
      refresher-refreshing-style="black"
    >
      <view v-if="loading" class="loading">加载中...</view>

      <view v-else-if="conversations.length === 0" class="empty">
        <text>暂无会话</text>
        <button class="start-chat" @tap="startNewChat">开始新聊天</button>
      </view>

      <view v-else class="list">
        <!-- 时间分组 -->
        <view v-for="(group, index) in groupedConversations" :key="index" class="group">
          <view class="group-title">{{ group.title }}</view>
          <view class="conversation-item" v-for="conv in group.items" :key="conv.id" @tap="openChat(conv)" @longpress="showDeleteMenu(conv)">
            <view class="avatar">🤖</view>
            <view class="info">
              <view class="title">{{ conv.title }}</view>
              <view class="last-msg">{{ conv.lastMessage }}</view>
            </view>
            <view class="time">{{ formatTime(conv.lastActiveAt) }}</view>
          </view>
        </view>

        <view v-if="loadingMore" class="loading-more">加载更多...</view>
        <view v-if="!hasMore && conversations.length > 0" class="no-more">没有更多了</view>
      </view>
    </scroll-view>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      conversations: [],       // 所有会话
      page: 1,
      pageSize: 20,
      hasMore: true,
      loading: false,
      loadingMore: false
    };
  },

  computed: {
    groupedConversations() {
      const groups = [];
      const today = new Date();
      const yesterday = new Date(today);
      yesterday.setDate(today.getDate() - 1);

      const todayItems = this.conversations.filter(conv => {
        const date = new Date(conv.lastActiveAt);
        return date.toDateString() === today.toDateString();
      });

      const yesterdayItems = this.conversations.filter(conv => {
        const date = new Date(conv.lastActiveAt);
        return date.toDateString() === yesterday.toDateString();
      });

      const earlierItems = this.conversations.filter(conv => {
        const date = new Date(conv.lastActiveAt);
        return date < yesterday;
      });

      if (todayItems.length > 0) groups.push({ title: '今天', items: todayItems });
      if (yesterdayItems.length > 0) groups.push({ title: '昨天', items: yesterdayItems });
      if (earlierItems.length > 0) groups.push({ title: '更早', items: earlierItems });

      return groups;
    }
  },

  onLoad() {
    this.loadConversations();
  },

  onShow() {
    // 从聊天页返回时自动刷新（延迟 500ms 避免太频繁）
    setTimeout(() => {
      this.loadConversations();
    }, 500);
  },

  onPullDownRefresh() {
    this.page = 1;
    this.hasMore = true;
    this.loadConversations();
  },

  loadMore() {
    if (this.hasMore && !this.loadingMore) {
      this.page++;
      this.loadMoreConversations();
    }
  },

  methods: {
    async loadConversations() {
      this.loading = true;
      const token = uni.getStorageSync('token');

      try {
        const res = await uni.request({
          url: `${testbase}/api/chat/conversations`,
          header: { Authorization: `Bearer ${token}` },
          data: { page: this.page, pageSize: this.pageSize },
          timeout: 10000  // 加超时防止卡死
        });

        console.log('接口返回:', res.statusCode, res.data);

        if (res.statusCode === 200 && res.data) {
          const newList = res.data.items || res.data;  // 兼容

          if (this.page === 1) {
            this.conversations = newList;
          } else {
            this.conversations = this.conversations.concat(newList);
          }

          this.hasMore = res.data.hasMore !== false && newList.length === this.pageSize;
        } else {
          uni.showToast({ title: '加载失败 ' + res.statusCode, icon: 'none' });
        }
      } catch (e) {
        console.error('请求异常:', e);
        uni.showToast({ title: '网络错误', icon: 'none' });
      } finally {
        this.loading = false;
        // 强制延迟结束刷新动画（解决卡住问题）
        setTimeout(() => {
          uni.stopPullDownRefresh();
          console.log('强制结束刷新动画');
        }, 600);
      }
    },

    async loadMoreConversations() {
      this.loadingMore = true;
      await this.loadConversations();
      this.loadingMore = false;
    },

    openChat(conv) {
      uni.navigateTo({
        url: `/pages/chat/chat?conversationId=${conv.id}`
      });
    },

    startNewChat() {
      uni.navigateTo({
        url: '/pages/chat/chat'
      });
    },

    showDeleteMenu(conv) {
      uni.showActionSheet({
        itemList: ['删除会话'],
        success: (res) => {
          if (res.tapIndex === 0) {
            uni.showModal({
              title: '删除会话',
              content: '删除后将不再显示，但后台保留历史记录',
              success: async (modalRes) => {
                if (modalRes.confirm) {
                  const token = uni.getStorageSync('token');
                  await uni.request({
                    url: `${testbase}/api/chat/conversation/${conv.id}`,
                    method: 'DELETE',
                    header: { Authorization: `Bearer ${token}` }
                  });
                  this.conversations = this.conversations.filter(c => c.id !== conv.id);
                  uni.showToast({ title: '已删除', icon: 'success' });
                }
              }
            });
          }
        }
      });
    },

    formatTime(time) {
      if (!time) return '';
      const date = new Date(time);
      const now = new Date();
      const diff = now - date;
      if (diff < 60 * 1000) return '刚刚';
      if (diff < 60 * 60 * 1000) return Math.floor(diff / 60 / 1000) + '分钟前';
      if (diff < 24 * 60 * 60 * 1000) return Math.floor(diff / 60 / 60 / 1000) + '小时前';
      return date.toLocaleDateString('zh-CN');
    }
  }
};
</script>

<style>
.conversations-page { height: 100vh; background: #f8f8f8; }
.header { padding: 20rpx; background: white; border-bottom: 1rpx solid #eee; display: flex; justify-content: space-between; align-items: center; }
.title { font-size: 36rpx; font-weight: bold; }
.new-chat-btn { background: #07c160; color: white; border-radius: 50rpx; padding: 10rpx 30rpx; font-size: 28rpx; }
.empty { text-align: center; padding: 200rpx 0; }
.start-chat { margin-top: 40rpx; background: #07c160; color: white; border-radius: 50rpx; width: 300rpx; }
.conversation-item { display: flex; padding: 20rpx; background: white; border-bottom: 1rpx solid #eee; }
.avatar { width: 80rpx; height: 80rpx; background: #ccc; border-radius: 50%; margin-right: 20rpx; display: flex; align-items: center; justify-content: center; font-size: 40rpx; }
.info { flex: 1; }
.title { font-size: 32rpx; font-weight: bold; }
.last-msg { font-size: 28rpx; color: #999; margin-top: 10rpx; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.time { font-size: 24rpx; color: #999; align-self: center; }
.group-title { padding: 20rpx 20rpx 10rpx; font-size: 28rpx; color: #666; background: #f8f8f8; }
.loading-more, .no-more { text-align: center; padding: 40rpx; color: #999; }
</style>