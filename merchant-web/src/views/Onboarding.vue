<template>
  <div>
    <h2 class="page-title">上手指南</h2>
    <p class="page-desc">
      清单来自 <code>GET /api/merchant/onboarding</code> 真实状态；完成绑店与团队配置后即可在收件箱审发草稿。
    </p>

    <el-card shadow="never" class="sx-card" v-loading="loading">
      <template #header>
        <div class="card-head">
          <span>开通清单</span>
          <el-tag v-if="data?.complete" type="success" size="small" effect="plain">全部完成</el-tag>
          <el-tag v-else type="info" size="small" effect="plain">
            {{ data?.doneCount ?? 0 }} / {{ data?.total ?? 5 }}
          </el-tag>
        </div>
      </template>

      <el-progress
        :percentage="percent"
        :stroke-width="10"
        style="margin-bottom: 20px"
        :status="data?.complete ? 'success' : undefined"
      />

      <div class="list">
        <div v-for="item in data?.items || []" :key="item.id" class="row">
          <div class="left">
            <el-icon :size="20" :color="item.done ? '#16a34a' : '#94a3b8'">
              <CircleCheckFilled v-if="item.done" />
              <CircleClose v-else />
            </el-icon>
            <div>
              <div class="title">{{ item.title }}</div>
              <div class="hint">{{ item.hint }}</div>
            </div>
          </div>
          <el-button
            v-if="!item.done && item.link"
            type="primary"
            link
            @click="$router.push(item.link)"
          >
            去完成
          </el-button>
          <el-tag v-else-if="item.done" size="small" type="success" effect="plain">已完成</el-tag>
        </div>
      </div>

      <el-empty v-if="!loading && !data" description="暂无法加载上手状态" />
    </el-card>

    <el-card shadow="never" class="sx-card" style="margin-top: 16px">
      <template #header><span>建议顺序</span></template>
      <ol class="tips">
        <li>店铺绑定：完成 Shopee OAuth，确认 Token 状态为 ok。</li>
        <li>团队：至少添加一名坐席（Agent），便于收件箱分配 / 认领。</li>
        <li>AI 设置：确认营业时间与 SLA 小时 / 告警阈值。</li>
        <li>收件箱：等待 Webhook 入站，审发 AI 草稿（默认 DraftFirst）。</li>
      </ol>
      <el-button type="primary" @click="$router.push('/inbox')">进入收件箱</el-button>
    </el-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { CircleCheckFilled, CircleClose } from '@element-plus/icons-vue'
import { getOnboarding, type OnboardingResult } from '@/api/merchant'

const loading = ref(false)
const data = ref<OnboardingResult | null>(null)

const percent = computed(() => {
  if (!data.value?.total) return 0
  return Math.round(((data.value.doneCount || 0) / data.value.total) * 100)
})

onMounted(async () => {
  loading.value = true
  try {
    data.value = await getOnboarding()
  } catch (e: unknown) {
    const msg = e instanceof Error ? e.message : '加载失败'
    ElMessage.error(msg)
  } finally {
    loading.value = false
  }
})
</script>

<style scoped lang="scss">
.card-head {
  display: flex;
  align-items: center;
  gap: 10px;
}
.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 12px 0;
  border-bottom: 1px solid var(--sx-border, #e2e8f0);
  &:last-child {
    border-bottom: none;
  }
}
.left {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}
.title {
  font-weight: 600;
  font-size: 14px;
}
.hint {
  font-size: 12px;
  color: var(--sx-muted, #64748b);
  margin-top: 2px;
}
.tips {
  margin: 0 0 16px;
  padding-left: 18px;
  color: var(--sx-muted, #64748b);
  line-height: 1.7;
  font-size: 13px;
}
.page-title {
  margin: 0 0 4px;
}
.page-desc {
  color: var(--sx-muted, #64748b);
  font-size: 13px;
  margin-bottom: 16px;
  code {
    font-size: 12px;
  }
}
</style>
