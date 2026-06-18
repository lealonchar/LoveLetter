<template>
  <div class="game-table">

    <!-- ── TOP PLAYER (opposite seat) ── -->
    <div class="seat seat-top">
      <OpponentSeat
          v-for="p in topPlayers"
          :key="p.id"
          :player="p"
          :is-current="isCurrentPlayer(p)"
      />
    </div>

    <!-- ── LEFT PLAYER ── -->
    <div class="seat seat-left">
      <OpponentSeat
          v-for="p in leftPlayers"
          :key="p.id"
          :player="p"
          :is-current="isCurrentPlayer(p)"
          orientation="vertical"
      />
    </div>

    <!-- ── RIGHT PLAYER ── -->
    <div class="seat seat-right">
      <OpponentSeat
          v-for="p in rightPlayers"
          :key="p.id"
          :player="p"
          :is-current="isCurrentPlayer(p)"
          orientation="vertical"
      />
    </div>

    <!-- ── TABLE CENTER ── -->
    <div class="table-center">

      <!-- Draw pile + set aside -->
      <div class="pile-area">
        <div class="card-back-stack">
          <div class="card-back" v-for="n in Math.min(state.gameState.drawPileCount, 5)" :key="n"
               :style="{ transform: `translateY(-${n * 1.5}px) translateX(${n * 0.5}px)` }" />
          <span class="pile-count">{{ state.gameState.drawPileCount }}</span>
        </div>
        <div class="card-back card-aside" title="Set aside card" />
      </div>

      <!-- Turn indicator -->
      <div class="turn-banner">
        <Transition name="fade" mode="out-in">
          <span :key="state.gameState.currentPlayerName">
            {{ isMyTurn ? "Your turn" : `${state.gameState.currentPlayerName}'s turn` }}
          </span>
        </Transition>
      </div>

      <!-- Tokens to win -->
      <div class="tokens-info">
        <span class="token-icon">💌</span>
        <span>{{ state.gameState.roundsToWin }} to win</span>
      </div>

      <!-- Priest reveal -->
      <Transition name="pop">
        <div v-if="state.priestReveal" class="priest-bubble">
          <span class="priest-eye">👁</span>
          <span>{{ getPlayerName(state.priestReveal.targetId) }} holds</span>
          <strong>{{ state.priestReveal.card }}</strong>
        </div>
      </Transition>
    </div>

    <!-- ── CHANCELLOR PICKER (full overlay) ── -->
    <Transition name="slide-up">
      <div v-if="isMyChancellorPending" class="chancellor-overlay">
        <p class="chancellor-title">Choose a card to keep</p>
        <p class="chancellor-sub">The other {{ state.gameState.chancellorOptions.length - 1 }} return to the bottom of the deck</p>
        <div class="chancellor-cards">
          <div
              v-for="card in state.gameState.chancellorOptions"
              :key="card.type"
              :class="['chancellor-card-option', selectedChancellorCard === card.type ? 'selected' : '']"
              @click="selectedChancellorCard = card.type">
            <CardFace :card="card" size="lg" />
          </div>
        </div>
        <button
            @click="confirmChancellor"
            :disabled="!selectedChancellorCard"
            class="confirm-btn">
          Keep this card
        </button>
      </div>
    </Transition>

    <!-- ── MY HAND (bottom) ── -->
    <div class="my-area">

      <!-- My info bar -->
      <div class="my-info-bar">
        <div class="my-tokens">
          <span v-for="n in state.gameState.roundsToWin" :key="n"
                :class="['token-dot', n <= (myPlayer?.tokens ?? 0) ? 'filled' : '']" />
        </div>
        <span class="my-name">{{ myPlayer?.name ?? '' }}</span>
        <span v-if="myPlayer?.isProtected" class="shield-badge">🛡 Protected</span>
      </div>

      <!-- My discard pile (face up, fanned) -->
      <div v-if="myPlayer?.discards?.length" class="my-discards">
        <div
            v-for="(card, i) in myPlayer.discards"
            :key="i"
            class="discard-card"
            :style="discardStyle(i, myPlayer.discards.length)"
            :title="card.name">
          <CardFace :card="card" size="sm" />
        </div>
      </div>

      <!-- Waiting message -->
      <div v-if="!isMyTurn && !isMyChancellorPending && myPlayer && !myPlayer.isEliminated"
           class="waiting-msg">
        Waiting for {{ state.gameState.currentPlayerName }}…
      </div>

      <div v-if="myPlayer?.isEliminated" class="eliminated-msg">
        You've been eliminated — spectating
      </div>

      <!-- My hand cards -->
      <div v-if="isMyTurn && !isMyChancellorPending" class="my-hand">

        <!-- Hand card -->
        <div
            v-if="myPlayer?.hand"
            :class="['hand-card', selectedCard === myPlayer.hand.type ? 'hand-card--selected' : '']"
            @click="selectCard(myPlayer.hand.type)">
          <CardFace :card="myPlayer.hand" size="lg" />
          <span class="card-label">In hand</span>
        </div>

        <!-- Drawn card -->
        <div
            v-if="state.gameState.drawnCard"
            :class="['hand-card hand-card--drawn', selectedCard === state.gameState.drawnCard.type ? 'hand-card--selected' : '']"
            @click="selectCard(state.gameState.drawnCard.type)">
          <CardFace :card="state.gameState.drawnCard" size="lg" />
          <span class="card-label card-label--new">Just drawn</span>
        </div>
      </div>

      <!-- Action panel (target + guess) -->
      <Transition name="slide-up">
        <div v-if="selectedCard && isMyTurn" class="action-panel">

          <!-- Target buttons -->
          <div v-if="needsTarget(selectedCard)" class="target-row">
            <span class="action-label">Target:</span>
            <button
                v-for="p in validTargets"
                :key="p.id"
                :class="['target-btn', selectedTarget === p.id ? 'target-btn--active' : '']"
                @click="selectedTarget = p.id">
              {{ p.name }}
            </button>
            <span v-if="validTargets.length === 0" class="no-targets">No valid targets</span>
          </div>

          <!-- Guard guess -->
          <div v-if="selectedCard === 'Guard' && selectedTarget" class="guess-row">
            <span class="action-label">Guess:</span>
            <button
                v-for="ct in guardGuesses"
                :key="ct"
                :class="['guess-btn', selectedGuess === ct ? 'guess-btn--active' : '']"
                @click="selectedGuess = ct">
              {{ ct }}
            </button>
          </div>

          <!-- Play button -->
          <button
              @click="confirmPlay"
              :disabled="!canConfirm"
              class="confirm-btn">
            Play {{ selectedCard }}
          </button>
        </div>
      </Transition>
    </div>

    <!-- ── COLLAPSIBLE LOG ── -->
    <div :class="['log-panel', logOpen ? 'log-panel--open' : '']">
      <button class="log-toggle" @click="logOpen = !logOpen">
        {{ logOpen ? '✕ Close log' : '📜 Game log' }}
      </button>
      <div v-if="logOpen" class="log-entries">
        <p
            v-for="(entry, i) in [...state.gameState.log].reverse()"
            :key="i"
            class="log-entry">
          {{ entry }}
        </p>
      </div>
    </div>

  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useGameStore } from '../stores/gameStore'
import CardFace from './CardFace.vue'
import OpponentSeat from './OpponentSeat.vue'

const { state, myPlayer, isMyTurn, playCard, resolveChancellor } = useGameStore()

const selectedCard = ref(null)
const selectedTarget = ref(null)
const selectedGuess = ref(null)
const selectedChancellorCard = ref(null)
const logOpen = ref(false)

const guardGuesses = ['Priest', 'Baron', 'Handmaid', 'Prince', 'Chancellor', 'King', 'Countess', 'Princess']
const targetedCards = ['Guard', 'Priest', 'Baron', 'King']
const optionalTargetCards = ['Prince']

function needsTarget(card) {
  return targetedCards.includes(card) || optionalTargetCards.includes(card)
}

const isMyChancellorPending = computed(() =>
    state.gameState?.pendingAction === 'Chancellor' &&
    state.gameState?.chancellorOptions?.length > 0
)

// Distribute opponents around the table
const opponents = computed(() =>
    state.gameState?.players.filter(p => p.id !== state.myId) ?? []
)

const topPlayers = computed(() => {
  const ops = opponents.value
  if (ops.length <= 2) return ops.slice(0, 1)
  if (ops.length === 3) return ops.slice(1, 2)
  return ops.slice(1, ops.length - 1)
})

const leftPlayers = computed(() => {
  const ops = opponents.value
  if (ops.length === 0) return []
  return [ops[0]]
})

const rightPlayers = computed(() => {
  const ops = opponents.value
  if (ops.length < 2) return []
  return [ops[ops.length - 1]]
})

const validTargets = computed(() => {
  if (!selectedCard.value) return []
  const base = state.gameState.players.filter(p =>
      !p.isEliminated && !p.isProtected && p.id !== state.myId
  )
  if (selectedCard.value === 'Prince') {
    const me = state.gameState.players.find(p => p.id === state.myId)
    return me ? [me, ...base] : base
  }
  return base
})

function selectCard(type) {
  selectedCard.value = type
  selectedTarget.value = null
  selectedGuess.value = null
}

function isCurrentPlayer(p) {
  return state.gameState?.players[state.gameState.currentPlayerId_Index]?.id === p.id
}

function getPlayerName(id) {
  return state.gameState?.players.find(p => p.id === id)?.name ?? 'Unknown'
}

function discardStyle(index, total) {
  const spread = Math.min(total * 28, 120)
  const start = -spread / 2
  const step = total > 1 ? spread / (total - 1) : 0
  return {
    transform: `translateX(${start + index * step}px) rotate(${(index - (total - 1) / 2) * 4}deg)`,
    zIndex: index,
  }
}

const canConfirm = computed(() => {
  if (!selectedCard.value) return false
  if (needsTarget(selectedCard.value) && validTargets.value.length > 0 && !selectedTarget.value) return false
  if (selectedCard.value === 'Guard' && selectedTarget.value && !selectedGuess.value) return false
  return true
})

async function confirmPlay() {
  if (!canConfirm.value) return
  await playCard(selectedCard.value, selectedTarget.value ?? null, selectedGuess.value ?? null)
  selectedCard.value = null
  selectedTarget.value = null
  selectedGuess.value = null
}

async function confirmChancellor() {
  if (!selectedChancellorCard.value) return
  await resolveChancellor(selectedChancellorCard.value)
  selectedChancellorCard.value = null
}
</script>

<style scoped>
/* ── Layout ── */
.game-table {
  width: 100vw;
  height: 100vh;
  background: radial-gradient(ellipse at center, #1a0a0a 0%, #0d0404 100%);
  display: grid;
  grid-template-areas:
    ". top ."
    "left center right"
    ". bottom .";
  grid-template-rows: auto 1fr auto;
  grid-template-columns: 180px 1fr 180px;
  position: relative;
  overflow: hidden;
}

/* subtle felt texture */
.game-table::before {
  content: '';
  position: absolute;
  inset: 0;
  background-image: radial-gradient(circle at 1px 1px, rgba(255,255,255,0.015) 1px, transparent 0);
  background-size: 24px 24px;
  pointer-events: none;
}

/* ── Seats ── */
.seat { display: flex; align-items: center; justify-content: center; gap: 16px; padding: 12px; }
.seat-top  { grid-area: top; }
.seat-left { grid-area: left; flex-direction: column; }
.seat-right { grid-area: right; flex-direction: column; }

/* ── Table center ── */
.table-center {
  grid-area: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  position: relative;
}

/* ── Pile area ── */
.pile-area {
  display: flex;
  gap: 32px;
  align-items: flex-end;
}

.card-back-stack {
  position: relative;
  width: 64px;
  height: 90px;
}

.card-back {
  position: absolute;
  width: 64px;
  height: 90px;
  border-radius: 8px;
  background: linear-gradient(135deg, #7f1d1d 0%, #450a0a 50%, #7f1d1d 100%);
  border: 2px solid rgba(255,200,150,0.25);
  box-shadow: 0 2px 8px rgba(0,0,0,0.6);
}

.card-back.card-aside {
  position: relative;
  opacity: 0.5;
  transform: rotate(-5deg);
}

.pile-count {
  position: absolute;
  bottom: -20px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 11px;
  color: #9f8070;
  white-space: nowrap;
}

/* ── Turn banner ── */
.turn-banner {
  font-size: 13px;
  font-weight: 600;
  color: #fda4af;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  background: rgba(255,255,255,0.04);
  border: 1px solid rgba(255,255,255,0.08);
  border-radius: 999px;
  padding: 6px 18px;
}

.tokens-info {
  font-size: 12px;
  color: #9f8070;
  display: flex;
  align-items: center;
  gap: 4px;
}

/* ── Priest bubble ── */
.priest-bubble {
  background: rgba(88, 28, 135, 0.85);
  border: 1px solid #a855f7;
  border-radius: 12px;
  padding: 10px 16px;
  font-size: 13px;
  color: #e9d5ff;
  display: flex;
  align-items: center;
  gap: 8px;
  backdrop-filter: blur(8px);
}

/* ── My area ── */
.my-area {
  grid-area: bottom;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 16px;
  padding-bottom: 24px;
}

.my-info-bar {
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 13px;
  color: #fda4af;
}

.my-name { font-weight: 700; }

.my-tokens { display: flex; gap: 4px; }
.token-dot {
  width: 10px; height: 10px;
  border-radius: 50%;
  border: 1.5px solid #9f1239;
  background: transparent;
  transition: background 0.3s;
}
.token-dot.filled { background: #f43f5e; }

.shield-badge {
  font-size: 11px;
  background: rgba(56, 189, 248, 0.15);
  border: 1px solid #38bdf8;
  color: #7dd3fc;
  border-radius: 999px;
  padding: 2px 8px;
}

/* ── Discard pile (fanned) ── */
.my-discards {
  position: relative;
  height: 72px;
  width: 100%;
  display: flex;
  justify-content: center;
}

.discard-card {
  position: absolute;
  transition: transform 0.3s ease;
}

/* ── Hand cards ── */
.my-hand {
  display: flex;
  gap: 24px;
  align-items: flex-end;
}

.hand-card {
  cursor: pointer;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  position: relative;
  border-radius: 12px;
}

.hand-card:hover {
  transform: translateY(-12px);
}

.hand-card--selected {
  transform: translateY(-20px);
  box-shadow: 0 0 0 3px #f43f5e, 0 8px 32px rgba(244, 63, 94, 0.4);
}

.hand-card--drawn::after {
  content: '';
  position: absolute;
  inset: -3px;
  border-radius: 14px;
  background: transparent;
  border: 2px dashed rgba(251, 191, 36, 0.5);
  pointer-events: none;
  animation: pulse-border 1.5s ease infinite;
}

@keyframes pulse-border {
  0%, 100% { opacity: 0.5; }
  50% { opacity: 1; }
}

.card-label {
  position: absolute;
  bottom: -20px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 10px;
  color: #9f8070;
  white-space: nowrap;
}

.card-label--new { color: #fbbf24; }

/* ── Action panel ── */
.action-panel {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 10px;
  background: rgba(0,0,0,0.6);
  border: 1px solid rgba(255,255,255,0.08);
  border-radius: 16px;
  padding: 14px 20px;
  backdrop-filter: blur(12px);
  width: 100%;
  max-width: 480px;
}

.action-label {
  font-size: 11px;
  color: #9f8070;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  margin-right: 8px;
}

.target-row, .guess-row {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 6px;
  width: 100%;
}

.target-btn, .guess-btn {
  font-size: 12px;
  padding: 5px 12px;
  border-radius: 999px;
  border: 1px solid #9f1239;
  background: transparent;
  color: #fda4af;
  cursor: pointer;
  transition: all 0.15s;
}

.target-btn:hover, .guess-btn:hover {
  background: rgba(159, 18, 57, 0.3);
}

.target-btn--active, .guess-btn--active {
  background: #9f1239;
  color: white;
  border-color: #f43f5e;
}

.no-targets { font-size: 12px; color: #6b3030; font-style: italic; }

.confirm-btn {
  width: 100%;
  max-width: 280px;
  padding: 12px;
  border-radius: 12px;
  background: #9f1239;
  color: white;
  font-weight: 700;
  font-size: 14px;
  border: none;
  cursor: pointer;
  transition: background 0.2s;
}

.confirm-btn:hover:not(:disabled) { background: #be123c; }
.confirm-btn:disabled { opacity: 0.4; cursor: not-allowed; }

/* ── Chancellor overlay ── */
.chancellor-overlay {
  position: absolute;
  inset: 0;
  background: rgba(10, 2, 2, 0.92);
  backdrop-filter: blur(8px);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  z-index: 50;
  padding: 24px;
}

.chancellor-title {
  font-size: 20px;
  font-weight: 700;
  color: #fda4af;
}

.chancellor-sub {
  font-size: 13px;
  color: #9f8070;
}

.chancellor-cards {
  display: flex;
  gap: 24px;
  flex-wrap: wrap;
  justify-content: center;
}

.chancellor-card-option {
  cursor: pointer;
  border-radius: 12px;
  transition: transform 0.2s, box-shadow 0.2s;
}

.chancellor-card-option:hover { transform: translateY(-8px); }
.chancellor-card-option.selected {
  transform: translateY(-12px);
  box-shadow: 0 0 0 3px #f43f5e, 0 8px 32px rgba(244,63,94,0.4);
}

/* ── Log panel ── */
.log-panel {
  position: fixed;
  bottom: 0;
  right: 16px;
  width: 280px;
  z-index: 40;
}

.log-toggle {
  background: rgba(30, 8, 8, 0.9);
  border: 1px solid rgba(255,255,255,0.1);
  border-bottom: none;
  color: #9f8070;
  font-size: 11px;
  padding: 6px 14px;
  border-radius: 8px 8px 0 0;
  cursor: pointer;
  width: 100%;
  text-align: left;
  transition: color 0.2s;
}

.log-toggle:hover { color: #fda4af; }

.log-entries {
  background: rgba(10, 2, 2, 0.95);
  border: 1px solid rgba(255,255,255,0.08);
  border-bottom: none;
  max-height: 200px;
  overflow-y: auto;
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.log-entry {
  font-size: 11px;
  color: #7c5050;
  line-height: 1.5;
}

/* ── Waiting / eliminated ── */
.waiting-msg { font-size: 13px; color: #7c5050; font-style: italic; }
.eliminated-msg { font-size: 13px; color: #6b3030; }

/* ── Transitions ── */
.fade-enter-active, .fade-leave-active { transition: opacity 0.3s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }

.slide-up-enter-active, .slide-up-leave-active { transition: all 0.3s ease; }
.slide-up-enter-from, .slide-up-leave-to { opacity: 0; transform: translateY(20px); }

.pop-enter-active { transition: all 0.25s cubic-bezier(0.34, 1.56, 0.64, 1); }
.pop-leave-active { transition: all 0.2s ease; }
.pop-enter-from, .pop-leave-to { opacity: 0; transform: scale(0.8); }
</style>