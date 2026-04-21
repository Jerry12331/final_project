<template>
  <div class="chat-page">
    <CircuitCanvas :currentLayer="currentLayer" :activeGates="currentActiveGates" :circuit="circuit" :inputs="inputVector" />

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
                <p v-html="formatMessage(round.verifier)"></p>
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
                <p v-html="formatMessage(round.prover)"></p>
              </div>
            </div>
          </div>
        </div>

        <div v-if="layer.isOpen && !layer.sumcheck" class="no-sumcheck">
          此 Layer 尚無 Sumcheck 資料
        </div>
      </div>
    </div>

    <div v-if="totalSteps > 0 && currentStep === totalSteps - 1" class="verify-success">
      ✓ 驗證成功！GKR 協議已完成，電路計算正確無誤。
    </div>

    <div class="controls">
      <button @click="prevStep" :disabled="currentStep <= 0">← 上一步</button>
      <span class="step-info">步驟 {{ currentStep + 1 }} / {{ totalSteps }}</span>
      <button @click="nextStep" :disabled="currentStep >= totalSteps - 1">下一步 →</button>
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
const inputVector = ref([]);

// 新增：儲存隱藏值與控制顯示狀態的變數
const hiddenValues = ref([]);
const showHidden = ref(false);

const protocolState = ref({
  currentLayer: 0,
  layers: []
});

onMounted(async () => {
  try {
    const circuitData = route.query.circuit ? JSON.parse(route.query.circuit) : [[0],[1,0]];
    const inputData = route.query.input ? JSON.parse(route.query.input) : [3,5,2,7];
    const hiddenData = route.query.hidden ? JSON.parse(route.query.hidden) : [];

    inputVector.value = inputData;
    circuit.value = buildDisplayCircuit(circuitData, inputData);
    
    // 把接到的隱藏值存進響應式變數中，供畫面上方渲染
    hiddenValues.value = hiddenData;

    // 呼叫 C# API
    const response = await fetch("http://localhost:5285/api/run_gkr", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        circuit: Array.isArray(circuitData) && typeof circuitData[0]?.[0] === "number" ? circuitData : [[0],[1,0]],
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

function buildDisplayCircuit(circuitData, inputData) {
  const looksLikeStructuredLayers = Array.isArray(circuitData)
    && Array.isArray(circuitData[0])
    && typeof circuitData[0][0] === "object"
    && circuitData[0][0] !== null
    && Object.prototype.hasOwnProperty.call(circuitData[0][0], "id");

  if (looksLikeStructuredLayers) return circuitData;

  const normalizedInputs = Array.isArray(inputData) ? [...inputData] : [];
  while (normalizedInputs.length < 3) {
    normalizedInputs.push(0);
  }

  const in0 = normalizedInputs[0];
  const in1 = normalizedInputs[1];
  const in2 = normalizedInputs[2];
  const g1Value = in0 * in1;
  const g2Value = in1 + in2;

  const inputLayer = normalizedInputs.map((value, index) => ({
    id: `in${index}`,
    type: "input",
    value
  }));

  return [
    [
      { id: "out", type: "add", inputs: ["g1", "g2"], value: g1Value + g2Value }
    ],
    [
      { id: "g1", type: "mul", inputs: ["in0", "in1"], value: g1Value },
      { id: "g2", type: "add", inputs: ["in1", "in2"], value: g2Value }
    ],
    inputLayer
  ];
}

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

function formatBigNum(str) {
  if (str.length <= 20) return str;
  return str.slice(0, 8) + '...' + str.slice(-8);
}

function formatMessage(msg) {
  if (!msg) return '';
  const escaped = msg
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/\n/g, '<br>');
  return escaped.replace(/\b(\d{17,})\b/g, (match) => {
    const truncated = match.slice(0, 8) + '...' + match.slice(-8);
    return `<span class="big-num" title="${match}">${truncated}</span>`;
  });
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
  if (!step) return { phase: "載入中", text: "等待驗證流程資料載入中", why: "", variables: [] };

  const layerIdx = step.layer;
  const roundIdx = step.round;

  switch (step.type) {
    case "SEND_RHO":
      return {
        phase: `Layer ${layerIdx} · Round ${roundIdx}`,
        text: "Verifier 送出隨機挑戰數 ρ（rho）給 Prover，要求對方回答多項式在此點的值。",
        why: "隨機性讓 Prover 無法預先偽造答案。若 Prover 說謊，被挑穿的機率極高（由有限體大小決定）。",
        variables: [{ name: "ρ (rho)", desc: `隨機挑戰點${step.data?.rho !== undefined ? `：${formatBigNum(String(step.data.rho))}` : ""}` }]
      };
    case "SEND_S":
      return {
        phase: `Layer ${layerIdx} · Round ${roundIdx}`,
        text: "Verifier 選取隨機點 s，要求 Prover 提供下一層電路在 s 的求值，以便繼續驗證。",
        why: "每一層都需要新的隨機點，形成一條從輸出層延伸到輸入層的驗證鏈，最終收斂於可公開驗證的輸入。",
        variables: [{ name: "s", desc: `隨機測試點${step.data?.s !== undefined ? `：${formatBigNum(String(step.data.s))}` : ""}` }]
      };
    case "CLAIM_VALUE":
      return {
        phase: `Layer ${layerIdx} · Round ${roundIdx}`,
        text: "Prover 提出計算結果（Claim）。Verifier 將此結果與先前隨機挑戰進行一致性檢查。",
        why: "若 Prover 提出的值與電路實際值不符，Sumcheck 協議會在後續步驟將矛盾揭穿，Prover 無法逃脫。",
        variables: [{ name: "claimed value", desc: `Prover 宣稱的輸出${step.data?.claimed !== undefined ? `：${formatBigNum(String(step.data.claimed))}` : ""}` }]
      };
    default:
      if (roundIdx === 1 && layerIdx === 0) {
        return {
          phase: "GKR 驗證啟動",
          text: "Prover 宣告電路的輸出值，GKR 協議正式開始。Verifier 將從輸出層逐層向下驗證到輸入層。",
          why: "GKR 協議的核心是把「電路計算的正確性」轉化為「一系列多項式求和問題」，讓驗證計算量大幅縮短。",
          variables: []
        };
      }
      return {
        phase: `Layer ${layerIdx} · Sumcheck Round ${roundIdx}`,
        text: `Prover 與 Verifier 正在執行第 ${layerIdx} 層的 Sumcheck 協議第 ${roundIdx} 輪。Prover 逐步證明多項式求和正確，Verifier 逐步驗證一致性。`,
        why: "Sumcheck 協議讓 Verifier 只需驗證少量點的值，就能確認整個多項式求和正確，大幅降低驗證計算量。",
        variables: []
      };
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

.big-num {
  font-family: monospace;
  background: #fef9c3;
  border-bottom: 1px dashed #ca8a04;
  cursor: help;
  border-radius: 2px;
  padding: 0 2px;
}

.verify-success {
  margin-top: 20px;
  padding: 14px 20px;
  background: #dcfce7;
  border: 1px solid #86efac;
  border-left: 4px solid #22c55e;
  border-radius: 8px;
  color: #15803d;
  font-weight: 600;
  font-size: 15px;
}

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
.controls button:disabled { background: #93c5fd; cursor: not-allowed; }
.step-info { color: #374151; font-size: 14px; font-weight: 500; }

@media (max-width: 1100px) {
  .chat-page { padding-right: 20px; }
}
</style>