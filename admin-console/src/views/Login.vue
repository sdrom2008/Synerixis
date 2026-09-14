<template>
  <div class="login-page">
    <el-card class="login-card" shadow="hover">
      <div class="brand">
        <div class="logo">S</div>
        <h1>Synerixis Admin</h1>
        <p>跨境电商 AI 客服 · 平台运营台</p>
      </div>

      <el-alert
        v-if="isDev"
        class="demo-alert"
        type="success"
        :closable="false"
        show-icon
        title="本地演示账号（seed-demo 后）"
      >
        <template #default>
          <div class="demo-creds">
            <div><strong>邮箱</strong> <code>admin@test.com</code></div>
            <div><strong>密码</strong> <code>Agent123!</code></div>
            <div class="demo-actions">
              <el-button size="small" @click="fillDemo">填入演示账号</el-button>
              <el-button size="small" type="warning" plain :loading="seeding" @click="onSeed">
                加载演示数据
              </el-button>
            </div>
          </div>
        </template>
      </el-alert>

      <el-form :model="form" @submit.prevent="onSubmit">
        <el-form-item label="邮箱">
          <el-input v-model="form.email" placeholder="Admin 邮箱" autocomplete="username" />
        </el-form-item>
        <el-form-item label="密码">
          <el-input
            v-model="form.password"
            type="password"
            show-password
            placeholder="密码"
            autocomplete="current-password"
          />
        </el-form-item>
        <el-button type="primary" class="submit" native-type="submit" :loading="loading" round>
          登录
        </el-button>
      </el-form>
      <p class="hint">
        使用 <code>POST /api/auth/agent-login</code>，账号须为
        <strong>AgentRole.Admin</strong>。<br />
        成功后进入 <strong>概览 Dashboard</strong>。<br />
        演示数据：<code>POST /api/dev/seed-demo</code>（仅 Development）。详见
        <code>docs/LOCAL_DEMO.md</code>。
      </p>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { agentLogin, seedDemo } from '@/api/admin'

const router = useRouter()
const route = useRoute()
const loading = ref(false)
const seeding = ref(false)
const isDev = import.meta.env.DEV
const form = reactive({
  email: isDev ? 'admin@test.com' : '',
  password: isDev ? 'Agent123!' : '',
})

function fillDemo() {
  form.email = 'admin@test.com'
  form.password = 'Agent123!'
  ElMessage.success('已填入演示账号')
}

async function onSeed() {
  seeding.value = true
  try {
    const res = await seedDemo()
    ElMessage.success(res.message || '演示数据已就绪，可用 admin@test.com 登录')
    fillDemo()
  } catch (e: unknown) {
    const msg =
      (e as { response?: { data?: { message?: string } | string } })?.response?.data
    const text =
      typeof msg === 'string'
        ? msg
        : (msg as { message?: string })?.message || (e as Error)?.message || '加载演示数据失败'
    ElMessage.error(text)
  } finally {
    seeding.value = false
  }
}

async function onSubmit() {
  if (!form.email.trim() || !form.password.trim()) {
    ElMessage.warning('请输入邮箱和密码')
    return
  }
  loading.value = true
  try {
    const res = await agentLogin(form.email.trim(), form.password)
    const role = String(res.role || res.userType || '')
    if (role.toLowerCase() !== 'admin') {
      ElMessage.error(`需要 Admin 角色，当前为 ${role || '未知'}`)
      return
    }
    if (!res.token) {
      ElMessage.error('登录失败：未返回 token')
      return
    }
    localStorage.setItem('sx_admin_token', res.token)
    localStorage.setItem('sx_admin_name', res.name || res.nickname || form.email)
    ElMessage.success('登录成功')
    const redirect = (route.query.redirect as string) || '/dashboard'
    await router.replace(redirect.startsWith('/') ? redirect : '/dashboard')
  } catch (e: unknown) {
    const data = (e as { response?: { data?: unknown } })?.response?.data
    const msg =
      typeof data === 'string'
        ? data
        : (data as { message?: string })?.message || (e as Error)?.message || '登录失败'
    ElMessage.error(typeof msg === 'string' ? msg : '登录失败')
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
    radial-gradient(ellipse at top left, rgba(37, 99, 235, 0.18), transparent 50%),
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
  margin-bottom: 20px;
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
  font-size: 22px;
}
p {
  margin: 6px 0 0;
  color: #64748b;
  font-size: 13px;
}
.demo-alert {
  margin-bottom: 16px;
}
.demo-creds {
  font-size: 13px;
  line-height: 1.7;
  code {
    font-size: 12px;
  }
}
.demo-actions {
  margin-top: 8px;
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.submit {
  width: 100%;
  margin-top: 8px;
}
.hint {
  margin-top: 16px;
  font-size: 12px;
  color: #94a3b8;
  text-align: left;
  line-height: 1.5;
  code {
    font-size: 11px;
  }
}
</style>
