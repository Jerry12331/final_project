<template>
  <div class="input-page">
    <h2 class="title">GKR Protocol Demo – Input Page</h2>

    <div class="block">
      <h3>電路矩陣（0 = 加法, 1 = 乘法）</h3>
      <p class="hint">範例：[[0],[1,0]]　或任意閘數：[[0,1,1,0],[0,1]]</p>
      <textarea v-model="circuitText" rows="4" placeholder="請輸入電路矩陣（JSON 格式）"></textarea>
    </div>

    <div class="block">
      <h3>公開 Input 向量</h3>
      <p class="hint">範例：[3,5,2,7]</p>
      <textarea v-model="inputText" rows="2" placeholder="請輸入公開 input（JSON 格式）"></textarea>
    </div>

    <!-- 連線設定（可選） -->
    <div class="block">
      <button class="toggle-conn-btn" @click="showConn = !showConn">
        {{ showConn ? '▲ 收起自訂連線' : '▼ 自訂閘連線（選填）' }}
      </button>
      <div v-if="showConn" class="conn-section">
        <p class="hint">
          每層用 <code>@</code> 分隔各閘，每個閘填 <code>[左子index, 右子index]</code>。<br>
          不填則自動使用二元樹預設（第 j 個閘接 2j 和 2j+1）。<br>
          範例（對應上方 [[0,1,1,0],[0,1]]）：<br>
          <code>[[[0,0],[0,1],[1,1],[0,1]],[[0,1],[2,3]]]</code>
        </p>
        <textarea
          v-model="connText"
          rows="4"
          placeholder='留空 = 二元樹預設&#10;範例：[[[0,0],[0,1],[1,1],[0,1]],[[0,1],[2,3]]]'
        ></textarea>
      </div>
    </div>

    <p v-if="error" class="error">{{ error }}</p>

    <button class="btn" @click="submit">開始 GKR 驗證</button>
  </div>
</template>

<script setup>
import { ref } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();

const circuitText = ref("[[0],[1,0]]");
const inputText   = ref("[3,5,2,7]");
const connText    = ref("");
const showConn    = ref(false);
const error       = ref("");

function submit() {
  error.value = "";
  try {
    const circuit = JSON.parse(circuitText.value);
    const input   = JSON.parse(inputText.value);

    if (!Array.isArray(circuit)) throw new Error("電路必須是二維陣列");
    if (!Array.isArray(input))   throw new Error("input 必須是陣列");

    let connections = null;
    if (connText.value.trim()) {
      connections = JSON.parse(connText.value.trim());
      if (!Array.isArray(connections)) throw new Error("連線必須是三維陣列");
    }

    router.push({
      path: "/chat",
      query: {
        circuit:     JSON.stringify(circuit),
        input:       JSON.stringify(input),
        connections: connections ? JSON.stringify(connections) : ""
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
  font-size: 17px;
}

.title {
  font-size: 28px;
  font-weight: bold;
  margin-bottom: 20px;
}

.block { margin-bottom: 20px; }

textarea {
  width: 100%;
  font-family: monospace;
  font-size: 16px;
  padding: 12px;
  border: 1px solid #d1d5db;
  border-radius: 4px;
  margin-top: 4px;
  box-sizing: border-box;
}

textarea:focus {
  outline: none;
  border-color: #2563eb;
}

.hint {
  font-size: 15px;
  color: #6b7280;
  margin-bottom: 4px;
  line-height: 1.6;
}

.hint code {
  background: #f1f5f9;
  padding: 1px 5px;
  border-radius: 3px;
  font-size: 14px;
}

.toggle-conn-btn {
  background: none;
  border: 1px solid #d1d5db;
  border-radius: 6px;
  padding: 8px 14px;
  font-size: 15px;
  cursor: pointer;
  color: #374151;
  margin-bottom: 10px;
}

.toggle-conn-btn:hover { background: #f1f5f9; }

.conn-section { margin-top: 4px; }

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
  font-size: 17px;
  transition: background 0.2s;
}

.btn:hover { background: #1d4ed8; }
</style>
