<template>
  <div class="w-full max-w-md space-y-6 text-center">
    <h2 class="text-3xl font-bold text-rose-200">Round Over</h2>

    <div class="bg-rose-900/60 rounded-2xl p-6 space-y-3">
      <!-- Standings -->
      <div class="space-y-2">
        <div
          v-for="p in sortedPlayers"
          :key="p.id"
          class="flex items-center justify-between bg-rose-800/40 rounded-xl px-4 py-2.5">
          <div class="flex items-center gap-2">
            <span class="text-rose-300 text-sm">{{ p.isAi ? '🤖' : '👤' }}</span>
            <span :class="['font-medium', p.id === state.myId ? 'text-rose-100' : 'text-rose-300']">
              {{ p.name }}
            </span>
          </div>
          <span class="text-rose-200 font-bold">{{ p.tokens }} / {{ state.gameState.roundsToWin }} 💌</span>
        </div>
      </div>

      <!-- Last 3 log entries -->
      <div class="border-t border-rose-800 pt-3 space-y-1">
        <p v-for="(entry, i) in recentLog" :key="i"
           class="text-rose-400 text-xs">{{ entry }}</p>
      </div>

      <button
        v-if="isHost"
        @click="startNextRound"
        class="w-full bg-rose-500 hover:bg-rose-400 text-white font-semibold rounded-xl py-3 transition-colors">
        Next Round
      </button>
      <p v-else class="text-rose-500 text-sm">Waiting for host to start next round…</p>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useGameStore } from '../stores/gameStore'

const { state, startNextRound } = useGameStore()

const isHost = computed(() => {
  return state.gameState?.hostId === state.myId
})

const sortedPlayers = computed(() =>
  [...(state.gameState?.players ?? [])].sort((a, b) => b.tokens - a.tokens)
)

const recentLog = computed(() =>
  (state.gameState?.log ?? state.gameState?.Log ?? []).slice(-3)
)
</script>
