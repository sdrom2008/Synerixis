<template>
  <div class="login-page">
    <el-card class="login-card" shadow="hover">
      <div class="brand">
        <div class="logo">S</div>
        <h1>Synerixis 桌面工作台</h1>
        <p>跨境多店客服 · AI 起草 · 人工审核发送</p>
      </div>

      <el-tabs v-model="tab">
        <el-tab-pane label="商家手机登录" name="phone">
          <el-form :model="phoneForm" @submit.prevent="onPhoneLogin" label-position="top">
            <el-form-item label="手机号">
              <el-input v-model="phoneForm.phone" placeholder="不含国家码" maxlength="20" autocomplete="tel" />
            </el-form-item>
            <el-form-item label="验证码">
              <el-input v-model="phoneForm.code" placeholder="开发环境可用 123456" maxlength="8" />
            </el-form-item>
            <el-button type="primary" class="submit" native-type="submit" :loading="loading" round>
              登录
            </el-button>
          </el-form>
        </el-tab-pane>

        <el-tab-pane label="坐席邮箱登录" name="agent">
          <el-form :model="agentForm" @submit.prevent="onAgentLogin" label-position="top">
            <el-form-item label="邮箱">
              <el-input v-model="agentForm.email" placeholder="agent@example.com" autocomplete="username" />
            </el-form-item>
            <el-form-item label="密码">
              <el-input
                v-model="agentForm.password"
                type="password"
                show-password
                placeholder="初始密码由商家设置"
                autocomplete="current-password"
              />
            </el-form-item>
            <el-button type="primary" class="submit" native-type="submit" :loading="loading" round>
              坐席登录
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
                placeholder="粘贴已有商家/坐席 JWT（Bearer 内容）"
              />
            </el-form-item>
            <el-button type="primary" class="submit" native-type="submit" :loading="loading" round>
              使用 Token 进入
            </el-button>
          </el-form>
        </el-tab-pane>
      </el-tabs>

      <p class="hint">
        API 基址：{{ apiHint }} · 商家主入口为 PC 桌面工作台 merchant-web；禁止全自动 chatbot。
      </p>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { phoneLogin, agentLogin } from '@/api/auth'
import { getApiBaseUrl } from '@/api/http'
import { useAuthStore, type MerchantProfile } from '@/stores/auth'

const router = useRouter()
const route = useRoute()
const auth = useAuthStore()
const loading = ref(false)
const tab = ref('phone')
const tokenInput = ref('')
const phoneForm = reactive({ phone: '', code: '123456' })
const agentForm = reactive({ email: '', password: '' })

const apiHint = computed(() => getApiBaseUrl() || '同源 /api（Vite 代理）')

async function enter(token: string, profile?: MerchantProfile) {
  auth.setSession(token, profile)
  const redirect = (route.query.redirect as string) || '/inbox'
  await router.replace(redirect)
}

function errText(e: unknown): string {
  const msg = (e as { response?: { data?: string | { message?: string } } })?.response?.data
  if (typeof msg === 'string') return msg
  return msg?.message || (e as Error)?.message || '登录失败'
}

async function onPhoneLogin() {
  if (!phoneForm.phone.trim() || !phoneForm.code.trim()) {
    ElMessage.warning('请输入手机号和验证码')
    return
  }
  loading.value = true
  try {
    const data = await phoneLogin({ phone: phoneForm.phone.trim(), code: phoneForm.code.trim() })
    if (!data?.token) {
      ElMessage.error('登录失败：未返回 token')
      return
    }
    await enter(data.token, {
      userId: data.userId,
      nickname: data.nickname || data.name,
      name: data.name || data.nickname,
      userType: data.userType || 'Seller',
      role: data.role || data.userType,
      shopId: data.shopId,
      subscriptionLevel: data.subscriptionLevel,
    })
  } catch (e: unknown) {
    ElMessage.error(errText(e))
  } finally {
    loading.value = false
  }
}

async function onAgentLogin() {
  if (!agentForm.email.trim() || !agentForm.password) {
    ElMessage.warning('请输入邮箱和密码')
    return
  }
  loading.value = true
  try {
    const data = await agentLogin({
      email: agentForm.email.trim(),
      password: agentForm.password,
    })
    if (!data?.token) {
      ElMessage.error('登录失败：未返回 token')
      return
    }
    await enter(data.token, {
      userId: data.userId || data.agentId,
      name: data.name,
      nickname: data.nickname || data.name,
      userType: data.userType || data.role || 'Agent',
      role: data.role || data.userType,
      shopId: data.shopId,
    })
  } catch (e: unknown) {
    ElMessage.error(errText(e))
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
    await enter(t, { nickname: 'Token 登录', userType: 'Seller' })
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
  max-width: 440px;
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
