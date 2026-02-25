<template>
  <div class="header">
    <slot />
  </div>
</template>

<style scoped>
.header {
  position: sticky;
  top: 0;
  background: white;
  padding: 12px;
  border-bottom: 1px solid #ddd;
  z-index: 10;
}
</style>
