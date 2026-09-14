<template>
  <div ref="el" class="chart" />
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useThemeStore } from '@/stores/theme'

echarts.use([LineChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer])

const el = ref<HTMLDivElement | null>(null)
const theme = useThemeStore()
let chart: echarts.ECharts | null = null

function render() {
  if (!el.value) return
  if (!chart) chart = echarts.init(el.value)
  const dark = theme.isDark
  chart.setOption({
    color: ['#2563eb'],
    textStyle: { color: dark ? '#94a3b8' : '#64748b' },
    grid: { left: 40, right: 20, top: 40, bottom: 30 },
    tooltip: { trigger: 'axis' },
    legend: { data: ['消息量（示意）'] },
    xAxis: {
      type: 'category',
      data: ['周一', '周二', '周三', '周四', '周五', '周六', '周日'],
      axisLine: { lineStyle: { color: dark ? '#334155' : '#e2e8f0' } },
    },
    yAxis: {
      type: 'value',
      splitLine: { lineStyle: { color: dark ? '#1e293b' : '#f1f5f9' } },
    },
    series: [
      {
        name: '消息量（示意）',
        type: 'line',
        smooth: true,
        // 仅作布局示意，非真实生产数据
        data: [0, 0, 0, 0, 0, 0, 0],
        areaStyle: { opacity: 0.08 },
      },
    ],
  })
}

onMounted(() => {
  render()
  window.addEventListener('resize', () => chart?.resize())
})

watch(() => theme.isDark, () => render())

onBeforeUnmount(() => {
  chart?.dispose()
  chart = null
})
</script>

<style scoped>
.chart {
  width: 100%;
  height: 320px;
}
</style>
