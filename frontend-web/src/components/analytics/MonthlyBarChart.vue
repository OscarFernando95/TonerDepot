<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue'
import * as echarts from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'

echarts.use([BarChart, GridComponent, TooltipComponent, CanvasRenderer])

// Barras por mes con los tokens del tablero (azul señal sobre fondo casi negro, esquinas rectas).
const props = defineProps<{ data: { label: string; value: number }[]; unit?: string }>()

const el = ref<HTMLDivElement | null>(null)
let chart: echarts.ECharts | null = null
let observer: ResizeObserver | null = null

function token(name: string, fallback: string) {
  return getComputedStyle(document.documentElement).getPropertyValue(name).trim() || fallback
}

function render() {
  if (!el.value) return
  chart ??= echarts.init(el.value)
  const ink = token('--flap-ink', '#eef0ec')
  const dim = token('--flap-ink-dim', '#99a1ab')
  const seam = token('--board-seam-soft', '#262e35')
  chart.setOption({
    animationDuration: 300,
    grid: { left: 36, right: 12, top: 16, bottom: 28 },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      backgroundColor: token('--board-panel-raised', '#21282f'),
      borderColor: token('--board-seam', '#5b6672'),
      textStyle: { color: ink },
      valueFormatter: (v: number) => `${v} ${props.unit ?? 'und'}`
    },
    xAxis: { type: 'category', data: props.data.map((d) => d.label), axisLine: { lineStyle: { color: seam } }, axisLabel: { color: dim }, axisTick: { show: false } },
    yAxis: { type: 'value', minInterval: 1, splitLine: { lineStyle: { color: seam } }, axisLabel: { color: dim } },
    series: [{ type: 'bar', data: props.data.map((d) => d.value), barMaxWidth: 36, itemStyle: { color: token('--signal-blue-bright', '#5b8dff'), borderRadius: 0 } }]
  })
}

onMounted(() => {
  render()
  observer = new ResizeObserver(() => chart?.resize())
  if (el.value) observer.observe(el.value)
})
watch(() => props.data, render, { deep: true })
onBeforeUnmount(() => {
  observer?.disconnect()
  chart?.dispose()
  chart = null
})
</script>

<template>
  <div ref="el" class="chart" role="img" :aria-label="`Gráfico de barras por mes: ${data.map((d) => `${d.label} ${d.value}`).join(', ')}`" />
</template>

<style scoped>
.chart { width: 100%; height: 240px; }
</style>
