<template>
  <div>
    <h2 class="page-title">快捷回复</h2>
    <p class="page-desc">
      店铺级话术模板 · CRUD <code>/api/merchant/quick-replies</code>。收件箱草稿区可一键插入。
    </p>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <div class="toolbar">
        <el-button type="primary" @click="openCreate">新建快捷回复</el-button>
        <el-button @click="load" :loading="loading">刷新</el-button>
      </div>

      <el-table :data="items" stripe empty-text="暂无快捷回复">
        <el-table-column prop="title" label="标题" min-width="140" />
        <el-table-column prop="content" label="内容" min-width="240" show-overflow-tooltip />
        <el-table-column prop="category" label="分类" width="100" />
        <el-table-column prop="scope" label="范围" width="90" />
        <el-table-column label="启用" width="80">
          <template #default="{ row }">
            <el-tag :type="row.isActive ? 'success' : 'info'" size="small">
              {{ row.isActive ? '是' : '否' }}
            </el-tag>
          </template>
        </el-table-column>
        <el-table-column prop="sortOrder" label="排序" width="70" />
        <el-table-column label="操作" width="160" fixed="right">
          <template #default="{ row }">
            <el-button link type="primary" @click="openEdit(row)">编辑</el-button>
            <el-button
              link
              type="danger"
              :disabled="row.scope === 'Global'"
              @click="onDelete(row)"
            >
              删除
            </el-button>
          </template>
        </el-table-column>
      </el-table>
    </el-card>

    <el-dialog v-model="dialogVisible" :title="editingId ? '编辑快捷回复' : '新建快捷回复'" width="520px">
      <el-form label-width="80px">
        <el-form-item label="标题" required>
          <el-input v-model="form.title" maxlength="80" />
        </el-form-item>
        <el-form-item label="内容" required>
          <el-input v-model="form.content" type="textarea" :rows="5" />
        </el-form-item>
        <el-form-item label="分类">
          <el-select v-model="form.category" style="width: 100%">
            <el-option v-for="c in categories" :key="c" :label="c" :value="c" />
          </el-select>
        </el-form-item>
        <el-form-item label="关键词">
          <el-input v-model="form.keywords" placeholder="逗号分隔，可选" />
        </el-form-item>
        <el-form-item label="排序">
          <el-input-number v-model="form.sortOrder" :min="0" :max="9999" />
        </el-form-item>
        <el-form-item label="启用">
          <el-switch v-model="form.isActive" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">取消</el-button>
        <el-button type="primary" :loading="saving" @click="onSave">保存</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  createQuickReply,
  deleteQuickReply,
  listQuickReplies,
  updateQuickReply,
  type QuickReplyItem,
} from '@/api/merchant'

const loading = ref(false)
const saving = ref(false)
const items = ref<QuickReplyItem[]>([])
const dialogVisible = ref(false)
const editingId = ref<string | null>(null)

const categories = ['General', 'PreSale', 'AfterSale', 'Logistics', 'Complaint', 'Payment']

const form = reactive({
  title: '',
  content: '',
  category: 'General',
  keywords: '',
  sortOrder: 0,
  isActive: true,
})

async function load() {
  loading.value = true
  try {
    const res = await listQuickReplies()
    items.value = res.items || []
  } catch {
    ElMessage.error('加载快捷回复失败')
    items.value = []
  } finally {
    loading.value = false
  }
}

function openCreate() {
  editingId.value = null
  form.title = ''
  form.content = ''
  form.category = 'General'
  form.keywords = ''
  form.sortOrder = 0
  form.isActive = true
  dialogVisible.value = true
}

function openEdit(row: QuickReplyItem) {
  editingId.value = row.id
  form.title = row.title || ''
  form.content = row.content || ''
  form.category = row.category || 'General'
  form.keywords = row.keywords || ''
  form.sortOrder = row.sortOrder ?? 0
  form.isActive = row.isActive !== false
  dialogVisible.value = true
}

async function onSave() {
  if (!form.title.trim() || !form.content.trim()) {
    ElMessage.warning('标题与内容必填')
    return
  }
  saving.value = true
  try {
    const payload = {
      title: form.title.trim(),
      content: form.content.trim(),
      category: form.category,
      keywords: form.keywords.trim() || undefined,
      sortOrder: form.sortOrder,
      isActive: form.isActive,
    }
    if (editingId.value) {
      await updateQuickReply(editingId.value, payload)
      ElMessage.success('已更新')
    } else {
      await createQuickReply(payload)
      ElMessage.success('已创建')
    }
    dialogVisible.value = false
    await load()
  } catch (e: unknown) {
    const msg = (e as { response?: { data?: { message?: string } } })?.response?.data?.message
    ElMessage.error(msg || '保存失败')
  } finally {
    saving.value = false
  }
}

async function onDelete(row: QuickReplyItem) {
  try {
    await ElMessageBox.confirm(`确定删除「${row.title}」？`, '删除快捷回复', { type: 'warning' })
    await deleteQuickReply(row.id)
    ElMessage.success('已删除')
    await load()
  } catch {
    /* cancel */
  }
}

onMounted(load)
</script>

<style scoped lang="scss">
.toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}
</style>
