<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import * as assetBrandsApi from '../../api/assetBrands'
import type { AssetBrandDto } from '../../api/types'

const router = useRouter()

const brands = ref<AssetBrandDto[]>([])
const loading = ref(false)
const dialogVisible = ref(false)
const saving = ref(false)

const form = reactive({ name: '' })

async function loadBrands() {
  loading.value = true
  try {
    const { data } = await assetBrandsApi.listAssetBrands()
    brands.value = data
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudieron cargar las marcas.')
  } finally {
    loading.value = false
  }
}

function openCreateDialog() {
  form.name = ''
  dialogVisible.value = true
}

async function handleSave() {
  saving.value = true
  try {
    await assetBrandsApi.createAssetBrand({ name: form.name })
    ElMessage.success('Marca creada.')
    dialogVisible.value = false
    await loadBrands()
  } catch (err: any) {
    ElMessage.error(err.response?.data?.title ?? 'No se pudo crear la marca.')
  } finally {
    saving.value = false
  }
}

function goToDetail(brand: AssetBrandDto) {
  router.push({ name: 'asset-brand-detail', params: { id: brand.id } })
}

onMounted(loadBrands)
</script>

<template>
  <div>
    <div class="page-header">
      <h1>Marcas</h1>
      <el-button type="primary" @click="openCreateDialog">Nueva marca</el-button>
    </div>
    <p class="hint">Entra a una marca para registrar sus modelos y los umbrales de mantenimiento.</p>

    <el-table :data="brands" v-loading="loading" stripe @row-click="goToDetail" class="clickable-rows">
      <el-table-column prop="name" label="Nombre" />
    </el-table>

    <el-dialog v-model="dialogVisible" title="Nueva marca" width="360px">
      <el-form :model="form" label-position="top">
        <el-form-item label="Nombre">
          <el-input v-model="form.name" placeholder="Ej: Ricoh" />
        </el-form-item>
      </el-form>
      <template #footer>
        <el-button @click="dialogVisible = false">Cancelar</el-button>
        <el-button type="primary" :loading="saving" @click="handleSave">Guardar</el-button>
      </template>
    </el-dialog>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 1rem;
}

.hint {
  color: var(--el-text-color-secondary);
  font-size: 0.85rem;
  margin: 0 0 1rem;
}

.clickable-rows :deep(tbody tr) {
  cursor: pointer;
}
</style>
