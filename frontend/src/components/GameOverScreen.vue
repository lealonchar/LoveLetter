<template>
  <div class="w-full max-w-md space-y-6 text-center">
    <div>
      <p class="text-rose-400 text-sm uppercase tracking-widest mb-1">Game Over</p>
      <h2 class="text-4xl font-bold text-rose-100">{{ winner?.name }} wins!</h2>
      <p class="text-rose-400 mt-1">{{ winner?.tokens }} affection tokens 💌</p>
    </div>

    <div class="bg-rose-900/60 rounded-2xl p-6 space-y-3">
      <div
        v-for="p in sortedPlayers"
        :key="p.id"
        class="flex items-center justify-between bg-rose-800/40 rounded-xl px-4 py-2.5">
        <span class="text-rose-200 font-medium">{{ p.name }}</span>
        <span class="text-rose-300">{{ p.tokens }} 💌</span>
      </div>

      <button
        @click="reload"
        class="w-full bg-rose-500 hover:bg-rose-400 text-white font-semibold rounded-xl py-3 mt-2 transition-colors">
        Play Again
      </button>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useGameStore } from '../stores/gameStore'

const { state } = useGameStore()

const sortedPlayers = computed(() =>
  [...(state.gameState?.players ?? [])].sort((a, b) => b.tokens - a.tokens)
)

const winner = computed(() => sortedPlayers.value[0])

function reload() {
  window.location.reload()
}
</script>
