<template>
  <view class="container">
    <view class="form-section">
      <view class="form-item">
        <text class="label">商品名称</text>
        <input v-model="form.productName" placeholder="请输入商品名称" class="input" />
      </view>
      <view class="form-item">
        <text class="label">关键词（逗号分隔）</text>
        <input v-model="form.keywords" placeholder="例如：蓝牙，无线，高音质" class="input" />
      </view>
      <view class="form-item">
        <text class="label">卖点（可选）</text>
        <textarea v-model="form.sellingPoints" placeholder="列举核心卖点，一行一个" class="textarea" auto-height />
      </view>
      <view class="form-item">
        <text class="label">语气风格</text>
        <radio-group @change="onToneChange">
          <label class="radio-item">
            <radio value="professional" :checked="form.tone === 'professional'" />专业严谨
          </label>
          <label class="radio-item">
            <radio value="lively" :checked="form.tone === 'lively'" />活泼年轻
          </label>
          <label class="radio-item">
            <radio value="luxury" :checked="form.tone === 'luxury'" />高端奢华
          </label>
        </radio-group>
      </view>
      <button type="primary" :loading="loading" @click="handleGenerate">生成文案</button>
    </view>

    <view class="result-section" v-if="result">
      <text class="result-title">生成结果</text>
      <view class="result-card">
        <text class="result-label">标题：</text>
        <text class="result-content">{{ result.title }}</text>
      </view>
      <view class="result-card">
        <text class="result-label">卖点：</text>
        <text class="result-content">{{ result.keySellingPoints }}</text>
      </view>
      <view class="result-card">
        <text class="result-label">详情描述：</text>
        <text class="result-content description">{{ result.description }}</text>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { BASE_URL } from '@/utils/config.js';
import { ref, reactive } from 'vue'

interface GenerateRequest {
  productName: string
  keywords: string
  sellingPoints: string
  tone: string
}

interface GenerateResponse {
  title: string
  keySellingPoints: string
  description: string
}

const form = reactive<GenerateRequest>({
  productName: '',
  keywords: '',
  sellingPoints: '',
  tone: 'professional'
})

const loading = ref(false)
const result = ref<GenerateResponse | null>(null)

const onToneChange = (e: any) => {
  form.tone = e.detail.value
}

const handleGenerate = async () => {
  if (!form.productName.trim()) {
    uni.showToast({ title: '请输入商品名称', icon: 'none' })
    return
  }
  loading.value = true
  try {
    const token = uni.getStorageSync('token')
    const headers: UniApp.RequestOptions['header'] = {}
    if (token) headers.Authorization = `Bearer ${token}`

    const apiRes = await uni.request<GenerateResponse>({
      url: `${BASE_URL}/api/marketing/generate-copy`,
      method: 'POST',
      header: headers,
      data: {
        productName: form.productName,
        keywords: form.keywords.split(',').map(k => k.trim()).filter(Boolean),
        toneOfVoice: form.tone
      }
    })
    if (apiRes.statusCode === 200) {
      result.value = apiRes.data
    } else {
      throw new Error(apiRes.data?.message || '生成失败')
    }
  } catch (err: any) {
    uni.showToast({ title: err.message || '请求失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.container {
  padding: 30rpx;
}
.form-section {
  background: #fff;
  border-radius: 16rpx;
  padding: 30rpx;
  margin-bottom: 30rpx;
}
.form-item {
  margin-bottom: 30rpx;
}
.label {
  display: block;
  margin-bottom: 12rpx;
  font-weight: 600;
  color: #333;
}
.input, .textarea {
  width: 100%;
  padding: 20rpx;
  border: 1px solid #e0e0e0;
  border-radius: 8rpx;
  font-size: 28rpx;
}
.textarea {
  min-height: 120rpx;
}
.radio-item {
  margin-right: 30rpx;
  font-size: 28rpx;
  display: inline-flex;
  align-items: center;
}
button {
  margin-top: 20rpx;
  background-color: #007AFF;
  color: #fff;
}
.result-section {
  background: #f9f9f9;
  border-radius: 16rpx;
  padding: 30rpx;
}
.result-title {
  font-size: 32rpx;
  font-weight: 700;
  margin-bottom: 20rpx;
  display: block;
}
.result-card {
  background: #fff;
  padding: 24rpx;
  border-radius: 12rpx;
  margin-bottom: 20rpx;
}
.result-label {
  font-weight: 600;
  color: #555;
  margin-bottom: 8rpx;
  display: block;
}
.result-content {
  color: #333;
  line-height: 1.6;
}
.result-content.description {
  white-space: pre-wrap;
}
</style>