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
            class="player-row">
          <span class="player-kind">{{ player.isAi ? 'AI' : 'You' }}</span>

          <input
              v-if="isHost && player.isAi"
              class="ai-name-input"
              :value="player.name"
              maxlength="24"
              @change="renameAi(player.id, $event.target.value)"
              @keyup.enter="$event.target.blur()"
          />
          <span v-else class="player-name">{{ player.name }}</span>

          <span v-if="player.id === state.gameState?.hostId" class="host-badge">
            host
          </span>
        </div>

        <p class="text-rose-500 text-sm">
          {{ state.gameState?.players?.length ?? 0 }}/6 players - need at least 3
        </p>
      </div>

      <template v-if="isHost">
        <button
            v-if="(state.gameState?.players?.length ?? 0) < 6"
            @click="addAiPlayer"
            class="w-full bg-rose-800 hover:bg-rose-700 text-rose-200 rounded-xl py-2.5 text-sm transition-colors">
          Add AI player
        </button>

        <button
            @click="startGame"
            :disabled="(state.gameState?.players?.length ?? 0) < 3"
            class="w-full bg-rose-500 hover:bg-rose-400 disabled:opacity-40 disabled:cursor-not-allowed
                 text-white font-semibold rounded-xl py-3 transition-colors">
          Start Game
        </button>
      </template>
      <p v-else class="text-rose-500 text-sm">Waiting for host to start...</p>

      <button
          type="button"
          @click="leaveConfirmOpen = true"
          class="leave-room-btn">
        Leave room
      </button>
    </div>

    <Transition name="pop">
      <div v-if="leaveConfirmOpen" class="leave-modal-backdrop" role="dialog" aria-modal="true">
        <div class="leave-modal">
          <p class="leave-modal-title">Leave room?</p>
          <p class="leave-modal-copy">
            You will return to the home screen and give up your seat in this room.
          </p>
          <div class="leave-modal-actions">
            <button type="button" class="modal-btn modal-btn--quiet" @click="leaveConfirmOpen = false">
              Stay
            </button>
            <button type="button" class="modal-btn modal-btn--danger" @click="confirmLeave">
              Leave
            </button>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useGameStore } from '../stores/gameStore'

const { state, addAiPlayer, renameAiPlayer, startGame, leaveGame } = useGameStore()
const leaveConfirmOpen = ref(false)

const isHost = computed(() => {
  return state.gameState?.hostId === state.myId
})

async function renameAi(aiPlayerId, rawName) {
  const name = rawName.trim()
  if (!name) return
  await renameAiPlayer(aiPlayerId, name)
}

async function confirmLeave() {
  leaveConfirmOpen.value = false
  await leaveGame()
}
</script>

<style scoped>
.player-row {
  display: grid;
  grid-template-columns: auto minmax(0, 1fr) auto;
  align-items: center;
  gap: 10px;
  background: rgba(159, 18, 57, 0.32);
  border: 1px solid rgba(251, 113, 133, 0.16);
  border-radius: 12px;
  padding: 10px 12px;
}

.player-kind {
  min-width: 34px;
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.04em;
  color: #fda4af;
  text-transform: uppercase;
  text-align: left;
}

.player-name {
  min-width: 0;
  color: #ffe4e6;
  font-weight: 700;
  text-align: left;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.ai-name-input {
  min-width: 0;
  width: 100%;
  border-radius: 8px;
  border: 1px solid rgba(251, 113, 133, 0.36);
  background: rgba(76, 5, 25, 0.65);
  color: #ffe4e6;
  font-weight: 700;
  padding: 7px 9px;
  outline: none;
}

.ai-name-input:focus {
  border-color: #fb7185;
  box-shadow: 0 0 0 2px rgba(251, 113, 133, 0.18);
}

.host-badge {
  color: #fecdd3;
  background: rgba(136, 19, 55, 0.7);
  border: 1px solid rgba(251, 113, 133, 0.22);
  border-radius: 8px;
  font-size: 11px;
  font-weight: 700;
  padding: 3px 7px;
}

.leave-room-btn {
  width: 100%;
  border: 1px solid rgba(251, 113, 133, 0.28);
  background: rgba(76, 5, 25, 0.38);
  color: #fecdd3;
  border-radius: 12px;
  padding: 10px 14px;
  font-size: 13px;
  font-weight: 700;
  transition: background 0.16s ease, border-color 0.16s ease;
}

.leave-room-btn:hover {
  background: rgba(159, 18, 57, 0.36);
  border-color: rgba(251, 113, 133, 0.46);
}

.leave-modal-backdrop {
  position: fixed;
  inset: 0;
  z-index: 60;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 18px;
  background: rgba(10, 2, 2, 0.72);
  backdrop-filter: blur(6px);
}

.leave-modal {
  width: min(100%, 360px);
  border: 1px solid rgba(251, 113, 133, 0.28);
  background:
    radial-gradient(circle at top, rgba(159, 18, 57, 0.34), transparent 58%),
    rgba(20, 5, 8, 0.96);
  border-radius: 8px;
  box-shadow: 0 24px 80px rgba(0,0,0,0.55);
  padding: 20px;
  text-align: center;
}

.leave-modal-title {
  margin: 0;
  color: #ffe4e6;
  font-size: 20px;
  font-weight: 800;
}

.leave-modal-copy {
  margin: 10px 0 18px;
  color: #fecdd3;
  font-size: 13px;
  line-height: 1.45;
}

.leave-modal-actions {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 10px;
}

.modal-btn {
  border-radius: 8px;
  min-height: 42px;
  padding: 10px 12px;
  font-size: 13px;
  font-weight: 800;
  cursor: pointer;
  border: 1px solid rgba(251, 113, 133, 0.28);
}

.modal-btn--quiet {
  color: #fecdd3;
  background: rgba(255,255,255,0.05);
}

.modal-btn--danger {
  color: white;
  background: #be123c;
  border-color: #fb7185;
}

.modal-btn--quiet:hover {
  background: rgba(255,255,255,0.09);
}

.modal-btn--danger:hover {
  background: #e11d48;
}

.pop-enter-active,
.pop-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}

.pop-enter-from,
.pop-leave-to {
  opacity: 0;
  transform: scale(0.96);
}
</style>
