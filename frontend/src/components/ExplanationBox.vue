<template>
  <div class="explanation-box">
    <div v-if="explanation.phase" class="phase-badge">{{ explanation.phase }}</div>

    <div v-if="explanation.templates" class="level-switch">
      <button
        v-for="opt in levelOptions"
        :key="opt.value"
        type="button"
        class="level-btn"
        :class="{ active: level === opt.value }"
        @click="level = opt.value"
      >{{ opt.label }}</button>
    </div>

    <h3>現在在做什麼？</h3>
    <p class="main-text">{{ displayText }}</p>

    <div v-if="displayWhy" class="why-section">
      <h4>為什麼要做這步？</h4>
      <p class="why-text">{{ displayWhy }}</p>
    </div>

    <div v-if="explanation.variables?.length">
      <h4>變數說明</h4>
      <ul>
        <li v-for="(v, index) in explanation.variables" :key="index">
          <b>{{ v.name }}</b>：{{ v.desc }}
        </li>
      </ul>
    </div>

    <div class="qa-section">
      <h4>還有其他問題嗎？</h4>
      <div class="qa-input-row">
        <input
          v-model="question"
          type="text"
          placeholder="還有其他問題嗎？"
          class="qa-input"
          maxlength="200"
          :disabled="asking"
          @keyup.enter="submitQuestion"
        />
        <button
          type="button"
          class="qa-submit-btn"
          :disabled="asking || !question.trim()"
          @click="submitQuestion"
        >{{ asking ? "送出中…" : "送出" }}</button>
      </div>

      <p v-if="qaError" class="qa-error">{{ qaError }}</p>

      <div v-if="qaAnswer" class="qa-answer">
        <div class="qa-question-echo">Q：{{ qaAskedQuestion }}</div>
        <p class="qa-answer-text">
          {{ qaAnswer }}
          <span v-if="qaFromCache" class="qa-cache-badge">⚡ 快速回覆</span>
        </p>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, watch } from "vue";

const props = defineProps({
  explanation: {
    type: Object,
    required: true
  }
});

const levelOptions = [
  { value: "brief", label: "簡短" },
  { value: "standard", label: "標準" },
  { value: "detailed", label: "詳細" }
];

const level = ref("standard");

// 有查到 json 範本時依詳細程度顯示對應文字，查不到就退回原本寫死的 text
const displayText = computed(() => {
  const templates = props.explanation.templates;
  return templates?.[level.value] ?? props.explanation.text;
});

// json 範本的文字本身已經包含「為什麼」，所以只有退回寫死文字時才需要另外顯示 why 區塊
const displayWhy = computed(() => {
  return props.explanation.templates ? "" : props.explanation.why;
});

// ── 自由提問：把目前步驟資訊 + 使用者問題送到後端 /api/ask_step ─────────────────
const question = ref("");
const asking = ref(false);
const qaAnswer = ref("");
const qaAskedQuestion = ref("");
const qaFromCache = ref(false);
const qaError = ref("");

// 換到別的步驟時，把上一步留下的問答內容清掉，避免答非所問
watch(
  () => [props.explanation.stepType, props.explanation.stepLayer, props.explanation.stepRound],
  () => {
    question.value = "";
    qaAnswer.value = "";
    qaAskedQuestion.value = "";
    qaFromCache.value = false;
    qaError.value = "";
  }
);

async function submitQuestion() {
  const q = question.value.trim();
  if (!q || asking.value) return;

  asking.value = true;
  qaError.value = "";

  try {
    const response = await fetch("http://localhost:5285/api/ask_step", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        type: props.explanation.stepType ?? null,
        layer: props.explanation.stepLayer ?? 0,
        round: props.explanation.stepRound ?? 0,
        data: props.explanation.stepData ?? null,
        question: q
      })
    });

    const body = await response.json().catch(() => ({}));

    if (!response.ok) {
      throw new Error(body?.error || `HTTP ${response.status}`);
    }

    qaAskedQuestion.value = q;
    // 後端 Program.cs 設定 PropertyNamingPolicy = null，JSON 欄位維持 C# 的 PascalCase（Answer / FromCache）
    qaAnswer.value = body.Answer ?? "";
    qaFromCache.value = !!body.FromCache;
    question.value = "";
  } catch (err) {
    qaError.value = "問題送出失敗：" + err.message;
  } finally {
    asking.value = false;
  }
}
</script>

<style scoped>
.explanation-box {
  position: fixed;
  top: 100px;
  right: 30px;

  width: 280px;
  max-height: calc(100vh - 120px);
  overflow-y: auto;
  padding: 16px;

  background: #ffffff;
  border: 1px solid #ddd;
  border-left: 4px solid #3b82f6;
  border-radius: 12px;

  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.1);
  z-index: 1000;
  transition: all 0.3s ease;
}

.phase-badge {
  display: inline-block;
  background: #eff6ff;
  color: #1d4ed8;
  border: 1px solid #bfdbfe;
  border-radius: 9999px;
  font-size: 11px;
  font-weight: 600;
  padding: 2px 10px;
  margin-bottom: 10px;
  letter-spacing: 0.03em;
}

.main-text {
  margin-bottom: 10px;
  line-height: 1.6;
  color: #1f2937;
  white-space: pre-line;
}

.level-switch {
  display: flex;
  gap: 6px;
  margin-bottom: 10px;
}

.level-btn {
  flex: 1;
  padding: 4px 0;
  font-size: 12px;
  font-weight: 600;
  color: #374151;
  background: #f3f4f6;
  border: 1px solid #e5e7eb;
  border-radius: 9999px;
  cursor: pointer;
  transition: all 0.15s ease;
}

.level-btn:hover {
  background: #e5e7eb;
}

.level-btn.active {
  background: #2563eb;
  border-color: #2563eb;
  color: white;
}

.why-section {
  background: #fefce8;
  border: 1px solid #fde68a;
  border-radius: 8px;
  padding: 10px 12px;
  margin-bottom: 12px;
}

.why-section h4 {
  margin: 0 0 6px;
  font-size: 13px;
  color: #92400e;
}

.why-text {
  margin: 0;
  font-size: 13px;
  color: #78350f;
  line-height: 1.55;
}

.explanation-box h3 {
  margin: 0 0 8px;
  font-size: 16px;
  color: #111827;
}

.explanation-box h4 {
  margin: 0 0 8px;
  font-size: 14px;
  color: #374151;
}

.explanation-box ul {
  margin: 0;
  padding-left: 16px;
}

.explanation-box li {
  margin-bottom: 6px;
  line-height: 1.45;
  font-size: 13px;
}

.qa-section {
  margin-top: 14px;
  padding-top: 12px;
  border-top: 1px solid #e5e7eb;
}

.qa-input-row {
  display: flex;
  gap: 6px;
}

.qa-input {
  flex: 1;
  min-width: 0;
  padding: 6px 10px;
  font-size: 13px;
  border: 1px solid #d1d5db;
  border-radius: 6px;
}

.qa-input:disabled {
  background: #f3f4f6;
}

.qa-submit-btn {
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 600;
  color: white;
  background: #2563eb;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  white-space: nowrap;
}

.qa-submit-btn:hover:not(:disabled) {
  background: #1d4ed8;
}

.qa-submit-btn:disabled {
  background: #93c5fd;
  cursor: not-allowed;
}

.qa-error {
  margin: 8px 0 0;
  font-size: 12px;
  color: #b91c1c;
}

.qa-answer {
  margin-top: 10px;
  padding: 10px 12px;
  background: #eff6ff;
  border: 1px solid #bfdbfe;
  border-radius: 8px;
}

.qa-question-echo {
  font-size: 12px;
  font-weight: 600;
  color: #1d4ed8;
  margin-bottom: 4px;
}

.qa-answer-text {
  margin: 0;
  font-size: 13px;
  color: #1f2937;
  line-height: 1.55;
  white-space: pre-line;
}

.qa-cache-badge {
  display: inline-block;
  margin-left: 6px;
  font-size: 11px;
  color: #a16207;
}

@media (max-width: 1100px) {
  .explanation-box {
    position: static;
    width: 100%;
    max-height: none;
    margin-top: 16px;
  }
}
</style>
