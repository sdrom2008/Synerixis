<template>
  <div class="login-page">
    <el-card class="login-card" shadow="hover">
      <div class="brand">
        <div class="logo">S</div>
        <h1>Synerixis 商家工作台</h1>
        <p>跨境多店客服 · AI 起草 · 人工审核发送</p>
      </div>

      <el-tabs v-model="tab">
        <el-tab-pane label="手机号登录" name="phone">
          <el-form :model="form" @submit.prevent="onPhoneLogin" label-position="top">
            <el-form-item label="手机号">
              <el-input v-model="form.phone" placeholder="不含国家码" maxlength="20" autocomplete="tel" />
            </el-form-item>
            <el-form-item label="验证码">
              <el-input v-model="form.code" placeholder="开发环境可用 123456" maxlength="8" />
            </el-form-item>
            <el-button type="primary" class="submit" native-type="submit" :loading="loading" round>
              登录
            </el-button>
          </el-form>
        </el-tab-pane>
        <el-tab-pane label="粘贴 Token" name="token">
          <el-form @submit.prevent="onTokenLogin" label-position="top">
            <el-form-item label="JWT">
              <el-input
                v-model="tokenInput"
                type="textarea"
                :rows="4"
                placeholder="粘贴已有商家 JWT（Bearer 内容）"
              />
            </el-form-item>
            <el-button type="primary" class="submit" native-type="submit" :loading="loading" round>
              使用 Token 进入
            </el-button>
          </el-form>
        </el-tab-pane>
      </el-tabs>

      <p class="hint">
        API 基址：{{ apiHint }} · 与移动端 frontend/ 共用商家 JWT，互不影响。
      </p>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { phoneLogin } from '@/api/auth'
import { getApiBaseUrl } from '@/api/http'
import { useAuthStore } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const loading = ref(false)
const tab = ref('phone')
const tokenInput = ref('')
const form = reactive({ phone: '', code: '123456' })

const apiHint = computed(() => getApiBaseUrl() || '同源 /api（Vite 代理）')

async function enter(token: string, profile?: { userId?: string; nickname?: string; userType?: string }) {
  auth.setSession(token, profile)
  const redirect = (route.query.redirect as string) || '/inbox'
  await router.replace(redirect)
}

async function onPhoneLogin() {
  if (!form.phone.trim() || !form.code.trim()) {
    ElMessage.warning('请输入手机号和验证码')
    return
  }
  loading.value = true
  try {
    const data = await phoneLogin({ phone: form.phone.trim(), code: form.code.trim() })
    if (!data?.token) {
      ElMessage.error('登录失败：未返回 token')
      return
    }
    await enter(data.token, {
      userId: data.userId,
      nickname: data.nickname,
      userType: data.userType,
    })
  } catch (e: unknown) {
    const msg =
      (e as { response?: { data?: string | { message?: string } } })?.response?.data
    const text =
      typeof msg === 'string' ? msg : msg?.message || (e as Error)?.message || '登录失败'
    ElMessage.error(text)
  } finally {
    loading.value = false
  }
}

async function onTokenLogin() {
  const t = tokenInput.value.trim().replace(/^Bearer\s+/i, '')
  if (!t) {
    ElMessage.warning('请粘贴 JWT')
    return
  }
  loading.value = true
  try {
    await enter(t, { nickname: 'Token 登录' })
  } finally {
    loading.value = false
  }
}
</script>

<style scoped lang="scss">
.login-page {
  min-height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  background:
    radial-gradient(ellipse at top left, rgba(37, 99, 235, 0.16), transparent 50%),
    linear-gradient(160deg, #0f172a, #1e293b);
  padding: 24px;
}
.login-card {
  width: 100%;
  max-width: 420px;
  border-radius: 16px;
}
.brand {
  text-align: center;
  margin-bottom: 8px;
}
.logo {
  width: 48px;
  height: 48px;
  margin: 0 auto 12px;
  border-radius: 12px;
  background: #2563eb;
  color: #fff;
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 20px;
}
h1 {
  margin: 0;
  font-size: 20px;
}
p {
  margin: 6px 0 0;
  color: #64748b;
  font-size: 13px;
}
.submit {
  width: 100%;
  margin-top: 4px;
}
.hint {
  margin-top: 16px;
  font-size: 12px;
  color: #94a3b8;
  text-align: center;
  line-height: 1.5;
}
</style>
