<template>
  <div class="controls">
    <button @click="$emit('prev')">Previous</button>
    <button @click="$emit('next')">Next</button>
  </div>
</template>

<style scoped>
.controls {
  display: flex;
  justify-content: space-between;
  padding: 16px;
}
</style>
