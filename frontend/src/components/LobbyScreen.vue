<template>
  <div class="w-full max-w-md space-y-6 text-center">
    <h2 class="text-3xl font-bold text-rose-200">Waiting for players</h2>

    <div class="bg-rose-900/60 rounded-2xl p-6 space-y-4">
      <div>
        <p class="text-rose-400 text-sm mb-1">Room code</p>
        <p class="text-4xl font-bold tracking-[0.3em] text-rose-100">{{ state.roomCode }}</p>
        <p class="text-rose-500 text-xs mt-1">Share this with friends</p>
      </div>

      <div class="border-t border-rose-800 pt-4 space-y-2">
        <div
          v-for="player in state.gameState?.players ?? []"
          :key="player.id"
          class="flex items-center gap-3 bg-rose-800/40 rounded-xl px-4 py-3">
          <span class="text-rose-300">{{ player.isAi ? '🤖' : '👤' }}</span>
          <span class="text-rose-100 font-medium">{{ player.name }}</span>
          <span v-if="player.id === state.gameState?.players[0]?.id"
                class="ml-auto text-xs text-rose-400 bg-rose-800 px-2 py-0.5 rounded-full">
            host
          </span>
        </div>

        <p class="text-rose-500 text-sm">
          {{ state.gameState?.players?.length ?? 0 }}/4 players · need at least 2
        </p>
      </div>

      <!-- Host controls -->
      <template v-if="isHost">
        <button
          v-if="(state.gameState?.players?.length ?? 0) < 4"
          @click="addAiPlayer"
          class="w-full bg-rose-800 hover:bg-rose-700 text-rose-200 rounded-xl py-2.5 text-sm transition-colors">
          + Add AI player
        </button>

        <button
          @click="startGame"
          :disabled="(state.gameState?.players?.length ?? 0) < 2"
          class="w-full bg-rose-500 hover:bg-rose-400 disabled:opacity-40 disabled:cursor-not-allowed
                 text-white font-semibold rounded-xl py-3 transition-colors">
          Start Game
        </button>
      </template>
      <p v-else class="text-rose-500 text-sm">Waiting for host to start…</p>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useGameStore } from '../stores/gameStore'

const { state, addAiPlayer, startGame } = useGameStore()

const isHost = computed(() => {
  const players = state.gameState?.players
  return players && players.length > 0 && players[0].id === state.myId
})
</script>
