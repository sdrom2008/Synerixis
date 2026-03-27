<template>
  <view class="container">
    <view class="form">
      <view class="form-item">
        <text class="label">商品标题</text>
        <textarea v-model="form.originalTitle" placeholder="当前商品标题" class="textarea" auto-height />
      </view>
      <view class="form-item">
        <text class="label">商品描述</text>
        <textarea v-model="form.originalDescription" placeholder="商品详情描述" class="textarea big" auto-height />
      </view>
      <view class="form-item">
        <text class="label">目标平台</text>
        <picker 
          :value="selectedIndex" 
          :range="platforms" 
          @change="onPlatformChange">
          <view class="picker">{{ form.targetPlatform || '请选择平台' }}</view>
        </picker>
      </view>
      <button type="primary" :loading="loading" @click="handleOptimize">开始优化</button>
    </view>

    <view class="result" v-if="result">
      <view class="card">
        <text class="card-title">优化后标题</text>
        <text class="text">{{ result.optimizedTitle }}</text>
      </view>
      <view class="card">
        <text class="card-title">优化描述</text>
        <rich-text :nodes="formatDesc(result.optimizedDescription)" class="desc"></rich-text>
      </view>
      <view class="card" v-if="result.marketingPlan">
        <text class="card-title">营销方案</text>
        <text class="text">短视频脚本：{{ result.marketingPlan.shortVideoScript || '无' }}</text>
        <text class="text">种草文案：{{ result.marketingPlan.plantingText || '无' }}</text>
        <text class="text">直播话术：{{ result.marketingPlan.liveScript || '无' }}</text>
      </view>
      <view class="card" v-if="result.imagePrompts?.length">
        <text class="card-title">图片生成提示词</text>
        <view v-for="(prompt, i) in result.imagePrompts" :key="i" class="prompt-item">{{ i+1}}. {{ prompt }}</view>
      </view>
    </view>
  </view>
</template>

<script setup lang="ts">
import { BASE_URL } from '@/utils/config.js';
import { reactive, ref } from 'vue'

interface OptimizeRequest {
  intent: string
  originalTitle?: string
  originalDescription?: string
  category?: string
  targetPlatform?: string
}

interface OptimizeResponse {
  optimizedTitle?: string
  optimizedDescription?: string
  imagePrompts?: string[]
  marketingPlan?: {
    shortVideoScript?: string
    plantingText?: string
    liveScript?: string
    keySellingPoints?: string[]
  }
}

const platforms = ['淘宝', '京东', '拼多多', '抖音', '小红书']

const form = reactive<OptimizeRequest>({
  intent: 'optimize',
  originalTitle: '',
  originalDescription: '',
  targetPlatform: ''
})

const selectedIndex = ref(0)

const onPlatformChange = (e: any) => {
  selectedIndex.value = Number(e.detail.value)
  form.targetPlatform = platforms[selectedIndex.value]
}

const loading = ref(false)
const result = ref<OptimizeResponse | null>(null)

const handleOptimize = async () => {
  if (!form.originalTitle?.trim()) {
    uni.showToast({ title: '请输入商品标题', icon: 'none' })
    return
  }
  loading.value = true
  try {
    const token = uni.getStorageSync('token')
    const headers: UniApp.RequestOptions['header'] = {}
    if (token) headers.Authorization = `Bearer ${token}`

    const apiRes = await uni.request<OptimizeResponse>({
      url: `${BASE_URL}/api/agent/optimizeproduct`,
      method: 'POST',
      header: headers,
      data: form
    })
    if (apiRes.statusCode === 200) {
      result.value = apiRes.data
    } else {
      throw new Error(apiRes.data?.message || '优化失败')
    }
  } catch (err: any) {
    uni.showToast({ title: err.message || '请求失败', icon: 'none' })
  } finally {
    loading.value = false
  }
}

const formatDesc = (desc?: string) => {
  if (!desc) return ''
  return desc.replace(/\n/g, '<br/>')
}
</script>

<style scoped>
.container { padding: 30rpx; }
.form { background: #fff; border-radius: 16rpx; padding: 30rpx; }
.form-item { margin-bottom: 30rpx; }
.label { display: block; margin-bottom: 12rpx; font-weight: 600; color: #333; }
.textarea { width: 100%; padding: 20rpx; border: 1px solid #e0e0e0; border-radius: 8rpx; font-size: 28rpx; min-height: 100rpx; }
.textarea.big { min-height: 200rpx; }
.picker { 
  padding: 20rpx; 
  border: 1px solid #e0e0e0; 
  border-radius: 8rpx; 
  font-size: 28rpx; 
  background: #fff;
}
button { margin-top: 20rpx; background-color: #007AFF; color: #fff; }

.result { margin-top: 30rpx; }
.card { background: #fff; border-radius: 16rpx; padding: 30rpx; margin-bottom: 20rpx; }
.card-title { font-size: 32rpx; font-weight: 700; margin-bottom: 16rpx; display: block; }
.text { font-size: 28rpx; line-height: 1.6; display: block; margin-bottom: 12rpx; }
.desc { line-height: 1.8; font-size: 28rpx; }
.prompt-item { font-size: 26rpx; color: #555; margin-bottom: 8rpx; }
</style>