<script setup lang="ts">
import { nextTick, onBeforeUnmount, ref } from 'vue'
import { ElMessage } from 'element-plus'
import L from 'leaflet'
import 'leaflet/dist/leaflet.css'
import markerIcon from 'leaflet/dist/images/marker-icon.png'
import markerIcon2x from 'leaflet/dist/images/marker-icon-2x.png'
import markerShadow from 'leaflet/dist/images/marker-shadow.png'
import { getCurrentPosition } from '../composables/useGeolocation'

// Selector de ubicación en mapa (OpenStreetMap: sin clave ni cuenta). Clic en el mapa o arrastrar el marcador
// para fijar el punto; la búsqueda usa Nominatim (uso ligero, un pedido por búsqueda del usuario).
const pinIcon = L.icon({
  iconUrl: markerIcon,
  iconRetinaUrl: markerIcon2x,
  shadowUrl: markerShadow,
  iconSize: [25, 41],
  iconAnchor: [12, 41],
  shadowSize: [41, 41]
})

interface Result {
  label: string
  lat: number
  lng: number
}

const COLOMBIA_CENTER: [number, number] = [4.57, -74.3]

const visible = ref(false)
const mapEl = ref<HTMLDivElement | null>(null)
const query = ref('')
const searching = ref(false)
const results = ref<Result[]>([])
const point = ref<{ lat: number; lng: number } | null>(null)

let map: L.Map | null = null
let marker: L.Marker | null = null
let resolvePicker: ((value: { lat: number; lng: number } | null) => void) | null = null
let initial: { lat: number; lng: number } | null = null

function setPoint(lat: number, lng: number, zoomTo?: number) {
  point.value = { lat: Number(lat.toFixed(6)), lng: Number(lng.toFixed(6)) }
  if (!map) return
  if (!marker) {
    marker = L.marker([lat, lng], { draggable: true, icon: pinIcon }).addTo(map)
    marker.on('dragend', () => {
      const p = marker!.getLatLng()
      point.value = { lat: Number(p.lat.toFixed(6)), lng: Number(p.lng.toFixed(6)) }
    })
  } else {
    marker.setLatLng([lat, lng])
  }
  if (zoomTo) map.setView([lat, lng], zoomTo)
}

function buildMap() {
  if (!mapEl.value || map) return
  map = L.map(mapEl.value).setView(initial ? [initial.lat, initial.lng] : COLOMBIA_CENTER, initial ? 17 : 6)
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
  }).addTo(map)
  map.on('click', (e: L.LeafletMouseEvent) => setPoint(e.latlng.lat, e.latlng.lng))
  if (initial) setPoint(initial.lat, initial.lng)
  // El diálogo termina de animarse después de montar: sin esto Leaflet calcula mal el tamaño y muestra fichas grises.
  setTimeout(() => map?.invalidateSize(), 200)
}

function destroyMap() {
  map?.remove()
  map = null
  marker = null
}

// Devuelve el punto elegido, o null si se cancela.
function pick(start?: { lat: number | null | undefined; lng: number | null | undefined }, searchHint?: string) {
  initial = start && start.lat != null && start.lng != null ? { lat: start.lat, lng: start.lng } : null
  point.value = initial
  query.value = searchHint ?? ''
  results.value = []
  visible.value = true
  nextTick(buildMap)
  return new Promise<{ lat: number; lng: number } | null>((resolve) => {
    resolvePicker = resolve
  })
}

function close(value: { lat: number; lng: number } | null) {
  visible.value = false
  resolvePicker?.(value)
  resolvePicker = null
  destroyMap()
}

async function search() {
  const text = query.value.trim()
  if (!text) return
  searching.value = true
  results.value = []
  try {
    const response = await fetch(
      `https://nominatim.openstreetmap.org/search?format=jsonv2&limit=5&countrycodes=co&q=${encodeURIComponent(text)}`,
      { headers: { 'Accept-Language': 'es' } }
    )
    if (!response.ok) throw new Error(`Nominatim respondió ${response.status}`)
    const data = (await response.json()) as { display_name: string; lat: string; lon: string }[]
    results.value = data.map((r) => ({ label: r.display_name, lat: Number(r.lat), lng: Number(r.lon) }))
    if (results.value.length === 0) ElMessage.info('No se encontró esa dirección. Prueba con menos detalle o marca el punto en el mapa.')
  } catch (err) {
    console.error('Búsqueda de dirección fallida:', err)
    ElMessage.error('No se pudo buscar la dirección. Marca el punto directamente en el mapa.')
  } finally {
    searching.value = false
  }
}

function chooseResult(result: Result) {
  setPoint(result.lat, result.lng, 17)
  results.value = []
}

async function useMyLocation() {
  const position = await getCurrentPosition()
  if (!position) {
    ElMessage.warning('No se pudo obtener la ubicación. Revisa el permiso del navegador.')
    return
  }
  setPoint(position.latitude, position.longitude, 17)
}

onBeforeUnmount(destroyMap)
defineExpose({ pick })
</script>

<template>
  <el-dialog
    v-model="visible"
    title="Elegir ubicación en el mapa"
    width="720px"
    :close-on-click-modal="false"
    append-to-body
    @close="close(null)"
  >
    <div class="search-row">
      <el-input v-model="query" placeholder="Buscar dirección (ej. Calle 5 #10-20, Pitalito)" clearable @keyup.enter="search" />
      <el-button :loading="searching" @click="search">Buscar</el-button>
      <el-button @click="useMyLocation">Mi ubicación</el-button>
    </div>
    <ul v-if="results.length" class="results">
      <li v-for="r in results" :key="`${r.lat},${r.lng}`" @click="chooseResult(r)">{{ r.label }}</li>
    </ul>
    <div ref="mapEl" class="map" />
    <p class="hint">
      <template v-if="point">Punto elegido: {{ point.lat }}, {{ point.lng }} — puedes arrastrar el marcador para afinarlo.</template>
      <template v-else>Haz clic en el mapa para marcar la ubicación exacta de la sede.</template>
    </p>
    <template #footer>
      <el-button @click="close(null)">Cancelar</el-button>
      <el-button type="primary" :disabled="!point" @click="close(point)">Usar esta ubicación</el-button>
    </template>
  </el-dialog>
</template>

<style scoped>
.search-row {
  display: flex;
  gap: 0.5rem;
  margin-bottom: 0.5rem;
}

.results {
  list-style: none;
  margin: 0 0 0.5rem;
  padding: 0;
  border: 1px solid var(--el-border-color);
  border-radius: 6px;
  max-height: 140px;
  overflow-y: auto;
}

.results li {
  padding: 0.4rem 0.6rem;
  cursor: pointer;
  font-size: 0.85rem;
}

.results li:hover {
  background: var(--el-fill-color-light);
}

.map {
  height: 380px;
  width: 100%;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0.5rem 0 0;
}
</style>
