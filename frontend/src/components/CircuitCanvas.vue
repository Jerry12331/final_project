<template>
  <div class="circuit-canvas">

    <h3 class="title">Circuit Visualization</h3>

    <div
      v-for="(layer, layerIndex) in circuit"
      :key="layerIndex"
      :class="['layer', { active: layerIndex === activeLayer }]"
    >
      <div class="layer-title">
        Layer {{ layerIndex }}
      </div>

      <div class="gates">
        <div
          v-for="(gate, gateIndex) in layer"
          :key="gateIndex"
          class="gate"
          :class="gate === 0 ? 'add' : 'mul'"
        >
          {{ gate === 0 ? '+' : '×' }}
        </div>
      </div>
    </div>

  </div>
</template>

<script setup>
defineProps({
  activeLayer: {
    type: Number,
    required: true
  }
});

// 定義電路結構
const circuit = [
  [0, 1],           // Layer 0
  [1, 0, 1, 0],     // Layer 1
  [0, 1, 0, 1, 1, 0, 1, 0]  // Layer 2
];
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

.layer {
  opacity: 0.3;
  padding: 12px;
  margin-bottom: 12px;
  border-radius: 8px;
  display: flex;
  align-items: center;
  transition: all 0.3s ease;
}

.layer.active {
  opacity: 1;
  border: 2px solid #ff4d4f;
  background: #fff1f0;
}

.layer-title {
  width: 80px;
  font-weight: bold;
}

.gates {
  display: flex;
  gap: 12px;
}

.gate {
  width: 50px;
  height: 50px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  font-weight: bold;
  border: 2px solid #333;
}

.gate.add {
  background: #e0f2fe;
}

.gate.mul {
  background: #fee2e2;
}
</style>
