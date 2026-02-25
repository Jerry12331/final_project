<template>
  <div class="border rounded shadow">
    <div
      class="bg-gray-100 px-4 py-2 cursor-pointer flex justify-between items-center"
      @click="$emit('toggle')"
    >
      <span>{{ title }}</span>
      <span>{{ open ? "▼" : "►" }}</span>
    </div>

    <div v-if="open" class="p-2">
      <slot></slot>
    </div>
  </div>
</template>

<script setup>
defineProps({
  title: String,
  open: Boolean
});
</script>
