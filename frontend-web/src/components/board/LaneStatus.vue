<script setup lang="ts">
/**
 * Board's state grammar: every status carries both a lamp shape and a text
 * label, so meaning never rides on color alone (accessibility, and the
 * direction's own raise borrowed from the declined iridescent-cloud world).
 */
type LaneState = 'on-time' | 'warning' | 'critical' | 'idle'

defineProps<{
  state: LaneState
  label: string
}>()
</script>

<template>
  <span class="lane-status" :class="`is-${state}`">
    <span class="lane-lamp" aria-hidden="true"></span>
    <span class="lane-label">{{ label }}</span>
  </span>
</template>

<style scoped>
.lane-status {
  display: inline-flex;
  align-items: center;
  gap: 0.4rem;
  font-family: var(--font-display);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  font-size: var(--text-2xs);
  font-weight: 600;
  line-height: 1;
}

.lane-lamp {
  width: 0.55rem;
  height: 0.55rem;
  flex: none;
}

.is-on-time .lane-lamp {
  background: var(--signal-blue-bright);
  border-radius: 50%;
}
.is-on-time .lane-label {
  color: var(--signal-blue-bright);
}

.is-warning .lane-lamp {
  background: var(--signal-amber);
  border-radius: 1px;
  transform: rotate(45deg);
}
.is-warning .lane-label {
  color: var(--signal-amber);
}

.is-critical .lane-lamp {
  background: transparent;
  border: 2px solid var(--signal-red);
  border-radius: 50%;
  width: 0.4rem;
  height: 0.4rem;
}
.is-critical .lane-label {
  color: var(--signal-red);
}

.is-idle .lane-lamp {
  background: var(--board-seam);
  border-radius: 50%;
}
.is-idle .lane-label {
  color: var(--flap-ink-dim);
}
</style>
