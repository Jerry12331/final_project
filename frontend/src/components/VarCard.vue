<template>
  <div class="var-card" :class="{ 'is-set': hasValue }" @mouseenter="showTip = true" @mouseleave="showTip = false">
    <div class="var-name">{{ name }}</div>

    <div v-if="hasValue" class="var-value">
      <span class="val-text">{{ displayValue }}</span>
      <span v-if="isTruncated" class="full-val-tooltip" :class="{ visible: showTip }">
        {{ fullValue }}
      </span>
    </div>
    <div v-else class="var-empty">尚未設定</div>

    <div class="var-desc-tooltip" :class="{ visible: showTip && !hasValue }">
      {{ desc }}
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'

const props = defineProps({
  name:  { type: String, required: true },
  value: { default: null },
  desc:  { type: String, default: '' }
})

const showTip = ref(false)

const hasValue = computed(() => {
  if (props.value === null || props.value === undefined) return false
  if (Array.isArray(props.value)) return props.value.length > 0
  return String(props.value).trim() !== ''
})

// 將 value 轉成顯示字串
const fullValue = computed(() => {
  if (!hasValue.value) return ''
  if (Array.isArray(props.value)) return '[' + props.value.join(', ') + ']'
  return String(props.value)
})

const TRUNC = 16
const isTruncated = computed(() => fullValue.value.length > TRUNC * 2 + 3)

const displayValue = computed(() => {
  const v = fullValue.value
  if (v.length <= TRUNC * 2 + 3) return v
  return v.slice(0, TRUNC) + '...' + v.slice(-TRUNC)
})
</script>

<style scoped>
.var-card {
  position: relative;
  min-width: 140px;
  max-width: 220px;
  padding: 8px 10px;
  border-radius: 8px;
  border: 1px solid #e5e7eb;
  background: #f9fafb;
  transition: border-color 0.15s, box-shadow 0.15s;
  cursor: default;
}

.var-card.is-set {
  border-color: #93c5fd;
  background: #eff6ff;
}

.var-card:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

.var-name {
  font-size: 13px;
  font-weight: 700;
  color: #374151;
  font-family: monospace;
  margin-bottom: 4px;
}

.var-value {
  position: relative;
}

.val-text {
  font-family: monospace;
  font-size: 13px;
  color: #1e3a5f;
  word-break: break-all;
}

.var-empty {
  font-size: 12px;
  color: #9ca3af;
  font-style: italic;
}

/* 完整數值的 tooltip */
.full-val-tooltip {
  display: none;
  position: absolute;
  bottom: calc(100% + 6px);
  left: 50%;
  transform: translateX(-50%);
  background: #1f2937;
  color: #f9fafb;
  font-family: monospace;
  font-size: 11px;
  padding: 6px 10px;
  border-radius: 6px;
  white-space: pre-wrap;
  word-break: break-all;
  max-width: 320px;
  z-index: 999;
  box-shadow: 0 4px 12px rgba(0,0,0,0.25);
}

.full-val-tooltip::after {
  content: '';
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  border: 5px solid transparent;
  border-top-color: #1f2937;
}

.full-val-tooltip.visible { display: block; }

/* 說明 tooltip（未設定時顯示） */
.var-desc-tooltip {
  display: none;
  position: absolute;
  bottom: calc(100% + 6px);
  left: 50%;
  transform: translateX(-50%);
  background: #374151;
  color: #f9fafb;
  font-size: 11px;
  padding: 6px 10px;
  border-radius: 6px;
  white-space: normal;
  max-width: 200px;
  z-index: 999;
  box-shadow: 0 4px 12px rgba(0,0,0,0.2);
  line-height: 1.5;
}

.var-desc-tooltip::after {
  content: '';
  position: absolute;
  top: 100%;
  left: 50%;
  transform: translateX(-50%);
  border: 5px solid transparent;
  border-top-color: #374151;
}

.var-desc-tooltip.visible { display: block; }
</style>
