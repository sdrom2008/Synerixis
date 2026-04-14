<template>
  <view class="team-page">
    <view class="header">
      <text class="title">{{ currentLang === 'zh-CN' ? '团队管理' : 'Team Management' }}</text>
      <button class="new-btn" @tap="openAddModal">+ {{ currentLang === 'zh-CN' ? '添加成员' : 'Add' }}</button>
    </view>

    <scroll-view scroll-y class="list">
      <view v-for="agent in teamMembers" :key="agent.id" class="agent-item">
        <view class="agent-info">
          <text class="name">{{ agent.name }}</text>
          <text class="email">{{ agent.email }}</text>
        </view>
        <view class="agent-status">
          <text class="role" :class="agent.role === 1 ? 'supervisor' : 'agent'">
            {{ getRoleName(agent.role) }}
          </text>
          <text class="status" :class="agent.isActive ? 'active' : 'inactive'">
            {{ agent.isActive ? (currentLang === 'zh-CN' ? '正常' : 'Active') : (currentLang === 'zh-CN' ? '已禁用' : 'Disabled') }}
          </text>
        </view>
        <view class="actions">
          <text class="action-btn text-danger" @tap="removeMember(agent.id)">
            {{ currentLang === 'zh-CN' ? '移除' : 'Remove' }}
          </text>
        </view>
      </view>

      <view v-if="!teamMembers.length && !loading" class="empty">
        <text>{{ currentLang === 'zh-CN' ? '暂无团队成员' : 'No team members' }}</text>
      </view>
    </scroll-view>

    <!-- 添加弹窗 (简易实现，实际中可用 uni-popup) -->
    <view v-if="showAddModal" class="modal-mask">
      <view class="modal-content">
        <text class="modal-title">{{ currentLang === 'zh-CN' ? '添加客服成员' : 'Add Team Member' }}</text>
        <input v-model="form.name" :placeholder="currentLang === 'zh-CN' ? '姓名' : 'Name'" class="modal-input" />
        <input v-model="form.email" :placeholder="currentLang === 'zh-CN' ? '登录邮箱' : 'Login Email'" class="modal-input" />
        <input v-model="form.password" :placeholder="currentLang === 'zh-CN' ? '初始密码' : 'Initial Password'" type="password" class="modal-input" />
        
        <view class="role-selector">
          <text>{{ currentLang === 'zh-CN' ? '角色' : 'Role' }}: </text>
          <radio-group @change="onRoleChange">
            <label class="radio-label">
              <radio value="0" :checked="form.role === 0" color="#3b82f6" />
              <text>{{ currentLang === 'zh-CN' ? '人工客服' : 'Agent' }}</text>
            </label>
            <label class="radio-label">
              <radio value="1" :checked="form.role === 1" color="#3b82f6" />
              <text>{{ currentLang === 'zh-CN' ? '客服主管' : 'Supervisor' }}</text>
            </label>
          </radio-group>
        </view>

        <view class="modal-actions">
          <button class="modal-btn cancel" @tap="closeAddModal">{{ currentLang === 'zh-CN' ? '取消' : 'Cancel' }}</button>
          <button class="modal-btn confirm" @tap="submitAddMember" :loading="submitLoading">{{ currentLang === 'zh-CN' ? '确认添加' : 'Add' }}</button>
        </view>
      </view>
    </view>
  </view>
</template>

<script setup>
import { ref, onMounted } from 'vue'
import { request } from '@/utils/request.js'
import { getLanguage } from '@/utils/i18n.js'

const currentLang = ref(getLanguage())
const teamMembers = ref([])
const loading = ref(false)

console.log('teamMembers:', teamMembers.value);

const showAddModal = ref(false)
const submitLoading = ref(false)
const form = ref({
  name: '',
  email: '',
  password: '',
  role: 0
})

const fetchTeam = async () => {
  loading.value = true;
  try {
    const res = await request({ url: '/api/seller/team', method: 'GET' });
    console.log('Fetched team members:', res);
    teamMembers.value = res || [];
  } catch (err) {
    console.error(err);
  } finally {
    loading.value = false;
  }
}

const getRoleName = (role) => {
  if (role === 1) return currentLang.value === 'zh-CN' ? '客服主管' : 'Supervisor';
  return currentLang.value === 'zh-CN' ? '人工客服' : 'Agent';
}

console.log('getRoleName:', getRoleName);

const openAddModal = () => {
  form.value = { name: '', email: '', password: '', role: 0 };
  showAddModal.value = true;
}

console.log('openAddModal');

const closeAddModal = () => {
  showAddModal.value = false;
}

console.log('closeAddModal');

const onRoleChange = (e) => {
  form.value.role = parseInt(e.detail.value);
}

console.log('onRoleChange');

const submitAddMember = async () => {
  if (!form.value.name || !form.value.email || !form.value.password) {
    uni.showToast({ title: currentLang.value === 'zh-CN' ? '请填写完整信息' : 'Please fill all fields', icon: 'none' });
    return;
  }

  submitLoading.value = true;
  try {
    await request({
      url: '/api/seller/team',
      method: 'POST',
      data: {
        Name: form.value.name,
        Email: form.value.email,
        Password: form.value.password,
        Role: form.value.role
      }
    });
    uni.showToast({ title: currentLang.value === 'zh-CN' ? '添加成功' : 'Added successfully', icon: 'success' });
    closeAddModal();
    fetchTeam();
  } catch (err) {
    // 错误在 request 拦截处理
    console.error(err);
  } finally {
    submitLoading.value = false;
  }
}

console.log('submitAddMember');

const removeMember = async (id) => {
  uni.showModal({
    title: currentLang.value === 'zh-CN' ? '确认移除' : 'Confirm',
    content: currentLang.value === 'zh-CN' ? '确定要移除该成员吗？' : 'Remove this member?',
    success: async (res) => {
      if (res.confirm) {
        try {
          await request({
            url: `/api/seller/team/${id}`,
            method: 'DELETE'
          });
          uni.showToast({ title: currentLang.value === 'zh-CN' ? '移除成功' : 'Removed', icon: 'success' });
          fetchTeam();
        } catch (e) {
          console.error(e);
        }
      }
    }
  });
}

console.log('removeMember');

onMounted(() => {
  fetchTeam();
});
</script>

<style>
.team-page {
  height: 100vh;
  background: #0a0e1a;
  display: flex;
  flex-direction: column;
}

.header {
  padding: 40rpx;
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: #1e293b;
}

.title {
  font-size: 40rpx;
  font-weight: bold;
  color: #f8fafc;
}

.new-btn {
  background: #3b82f6;
  color: white;
  font-size: 28rpx;
  border-radius: 40rpx;
  padding: 0 40rpx;
  height: 64rpx;
  line-height: 64rpx;
  margin: 0;
}

.list {
  flex: 1;
  padding: 20rpx 40rpx;
}

.agent-item {
  background: #1e293b;
  border-radius: 24rpx;
  padding: 32rpx;
  margin-bottom: 24rpx;
  display: flex;
  flex-direction: column;
}

.agent-info {
  display: flex;
  justify-content: space-between;
  margin-bottom: 16rpx;
}

.name {
  font-size: 32rpx;
  font-weight: bold;
  color: #f8fafc;
}

.email {
  font-size: 28rpx;
  color: #94a3b8;
}

.agent-status {
  display: flex;
  align-items: center;
  margin-bottom: 20rpx;
}

.role {
  font-size: 24rpx;
  padding: 4rpx 16rpx;
  border-radius: 8rpx;
  margin-right: 16rpx;
}

.role.supervisor {
  background: #f59e0b;
  color: #fff;
}

.role.agent {
  background: #22c55e;
  color: #fff;
}

.status.active {
  color: #22c55e;
  font-size: 24rpx;
}

.status.inactive {
  color: #ef4444;
  font-size: 24rpx;
}

.actions {
  display: flex;
  justify-content: flex-end;
  border-top: 1px solid #334155;
  padding-top: 16rpx;
}

.action-btn {
  font-size: 28rpx;
  padding: 8rpx 24rpx;
}

.text-danger {
  color: #ef4444;
}

.empty {
  text-align: center;
  color: #64748b;
  padding: 80rpx 0;
}

.modal-mask {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background: rgba(0,0,0,0.6);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 999;
}

.modal-content {
  width: 600rpx;
  background: #1e293b;
  border-radius: 24rpx;
  padding: 40rpx;
  display: flex;
  flex-direction: column;
}

.modal-title {
  font-size: 36rpx;
  color: white;
  margin-bottom: 40rpx;
  text-align: center;
  font-weight: bold;
}

.modal-input {
  background: #0f172a;
  border: 1px solid #334155;
  border-radius: 12rpx;
  padding: 20rpx;
  color: white;
  margin-bottom: 24rpx;
  font-size: 28rpx;
}

.role-selector {
  color: white;
  font-size: 28rpx;
  margin-bottom: 40rpx;
}

.radio-label {
  margin-right: 32rpx;
  display: inline-flex;
  align-items: center;
}

.modal-actions {
  display: flex;
  justify-content: space-between;
}

.modal-btn {
  flex: 1;
  height: 80rpx;
  line-height: 80rpx;
  border-radius: 40rpx;
  font-size: 30rpx;
  margin: 0 10rpx;
}

.cancel {
  background: #334155;
  color: white;
}

.confirm {
  background: #3b82f6;
  color: white;
}
</style>