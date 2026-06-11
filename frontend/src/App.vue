<template>
  <div class="min-h-screen bg-rose-950 text-rose-50 font-serif flex flex-col items-center justify-center p-4">

    <!-- Error toast -->
    <Transition name="toast">
      <div v-if="state.pendingError"
           class="fixed top-4 left-1/2 -translate-x-1/2 bg-red-700 text-white px-5 py-3 rounded-xl shadow-lg z-50 text-sm">
        {{ state.pendingError }}
      </div>
    </Transition>

    <div v-if="state.isConnecting" class="text-rose-300 text-lg animate-pulse">
      Connecting to server…
    </div>

    <template v-else>
      <!-- Home screen -->
      <HomeScreen v-if="!state.roomCode" />

      <!-- Lobby (waiting for players) -->
      <LobbyScreen
        v-else-if="state.gameState?.phase === 'Lobby' || !state.gameState"
      />

      <!-- Game screen -->
      <GameScreen
        v-else-if="state.gameState?.phase === 'Playing'"
      />

      <!-- Round over -->
      <RoundOverScreen
        v-else-if="state.gameState?.phase === 'RoundOver'"
      />

      <!-- Game over -->
      <GameOverScreen
        v-else-if="state.gameState?.phase === 'GameOver'"
      />
    </template>
  </div>
</template>

<script setup>
import { onMounted } from 'vue'
import { useGameStore } from './stores/gameStore'
import HomeScreen from './components/HomeScreen.vue'
import LobbyScreen from './components/LobbyScreen.vue'
import GameScreen from './components/GameScreen.vue'
import RoundOverScreen from './components/RoundOverScreen.vue'
import GameOverScreen from './components/GameOverScreen.vue'

const { state, init } = useGameStore()
onMounted(() => init())
</script>

<style>
.toast-enter-active, .toast-leave-active { transition: all 0.3s ease; }
.toast-enter-from, .toast-leave-to { opacity: 0; transform: translateX(-50%) translateY(-12px); }
</style>
