<template>
  <view class="ai-preference-page">
    <!-- 导航栏 -->
    <view class="navbar">
      <view class="back" @tap="goBack">←</view>
      <view class="title">AI 偏好设置</view>
      <view class="placeholder"></view>
    </view>

    <!-- 说明卡片 -->
    <view class="tips-card">
      <text>这些设置会影响 AI 在客服对话、商品优化、营销方案中的表现方式。</text>
      <text>建议根据你的店铺风格和客户群体选择。</text>
    </view>

    <!-- 回复语气 -->
    <view class="section">
      <view class="section-title">回复语气</view>
      <view class="segmented">
        <view v-for="(item, index) in toneOptions" :key="index" 
              :class="['segment', toneIndex === index ? 'active' : '']" 
              @tap="toneIndex = index; form.defaultReplyTone = item.value">
          {{ item.label }}
        </view>
      </view>
      <view class="description">
        当前选择：{{ toneOptions[toneIndex].desc }}
      </view>
    </view>

    <!-- 偏好语言 -->
    <view class="section">
      <view class="section-title">偏好语言</view>
      <view class="segmented">
        <view v-for="(item, index) in langOptions" :key="index" 
              :class="['segment', langIndex === index ? 'active' : '']" 
              @tap="langIndex = index; form.preferredLanguage = item.value">
          {{ item.label }}
        </view>
      </view>
      <view class="description">
        AI 将优先使用此语言回复客户（双语模式会中英混排）
      </view>
    </view>

    <!-- 主动提醒 -->
    <view class="section">
      <view class="section-title">主动营销提醒</view>
      <view class="switch-group">
        <view class="switch-item">
          <text>开启每日营销建议提醒</text>
          <switch :checked="form.enableAutoMarketingReminder" @change="form.enableAutoMarketingReminder = $event.detail.value" color="#007aff" />
        </view>
        <view class="switch-tip">
          开启后，AI 会在每天固定时间推送个性化营销建议（如文案、活动思路）
        </view>
      </view>
    </view>

    <!-- 记忆保留 -->
    <view class="section">
      <view class="section-title">记忆保留时长</view>
      <view class="stepper-group">
        <text>AI 记住历史对话的天数（0 为永久）</text>
        <view class="stepper">
          <button class="step-btn" @tap="decreaseDays">-</button>
          <input type="number" v-model="form.memoryRetentionDays" class="step-input" min="0" max="365" />
          <button class="step-btn" @tap="increaseDays">+</button>
        </view>
        <view class="tip">建议 90-180 天，过长可能增加 token 消耗</view>
      </view>
    </view>

    <!-- 保存按钮 -->
    <button class="save-btn" :loading="saving" @tap="saveConfig">
      保存 AI 偏好
    </button>
  </view>
</template>

<script>
import { BASE_URL as testbase } from '@/utils/config.js';

export default {
  data() {
    return {
      form: {
        defaultReplyTone: 'professional',
        preferredLanguage: 'zh',
        enableAutoMarketingReminder: true,
        memoryRetentionDays: 180
      },
      toneOptions: [
        { label: '专业', value: 'professional', desc: '正式、严谨、注重专业术语' },
        { label: '亲切', value: 'friendly', desc: '温暖、友好，像朋友聊天' },
        { label: '幽默', value: 'humorous', desc: '轻松、带点幽默，增加趣味性' }
      ],
      toneIndex: 0,
      langOptions: [
        { label: '中文优先', value: 'zh' },
        { label: '英文优先', value: 'en' },
        { label: '双语混排', value: 'bilingual' }
      ],
      langIndex: 0,
      saving: false
    };
  },

  onLoad() {
    this.loadAiConfig();
  },

  methods: {
    async loadAiConfig() {
      const token = uni.getStorageSync('token');
      if (!token) return uni.navigateTo({ url: '/pages/login/login' });

      const res = await uni.request({
        url: `${testbase}/api/seller/profile`,
        header: { Authorization: `Bearer ${token}` }
      });

      if (res.statusCode === 200 && res.data.config) {
        const config = res.data.config;
        this.form = {
          defaultReplyTone: config.defaultReplyTone || 'professional',
          preferredLanguage: config.preferredLanguage || 'zh',
          enableAutoMarketingReminder: config.enableAutoMarketingReminder ?? true,
          memoryRetentionDays: config.memoryRetentionDays ?? 180
        };
        this.toneIndex = this.toneOptions.findIndex(t => t.value === this.form.defaultReplyTone);
        this.langIndex = this.langOptions.findIndex(l => l.value === this.form.preferredLanguage);
      }
    },

    decreaseDays() {
      if (this.form.memoryRetentionDays > 0) {
        this.form.memoryRetentionDays--;
      }
    },

    increaseDays() {
      if (this.form.memoryRetentionDays < 365) {
        this.form.memoryRetentionDays++;
      }
    },

    async saveConfig() {
      this.saving = true;
      const token = uni.getStorageSync('token');

      const res = await uni.request({
        url: `${testbase}/api/seller/config`,
        method: 'PUT',
        header: { 'content-type': 'application/json', Authorization: `Bearer ${token}` },
        data: this.form
      });

      this.saving = false;

      if (res.statusCode === 200) {
        uni.showToast({ title: '保存成功', icon: 'success' });
        uni.navigateBack();
      } else {
        uni.showToast({ title: res.data?.msg || '保存失败，请重试', icon: 'none' });
      }
    },

    goBack() {
      uni.navigateBack();
    }
  }
};
</script>

<style>
.ai-preference-page { background: #f5f5f5; min-height: 100vh; padding-bottom: 120rpx; }
.navbar { height: 88rpx; background: #000; color: white; display: flex; align-items: center; justify-content: space-between; padding: 0 30rpx; position: fixed; top: 0; left: 0; right: 0; z-index: 999; }
.back { font-size: 48rpx; }
.title { font-size: 36rpx; font-weight: bold; }
.tips-card { background: #fff; margin: 88rpx 30rpx 30rpx; padding: 40rpx; border-radius: 24rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.06); font-size: 28rpx; color: #666; line-height: 48rpx; }
.section { background: white; margin: 0 30rpx 30rpx; padding: 40rpx; border-radius: 24rpx; box-shadow: 0 8rpx 32rpx rgba(0,0,0,0.08); }
.section-title { font-size: 36rpx; font-weight: bold; margin-bottom: 40rpx; }
.segmented { display: flex; background: #f8f8f8; border-radius: 16rpx; overflow: hidden; margin-top: 20rpx; }
.segment { flex: 1; text-align: center; padding: 28rpx 0; font-size: 30rpx; color: #666; border-right: 1rpx solid #ddd; }
.segment:last-child { border-right: none; }
.segment.active { background: #007aff; color: white; }
.description { margin-top: 20rpx; font-size: 28rpx; color: #666; line-height: 40rpx; }
.switch-group { margin-top: 20rpx; }
.switch-item { display: flex; justify-content: space-between; align-items: center; font-size: 32rpx; margin-bottom: 16rpx; }
.switch-tip { font-size: 26rpx; color: #999; line-height: 40rpx; }
.stepper-group { margin-top: 20rpx; }
.stepper { display: flex; align-items: center; margin-top: 20rpx; }
.step-btn { width: 80rpx; height: 80rpx; background: #f0f0f0; border-radius: 16rpx; text-align: center; line-height: 80rpx; font-size: 40rpx; }
.step-input { width: 120rpx; text-align: center; margin: 0 20rpx; font-size: 36rpx; border: 1rpx solid #ddd; border-radius: 16rpx; padding: 20rpx 0; }
.save-btn { position: fixed; bottom: 40rpx; left: 30rpx; right: 30rpx; background: #007aff; color: white; border-radius: 50rpx; height: 96rpx; line-height: 96rpx; font-size: 36rpx; text-align: center; box-shadow: 0 8rpx 24rpx rgba(0,122,255,0.3); }
</style>