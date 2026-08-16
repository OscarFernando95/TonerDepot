<script setup lang="ts">
/**
 * Board's signature motion: a value lands the way a split-flap cell lands,
 * clattering through a short random run of same-alphabet glyphs before
 * settling. This is the one authored moment of the direction — reserved for
 * the login wordmark and dashboard KPI values, never scattered onto
 * ordinary UI transitions.
 */
import { computed, onMounted, ref, watch } from 'vue'

const props = defineProps<{ value: string | number }>()

const target = computed(() => String(props.value))
const shown = ref<string[]>(target.value.split(''))
const flipping = ref<boolean[]>(target.value.split('').map(() => false))

const DIGITS = '0123456789'
const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'
const reduceMotion =
  typeof window !== 'undefined' && window.matchMedia('(prefers-reduced-motion: reduce)').matches

function randomFrom(charset: string) {
  return charset[Math.floor(Math.random() * charset.length)]
}

function charsetFor(char: string): string | null {
  if (char >= '0' && char <= '9') return DIGITS
  if (char.toUpperCase() >= 'A' && char.toUpperCase() <= 'Z') return LETTERS
  return null
}

function clatter(newTarget: string) {
  const chars = newTarget.split('')

  if (reduceMotion) {
    shown.value = chars
    flipping.value = chars.map(() => false)
    return
  }

  shown.value = chars.map((c, i) => shown.value[i] ?? c)
  flipping.value = chars.map(() => false)

  chars.forEach((finalChar, i) => {
    const charset = charsetFor(finalChar)
    const steps = charset ? 3 + Math.floor(Math.random() * 3) : 0
    const startDelay = i * 45
    let step = 0

    const tick = () => {
      flipping.value[i] = true
      if (step < steps && charset) {
        shown.value[i] = randomFrom(charset)
        step += 1
        setTimeout(tick, 55 + step * 12)
      } else {
        shown.value[i] = finalChar
        flipping.value[i] = false
      }
    }
    setTimeout(tick, startDelay)
  })
}

onMounted(() => clatter(target.value))
watch(target, (v) => clatter(v))
</script>

<template>
  <span class="flap-text">
    <span class="visually-hidden">{{ target }}</span>
    <span class="flap-cells" aria-hidden="true">
      <span
        v-for="(ch, i) in shown"
        :key="i"
        class="flap-cell"
        :class="{ 'is-flipping': flipping[i] }"
        >{{ ch === ' ' ? ' ' : ch }}</span
      >
    </span>
  </span>
</template>

<style scoped>
.flap-text {
  display: inline-flex;
}

.flap-cells {
  display: inline-flex;
  font-variant-numeric: tabular-nums;
}

.flap-cell {
  display: inline-block;
  transition:
    filter 90ms ease-out,
    transform 90ms ease-out,
    opacity 90ms ease-out;
}

.flap-cell.is-flipping {
  transform: scaleY(0.82) translateY(-6%);
  filter: blur(1.5px);
  opacity: 0.7;
}
</style>
