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

const props = defineProps<{
  points?: { date: string; count: number }[]
}>()

const el = ref<HTMLDivElement | null>(null)
const theme = useThemeStore()
let chart: echarts.ECharts | null = null

function render() {
  if (!el.value) return
  if (!chart) chart = echarts.init(el.value)
  const dark = theme.isDark
  const pts = props.points?.length
    ? props.points
    : []
  const hasData = pts.length > 0
  chart.setOption({
    color: ['#2563eb'],
    textStyle: { color: dark ? '#94a3b8' : '#64748b' },
    grid: { left: 40, right: 20, top: 40, bottom: 30 },
    tooltip: { trigger: 'axis' },
    legend: { data: [hasData ? '会话数' : '暂无数据'] },
    xAxis: {
      type: 'category',
      data: hasData ? pts.map((p) => p.date) : ['—'],
      axisLine: { lineStyle: { color: dark ? '#334155' : '#e2e8f0' } },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      splitLine: { lineStyle: { color: dark ? '#1e293b' : '#f1f5f9' } },
    },
    series: [
      {
        name: hasData ? '会话数' : '暂无数据',
        type: 'line',
        smooth: true,
        data: hasData ? pts.map((p) => p.count) : [0],
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
watch(() => props.points, () => render(), { deep: true })

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
