<template>
  <div class="input-page">
    <h2 class="title">GKR Protocol Demo – Input Page</h2>

    <div class="block">
      <h3>電路矩陣（0 = 加法, 1 = 乘法）</h3>
      <p class="hint">範例：[[0],[0,1]] (需符合二元樹結構)</p>
      <textarea
        v-model="circuitText"
        rows="4"
        placeholder="請輸入電路矩陣（JSON 格式）"
      ></textarea>
    </div>

    <div class="block">
      <h3>公開 Input 向量</h3>
      <p class="hint">範例：[3,5,2,7]</p>
      <textarea
        v-model="inputText"
        rows="2"
        placeholder="請輸入公開 input（JSON 格式）"
      ></textarea>
    </div>

    <div class="block hidden-section">
      <h3 class="text-purple-600">隱藏值 (Hidden Values / Witness)</h3>
      <p class="hint">用於 Commitment 的私密數值或隨機因子。範例：[1,0,0,1]</p>
      <textarea
        v-model="hiddenText"
        rows="2"
        placeholder="請輸入隱藏值（JSON 格式）"
        class="border-purple-200"
      ></textarea>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <button class="btn" @click="submit">
      開始 GKR 驗證
    </button>
  </div>
</template>

<script setup>
import { ref } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();

const circuitText = ref("[[0],[0,1]]");
const inputText = ref("[3,5,2,7]");
// ⭐️ 初始化隱藏值
const hiddenText = ref("[0,0,0,0]"); 
const error = ref("");

function submit() {
  error.value = "";

  try {
    const circuit = JSON.parse(circuitText.value);
    const input = JSON.parse(inputText.value);
    const hidden = JSON.parse(hiddenText.value);

    if (!Array.isArray(circuit)) throw new Error("電路必須是二維陣列");
    if (!Array.isArray(input)) throw new Error("input 必須是陣列");
    if (!Array.isArray(hidden)) throw new Error("隱藏值必須是陣列");

    // ⭐ 重點：把資料（包含隱藏值）送到 ChatPage
    router.push({
      path: "/chat",
      query: {
        circuit: JSON.stringify(circuit),
        input: JSON.stringify(input),
        hidden: JSON.stringify(hidden) 
      }
    });

  } catch (e) {
    error.value = "格式錯誤：" + e.message;
  }
}
</script>

<style scoped>
.input-page {
  max-width: 700px;
  margin: auto;
  padding: 24px;
  font-family: sans-serif;
}

.title {
  font-size: 24px;
  font-weight: bold;
  margin-bottom: 20px;
}

.block {
  margin-bottom: 20px;
}

/* 隱藏值區塊樣式 */
.hidden-section {
  margin-top: 30px;
  padding-top: 20px;
  border-top: 2px dashed #e9d5ff;
}
.text-purple-600 {
  color: #9333ea;
  font-weight: bold;
  margin-bottom: 8px;
}
.border-purple-200 {
  border: 1px solid #e9d5ff;
}

textarea {
  width: 100%;
  font-family: monospace;
  padding: 10px;
  border: 1px solid #d1d5db;
  border-radius: 4px;
  margin-top: 4px;
}

textarea:focus {
  outline: none;
  border-color: #2563eb;
}

.hint {
  font-size: 13px;
  color: #6b7280;
  margin-bottom: 4px;
}

.error {
  color: #dc2626;
  background: #fef2f2;
  padding: 8px;
  border-radius: 4px;
  margin-bottom: 10px;
}

.btn {
  background: #2563eb;
  color: white;
  padding: 12px 24px;
  border-radius: 6px;
  border: none;
  cursor: pointer;
  font-weight: 600;
  transition: background 0.2s;
}

.btn:hover {
  background: #1d4ed8;
}
</style>