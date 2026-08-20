<template>
  <div class="w-full max-w-md space-y-6 text-center">
    <div>
      <p class="text-rose-400 text-sm uppercase tracking-widest mb-1">Game Over</p>
      <h2 class="text-4xl font-bold text-rose-100">{{ winnerTitle }}</h2>
      <p class="text-rose-400 mt-1">{{ winnerTokenText }}</p>
    </div>

    <div class="bg-rose-900/60 rounded-2xl p-6 space-y-3">
      <div
        v-for="p in sortedPlayers"
        :key="p.id"
        class="flex items-center justify-between bg-rose-800/40 rounded-xl px-4 py-2.5">
        <span class="flex items-center gap-2 text-rose-200 font-medium">
          {{ p.name }}
          <span
            v-if="winnerIds.has(p.id)"
            class="rounded-md border border-rose-400/40 bg-rose-500/15 px-2 py-0.5 text-[10px] font-bold uppercase text-rose-100">
            Winner
          </span>
        </span>
        <span class="text-rose-300">{{ p.tokens }} tokens</span>
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

const winnerIds = computed(() => {
  const explicitWinnerIds = state.gameState?.gameWinnerIds ?? []
  if (explicitWinnerIds.length > 0)
    return new Set(explicitWinnerIds)

  const roundsToWin = state.gameState?.roundsToWin ?? Number.POSITIVE_INFINITY
  const winners = sortedPlayers.value.filter(p => p.tokens >= roundsToWin)
  return new Set((winners.length > 0 ? winners : sortedPlayers.value.slice(0, 1)).map(p => p.id))
})

const winners = computed(() =>
  sortedPlayers.value.filter(p => winnerIds.value.has(p.id))
)

const winnerTitle = computed(() => {
  const names = winners.value.map(p => p.name)
  if (names.length === 0) return 'Game over'
  if (names.length === 1) return `${names[0]} wins!`
  if (names.length === 2) return `${names[0]} and ${names[1]} win!`
  return `${names.slice(0, -1).join(', ')}, and ${names[names.length - 1]} win!`
})

const winnerTokenText = computed(() => {
  if (winners.value.length === 0) return ''
  const highTokenCount = Math.max(...winners.value.map(p => p.tokens))
  return `${highTokenCount} affection tokens`
})

function reload() {
  window.location.reload()
}
</script>
