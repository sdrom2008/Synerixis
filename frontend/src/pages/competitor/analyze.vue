<template>
  <view class="container">
    <view class="form">
      <view class="form-item">
        <text class="label">分析关键词</text>
        <input v-model="form.keyword" placeholder="例如：蓝牙耳机" class="input" />
      </view>
      <view class="form-item">
        <text class="label">平台（可选）</text>
        <picker v-model="form.platform" :range="platforms">
          <view class="picker">{{ form.platform || '请选择平台' }}</view>
        </picker>
      </view>
      <button type="primary" :loading="loading" @click="handleAnalyze">开始分析</button>
    </view>

    <view class="result" v-if="result">
      <view class="card">
        <text class="card-title">竞品分析报告</text>
        <rich-text :nodes="formatReport(result.report)" class="report-content"></rich-text>
        <text class="time">生成时间：{{ result.generatedAt }}</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'

interface AnalyzeRequest {
  keyword: string
  platform?: string
}

interface AnalyzeResponse {
  report: string
  generatedAt: string
}

const platforms = ['淘宝', '京东', '拼多多', '抖音', '小红书', '亚马逊']

const form = reactive<AnalyzeRequest>({
  keyword: '',
  platform: ''
})

const loading = ref(false)
const result = ref<AnalyzeResponse | null>(null)

const handleAnalyze = async () => {
  if (!form.keyword.trim()) {
    uni.showToast({ title: '请输入分析关键词', icon: 'none' })
    return
  }
  loading.value = true
  try {
    const token = uni.getStorageSync('token')
    const headers: UniApp.RequestOptions['header'] = { 'Content-Type': 'application/json' }
    if (token) headers.Authorization = `Bearer ${token}`

    const apiRes = await uni.request<AnalyzeResponse>({
      url: 'http://localhost:5001/api/competitor/analyze',
      method: 'POST',
      header: headers,
      data: form
    })
    if (apiRes.statusCode === 200) {
      result.value = apiRes.data
    } else {
      throw new Error(apiRes.data?.message || '分析失败')
    }
  } catch (err: any) {
    uni.showToast({ title: err.message || '请求失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}

const formatReport = (report: string) => {
  // 简单地将换行和 markdown 风格转换为 rich-text（实际可引入 marked 库）
  return report
    .replace(/\n/g, '<br/>')
    .replace(/\*\*(.*?)\*\*/g, '<b>$1</b>')
    .replace(/\*(.*?)\*/g, '<i>$1</i>')
}
</script>

<style scoped>
.container { padding: 30rpx; }
.form { background: #fff; border-radius: 16rpx; padding: 30rpx; }
.form-item { margin-bottom: 30rpx; }
.label { display: block; margin-bottom: 12rpx; font-weight: 600; color: #333; }
.input { width: 100%; padding: 20rpx; border: 1px solid #e0e0e0; border-radius: 8rpx; font-size: 28rpx; }
.picker { padding: 20rpx; border: 1px solid #e0e0e0; border-radius: 8rpx; font-size: 28rpx; }
button { margin-top: 20rpx; background-color: #007AFF; color: #fff; }

.result { margin-top: 30rpx; }
.card { background: #fff; border-radius: 16rpx; padding: 30rpx; }
.card-title { font-size: 32rpx; font-weight: 700; margin-bottom: 16rpx; display: block; }
.report-content { font-size: 28rpx; line-height: 1.8; color: #333; }
.time { font-size: 24rpx; color: #999; margin-top: 20rpx; display: block; }
</style>
