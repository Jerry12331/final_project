<template>
  <div
    :class="[
      'p-3 my-2 rounded-lg max-w-[70%] shadow',
      side === 'left' ? 'bg-gray-200 ml-0' : 'bg-blue-200 ml-auto text-right'
    ]"
  >
    {{ text }}
  </div>
</template>

<script setup>
defineProps({
  text: String,
  side: String  // 'left' or 'right'
});
</script>
