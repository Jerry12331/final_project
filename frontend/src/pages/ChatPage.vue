<template>
  <div class="chat-page">
    <!-- Circuit Visualization -->
    <CircuitCanvas :activeLayer="activeLayer" />

    <!-- 結構化的 Protocol View -->
    <div class="protocol-container">
      <div 
        v-for="layer in protocolState.layers" 
        :key="layer.layerIndex"
        class="layer-section"
      >
        <!-- Layer Header (可折疊) -->
        <button 
          @click="toggleLayer(layer.layerIndex)"
          class="layer-header"
          :class="{ active: currentLayer === layer.layerIndex }"
        >
          <span class="layer-title">Layer {{ layer.layerIndex }}</span>
          <span class="collapse-icon">{{ layer.isOpen ? '▼' : '▶' }}</span>
        </button>

        <!-- Sumcheck Content (折疊區) -->
        <div v-if="layer.isOpen && layer.sumcheck" class="sumcheck-content">
          <div class="chat-columns">
            <!-- Verifier Column -->
            <div class="chat-column verifier">
              <h4>Verifier</h4>
              <div
                v-for="(round, idx) in visibleRounds(layer)"
                :key="idx"
                class="chat-bubble verifier-bubble"
              >
                <span class="round-label">Round {{ round.round }}</span>
                <p>{{ round.verifier }}</p>
              </div>
            </div>

            <!-- Prover Column -->
            <div class="chat-column prover">
              <h4>Prover</h4>
              <div
                v-for="(round, idx) in visibleRounds(layer)"
                :key="idx"
                class="chat-bubble prover-bubble"
              >
                <span class="round-label">Round {{ round.round }}</span>
                <p>{{ round.prover }}</p>
              </div>
            </div>
          </div>
        </div>

        <!-- 沒有 sumcheck 的 layer -->
        <div v-if="layer.isOpen && !layer.sumcheck" class="no-sumcheck">
          No sumcheck for this layer yet.
        </div>
      </div>
    </div>

    <div class="controls">
      <button @click="prevStep">Previous Step</button>
      <button @click="nextStep">Next Step</button>
      <span class="step-info">Step {{ currentStep + 1 }} / {{ totalSteps }}</span>
    </div>
  </div>
</template>



<script setup>
import { ref, computed } from "vue";
import CircuitCanvas from "../components/CircuitCanvas.vue";

const currentStep = ref(0);

// ✅ 核心資料結構：每個 Layer 有自己的 Sumcheck
const protocolState = ref({
  currentLayer: 2,
  layers: [
    {
      layerIndex: 0,
      isOpen: false,
      sumcheck: {
        boundary: "Layer 0 (output layer)",
        rounds: [
          {
            round: 1,
            verifier: "Sumcheck 完成，請給我最後 gate value",
            prover: "最終 gate value = 42"
          }
        ]
      }
    },
    {
      layerIndex: 1,
      isOpen: false,
      sumcheck: {
        boundary: "Layer 1",
        rounds: [
          {
            round: 1,
            verifier: "請給我下一個隨機 r",
            prover: "r = 7"
          },
          {
            round: 2,
            verifier: "驗證這一層的多項式",
            prover: "g2(t) = 2t^2 + 3"
          }
        ]
      }
    },
    {
      layerIndex: 2,
      isOpen: true, // 預設打開
      sumcheck: {
        boundary: "Layer 2",
        rounds: [
          {
            round: 1,
            verifier: "你現在在哪一層？送我 boundary!",
            prover: "目前在 Layer 2，這是 boundary。"
          },
          {
            round: 2,
            verifier: "給我 g1(t) 多項式",
            prover: "g1(t) = 5 + 3t"
          },
          {
            round: 3,
            verifier: "請給我這一輪的隨機挑戰",
            prover: "r1 = 0.42"
          }
        ]
      }
    }
  ]
});

const currentLayer = ref(2);

// 計算總步數（所有 rounds 加總）
const totalSteps = computed(() => {
  return protocolState.value.layers.reduce((sum, layer) => {
    return sum + (layer.sumcheck?.rounds.length || 0);
  }, 0);
});

// 根據 currentStep 決定要顯示到哪一輪
function visibleRounds(layer) {
  if (!layer.sumcheck) return [];
  
  // 簡化版：如果 layer 是打開的，顯示前 N 個 rounds
  // 後續可以根據 currentStep 來精確控制
  const visibleCount = Math.min(
    currentStep.value + 1, 
    layer.sumcheck.rounds.length
  );
  
  return layer.sumcheck.rounds.slice(0, visibleCount);
}

// 當前 active 的 layer（用於高亮 circuit）
const activeLayer = computed(() => {
  // 簡化：根據 currentStep 計算
  let stepCount = 0;
  for (const layer of protocolState.value.layers) {
    if (!layer.sumcheck) continue;
    stepCount += layer.sumcheck.rounds.length;
    if (currentStep.value < stepCount) {
      return layer.layerIndex;
    }
  }
  return 0;
});

function toggleLayer(layerIndex) {
  const layer = protocolState.value.layers.find(l => l.layerIndex === layerIndex);
  if (layer) {
    layer.isOpen = !layer.isOpen;
  }
}

function nextStep() {
  if (currentStep.value < totalSteps.value - 1) {
    currentStep.value++;
  }
}

function prevStep() {
  if (currentStep.value > 0) {
    currentStep.value--;
  }
}
</script>



<style scoped>
.chat-page {
  padding: 20px;
  font-family: sans-serif;
}

/* Protocol Container - 結構化的 layers */
.protocol-container {
  margin-top: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

/* Layer Section */
.layer-section {
  border: 2px solid #e5e7eb;
  border-radius: 8px;
  overflow: hidden;
}

.layer-header {
  width: 100%;
  padding: 12px 16px;
  background: #f3f4f6;
  border: none;
  display: flex;
  justify-content: space-between;
  align-items: center;
  cursor: pointer;
  font-size: 16px;
  font-weight: 600;
  transition: background 0.2s;
}

.layer-header:hover {
  background: #e5e7eb;
}

.layer-header.active {
  background: #dbeafe;
  border-left: 4px solid #2563eb;
}

.layer-title {
  color: #1f2937;
}

.collapse-icon {
  color: #6b7280;
  font-size: 12px;
}

/* Sumcheck Content */
.sumcheck-content {
  padding: 16px;
  background: #fafafa;
}

.no-sumcheck {
  padding: 20px;
  text-align: center;
  color: #9ca3af;
  font-style: italic;
}

/* Chat Columns inside each layer */
.chat-columns {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 16px;
}

.chat-column {
  border: 1px solid #e5e7eb;
  padding: 12px;
  max-height: 400px;
  overflow-y: auto;
  background: white;
  border-radius: 6px;
}

.chat-column h4 {
  margin-bottom: 12px;
  font-weight: bold;
  font-size: 1em;
  color: #374151;
}

.chat-bubble {
  padding: 12px;
  margin: 8px 0;
  border-radius: 8px;
  line-height: 1.5;
}

.round-label {
  display: block;
  font-size: 11px;
  font-weight: 600;
  color: #6b7280;
  margin-bottom: 4px;
  text-transform: uppercase;
}

.chat-bubble p {
  margin: 0;
}

.verifier-bubble {
  background-color: #eef2ff;
  border-left: 3px solid #3b82f6;
}

.prover-bubble {
  background-color: #fef2f2;
  border-left: 3px solid #ef4444;
}

/* Controls */
.controls {
  margin-top: 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.controls button {
  background: #2563eb;
  color: white;
  padding: 10px 20px;
  border-radius: 6px;
  border: none;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
}

.controls button:hover {
  background: #1d4ed8;
}

.step-info {
  color: #6b7280;
  font-size: 14px;
}
</style>
