<script setup lang="ts">
import { computed, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as techniciansApi from '../../api/technicians'
import type { TechnicianDto, WorkIntervalDto } from '../../api/technicians'

const emit = defineEmits<{ (e: 'saved'): void }>()

// Lunes primero; el backend usa 0 = domingo.
const DAYS = [
  { day: 1, label: 'Lunes' },
  { day: 2, label: 'Martes' },
  { day: 3, label: 'Miércoles' },
  { day: 4, label: 'Jueves' },
  { day: 5, label: 'Viernes' },
  { day: 6, label: 'Sábado' },
  { day: 0, label: 'Domingo' }
]

const visible = ref(false)
const loading = ref(false)
const saving = ref(false)
const technician = ref<TechnicianDto | null>(null)
const isDefault = ref(true)
const timeZoneId = ref('')
const days = reactive<Record<number, { start: string; end: string }[]>>({})

const hasAnyInterval = computed(() => DAYS.some((d) => (days[d.day] ?? []).length > 0))

function fill(intervals: WorkIntervalDto[]) {
  for (const d of DAYS) days[d.day] = []
  for (const i of intervals) days[i.day].push({ start: i.start, end: i.end })
  for (const d of DAYS) days[d.day].sort((a, b) => a.start.localeCompare(b.start))
}

async function open(target: TechnicianDto) {
  technician.value = target
  visible.value = true
  loading.value = true
  try {
    const { data } = await techniciansApi.getTechnicianSchedule(target.id)
    isDefault.value = data.isDefault
    timeZoneId.value = data.timeZoneId
    fill(data.intervals)
  } catch (err: any) {
    console.error('No se pudo cargar el horario:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo cargar el horario.')
    visible.value = false
  } finally {
    loading.value = false
  }
}

function addInterval(day: number) {
  const list = days[day]
  const last = list[list.length - 1]
  list.push(last ? { start: last.end, end: '18:00' } : { start: '08:00', end: '17:00' })
}

function removeInterval(day: number, index: number) {
  days[day].splice(index, 1)
}

async function save() {
  if (!technician.value) return
  const intervals: WorkIntervalDto[] = DAYS.flatMap((d) =>
    (days[d.day] ?? []).map((i) => ({ day: d.day, start: i.start, end: i.end }))
  )
  saving.value = true
  try {
    await techniciansApi.setTechnicianSchedule(technician.value.id, intervals)
    ElMessage.success('Horario guardado.')
    visible.value = false
    emit('saved')
  } catch (err: any) {
    console.error('No se pudo guardar el horario:', err)
    const errors = err.response?.data?.errors
    ElMessage.error((errors && Object.values(errors).flat().join(' ')) || err.response?.data?.title || 'No se pudo guardar el horario.')
  } finally {
    saving.value = false
  }
}

async function resetToDefault() {
  if (!technician.value) return
  saving.value = true
  try {
    await techniciansApi.resetTechnicianSchedule(technician.value.id)
    ElMessage.success('Se restableció el horario de la empresa.')
    visible.value = false
    emit('saved')
  } catch (err: any) {
    console.error('No se pudo restablecer el horario:', err)
    ElMessage.error(err.response?.data?.title ?? 'No se pudo restablecer el horario.')
  } finally {
    saving.value = false
  }
}

defineExpose({ open })
</script>

<template>
  <el-dialog v-model="visible" :title="`Horario laboral — ${technician?.fullName ?? ''}`" width="640px">
    <div v-loading="loading">
      <p class="hint">
        Hora local ({{ timeZoneId }}). Un día sin tramos no se trabaja. Los festivos de Colombia se descuentan solos.
        <template v-if="isDefault"> Hoy usa el horario general de la empresa.</template>
      </p>
      <div v-for="d in DAYS" :key="d.day" class="day-row">
        <span class="day-label">{{ d.label }}</span>
        <div class="day-intervals">
          <span v-if="days[d.day]?.length === 0" class="muted">No trabaja</span>
          <div v-for="(interval, index) in days[d.day]" :key="index" class="interval">
            <el-time-select v-model="interval.start" start="00:00" end="23:30" step="00:30" :max-time="interval.end" style="width: 110px" />
            <span>a</span>
            <el-time-select v-model="interval.end" start="00:00" end="23:30" step="00:30" :min-time="interval.start" style="width: 110px" />
            <el-button link type="danger" @click="removeInterval(d.day, index)">Quitar</el-button>
          </div>
          <el-button link type="primary" @click="addInterval(d.day)">+ Agregar tramo</el-button>
        </div>
      </div>
    </div>
    <template #footer>
      <el-button :disabled="isDefault" :loading="saving" @click="resetToDefault">Usar horario de la empresa</el-button>
      <el-button @click="visible = false">Cancelar</el-button>
      <el-button type="primary" :loading="saving" :disabled="!hasAnyInterval" @click="save">Guardar</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
  margin-right: 0.5rem;
}

.day-row {
  display: flex;
  gap: 1rem;
  padding: 0.5rem 0;
  border-bottom: 1px solid var(--el-border-color-lighter);
}

.day-label {
  width: 90px;
  font-weight: 600;
  padding-top: 0.3rem;
}

.day-intervals {
  flex: 1;
}

.interval {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  margin-bottom: 0.4rem;
}
</style>
