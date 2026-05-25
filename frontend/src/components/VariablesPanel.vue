<template>
  <div class="vars-panel">
    <div class="vars-header" @click="open = !open">
      <span class="vars-title">📐 目前協議變數</span>
      <span class="vars-toggle">{{ open ? '▲ 收起' : '▼ 展開' }}</span>
    </div>

    <div v-if="open" class="vars-body">
      <!-- 第一類：基礎資料 -->
      <div class="var-group">
        <div class="group-label cat-base">基礎資料</div>
        <div class="var-grid">
          <VarCard name="layer" :value="vars.layer" desc="目前驗證所在的電路層（0 = 輸出層）" />
          <VarCard name="inputValues" :value="vars.inputValues" desc="電路最底層的公開輸入值" />
        </div>
      </div>

      <!-- 第二類：對話核心 -->
      <div class="var-group">
        <div class="group-label cat-core">對話核心</div>
        <div class="var-grid">
          <VarCard name="fixed_var" :value="vars.fixedVar" desc="Verifier 選定的隨機求值座標，Prover 必須在此點正確回答" />
          <VarCard name="claimed" :value="vars.claimed" desc="Prover 目前宣稱的多項式求值結果" />
          <VarCard name="s（Sumcheck 隨機數）" :value="vars.s" desc="Verifier 在每輪 Sumcheck 隨機選取的挑戰點" />
        </div>
      </div>

      <!-- 第三類：安全機制 -->
      <div class="var-group">
        <div class="group-label cat-sec">安全機制</div>
        <div class="var-grid">
          <VarCard name="rho" :value="vars.rho" desc="用於混合遮掩多項式 g 的隨機係數，防止零知識洩漏" />
          <VarCard name="maskSum" :value="vars.maskSum" desc="遮掩多項式 g 在所有輸入的總和，用來抵消 rho 的影響" />
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import VarCard from './VarCard.vue'

defineProps({
  vars: {
    type: Object,
    required: true
  }
})

const open = ref(true)
</script>

<style scoped>
.vars-panel {
  margin-top: 16px;
  border: 1px solid #e5e7eb;
  border-radius: 10px;
  overflow: hidden;
  background: #fff;
}

.vars-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 16px;
  background: #f8fafc;
  cursor: pointer;
  border-bottom: 1px solid #e5e7eb;
  user-select: none;
}

.vars-header:hover { background: #f1f5f9; }

.vars-title {
  font-weight: 700;
  font-size: 16px;
  color: #1e3a5f;
}

.vars-toggle {
  font-size: 14px;
  color: #6b7280;
}

.vars-body {
  padding: 12px 16px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}

.var-group { display: flex; flex-direction: column; gap: 8px; }

.group-label {
  font-size: 13px;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  padding: 3px 10px;
  border-radius: 4px;
  width: fit-content;
}

.cat-base  { background: #dbeafe; color: #1d4ed8; }
.cat-core  { background: #dcfce7; color: #15803d; }
.cat-sec   { background: #fef9c3; color: #92400e; }

.var-grid {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
</style>
