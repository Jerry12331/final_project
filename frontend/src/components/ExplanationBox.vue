<template>
  <div class="explanation-box">
    <h3>現在在做什麼？</h3>

    <p class="main-text">{{ explanation.text }}</p>

    <div v-if="explanation.variables?.length">
      <h4>變數說明</h4>
      <ul>
        <li v-for="(v, index) in explanation.variables" :key="index">
          <b>{{ v.name }}</b>: {{ v.desc }}
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

.main-text {
  margin-bottom: 10px;
  line-height: 1.55;
}

.explanation-box h3 {
  margin: 0 0 8px;
  font-size: 18px;
}

.explanation-box h4 {
  margin: 0 0 8px;
  font-size: 15px;
}

.explanation-box ul {
  margin: 0;
  padding-left: 16px;
}

.explanation-box li {
  margin-bottom: 6px;
  line-height: 1.45;
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
