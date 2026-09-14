<template>
  <div>
    <h2 class="page-title">系统设置</h2>
    <p class="page-desc">只读配置说明 · <code>GET /api/admin/settings</code></p>

    <el-card shadow="never" v-loading="loading">
      <el-descriptions v-if="data" :column="1" border>
        <el-descriptions-item label="环境">{{ data.environment || '—' }}</el-descriptions-item>
        <el-descriptions-item label="产品定位">{{ data.productPositioning || '—' }}</el-descriptions-item>
        <el-descriptions-item label="默认出站">{{ data.defaultOutboundMode || '—' }}</el-descriptions-item>
        <el-descriptions-item label="Admin 鉴权">{{ data.adminAuth || '—' }}</el-descriptions-item>
        <el-descriptions-item label="Admin 前端">
          {{ (data.frontends as Record<string, string>)?.adminConsole || '—' }}
        </el-descriptions-item>
        <el-descriptions-item label="商家前端">
          {{ (data.frontends as Record<string, string>)?.merchantWeb || '—' }}
        </el-descriptions-item>
        <el-descriptions-item label="特性">
          <pre class="feat">{{ JSON.stringify(data.features, null, 2) }}</pre>
        </el-descriptions-item>
      </el-descriptions>
      <el-empty v-else-if="!loading" description="暂无配置说明" />
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { getSettings } from '@/api/admin'

const loading = ref(false)
const data = ref<Record<string, unknown> | null>(null)

onMounted(async () => {
  loading.value = true
  try {
    data.value = await getSettings()
  } catch {
    ElMessage.error('加载设置失败')
    data.value = null
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.feat {
  margin: 0;
  font-size: 12px;
  white-space: pre-wrap;
}
</style>
