<template>
  <button
    :class="['relative rounded-xl border-2 p-3 text-left transition-all cursor-pointer w-full',
      selected
        ? 'border-rose-400 bg-rose-700/60 shadow-lg scale-[1.02]'
        : 'border-rose-700 bg-rose-900/50 hover:border-rose-500 hover:bg-rose-800/50']">
    <div class="flex justify-between items-start mb-1">
      <span class="font-bold text-rose-100 text-sm">{{ card.name }}</span>
      <span class="text-rose-400 font-bold text-lg leading-none">{{ card.value }}</span>
    </div>
    <p class="text-rose-400 text-[11px] leading-tight">{{ card.description }}</p>
    <span v-if="label"
          class="absolute top-1.5 right-1.5 text-[9px] bg-rose-800 text-rose-400 px-1.5 py-0.5 rounded-full">
      {{ label }}
    </span>
  </button>
</template>

<script setup>
defineProps({
  card:     { type: Object,  required: true },
  selected: { type: Boolean, default: false },
  label:    { type: String,  default: '' },
})
</script>
