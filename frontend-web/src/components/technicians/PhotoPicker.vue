<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'

// Foto de evidencia: en un celular abre la cámara (capture), en escritorio deja elegir un archivo. Reduce la
// imagen antes de enviarla (las fotos de celular pesan varios MB y el servidor acepta hasta 10 MB).
const props = defineProps<{ modelValue: File | null }>()
const emit = defineEmits<{ (e: 'update:modelValue', value: File | null): void }>()

const MAX_SIDE = 1600
const previewUrl = ref<string | null>(null)
const busy = ref(false)

function revoke() {
  if (previewUrl.value) {
    URL.revokeObjectURL(previewUrl.value)
    previewUrl.value = null
  }
}

watch(
  () => props.modelValue,
  (file) => {
    revoke()
    if (file) previewUrl.value = URL.createObjectURL(file)
  },
  { immediate: true }
)
onBeforeUnmount(revoke)

async function shrink(file: File): Promise<File> {
  const bitmap = await createImageBitmap(file)
  const scale = Math.min(1, MAX_SIDE / Math.max(bitmap.width, bitmap.height))
  const canvas = document.createElement('canvas')
  canvas.width = Math.round(bitmap.width * scale)
  canvas.height = Math.round(bitmap.height * scale)
  canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height)
  bitmap.close()
  const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.82))
  if (!blob) throw new Error('No se pudo procesar la imagen.')
  return new File([blob], 'evidencia.jpg', { type: 'image/jpeg' })
}

async function onChange(event: Event) {
  const input = event.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  if (!file.type.startsWith('image/')) {
    ElMessage.warning('Elige una imagen.')
    return
  }
  busy.value = true
  try {
    emit('update:modelValue', await shrink(file))
  } catch (err) {
    console.error('No se pudo procesar la foto:', err)
    ElMessage.error('No se pudo procesar la foto. Intenta con otra.')
  } finally {
    busy.value = false
  }
}
</script>

<template>
  <div class="photo-picker">
    <img v-if="previewUrl" :src="previewUrl" alt="Vista previa de la evidencia" class="preview" />
    <label class="pick-btn">
      <input type="file" accept="image/*" capture="environment" hidden :disabled="busy" @change="onChange" />
      <el-button tag="span" :loading="busy">{{ modelValue ? 'Cambiar foto' : 'Tomar / elegir foto' }}</el-button>
    </label>
  </div>
</template>

<style scoped>
.photo-picker {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  align-items: flex-start;
}

.preview {
  max-width: 100%;
  max-height: 220px;
  border-radius: 8px;
  border: 1px solid var(--el-border-color);
}

.pick-btn {
  cursor: pointer;
}
</style>
