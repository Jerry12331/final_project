<template>
  <div class="chat-page">
    <CircuitCanvas :currentLayer="currentLayer" :activeGates="currentActiveGates" :circuit="circuit" />

    <ExplanationBox :explanation="currentExplanation" />

    <div class="hidden-values-wrapper" v-if="hiddenValues && hiddenValues.length > 0">
      <button @click="showHidden = !showHidden" class="toggle-hidden-btn">
        {{ showHidden ? '點擊收起 Witness' : '點擊查看 Witness (隱藏值)' }}
      </button>
      
      <div v-show="showHidden" class="hidden-values-content">
        <span class="val-label">目前持有的隱藏值：</span>
        <div class="pill-container">
          <span v-for="(val, idx) in hiddenValues" :key="idx" class="val-pill">
            {{ val }}
          </span>
        </div>
      </div>
    </div>
    <div class="protocol-container">
      <div 
        v-for="layer in protocolState.layers" 
        :key="layer.layerIndex"
        class="layer-section"
      >
        <button 
          @click="toggleLayer(layer.layerIndex)"
          class="layer-header"
          :class="{ active: currentLayer === layer.layerIndex }"
        >
          <span class="layer-title">Layer {{ layer.layerIndex }}</span>
          <span class="collapse-icon">{{ layer.isOpen ? '▼' : '▶' }}</span>
        </button>

        <div v-if="layer.isOpen && layer.sumcheck" class="sumcheck-content">
          <div class="chat-columns">
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
import ExplanationBox from "../components/ExplanationBox.vue";

const route = useRoute();
const currentStep = ref(0);

// 存儲從後端或路由獲得的資料
const circuit = ref(null);

// 新增：儲存隱藏值與控制顯示狀態的變數
const hiddenValues = ref([]);
const showHidden = ref(false);

const protocolState = ref({
  currentLayer: 0,
  layers: []
});

onMounted(async () => {
  try {
    const circuitData = route.query.circuit ? JSON.parse(route.query.circuit) : [[0],[0,1]];
    const inputData = route.query.input ? JSON.parse(route.query.input) : [3,5,2,7];
    const hiddenData = route.query.hidden ? JSON.parse(route.query.hidden) : [];

    circuit.value = circuitData;
    
    // 把接到的隱藏值存進響應式變數中，供畫面上方渲染
    hiddenValues.value = hiddenData;

    // 呼叫 C# API
    const response = await fetch("http://localhost:5285/api/run_gkr", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        circuit: circuitData,
        inputs: inputData,
        hiddenValues: hiddenData // 目前後端會忽略，之後加 Commitment 時就用得到
      })
    });

    if (!response.ok) throw new Error(`HTTP error! status: ${response.status}`);
    
    const data = await response.json();
    const events = data.Log || data.log; 

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
    const pLayer = event.ProtocolLayer ?? event.protocolLayer;
    const pRound = event.Round ?? event.round;
    const pRole = event.Role ?? event.role;
    const pType = event.Type ?? event.type ?? null;
    const pMessage = event.Message ?? event.message;
    const pData = event.Data ?? event.data ?? null;

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
      roundsMap.set(pRound, {
        round: pRound,
        prover: "",
        verifier: "",
        type: null,
        data: {}
      });
    }

    const roundObj = roundsMap.get(pRound);
    if (pRole === "Prover") roundObj.prover += pMessage + "\n";
    else if (pRole === "Verifier") roundObj.verifier += pMessage + "\n";
    else roundObj.verifier += `${pMessage}\n`;

    if (pType && ["SEND_RHO", "SEND_S", "CLAIM_VALUE"].includes(pType)) {
      roundObj.type = pType;
      roundObj.data = { ...roundObj.data, ...(pData || {}) };
    }
  });

  protocolState.value.layers = Array.from(layersMap.values()).map(l => ({
    ...l,
    sumcheck: {
      boundary: l.sumcheck.boundary,
      rounds: Array.from(l.sumcheck.roundsMap.values()).sort((a, b) => a.round - b.round)
    }
  })).sort((a, b) => a.layerIndex - b.layerIndex);

  currentStep.value = 0; 
}

const totalSteps = computed(() => {
  if (!protocolState.value.layers) return 0;
  return protocolState.value.layers.reduce((sum, layer) => {
    return sum + (layer.sumcheck?.rounds?.length || 0);
  }, 0);
});

function visibleRounds(layer) {
  if (!layer.sumcheck || !layer.sumcheck.rounds) return [];
  
  let previousRoundsCount = 0;
  for (const l of protocolState.value.layers) {
    if (l.layerIndex === layer.layerIndex) break;
    previousRoundsCount += l.sumcheck.rounds.length;
  }

  const currentStepInThisLayer = currentStep.value - previousRoundsCount;

  if (currentStepInThisLayer < 0) return []; 

  const visibleCount = Math.min(currentStepInThisLayer + 1, layer.sumcheck.rounds.length);
  return layer.sumcheck.rounds.slice(0, visibleCount);
}

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
  if (currentStep.value > 0) currentStep.value--;
}

const flattenedRounds = computed(() => {
  return protocolState.value.layers.flatMap((layer) => {
    return (layer.sumcheck?.rounds || []).map((round) => ({
      layer: layer.layerIndex,
      round: round.round,
      type: round.type || "SYSTEM_MESSAGE",
      data: round.data || {},
      verifier: round.verifier || "",
      prover: round.prover || ""
    }));
  });
});

const currentLayer = computed(() => {
  return flattenedRounds.value[currentStep.value]?.layer ?? 0;
});

const currentActiveGates = computed(() => {
  const step = flattenedRounds.value[currentStep.value];
  const gates = step?.data?.activeGates;
  return Array.isArray(gates) ? gates : [];
});

const currentExplanation = computed(() => {
  const step = flattenedRounds.value[currentStep.value];
  if (!step) return { text: "等待驗證流程資料載入中", variables: [] };

  switch (step.type) {
    case "SEND_RHO":
      return { text: "Verifier 正在用隨機數測試 Prover", variables: [{ name: "rho", desc: `隨機挑戰數${step.data?.rho !== undefined ? ` (目前值: ${step.data.rho})` : ""}` }] };
    case "SEND_S":
      return { text: "Verifier 選擇一個隨機點來檢查多項式", variables: [{ name: "s0", desc: `測試隨機點${step.data?.s !== undefined ? ` (目前值: ${step.data.s})` : ""}` }] };
    case "CLAIM_VALUE":
      return { text: "Prover 提出計算結果", variables: [{ name: "claimed value", desc: `Prover 聲稱的結果${step.data?.claimed !== undefined ? ` (目前值: ${step.data.claimed})` : ""}` }] };
    default:
      return { text: "正在進行 GKR 驗證", variables: [] };
  }
});
</script>

<style scoped>
.chat-page {
  padding: 20px;
  font-family: sans-serif;
  padding-right: 340px;
}

/* ===== 隱藏值 (Witness) 區塊樣式 ===== */
.hidden-values-wrapper {
  margin-top: 16px;
  padding: 12px 16px;
  background-color: #faf5ff; /* 淺紫底色 */
  border: 1px dashed #d8b4fe;
  border-radius: 8px;
}

.toggle-hidden-btn {
  background: none;
  border: none;
  color: #9333ea;
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  padding: 0;
  display: flex;
  align-items: center;
}

.toggle-hidden-btn:hover {
  text-decoration: underline;
  color: #7e22ce;
}

.hidden-values-content {
  margin-top: 12px;
  display: flex;
  align-items: center;
  gap: 12px;
}

.val-label {
  font-size: 13px;
  color: #6b21a8;
  font-weight: 500;
}

.pill-container {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}

.val-pill {
  background-color: #f3e8ff;
  color: #6b21a8;
  padding: 4px 12px;
  border-radius: 9999px; /* 圓角藥丸形狀 */
  font-family: monospace;
  font-weight: bold;
  border: 1px solid #e9d5ff;
  font-size: 14px;
}
/* ======================================= */

.protocol-container {
  margin-top: 20px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}

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

.layer-title { color: #1f2937; }
.collapse-icon { color: #6b7280; font-size: 12px; }
.sumcheck-content { padding: 16px; background: #fafafa; }
.no-sumcheck { padding: 20px; text-align: center; color: #9ca3af; font-style: italic; }

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

.chat-column h4 { margin-bottom: 12px; font-weight: bold; font-size: 1em; color: #374151; }
.chat-bubble { padding: 12px; margin: 8px 0; border-radius: 8px; line-height: 1.5; }
.round-label { display: block; font-size: 11px; font-weight: 600; color: #6b7280; margin-bottom: 4px; text-transform: uppercase; }
.chat-bubble p { margin: 0; }
.verifier-bubble { background-color: #eef2ff; border-left: 3px solid #3b82f6; }
.prover-bubble { background-color: #fef2f2; border-left: 3px solid #ef4444; }

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

.controls button:hover { background: #1d4ed8; }
.step-info { color: #6b7280; font-size: 14px; }

@media (max-width: 1100px) {
  .chat-page { padding-right: 20px; }
}
</style>