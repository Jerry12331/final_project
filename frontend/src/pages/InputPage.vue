<template>
  <div class="input-page">

    <h2 class="title">GKR Protocol Demo – Input Page</h2>

    <!-- ===== Circuit Input ===== -->
    <div class="block">
      <h3>電路矩陣（0 = 加法, 1 = 乘法）</h3>
      <p class="hint">
        範例：[[0,1],[1,0]]
      </p>
      <textarea
        v-model="circuitText"
        rows="6"
        placeholder="請輸入電路矩陣（JSON 格式）"
      ></textarea>
    </div>

    <!-- ===== Input Vector ===== -->
    <div class="block">
      <h3>Input 向量</h3>
      <p class="hint">
        範例：[1,0,1,1]
      </p>
      <textarea
        v-model="inputText"
        rows="3"
        placeholder="請輸入 input（JSON 格式）"
      ></textarea>
    </div>

    <!-- ===== Error Message ===== -->
    <p v-if="error" class="error">{{ error }}</p>

    <!-- ===== Submit Button ===== -->
    <button class="btn" @click="submit">
      開始 GKR 驗證
    </button>

  </div>
</template>

<script setup>
import { ref } from "vue";
import { useRouter } from "vue-router";

const router = useRouter();

const circuitText = ref("[[0,1],[1,0]]");
const inputText = ref("[1,0,1,1]");
const error = ref("");

function submit() {
  error.value = "";

  try {
    const circuit = JSON.parse(circuitText.value);
    const input = JSON.parse(inputText.value);

    if (!Array.isArray(circuit)) {
      throw new Error("電路必須是二維陣列");
    }
    if (!Array.isArray(input)) {
      throw new Error("input 必須是陣列");
    }

    // ⭐ 重點：用 router 把資料送到 ChatPage
    router.push({
      path: "/chat",
      query: {
        circuit: JSON.stringify(circuit),
        input: JSON.stringify(input)
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

textarea {
  width: 100%;
  font-family: monospace;
  padding: 8px;
}

.hint {
  font-size: 13px;
  color: #555;
  margin-bottom: 4px;
}

.error {
  color: red;
  margin-bottom: 10px;
}

.btn {
  background: #2563eb;
  color: white;
  padding: 10px 20px;
  border-radius: 6px;
}
</style>
