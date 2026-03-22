<template>
  <view class="chat-page">
    <scroll-view scroll-y class="msg-list" :scroll-into-view="scrollId">
      <view class="msg" v-for="(m, i) in messages" :key="m.id || i" :class="m.role">
        <view class="bubble">
          <view v-if="m.type === 'text'" class="text">{{ m.content }}</view>
          <view v-else-if="m.type === 'product_opt'" class="card">
            <text class="card-title">商品优化建议</text>
            <text>{{ m.content }}</text>
            <!-- 加更多结构化展示 -->
          </view>
          <view v-else class="text">{{ m.content }}</view>
        </view>
      </view>
    </scroll-view>

    <view class="input-area">
      <input v-model="inputText" placeholder="说点什么..." confirm-type="send" @confirm="send" />
      <button @tap="send" :loading="sending">发送</button>
    </view>
  </view>
</template>

<script>
	
	     const testbase = 'http://192.168.1.254:7092';
		 
export default {
  data() {
    return {
      id: '',  // conversationId (string 类型)
      messages: [],
      inputText: '',
      sending: false,
      scrollId: '',
	  isNewChat: true  // 新增标志：是否新会话
    };
  },
  
  
  openChat(id) {
		  console.log('点击会话，ID：', id);  // 先打印看 id 是否有值

		  if (!id) {
			uni.showToast({ title: '会话ID为空', icon: 'none' });
			return;
		  }

		  uni.navigateTo({
			url: `/pages/chat/chat?conversationId=${id}`,  // 必须用 conversationId=xxx
			success: () => console.log('跳转成功'),
			fail: (err) => console.error('跳转失败：', err)
		  });
},

onLoad(options) {
  console.log('chat onLoad 参数：', options);

  // 优先使用 URL 参数（从列表页传来的 conversationId）
  this.id = options.conversationId || '';

  // 清空旧数据，确保不带入历史会话
  this.messages = [];
  this.inputText = '';

  // 如果有 id，加载历史；否则就是新会话
  if (this.id) {
    console.log('加载历史会话 ID：', this.id);
    this.loadHistory();
    // 同时更新 storage（保持一致）
    uni.setStorageSync('conversationId', this.id);
  } else {
    console.log('新会话，无 ID');
    // 新会话时清空 storage，避免干扰下次
    uni.removeStorageSync('conversationId');
  }

  this.scrollToBottom();
},
  
  
  onShow() {
    // 页面显示时再同步一次 storage（防切换 tab 丢失）
    const storedId = uni.getStorageSync('conversationId');
    if (storedId && storedId !== this.id) {
      this.id = storedId;
      this.isNewChat = false;
      this.loadHistory();
    }
  },

  methods: {
     async loadHistory() {
        if (!this.id) return;
  
        uni.setStorageSync('conversationId',this.id);
        const token = uni.getStorageSync('token');
        try {
          console.log('加载历史 - ID:', this.id);
          const res = await uni.request({
            url: `${testbase}/api/chat/conversation/${this.id}`,
            header: { Authorization: `Bearer ${token}` }
          });
  
          if (res.statusCode === 200 && res.data.messages) {
            this.messages = res.data.messages.map(m => ({
              role: m.SenderType === 1 ? 'user' : 'assistant',
              type: m.MessageType === 1 ? 'text' : 'other',
              content: m.Content,
              id: Date.now() + Math.random()
            }));
            this.scrollToBottom();
          } else {
            uni.showToast({ title: '加载历史失败', icon: 'none' });
          }
        } catch (e) {
          uni.showToast({ title: '网络错误', icon: 'none' });
        }
      },
  
      async send() {
        const text = this.inputText.trim();
        if (!text) return;
  
        const userMsg = { role: 'user', type: 'text', content: text, id: Date.now() };
        this.messages.push(userMsg);
        this.inputText = '';
        this.scrollToBottom();
  
        this.sending = true;
  
        try {
          const sendId = this.id || '';
  
          const res = await uni.request({
            url: `${testbase}/api/chat/send`,
            method: 'POST',
            header: {
              'Content-Type': 'application/json',
              'Authorization': `Bearer ${uni.getStorageSync('token')}`
            },
            data: {
              conversationId: sendId || undefined,  // undefined 会被序列化为 null，不发送
              message: text
            }
          });
  
          console.log('响应：', res);
  
          if (res.statusCode === 200 && res.data) {
            // 保存/更新 conversationId（以服务器返回为准）
            if (res.data.conversationId && res.data.conversationId !== this.id) {
              this.id = res.data.conversationId;
              uni.setStorageSync('conversationId', this.id);
              console.log('更新 conversationId：', this.id);
              this.isNewChat = false;
            }
  
            // 响应直接是 AI 消息，不是数组
            const aiMsg = {
              role: 'assistant',
              type: res.data.messageType === 'text' ? 'text' : 'other',
              content: res.data.content,
              id: res.data.messageId || Date.now()
            };
  
            this.messages.push(aiMsg);
            this.scrollToBottom();
          } else {
            uni.showToast({ title: res.data?.message || '发送失败', icon: 'none' });
          }
        } catch (e) {
          console.error('发送失败：', e);
          uni.showToast({ title: e.errMsg || '网络错误', icon: 'none' });
        } finally {
          this.sending = false;
        }
      },

 
     scrollToBottom() {
       this.$nextTick(() => {
         this.scrollId = 'msg-' + (this.messages.length - 1);
       });
     }
   }
 };
</script>

<style>
.chat-page { height: 100vh; display: flex; flex-direction: column; }
.msg-list { flex: 1; padding: 20rpx; background: #f8f8f8; }
.msg { margin: 20rpx 0; display: flex; }
.user { justify-content: flex-end; }
.bubble { max-width: 70%; padding: 20rpx; border-radius: 20rpx; }
.user .bubble { background: #07c160; color: white; border-bottom-right-radius: 0; }
.assistant .bubble { background: #fff; border-bottom-left-radius: 0; box-shadow: 0 2rpx 8rpx rgba(0,0,0,0.1); }
.text { font-size: 32rpx; line-height: 48rpx; }
.card { background: #e6f7ff; padding: 30rpx; border-radius: 20rpx; border: 1rpx solid #91d5ff; }
.card-title { font-weight: bold; font-size: 36rpx; display: block; margin-bottom: 20rpx; }
.input-area { display: flex; padding: 20rpx; background: #fff; border-top: 1rpx solid #eee; }
input { flex: 1; border: 1rpx solid #ddd; border-radius: 40rpx; padding: 20rpx 30rpx; margin-right: 20rpx; }
button { background: #07c160; color: white; border-radius: 40rpx; }
</style>