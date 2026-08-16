<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import * as techniciansApi from '../../api/technicians'
import * as citiesApi from '../../api/cities'
import type { TechnicianDto, TechnicianCoverageDto } from '../../api/technicians'
import type { CityDto } from '../../api/types'

const technicians = ref<TechnicianDto[]>([])
const cities = ref<CityDto[]>([])
const loading = ref(false)

const coverageDialogVisible = ref(false)
const selectedTechnician = ref<TechnicianDto | null>(null)
const coverage = ref<TechnicianCoverageDto[]>([])
const loadingCoverage = ref(false)
const savingCoverage = ref(false)

const addForm = reactive({ cityId: '' })

async function loadData() {
  loading.value = true
  try {
    const [techRes, citiesRes] = await Promise.all([techniciansApi.listTechnicians(), citiesApi.listCities()])
    technicians.value = techRes.data
    cities.value = citiesRes.data
  } finally {
    loading.value = false
  }
}

function statusTagType(status: string) {
  switch (status) {
    case 'Disponible':
      return 'success'
    case 'Ocupado':
      return 'warning'
    case 'EnTransito':
      return 'primary'
    default:
      return 'info'
  }
}

async function openCoverageDialog(technician: TechnicianDto) {
  selectedTechnician.value = technician
  addForm.cityId = ''
  coverageDialogVisible.value = true
  loadingCoverage.value = true
  try {
    const { data } = await techniciansApi.listTechnicianCoverage(technician.id)
    coverage.value = data
  } finally {
    loadingCoverage.value = false
  }
}

async function addCoverage() {
  if (!selectedTechnician.value || !addForm.cityId) return
  savingCoverage.value = true
  try {
    await techniciansApi.addTechnicianCoverage(selectedTechnician.value.id, addForm.cityId)
    ElMessage.success('Cobertura agregada.')
    addForm.cityId = ''
    const { data } = await techniciansApi.listTechnicianCoverage(selectedTechnician.value.id)
    coverage.value = data
    await loadData()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo agregar la cobertura.')
  } finally {
    savingCoverage.value = false
  }
}

async function removeCoverage(item: TechnicianCoverageDto) {
  if (!selectedTechnician.value) return
  await techniciansApi.removeTechnicianCoverage(selectedTechnician.value.id, item.id)
  ElMessage.success('Cobertura eliminada.')
  const { data } = await techniciansApi.listTechnicianCoverage(selectedTechnician.value.id)
  coverage.value = data
  await loadData()
}

onMounted(loadData)
</script>

<template>
  <div>
    <h1>Técnicos</h1>
    <p class="hint">
      La cobertura por ciudad es lo que usa el motor de asignación para elegir candidatos. El estado
      (Disponible/Ocupado) lo mueve automáticamente el check-in/check-out del técnico, no se edita aquí.
    </p>

    <el-table :data="technicians" v-loading="loading" stripe empty-text="No hay técnicos registrados.">
      <el-table-column prop="fullName" label="Nombre" sortable />
      <el-table-column prop="status" label="Estado" width="130" sortable>
        <template #default="{ row }">
          <el-tag :type="statusTagType(row.status)" size="small">{{ row.status }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="Cobertura">
        <template #default="{ row }">
          <span v-if="row.coverageCityNames.length === 0" class="muted">Sin ciudades asignadas</span>
          <el-tag v-for="c in row.coverageCityNames" :key="c" size="small" class="coverage-tag">{{ c }}</el-tag>
        </template>
      </el-table-column>
      <el-table-column label="" width="160">
        <template #default="{ row }">
          <el-button link @click="openCoverageDialog(row)">Gestionar cobertura</el-button>
        </template>
      </el-table-column>
    </el-table>

    <el-dialog v-model="coverageDialogVisible" :title="`Cobertura — ${selectedTechnician?.fullName}`" width="440px">
      <div v-loading="loadingCoverage">
        <div class="coverage-list">
          <el-tag
            v-for="c in coverage"
            :key="c.id"
            closable
            class="coverage-tag"
            @close="removeCoverage(c)"
          >
            {{ c.cityName }}
          </el-tag>
          <span v-if="coverage.length === 0" class="muted">Todavía no tiene ciudades asignadas.</span>
        </div>
        <div class="add-coverage-row">
          <el-select v-model="addForm.cityId" filterable placeholder="Agregar ciudad" style="flex: 1">
            <el-option v-for="c in cities" :key="c.id" :label="c.name" :value="c.id" />
          </el-select>
          <el-button type="primary" :loading="savingCoverage" @click="addCoverage">Agregar</el-button>
        </div>
      </div>
      <template #footer>
        <el-button @click="coverageDialogVisible = false">Cerrar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.muted {
  color: #9ca3af;
}

.coverage-tag {
  margin-right: 0.4rem;
  margin-bottom: 0.25rem;
}

.coverage-list {
  min-height: 2rem;
  margin-bottom: 1rem;
}

.add-coverage-row {
  display: flex;
  gap: 0.5rem;
}
</style>
