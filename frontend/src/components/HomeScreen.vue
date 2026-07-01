<template>
  <div class="w-full max-w-sm space-y-8 text-center">
    <div>
      <h1 class="text-5xl font-bold text-rose-200 tracking-wide mb-1">Love Letter</h1>
      <p class="text-rose-400 text-sm">A game of risk, deduction, and luck</p>
    </div>

    <div class="bg-rose-900/60 rounded-2xl p-6 space-y-4">
      <input
        v-model="name"
        placeholder="Your name"
        maxlength="16"
        class="w-full bg-rose-800/50 border border-rose-700 rounded-xl px-4 py-3
               text-rose-100 placeholder-rose-500 focus:outline-none focus:border-rose-400"
        @keyup.enter="mode === 'create' ? createRoom() : joinRoom()"
      />

      <div class="flex gap-2">
        <button
          :class="tabClass(mode === 'create')"
          @click="mode = 'create'">Create room</button>
        <button
          :class="tabClass(mode === 'join')"
          @click="mode = 'join'">Join room</button>
      </div>

      <input
        v-if="mode === 'join'"
        v-model="code"
        placeholder="Room code"
        maxlength="6"
        class="w-full bg-rose-800/50 border border-rose-700 rounded-xl px-4 py-3
               text-rose-100 placeholder-rose-500 uppercase tracking-widest
               focus:outline-none focus:border-rose-400"
        @keyup.enter="joinRoom"
      />

      <button
        @click="mode === 'create' ? createRoom() : joinRoom()"
        :disabled="!name.trim() || (mode === 'join' && code.length < 6)"
        class="w-full bg-rose-500 hover:bg-rose-400 disabled:opacity-40 disabled:cursor-not-allowed
               text-white font-semibold rounded-xl py-3 transition-colors">
        {{ mode === 'create' ? 'Create Room' : 'Join Room' }}
      </button>

      <button
        type="button"
        @click="rulesOpen = true"
        class="rules-open-btn">
        Game rules
      </button>
    </div>

    <Transition name="pop">
      <div v-if="rulesOpen" class="rules-modal-backdrop" role="dialog" aria-modal="true">
        <div class="rules-modal">
          <div class="rules-modal-head">
            <div>
              <p class="rules-eyebrow">Love Letter</p>
              <h2 class="rules-title">Game rules</h2>
            </div>
            <button type="button" class="rules-close-btn" @click="rulesOpen = false">
              Close
            </button>
          </div>

          <div class="rules-content">
            <section class="rules-section">
              <h3>Goal</h3>
              <p>Win rounds to gain affection tokens. With 3 players you need 5 tokens. With 4 or more players you need 4 tokens.</p>
            </section>

            <section class="rules-section">
              <h3>Your turn</h3>
              <p>Draw 1 card, then play 1 of your 2 cards. Follow the card effect. If you are protected by Handmaid, effects from other players cannot target you.</p>
            </section>

            <section class="rules-section">
              <h3>Round end</h3>
              <p>A round ends when only 1 player is still in, or when the draw pile is empty. If the pile is empty, the highest hand wins. Ties use discard values.</p>
            </section>

            <section class="rules-section">
              <h3>Cards</h3>
              <div class="rules-card-list">
                <div
                  v-for="card in cards"
                  :key="card.name"
                  class="rules-card">
                  <div class="rules-card-head">
                    <span class="rules-card-value">{{ card.value }}</span>
                    <span class="rules-card-name">{{ card.name }}</span>
                    <span class="rules-card-count">x{{ card.count }}</span>
                  </div>
                  <p>{{ card.description }}</p>
                </div>
              </div>
            </section>
          </div>
        </div>
      </div>
    </Transition>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useGameStore } from '../stores/gameStore'
import { cardReference as cards } from '../data/cardReference'

const { createRoom: create, joinRoom: join } = useGameStore()
const name = ref('')
const code = ref('')
const mode = ref('create')
const rulesOpen = ref(false)

function tabClass(active) {
  return `flex-1 py-2 rounded-lg text-sm font-medium transition-colors ${
    active
      ? 'bg-rose-500 text-white'
      : 'bg-rose-800/50 text-rose-400 hover:text-rose-200'
  }`
}

async function createRoom() {
  if (!name.value.trim()) return
  await create(name.value.trim())
}

async function joinRoom() {
  if (!name.value.trim() || code.value.length < 6) return
  await join(code.value.trim(), name.value.trim())
}
</script>

<style scoped>
.rules-open-btn {
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

.rules-open-btn:hover {
  background: rgba(159, 18, 57, 0.36);
  border-color: rgba(251, 113, 133, 0.46);
}

.rules-modal-backdrop {
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

.rules-modal {
  width: min(100%, 760px);
  max-height: min(760px, calc(100dvh - 36px));
  overflow-y: auto;
  border: 1px solid rgba(251, 113, 133, 0.28);
  background:
    radial-gradient(circle at top, rgba(159, 18, 57, 0.34), transparent 58%),
    rgba(20, 5, 8, 0.96);
  border-radius: 8px;
  box-shadow: 0 24px 80px rgba(0,0,0,0.55);
  padding: 20px;
  text-align: left;
}

.rules-modal-head {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 14px;
  margin-bottom: 16px;
}

.rules-eyebrow {
  margin: 0 0 2px;
  color: #fda4af;
  font-size: 11px;
  font-weight: 800;
  text-transform: uppercase;
}

.rules-title {
  margin: 0;
  color: #ffe4e6;
  font-size: 24px;
  font-weight: 800;
}

.rules-close-btn {
  border: 1px solid rgba(251, 113, 133, 0.28);
  background: rgba(255,255,255,0.05);
  color: #fecdd3;
  border-radius: 8px;
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 800;
}

.rules-content {
  display: grid;
  gap: 12px;
}

.rules-section {
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(255,255,255,0.035);
  border-radius: 8px;
  padding: 12px;
}

.rules-section h3 {
  margin: 0 0 6px;
  color: #ffe4e6;
  font-size: 14px;
  font-weight: 800;
}

.rules-section p,
.rules-list {
  margin: 0;
  color: #fecdd3;
  font-size: 13px;
  line-height: 1.45;
}

.rules-list {
  padding-left: 18px;
}

.rules-list li + li {
  margin-top: 5px;
}

.rules-card-list {
  display: grid;
  gap: 8px;
}

.rules-card {
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(0,0,0,0.18);
  border-radius: 8px;
  padding: 9px 10px;
}

.rules-card-head {
  display: grid;
  grid-template-columns: 24px minmax(0, 1fr) auto;
  align-items: center;
  gap: 8px;
  margin-bottom: 5px;
}

.rules-card-value {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 22px;
  height: 22px;
  border-radius: 6px;
  background: rgba(251, 113, 133, 0.16);
  color: #ffe4e6;
  font-size: 12px;
  font-weight: 800;
}

.rules-card-name {
  min-width: 0;
  color: #ffe4e6;
  font-size: 13px;
  font-weight: 800;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.rules-card-count {
  color: #fda4af;
  font-size: 12px;
  font-weight: 800;
}

.rules-card p {
  margin: 0;
  color: #fecdd3;
  font-size: 12px;
  line-height: 1.35;
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

@media (min-width: 720px) {
  .rules-modal {
    padding: 24px;
  }

  .rules-content {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }

  .rules-section:last-child {
    grid-column: 1 / -1;
  }

  .rules-card-list {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
</style>
