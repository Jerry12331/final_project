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
    const circuitData = route.query.circuit ? JSON.parse(route.query.circuit) : [[0,1],[1,0]];
    const inputData = route.query.input ? JSON.parse(route.query.input) : [3,5,2,7];

    // 將此處換成您實際的網址與埠號
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
    
    // 💡 印出 C# 傳來的原始資料，如果您發現對話框文字怪怪的，可以來這裡看
    console.log("C# 回傳的原始 Log:", data.log);
    
    parseBackendLogs(data.log);

  } catch (error) {
    console.error("GKR API Error:", error);
    alert("與後端連線失敗，請檢查 C# 伺服器是否開啟。\n" + error.message);
  }
});

// 解析 C# Log，轉換為對話框結構
function parseBackendLogs(logs) {
  let parsedLayers = [];
  let currentLayerObj = null;
  let currentRoundObj = null;
  let roundCounter = 1;
  let layerIdx = 0;

  for (let line of logs) {
    // 偵測是否換層
    if (line.includes("Setting up Circuit") || line.includes("send D()")) {
      if (currentRoundObj && currentLayerObj) {
          currentLayerObj.sumcheck.rounds.push(currentRoundObj);
          currentRoundObj = null;
      }
      currentLayerObj = { 
          layerIndex: layerIdx++, 
          isOpen: true, 
          sumcheck: { boundary: "Output Layer", rounds: [] } 
      };
      parsedLayers.push(currentLayerObj);
      roundCounter = 1;
    } else if (line.includes("sum check start")) {
      if (currentRoundObj && currentLayerObj) {
          currentLayerObj.sumcheck.rounds.push(currentRoundObj);
          currentRoundObj = null;
      }
      currentLayerObj = { 
          layerIndex: layerIdx++, 
          isOpen: true, 
          sumcheck: { boundary: `Layer ${layerIdx-1}`, rounds: [] } 
      };
      parsedLayers.push(currentLayerObj);
      roundCounter = 1;
    }

    if (!currentLayerObj) {
        currentLayerObj = { layerIndex: layerIdx++, isOpen: true, sumcheck: { boundary: "System", rounds: [] } };
        parsedLayers.push(currentLayerObj);
    }

    // 處理 Prover 與 Verifier 對話
    if (line.startsWith("P:") || line.startsWith("V:")) {
      if (!currentRoundObj) {
          currentRoundObj = { round: roundCounter, verifier: "", prover: "" };
      }
      
      if (line.startsWith("P:")) {
          currentRoundObj.prover += line.substring(2).trim() + "\n";
      } else if (line.startsWith("V:")) {
          currentRoundObj.verifier += line.substring(2).trim() + "\n";
          // V 講完話，推入這回合
          currentLayerObj.sumcheck.rounds.push({...currentRoundObj});
          currentRoundObj = null;
          roundCounter++;
      }
    } else {
       // 其他系統訊息
       if (!line.includes("Setting up Circuit") && !line.includes("sum check start")) {
           if (!currentRoundObj) {
               currentRoundObj = { round: roundCounter, verifier: "", prover: "" };
           }
           currentRoundObj.verifier += `[系統] ${line}\n`;
       }
    }
  }
  
  if (currentRoundObj && currentLayerObj) {
      currentLayerObj.sumcheck.rounds.push(currentRoundObj);
  }

  // 避免空白資料
  if (parsedLayers.length === 0 || parsedLayers[0].sumcheck.rounds.length === 0) {
      parsedLayers = [{
          layerIndex: 0, 
          isOpen: true, 
          sumcheck: { boundary: "GKR 執行紀錄", rounds: [{ round: 1, verifier: "Raw Log", prover: logs.join("\n") }] }
      }];
  }

  protocolState.value.layers = parsedLayers;
  
  // ⭐️ 修正 1：確保強制從第 0 步開始
  currentStep.value = 0; 
}

const totalSteps = computed(() => {
  return protocolState.value.layers.reduce((sum, layer) => {
    return sum + (layer.sumcheck?.rounds.length || 0);
  }, 0);
});

// ⭐️ 修正 2：計算跨層 (Global) 的目前步驟，讓對話框能正確隨步驟顯示
function visibleRounds(layer) {
  if (!layer.sumcheck) return [];
  
  let previousRoundsCount = 0;
  for (const l of protocolState.value.layers) {
    if (l.layerIndex === layer.layerIndex) break;
    previousRoundsCount += l.sumcheck.rounds.length;
  }

  // 計算屬於「這一層」可顯示的數量
  const availableStepsForThisLayer = currentStep.value + 1 - previousRoundsCount;

  if (availableStepsForThisLayer <= 0) return []; // 還沒輪到這層

  const visibleCount = Math.min(availableStepsForThisLayer, layer.sumcheck.rounds.length);
  return layer.sumcheck.rounds.slice(0, visibleCount);
}

const activeLayer = computed(() => {
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
  if (layer) layer.isOpen = !layer.isOpen;
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
