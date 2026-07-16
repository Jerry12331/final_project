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
  </div>
</template>

<script setup>
import { ref, computed } from "vue";

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

@media (max-width: 1100px) {
  .explanation-box {
    position: static;
    width: 100%;
    max-height: none;
    margin-top: 16px;
  }
}
</style>
