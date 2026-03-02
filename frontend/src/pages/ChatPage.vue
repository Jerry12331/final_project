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
import { ref, computed, onMounted } from "vue";
import { useRoute } from "vue-router";
import CircuitCanvas from "../components/CircuitCanvas.vue";

const route = useRoute();
const currentStep = ref(0);
const currentLayer = ref(0);

const protocolState = ref({
  currentLayer: 0,
  layers: []
});

onMounted(async () => {
  try {
    const circuitData = route.query.circuit ? JSON.parse(route.query.circuit) : [[0],[0,1]];
    const inputData = route.query.input ? JSON.parse(route.query.input) : [3,5,2,7];

    // 呼叫 C# API
    const response = await fetch("http://localhost:5285/api/run_gkr", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        circuit: circuitData,
        inputs: inputData
      })
    });

    if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
    const data = await response.json();
    
    console.log("C# 回傳的結構化 Events:", data.log);
    
    // ⭐️ 直接處理結構化的 Event Array
    parseEvents(data.log);

  } catch (error) {
    console.error("GKR API Error:", error);
    alert("與後端連線失敗，請檢查 C# 伺服器。\n" + error.message);
  }
});

// ⭐️ 全新的解析邏輯：乾淨、強健、不需要猜字串
function parseEvents(events) {
  // 防呆：如果後端回傳空資料或 Error
  if (!events || events.length === 0) return;
  if (events.some(e => e.Role === "System" && e.Message.includes("Error"))) {
     protocolState.value.layers = [{
        layerIndex: 0, isOpen: true, 
        sumcheck: { boundary: "⚠️ 計算發生錯誤", rounds: [{ round: 1, verifier: events.map(e => e.Message).join('\n'), prover: "" }] }
     }];
     return;
  }

  // 用 Map 來整理 Layer 和 Round
  const layersMap = new Map();

  events.forEach(event => {
    // 1. 找尋或建立 Layer
    if (!layersMap.has(event.ProtocolLayer)) {
      layersMap.set(event.ProtocolLayer, {
        layerIndex: event.ProtocolLayer,
        isOpen: true,
        sumcheck: {
          boundary: event.ProtocolLayer === 0 ? "Layer 0 (Output Layer)" : `Layer ${event.ProtocolLayer} Sumcheck`,
          roundsMap: new Map() // 暫時用來分組 Round 的 Map
        }
      });
    }

    const layerObj = layersMap.get(event.ProtocolLayer);
    const roundsMap = layerObj.sumcheck.roundsMap;

    // 2. 找尋或建立 Round
    if (!roundsMap.has(event.Round)) {
      roundsMap.set(event.Round, {
        round: event.Round,
        prover: "",
        verifier: ""
      });
    }

    const roundObj = roundsMap.get(event.Round);

    // 3. 把對話塞進對應的氣泡框
    if (event.Role === "Prover") {
      roundObj.prover += event.Message + "\n";
    } else if (event.Role === "Verifier") {
      roundObj.verifier += event.Message + "\n";
    } else {
      // System 訊息統一放在 Verifier 框框當作提示
      roundObj.verifier += `[系統] ${event.Message}\n`;
    }
  });

  // 4. 將 Map 轉回 Vue 可以渲染的 Array，並排序
  const parsedLayers = Array.from(layersMap.values()).map(layer => {
    return {
      layerIndex: layer.layerIndex,
      isOpen: layer.isOpen,
      sumcheck: {
        boundary: layer.sumcheck.boundary,
        rounds: Array.from(layer.sumcheck.roundsMap.values()).sort((a, b) => a.round - b.round)
      }
    };
  }).sort((a, b) => a.layerIndex - b.layerIndex);

  protocolState.value.layers = parsedLayers;
  currentStep.value = 0; // 強制從第 0 步開始
}


// ==========================================
// 介面控制邏輯 (完全不用動)
// ==========================================
const totalSteps = computed(() => {
  return protocolState.value.layers.reduce((sum, layer) => {
    return sum + (layer.sumcheck?.rounds.length || 0);
  }, 0);
});

function visibleRounds(layer) {
  if (!layer.sumcheck) return [];
  
  let previousRoundsCount = 0;
  for (const l of protocolState.value.layers) {
    if (l.layerIndex === layer.layerIndex) break;
    previousRoundsCount += l.sumcheck.rounds.length;
  }

  const availableStepsForThisLayer = currentStep.value + 1 - previousRoundsCount;
  if (availableStepsForThisLayer <= 0) return []; 

  const visibleCount = Math.min(availableStepsForThisLayer, layer.sumcheck.rounds.length);
  return layer.sumcheck.rounds.slice(0, visibleCount);
}

const activeLayer = computed(() => {
  let stepCount = 0;
  for (const layer of protocolState.value.layers) {
    if (!layer.sumcheck) continue;
    stepCount += layer.sumcheck.rounds.length;
    if (currentStep.value < stepCount) return layer.layerIndex;
  }
  return 0;
});

function toggleLayer(layerIndex) {
  const layer = protocolState.value.layers.find(l => l.layerIndex === layerIndex);
  if (layer) layer.isOpen = !layer.isOpen;
}

function nextStep() {
  if (currentStep.value < totalSteps.value - 1) currentStep.value++;
}

function prevStep() {
  if (currentStep.value > 0) currentStep.value--;
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
