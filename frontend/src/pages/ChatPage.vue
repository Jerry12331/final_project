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
          :class="{ active: activeLayer === layer.layerIndex }"
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
      <button @click="prevStep" :disabled="currentStep <= 0">Previous Step</button>
      <button @click="nextStep" :disabled="currentStep >= totalSteps - 1">Next Step</button>
      
      <span class="step-info">
        Debug: Step Index {{ currentStep }} | Total {{ totalSteps }}
      </span>
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
    // 在 ChatPage.vue 的 onMounted 裡面
    const data = await response.json();

    // ⭐️ 這裡增加一個相容性判斷，確保大寫 Log 也能被讀到
    const events = data.Log || data.log; 

    console.log("收到 Events:", events);
    parseEvents(events);

  } catch (error) {
    console.error("GKR API Error:", error);
    alert("與後端連線失敗，請檢查 C# 伺服器。\n" + error.message);
  }
});


function parseEvents(events) {
  if (!events || events.length === 0) return;

  const layersMap = new Map();

  events.forEach(event => {
    // 再次確保內部屬性的大寫相容
    const pLayer = event.ProtocolLayer ?? event.protocolLayer;
    const pRound = event.Round ?? event.round;
    const pRole = event.Role ?? event.role;
    const pMessage = event.Message ?? event.message;

    if (!layersMap.has(pLayer)) {
      layersMap.set(pLayer, {
        layerIndex: pLayer,
        isOpen: true,
        sumcheck: {
          boundary: pLayer === 0 ? "Output Layer" : `Layer ${pLayer} Sumcheck`,
          roundsMap: new Map()
        }
      });
    }

    const roundsMap = layersMap.get(pLayer).sumcheck.roundsMap;
    if (!roundsMap.has(pRound)) {
      roundsMap.set(pRound, { round: pRound, prover: "", verifier: "" });
    }

    const roundObj = roundsMap.get(pRound);
    if (pRole === "Prover") roundObj.prover += pMessage + "\n";
    else if (pRole === "Verifier") roundObj.verifier += pMessage + "\n";
    else roundObj.verifier += `[系統] ${pMessage}\n`;
  });

  // 轉換 Map 為 Array
  protocolState.value.layers = Array.from(layersMap.values()).map(l => ({
    ...l,
    sumcheck: {
      boundary: l.sumcheck.boundary,
      rounds: Array.from(l.sumcheck.roundsMap.values()).sort((a, b) => a.round - b.round)
    }
  })).sort((a, b) => a.layerIndex - b.layerIndex);

  currentStep.value = 0; // 從第一步開始
}

// ==========================================
// 介面控制邏輯 (完全不用動)
// ==========================================
// 1. 修正：計算總步數（所有的 Round 總和）
const totalSteps = computed(() => {
  if (!protocolState.value.layers) return 0;
  return protocolState.value.layers.reduce((sum, layer) => {
    return sum + (layer.sumcheck?.rounds?.length || 0);
  }, 0);
});

// 2. 修正：讓對話框能正確顯示
function visibleRounds(layer) {
  if (!layer.sumcheck || !layer.sumcheck.rounds) return [];
  
  // 計算在這一層之前已經用掉了多少步
  let previousRoundsCount = 0;
  for (const l of protocolState.value.layers) {
    if (l.layerIndex === layer.layerIndex) break;
    previousRoundsCount += l.sumcheck.rounds.length;
  }

  // 目前這一步相對於這一層的偏移量
  const currentStepInThisLayer = currentStep.value - previousRoundsCount;

  // 如果 currentStep 還沒到這一層，回傳空陣列
  if (currentStepInThisLayer < 0) return []; 

  // 回傳該層目前應該顯示的 Round 數量
  const visibleCount = Math.min(currentStepInThisLayer + 1, layer.sumcheck.rounds.length);
  return layer.sumcheck.rounds.slice(0, visibleCount);
}

// 3. 修正：自動判斷當前處於哪一層 (讓 UI 自動展開對應的 Layer)
const activeLayer = computed(() => {
  let stepCount = 0;
  for (const layer of protocolState.value.layers) {
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
  // 只要目前步數小於總步數，就允許下一步
  if (currentStep.value < totalSteps.value - 1) {
    currentStep.value++;
  } else {
    console.log("已經是最後一步了", currentStep.value, totalSteps.value);
  }
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
