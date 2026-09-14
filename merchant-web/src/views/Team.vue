<template>
  <div>
    <div class="page-head">
      <div>
        <h2 class="page-title">团队管理</h2>
        <p class="page-desc">添加坐席、调整角色与启用状态。普通 Agent 无权限访问本页。</p>
      </div>
      <el-button v-if="canManage" type="primary" @click="openCreate">添加坐席</el-button>
    </div>

    <el-alert
      v-if="!canManage"
      type="warning"
      show-icon
      :closable="false"
      title="无权限"
      description="仅商家或主管可管理团队。正在返回收件箱…"
      class="mb"
    />

    <el-card v-else shadow="never" class="sx-card" v-loading="loading">
      <el-table :data="members" stripe empty-text="暂无坐席，点击右上角添加">
        <el-table-column prop="name" label="姓名" min-width="120" />
        <el-table-column prop="email" label="邮箱" min-width="180" />
        <el-table-column label="角色" width="130">
          <template #default="{ row }">
            <el-tag size="small" :type="roleTag(row.role)">{{ row.role }}</el-tag>
          </template>
        </el-table-column>
        <el-table-column label="状态" width="100">
          <template #default="{ row }">
            <el-tag size="small" :type="row.isActive ? 'success' : 'info'">
              {{ row.isActive ? '启用' : '禁用' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column label="在线" width="90">
          <template #default="{ row }">
            <span>{{ row.isOnline ? '在线' : '离线' }}</span>
          </template>
        </el-table-column>
        <el-table-column label="最近登录" min-width="160">
          <template #default="{ row }">
            {{ formatTime(row.lastLoginAt) }}
          </template>
        </el-table-column>
        <el-table-column label="操作" width="280" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="openEdit(row)">改角色</el-button>
            <el-button link :type="row.isActive ? 'warning' : 'success'" @click="toggleActive(row)">
              {{ row.isActive ? '禁用' : '启用' }}
            </el-button>
            <el-button link type="danger" @click="openReset(row)">重置密码</el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="createVisible" title="添加坐席" width="480px" destroy-on-close>
      <el-form :model="createForm" label-position="top">
        <el-form-item label="邮箱" required>
          <el-input v-model="createForm.email" placeholder="login@shop.com" />
        </el-form-item>
        <el-form-item label="姓名" required>
          <el-input v-model="createForm.name" />
        </el-form-item>
        <el-form-item label="初始密码" required>
          <el-input v-model="createForm.password" type="password" show-password />
        </el-form-item>
        <el-form-item label="角色">
          <el-select v-model="createForm.role" style="width: 100%">
            <el-option label="Agent（普通坐席）" value="Agent" />
            <el-option label="Supervisor（主管）" value="Supervisor" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="createVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submitCreate">添加</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="editVisible" title="修改角色" width="400px" destroy-on-close>
      <el-form label-position="top">
        <el-form-item label="角色">
          <el-select v-model="editForm.role" style="width: 100%">
            <el-option label="Agent" value="Agent" />
            <el-option label="Supervisor" value="Supervisor" />
          </el-select>
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="editVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submitEdit">保存</el-button>
      </template>
    </el-dialog>

    <el-dialog v-model="resetVisible" title="重置密码" width="400px" destroy-on-close>
      <el-form label-position="top">
        <el-form-item label="新密码" required>
          <el-input v-model="resetPassword" type="password" show-password />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="resetVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="submitReset">确认重置</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  addTeamMember,
  getTeam,
  resetTeamMemberPassword,
  updateTeamMember,
  type TeamMember,
} from '@/api/seller'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const canManage = computed(() => auth.permissions.canManageTeam)

const loading = ref(false)
const saving = ref(false)
const members = ref<TeamMember[]>([])

const createVisible = ref(false)
const editVisible = ref(false)
const resetVisible = ref(false)
const createForm = reactive({ email: '', name: '', password: '', role: 'Agent' })
const editForm = reactive({ id: '', role: 'Agent' })
const resetTargetId = ref('')
const resetPassword = ref('')

function roleTag(role: string) {
  if (role === 'Supervisor') return 'warning'
  if (role === 'Admin') return 'danger'
  return 'info'
}

function formatTime(v?: string | null) {
  if (!v) return '—'
  try {
    return new Date(v).toLocaleString()
  } catch {
    return v
  }
}

function normalizeList(data: unknown): TeamMember[] {
  if (Array.isArray(data)) return data as TeamMember[]
  const obj = data as { items?: TeamMember[] }
  return obj?.items || []
}

async function load() {
  if (!canManage.value) return
  loading.value = true
  try {
    const data = await getTeam()
    members.value = normalizeList(data)
  } catch (e: unknown) {
    const msg =
      (e as { response?: { data?: { message?: string } | string } })?.response?.data
    ElMessage.error(typeof msg === 'string' ? msg : msg?.message || '加载团队失败')
  } finally {
    loading.value = false
  }
}

function openCreate() {
  createForm.email = ''
  createForm.name = ''
  createForm.password = ''
  createForm.role = 'Agent'
  createVisible.value = true
}

async function submitCreate() {
  if (!createForm.email.trim() || !createForm.name.trim() || !createForm.password) {
    ElMessage.warning('请填写邮箱、姓名与初始密码')
    return
  }
  saving.value = true
  try {
    await addTeamMember({
      email: createForm.email.trim(),
      name: createForm.name.trim(),
      password: createForm.password,
      role: createForm.role,
    })
    ElMessage.success('坐席已添加')
    createVisible.value = false
    await load()
  } catch (e: unknown) {
    const msg =
      (e as { response?: { data?: { message?: string } | string } })?.response?.data
    ElMessage.error(typeof msg === 'string' ? msg : msg?.message || '添加失败')
  } finally {
    saving.value = false
  }
}

function openEdit(row: TeamMember) {
  editForm.id = row.id
  editForm.role = row.role === 'Admin' ? 'Supervisor' : row.role || 'Agent'
  editVisible.value = true
}

async function submitEdit() {
  saving.value = true
  try {
    await updateTeamMember(editForm.id, { role: editForm.role })
    ElMessage.success('角色已更新')
    editVisible.value = false
    await load()
  } catch (e: unknown) {
    ElMessage.error((e as Error)?.message || '更新失败')
  } finally {
    saving.value = false
  }
}

async function toggleActive(row: TeamMember) {
  const next = !row.isActive
  try {
    await ElMessageBox.confirm(
      next ? `启用坐席「${row.name}」？` : `禁用坐席「${row.name}」？禁用后无法登录。`,
      '确认',
      { type: 'warning' },
    )
  } catch {
    return
  }
  try {
    await updateTeamMember(row.id, { isActive: next })
    ElMessage.success(next ? '已启用' : '已禁用')
    await load()
  } catch (e: unknown) {
    ElMessage.error((e as Error)?.message || '操作失败')
  }
}

function openReset(row: TeamMember) {
  resetTargetId.value = row.id
  resetPassword.value = ''
  resetVisible.value = true
}

async function submitReset() {
  if (!resetPassword.value) {
    ElMessage.warning('请输入新密码')
    return
  }
  saving.value = true
  try {
    await resetTeamMemberPassword(resetTargetId.value, resetPassword.value)
    ElMessage.success('密码已重置')
    resetVisible.value = false
  } catch (e: unknown) {
    ElMessage.error((e as Error)?.message || '重置失败')
  } finally {
    saving.value = false
  }
}

onMounted(async () => {
  if (!canManage.value) {
    setTimeout(() => router.replace({ name: 'inbox' }), 1200)
    return
  }
  await load()
})
</script>

<style scoped lang="scss">
.page-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 16px;
  margin-bottom: 16px;
}
.page-title {
  margin: 0;
  font-size: 20px;
}
.page-desc {
  margin: 6px 0 0;
  color: var(--sx-muted);
  font-size: 13px;
}
.mb {
  margin-bottom: 16px;
}
</style>
