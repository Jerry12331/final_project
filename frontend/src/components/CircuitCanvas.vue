<template>
  <div class="circuit-canvas">
    <h3 class="title">Circuit Visualization</h3>
    <div ref="circuitRef" class="circuit">
      <svg class="wires" aria-hidden="true">
        <line
          v-for="wire in wires"
          :key="wire.id"
          :x1="wire.x1"
          :y1="wire.y1"
          :x2="wire.x2"
          :y2="wire.y2"
          class="wire"
        />
      </svg>

      <div
        v-for="(layer, layerIndex) in normalizedLayers"
        :key="layerIndex"
        :class="['layer', { 'active-layer': layerIndex === currentLayer }]"
      >
        <div class="layer-label">Layer {{ layerIndex }}</div>
        <div class="gates-row" :style="layerStyle(layer)">
          <div
            v-for="(gate, gateIndex) in layer"
            :key="gate.id ?? gateIndex"
            :ref="el => setGateRef(layerIndex, gateIndex, el)"
            class="gate"
            :class="[gateType(gate), { active: isActiveGate(layerIndex, gateIndex) }]"
            @mouseenter="setHoveredGate(gate, layerIndex, gateIndex)"
            @mouseleave="hoveredGate = null"
          >
            {{ gateSymbol(gate) }}
          </div>
        </div>
      </div>
    </div>

    <div v-if="hoveredGate" class="hover-box">
      <p><b>Gate {{ hoveredGate.gateIndex + 1 }}</b></p>
      <p>type: {{ tooltipGateType(hoveredGate.gate, hoveredGate.layerIndex) }}</p>
      <p>value: {{ tooltipGateValue(hoveredGate.gate, hoveredGate.layerIndex) }}</p>
      <p v-if="tooltipInputs(hoveredGate.gate, hoveredGate.layerIndex, hoveredGate.gateIndex)">
        來自: {{ tooltipInputs(hoveredGate.gate, hoveredGate.layerIndex, hoveredGate.gateIndex) }}
      </p>
    </div>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from "vue";

const props = defineProps({
  currentLayer: {
    type: Number,
    required: true
  },
  activeGates: {
    type: Array,
    default: () => []
  },
  circuit: {
    type: Array,
    default: () => []
  }
});

const GATE_SPACING = 80;
const circuitRef = ref(null);
const gateRefs = ref([]);
const wires = ref([]);
const hoveredGate = ref(null);

const normalizedLayers = computed(() => {
  if (!Array.isArray(props.circuit)) return [];
  return props.circuit;
});

const maxWidth = computed(() => {
  if (normalizedLayers.value.length === 0) return 0;
  return Math.max(...normalizedLayers.value.map(layer => layer.length));
});

function layerStyle(layer) {
  const offset = (maxWidth.value - layer.length) / 2;
  return {
    marginLeft: `${offset * GATE_SPACING}px`
  };
}

function setGateRef(layerIndex, gateIndex, el) {
  if (!gateRefs.value[layerIndex]) gateRefs.value[layerIndex] = [];
  gateRefs.value[layerIndex][gateIndex] = el;
}

function gateCenter(el, circuitRect) {
  const rect = el.getBoundingClientRect();
  return {
    x: rect.left - circuitRect.left + rect.width / 2,
    y: rect.top - circuitRect.top + rect.height / 2
  };
}

function recalculateWires() {
  if (!circuitRef.value) return;

  const circuitRect = circuitRef.value.getBoundingClientRect();
  const layers = normalizedLayers.value;
  const segments = [];

  for (let layerIndex = 0; layerIndex < layers.length - 1; layerIndex++) {
    const upperLen = layers[layerIndex].length;
    const lowerLen = layers[layerIndex + 1].length;
    if (upperLen === 0 || lowerLen === 0) continue;

    for (let lowerGateIndex = 0; lowerGateIndex < lowerLen; lowerGateIndex++) {
      const upperGateIndex = Math.floor((lowerGateIndex * upperLen) / lowerLen);
      const upperEl = gateRefs.value[layerIndex]?.[upperGateIndex];
      const lowerEl = gateRefs.value[layerIndex + 1]?.[lowerGateIndex];
      if (!upperEl || !lowerEl) continue;

      const source = gateCenter(upperEl, circuitRect);
      const target = gateCenter(lowerEl, circuitRect);

      segments.push({
        id: `${layerIndex}-${upperGateIndex}-${layerIndex + 1}-${lowerGateIndex}`,
        x1: source.x,
        y1: source.y,
        x2: target.x,
        y2: target.y
      });
    }
  }

  wires.value = segments;
}

function gateType(gate) {
  const type = typeof gate === "object" ? gate.type : gate;
  return type === 0 || type === "add" ? "add" : "mul";
}

function gateSymbol(gate) {
  return gateType(gate) === "add" ? "+" : "×";
}

function setHoveredGate(gate, layerIndex, gateIndex) {
  hoveredGate.value = { gate, layerIndex, gateIndex };
}

function isActiveGate(layerIndex, gateIndex) {
  return props.activeGates.some((g) => {
    const gLayer = g.layer ?? g.layerIndex;
    const gIndex = g.index ?? g.gateIndex;
    return gLayer === layerIndex && gIndex === gateIndex;
  });
}

function tooltipGateType(gate, layerIndex) {
  if (layerIndex === normalizedLayers.value.length - 1) return "INPUT";
  return gateType(gate).toUpperCase();
}

function tooltipGateValue(gate, layerIndex) {
  if (typeof gate === "object" && gate?.value !== undefined && gate?.value !== null) {
    return gate.value;
  }

  if (layerIndex === normalizedLayers.value.length - 1 && typeof gate === "number") {
    return gate;
  }

  return "N/A";
}

function tooltipInputs(gate, layerIndex, gateIndex) {
  if (Array.isArray(gate?.inputs) && gate.inputs.length) {
    return gate.inputs.join(", ");
  }

  const nextLayer = normalizedLayers.value[layerIndex + 1];
  if (!nextLayer) return "";

  const left = gateIndex * 2;
  const right = gateIndex * 2 + 1;
  if (right >= nextLayer.length) return "";

  return `Gate ${left + 1} + Gate ${right + 1}`;
}

async function syncWires() {
  await nextTick();
  recalculateWires();
}

function handleResize() {
  recalculateWires();
}

onMounted(() => {
  window.addEventListener("resize", handleResize);
  syncWires();
});

watch(
  () => props.currentLayer,
  () => {
    syncWires();
  }
);

watch(
  normalizedLayers,
  () => {
    gateRefs.value = [];
    syncWires();
  },
  { deep: true }
);

onBeforeUnmount(() => {
  window.removeEventListener("resize", handleResize);
});
</script>

<style scoped>
.circuit-canvas {
  border: 1px solid #ccc;
  padding: 16px;
  margin-bottom: 20px;
  background: #fff;
}

.title {
  font-weight: bold;
  margin-bottom: 12px;
}

.circuit {
  position: relative;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 60px;
  padding: 10px 20px;
}

.wires {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  pointer-events: none;
  z-index: 1;
}

.wire {
  stroke: #9ca3af;
  stroke-width: 2;
  stroke-linecap: round;
}

.layer {
  display: flex;
  align-items: center;
  gap: 24px;
  opacity: 0.45;
  padding: 10px;
  min-height: 40px;
  border-radius: 8px;
  transition: all 0.3s ease;
  position: relative;
  z-index: 2;
}

.active-layer {
  opacity: 1;
  background: rgba(59, 130, 246, 0.1);
  border-radius: 12px;
}

.layer-label {
  width: 80px;
  font-size: 13px;
  font-weight: 600;
  color: #4b5563;
}

.gates-row {
  display: flex;
  align-items: center;
  gap: 80px;
}

.gate {
  width: 40px;
  height: 40px;
  border-radius: 8px;
  background: #f3f4f6;
  border: 2px solid #374151;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 22px;
  font-weight: bold;
  transition: transform 0.2s ease, box-shadow 0.2s ease, background 0.2s ease;
}

.gate.add {
  background: #dbeafe;
}

.gate.mul {
  background: #fde68a;
}

.gate.active {
  background: #3b82f6;
  color: #fff;
  transform: scale(1.1);
  box-shadow: 0 8px 16px rgba(59, 130, 246, 0.35);
}

.hover-box {
  position: fixed;
  bottom: 40px;
  right: 40px;
  background: #111;
  color: #fff;
  padding: 10px 14px;
  border-radius: 8px;
  font-size: 14px;
  pointer-events: none;
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.2);
  z-index: 2000;
}

.hover-box p {
  margin: 3px 0;
}
</style>
