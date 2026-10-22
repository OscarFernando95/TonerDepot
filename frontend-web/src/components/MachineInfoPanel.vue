<script setup lang="ts">
import { computed } from 'vue'
import { Odometer, OfficeBuilding, Location, Shop, Grid } from '@element-plus/icons-vue'

// "Datos de la máquina": cliente, sede, ciudad, área y último contador. Mismo bloque en órdenes, tickets e
// instalaciones para que siempre se lea igual. Las filas sin dato se omiten; el contador siempre se muestra.
const props = withDefaults(
  defineProps<{
    clientName?: string | null
    locationName?: string | null
    cityName?: string | null
    area?: string | null
    lastMeterReading?: number | null
    showCounter?: boolean
    compact?: boolean
  }>(),
  { clientName: null, locationName: null, cityName: null, area: null, lastMeterReading: null, showCounter: true, compact: false }
)

const counterFormatter = new Intl.NumberFormat('es-CO')

const rows = computed(() =>
  [
    { key: 'client', icon: OfficeBuilding, label: 'Cliente', value: props.clientName, strong: true },
    { key: 'location', icon: Shop, label: 'Sede', value: props.locationName, strong: false },
    { key: 'city', icon: Location, label: 'Ciudad', value: props.cityName, strong: false },
    { key: 'area', icon: Grid, label: 'Área', value: props.area, strong: false }
  ].filter((r) => !!r.value?.trim())
)

const counterLabel = computed(() =>
  props.lastMeterReading == null ? 'Sin lecturas' : counterFormatter.format(props.lastMeterReading)
)
</script>

<template>
  <div v-if="rows.length > 0 || showCounter" class="machine-info" :class="{ compact }">
    <div v-if="!compact" class="machine-info-title">Datos de la máquina</div>
    <div class="machine-info-grid">
      <div v-for="row in rows" :key="row.key" class="machine-info-item">
        <span class="machine-info-icon"><el-icon><component :is="row.icon" /></el-icon></span>
        <span class="machine-info-text">
          <span class="machine-info-label">{{ row.label }}</span>
          <span class="machine-info-value" :class="{ strong: row.strong }">{{ row.value }}</span>
        </span>
      </div>
      <div v-if="showCounter" class="machine-info-counter">
        <span class="machine-info-icon counter"><el-icon><Odometer /></el-icon></span>
        <span class="machine-info-text">
          <span class="machine-info-label">Último contador</span>
          <span class="machine-info-counter-value" :class="{ empty: lastMeterReading == null }">{{ counterLabel }}</span>
        </span>
      </div>
    </div>
  </div>
</template>

<style scoped>
.machine-info {
  border: 1px solid var(--signal-blue-wash, rgba(47, 111, 237, 0.16));
  background: color-mix(in srgb, var(--signal-blue, #2760d1) 7%, transparent);
  border-radius: 12px;
  padding: 14px 16px;
}
.machine-info.compact {
  padding: 10px 12px;
}
.machine-info-title {
  font-size: 0.72rem;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--signal-blue-bright, #5b8dff);
  margin-bottom: 10px;
}
.machine-info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(190px, 1fr));
  gap: 10px 18px;
}
.machine-info-item,
.machine-info-counter {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 0;
}
.machine-info-icon {
  flex: none;
  width: 30px;
  height: 30px;
  border-radius: 9px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  color: var(--signal-blue-bright, #5b8dff);
  background: var(--signal-blue-wash, rgba(47, 111, 237, 0.16));
}
.machine-info-icon.counter {
  color: var(--signal-amber, #d9a441);
  background: var(--signal-amber-wash, rgba(217, 164, 65, 0.14));
}
.machine-info-text {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.machine-info-label {
  font-size: 0.72rem;
  color: var(--flap-ink-dim, #99a1ab);
}
.machine-info-value {
  font-size: 0.9rem;
  overflow-wrap: anywhere;
}
.machine-info-value.strong {
  font-weight: 700;
}
.machine-info-counter-value {
  font-size: 1.15rem;
  font-weight: 700;
  font-variant-numeric: tabular-nums;
}
.machine-info-counter-value.empty {
  font-size: 0.9rem;
  font-weight: 500;
  color: var(--flap-ink-dim, #99a1ab);
}
</style>
