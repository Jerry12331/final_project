<template>
  <div class="explanation-box">
    <div v-if="explanation.phase" class="phase-badge">{{ explanation.phase }}</div>

    <h3>現在在做什麼？</h3>
    <p class="main-text">{{ explanation.text }}</p>

    <div v-if="explanation.why" class="why-section">
      <h4>為什麼要做這步？</h4>
      <p class="why-text">{{ explanation.why }}</p>
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
defineProps({
  explanation: {
    type: Object,
    required: true
  }
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
