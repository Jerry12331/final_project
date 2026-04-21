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
        <div class="layer-label">
          Layer {{ layerIndex }}{{ isInputLayer(layer) ? " (Input)" : "" }}
        </div>
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
  },
  inputs: {
    type: Array,
    default: () => []
  }
});

const GATE_SPACING = 80;
const circuitRef = ref(null);
const gateRefs = ref([]);
const wires = ref([]);
const hoveredGate = ref(null);

function isObjectGate(gate) {
  return gate && typeof gate === "object" && !Array.isArray(gate);
}

function normalizeNumericCircuit(circuit, inputValues) {
  const layers = circuit.map((layer, layerIndex) => {
    return layer.map((gate, gateIndex) => ({
      id: `l${layerIndex}g${gateIndex}`,
      type: gate === 0 ? "add" : "mul"
    }));
  });

  const inputs = (inputValues.length > 0 ? inputValues : [3, 5, 2, 7]).map((value, index) => ({
    id: `in${index}`,
    type: "input",
    value
  }));

  layers.push(inputs);

  for (let layerIndex = 0; layerIndex < layers.length - 1; layerIndex++) {
    const currentLayer = layers[layerIndex];
    const nextLayer = layers[layerIndex + 1];

    currentLayer.forEach((gate, gateIndex) => {
      const left = nextLayer[gateIndex * 2]?.id;
      const right = nextLayer[gateIndex * 2 + 1]?.id;
      gate.inputs = [left, right].filter(Boolean);
    });
  }

  return layers;
}

const normalizedLayers = computed(() => {
  if (!Array.isArray(props.circuit) || props.circuit.length === 0) return [];

  const firstGate = props.circuit[0]?.[0];
  if (isObjectGate(firstGate)) {
    return props.circuit;
  }

  return normalizeNumericCircuit(props.circuit, Array.isArray(props.inputs) ? props.inputs : []);
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

  const gatePosition = new Map();
  layers.forEach((layer, layerIndex) => {
    layer.forEach((gate, gateIndex) => {
      const gateId = isObjectGate(gate) ? gate.id : null;
      if (gateId) {
        gatePosition.set(gateId, { layerIndex, gateIndex });
      }
    });
  });

  layers.forEach((layer, layerIndex) => {
    layer.forEach((gate, gateIndex) => {
      if (!Array.isArray(gate?.inputs) || gate.inputs.length === 0) return;

      const currentEl = gateRefs.value[layerIndex]?.[gateIndex];
      if (!currentEl) return;
      const target = gateCenter(currentEl, circuitRect);

      gate.inputs.forEach((inputId) => {
        const parent = gatePosition.get(inputId);
        if (!parent) return;

        const parentEl = gateRefs.value[parent.layerIndex]?.[parent.gateIndex];
        if (!parentEl) return;

        const source = gateCenter(parentEl, circuitRect);
        segments.push({
          id: `${inputId}-${gate.id}`,
          x1: source.x,
          y1: source.y,
          x2: target.x,
          y2: target.y
        });
      });
    });
  });

  wires.value = segments;
}

function gateType(gate) {
  const type = typeof gate === "object" ? gate.type : gate;
  if (type === "input") return "input";
  return type === 0 || type === "add" ? "add" : "mul";
}

function gateSymbol(gate) {
  const type = gateType(gate);
  if (type === "input") return gate?.value ?? "?";
  return type === "add" ? "+" : "×";
}

function isInputLayer(layer) {
  return Array.isArray(layer) && layer.length > 0 && layer.every((gate) => gateType(gate) === "input");
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
  if (gateType(gate) === "input" || layerIndex === normalizedLayers.value.length - 1) return "INPUT";
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

  return "";
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

.gate.input {
  background: #dcfce7;
  border: 2px solid #16a34a;
  color: #166534;
  font-size: 18px;
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
