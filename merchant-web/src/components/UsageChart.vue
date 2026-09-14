<template>
  <div ref="el" class="chart" />
</template>

<script setup lang="ts">
import { onMounted, onBeforeUnmount, ref, watch } from 'vue'
import * as echarts from 'echarts/core'
import { LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'

echarts.use([LineChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer])

const props = defineProps<{
  points?: { date: string; count: number }[]
  seriesName?: string
}>()

const el = ref<HTMLDivElement | null>(null)
let chart: echarts.ECharts | null = null

function render() {
  if (!el.value) return
  if (!chart) chart = echarts.init(el.value)
  const pts = props.points?.length ? props.points : []
  const hasData = pts.some((p) => (p.count ?? 0) > 0)
  const name = props.seriesName || (hasData ? '会话数' : '暂无数据')
  chart.setOption({
    color: ['#2563eb'],
    textStyle: { color: '#64748b' },
    grid: { left: 40, right: 20, top: 40, bottom: 30 },
    tooltip: { trigger: 'axis' },
    legend: { data: [name] },
    xAxis: {
      type: 'category',
      data: pts.length ? pts.map((p) => p.date) : ['—'],
      axisLine: { lineStyle: { color: '#e2e8f0' } },
    },
    yAxis: {
      type: 'value',
      minInterval: 1,
      splitLine: { lineStyle: { color: '#f1f5f9' } },
    },
    series: [
      {
        name,
        type: 'line',
        smooth: true,
        data: pts.length ? pts.map((p) => p.count) : [0],
        areaStyle: { opacity: 0.08 },
      },
    ],
    graphic: hasData
      ? undefined
      : {
          type: 'text',
          left: 'center',
          top: 'middle',
          style: {
            text: '近 7 日暂无会话数据',
            fill: '#94a3b8',
            fontSize: 14,
          },
        },
  })
}

onMounted(() => {
  render()
  window.addEventListener('resize', () => chart?.resize())
})

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
