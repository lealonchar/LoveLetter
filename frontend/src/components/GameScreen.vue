<template>
  <div class="game-table">
    <button type="button" class="leave-game-btn" @click="openLeaveConfirm">
      Leave game
    </button>

    <div class="opponents-bar">
      <OpponentSeat
          v-for="p in opponents"
          :key="p.id"
          :player="p"
          :is-current="isCurrentPlayer(p)"
      />
    </div>

    <main class="table-center">
      <div class="pile-area">
        <div class="card-back-stack" aria-label="Draw pile">
          <div
              v-for="n in Math.min(state.gameState.drawPileCount, 5)"
              :key="n"
              class="card-back"
              :style="{ transform: `translateY(-${n * 1.5}px) translateX(${n * 0.5}px)` }"
          />
          <span class="pile-count">{{ state.gameState.drawPileCount }}</span>
        </div>
        <div class="card-back card-aside" title="Set aside card" />
      </div>

      <div class="turn-banner">
        <Transition name="fade" mode="out-in">
          <span :key="state.gameState.currentPlayerName">
            {{ isMyTurn ? "Your turn" : `${state.gameState.currentPlayerName}'s turn` }}
          </span>
        </Transition>
      </div>

      <div class="tokens-info">{{ state.gameState.roundsToWin }} tokens to win</div>

      <Transition name="table-event" mode="out-in">
        <div v-if="tableEvent" :key="tableEventKey" class="table-event-toast">
          {{ tableEvent }}
        </div>
      </Transition>

      <Transition name="pop">
        <div v-if="state.priestReveal" class="priest-bubble">
          <span>{{ getPlayerName(state.priestReveal.targetId) }} holds</span>
          <strong>{{ state.priestReveal.card }}</strong>
        </div>
      </Transition>
    </main>

    <aside :class="['log-panel', logOpen ? 'log-panel--open' : '']" aria-label="Game log">
      <button type="button" class="log-toggle" @click="logOpen = !logOpen">
        <span>{{ logOpen ? 'Close log' : 'Game log' }}</span>
        <span class="log-count">{{ gameLog.length }}</span>
      </button>
      <div v-if="logOpen" class="log-entries">
        <p v-if="gameLog.length === 0" class="log-entry log-entry--empty">
          No log entries yet.
        </p>
        <p
            v-for="(entry, i) in gameLog"
            :key="i"
            class="log-entry">
          {{ entry }}
        </p>
      </div>
    </aside>

    <aside :class="['reference-panel', cardReferenceOpen ? 'reference-panel--open' : '']" aria-label="Card reference">
      <button type="button" class="reference-toggle" @click="cardReferenceOpen = !cardReferenceOpen">
        <span>{{ cardReferenceOpen ? 'Close cards' : 'Cards' }}</span>
        <span class="reference-count">{{ totalCardCount }}</span>
      </button>
      <div v-if="cardReferenceOpen" class="reference-entries">
        <div
            v-for="card in cardReference"
            :key="card.type"
            class="reference-card">
          <div class="reference-card-head">
            <span class="reference-value">{{ card.value }}</span>
            <span class="reference-name">{{ card.name }}</span>
            <span class="reference-copy">x{{ card.count }}</span>
          </div>
          <p class="reference-description">{{ card.description }}</p>
        </div>
      </div>
    </aside>

    <section :class="['my-area', selectedCard && isMyTurn ? 'my-area--acting' : '']">
      <div class="my-info-bar">
        <div class="my-tokens">
          <span
              v-for="n in state.gameState.roundsToWin"
              :key="n"
              :class="['token-dot', n <= (myPlayer?.tokens ?? 0) ? 'filled' : '']"
          />
        </div>
        <span class="my-name">{{ myPlayer?.name ?? '' }}</span>
        <span v-if="myPlayer?.isProtected" class="shield-badge">Protected</span>
      </div>

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

      <div v-if="!isMyTurn && !isMyChancellorPending && myPlayer && !myPlayer.isEliminated" class="waiting-msg">
        Waiting for {{ state.gameState.currentPlayerName }}...
      </div>

      <div v-if="myPlayer?.isEliminated" class="eliminated-msg">
        You've been eliminated - spectating
      </div>

      <div v-if="myPlayer?.hand && !isMyChancellorPending && !myPlayer.isEliminated" class="my-hand">
        <button
            type="button"
            :disabled="!isMyTurn"
            :class="['hand-card', !isMyTurn ? 'hand-card--disabled' : '', selectedCard === myPlayer.hand.type ? 'hand-card--selected' : '']"
            @click="selectCard(myPlayer.hand.type)">
          <CardFace :card="myPlayer.hand" size="lg" />
          <span class="card-label">In hand</span>
        </button>

        <button
            v-if="state.gameState.drawnCard"
            type="button"
            :class="['hand-card hand-card--drawn', selectedCard === state.gameState.drawnCard.type ? 'hand-card--selected' : '']"
            @click="selectCard(state.gameState.drawnCard.type)">
          <CardFace :card="state.gameState.drawnCard" size="lg" />
          <span class="card-label card-label--new">Just drawn</span>
        </button>
      </div>

      <Transition name="slide-up">
        <div v-if="selectedCard && isMyTurn" class="action-panel">
          <div v-if="needsTarget(selectedCard)" class="choice-row">
            <span class="action-label">Target</span>
            <button
                v-for="p in validTargets"
                :key="p.id"
                type="button"
                :class="['choice-btn', selectedTarget === p.id ? 'choice-btn--active' : '']"
                @click="selectedTarget = p.id">
              {{ p.name }}
            </button>
            <span v-if="validTargets.length === 0" class="no-targets">No valid targets</span>
          </div>

          <div v-if="selectedCard === 'Guard' && selectedTarget" class="choice-row">
            <span class="action-label">Guess</span>
            <button
                v-for="ct in guardGuesses"
                :key="ct"
                type="button"
                :class="['choice-btn', selectedGuess === ct ? 'choice-btn--active' : '']"
                @click="selectedGuess = ct">
              {{ ct }}
            </button>
          </div>

          <button
              type="button"
              @click="confirmPlay"
              :disabled="!canConfirm"
              class="confirm-btn">
            Play {{ selectedCard }}
          </button>
        </div>
      </Transition>
    </section>

    <Transition name="slide-up">
      <div v-if="isMyChancellorPending" class="chancellor-overlay">
        <p class="chancellor-title">Choose a card to keep</p>
        <p class="chancellor-sub">The other {{ state.gameState.chancellorOptions.length - 1 }} return to the bottom of the deck</p>
        <div class="chancellor-cards">
          <button
              v-for="(card, index) in state.gameState.chancellorOptions"
              :key="`${card.type}-${index}`"
              type="button"
              :class="['chancellor-card-option', selectedChancellorCard === card.type ? 'selected' : '']"
              @click="selectedChancellorCard = card.type">
            <CardFace :card="card" size="lg" />
          </button>
        </div>
        <button
            type="button"
            @click="confirmChancellor"
            :disabled="!selectedChancellorCard"
            class="confirm-btn">
          Keep this card
        </button>
      </div>
    </Transition>

    <Transition name="pop">
      <div v-if="leaveConfirmOpen" class="leave-modal-backdrop" role="dialog" aria-modal="true">
        <div class="leave-modal">
          <p class="leave-modal-title">Leave game?</p>
          <p class="leave-modal-copy">
            You will return to the home screen and give up your seat at this table.
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
import { ref, computed, watch } from 'vue'
import { useGameStore } from '../stores/gameStore'
import CardFace from './CardFace.vue'
import OpponentSeat from './OpponentSeat.vue'
import { cardReference, totalCardCount } from '../data/cardReference'

const { state, myPlayer, isMyTurn, playCard, resolveChancellor, leaveGame } = useGameStore()

const selectedCard = ref(null)
const selectedTarget = ref(null)
const selectedGuess = ref(null)
const selectedChancellorCard = ref(null)
const logOpen = ref(false)
const cardReferenceOpen = ref(false)
const leaveConfirmOpen = ref(false)
const tableEvent = ref(null)
const tableEventKey = ref(0)
let tableEventTimer = null
let lastSeenLogEntry = null

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

const opponents = computed(() =>
    state.gameState?.players.filter(p => p.id !== state.myId) ?? []
)

const gameLog = computed(() =>
    state.gameState?.log ?? state.gameState?.Log ?? []
)

watch(
    () => {
      const entries = gameLog.value
      const latest = entries[entries.length - 1]
      return latest ? `${entries.length}:${latest}` : null
    },
    (entryKey) => {
      if (!entryKey || entryKey === lastSeenLogEntry) return
      lastSeenLogEntry = entryKey

      const latest = gameLog.value[gameLog.value.length - 1]
      tableEvent.value = latest
      tableEventKey.value += 1

      if (tableEventTimer) clearTimeout(tableEventTimer)
      tableEventTimer = setTimeout(() => {
        tableEvent.value = null
      }, 7000)
    }
)

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
  const spread = Math.min(total * 22, 112)
  const start = -spread / 2
  const step = total > 1 ? spread / (total - 1) : 0
  return {
    transform: `translateX(${start + index * step}px) rotate(${(index - (total - 1) / 2) * 3}deg)`,
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

function openLeaveConfirm() {
  leaveConfirmOpen.value = true
}

async function confirmLeave() {
  leaveConfirmOpen.value = false
  await leaveGame()
}
</script>

<style scoped>
.game-table {
  width: 100%;
  height: calc(100dvh - 2rem);
  background: radial-gradient(ellipse at center, #1a0a0a 0%, #0d0404 100%);
  color: #fee2e2;
  display: grid;
  grid-template-areas:
    "opponents"
    "center"
    "player";
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto minmax(120px, 0.8fr) auto;
  gap: 10px;
  padding: 10px 14px;
  overflow-x: hidden;
  overflow-y: hidden;
  position: relative;
}

.game-table::before {
  content: '';
  position: absolute;
  inset: 0;
  background-image: radial-gradient(circle at 1px 1px, rgba(255,255,255,0.018) 1px, transparent 0);
  background-size: 24px 24px;
  pointer-events: none;
}

.opponents-bar,
.table-center,
.my-area,
.log-panel,
.reference-panel,
.leave-game-btn {
  position: relative;
  z-index: 1;
}

.opponents-bar {
  grid-area: opponents;
  display: flex;
  gap: 10px;
  overflow-x: auto;
  padding: 0 150px 4px;
  justify-content: center;
  min-width: 0;
  scrollbar-width: thin;
}

.table-center {
  grid-area: center;
  min-height: 120px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 10px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(255,255,255,0.025);
  border-radius: 8px;
  padding: 12px;
}

.pile-area {
  display: flex;
  gap: 36px;
  align-items: flex-end;
  justify-content: center;
  transform: scale(0.86);
  transform-origin: center;
  margin: -4px 0 -6px;
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
  bottom: -22px;
  left: 50%;
  transform: translateX(-50%);
  font-size: 12px;
  color: #fecdd3;
  white-space: nowrap;
}

.turn-banner {
  max-width: min(100%, 420px);
  font-size: 13px;
  font-weight: 700;
  color: #ffe4e6;
  text-transform: uppercase;
  background: rgba(255,255,255,0.07);
  border: 1px solid rgba(255,255,255,0.12);
  border-radius: 8px;
  padding: 7px 14px;
  text-align: center;
}

.tokens-info,
.waiting-msg,
.eliminated-msg {
  font-size: 13px;
  color: #fda4af;
}

.table-event-toast {
  max-width: min(100%, 540px);
  border: 1px solid rgba(251, 113, 133, 0.26);
  background: rgba(20, 5, 8, 0.88);
  color: #ffe4e6;
  border-radius: 8px;
  box-shadow: 0 12px 34px rgba(0,0,0,0.36);
  padding: 10px 14px;
  font-size: 13px;
  line-height: 1.35;
  text-align: center;
  overflow-wrap: anywhere;
}

.priest-bubble {
  background: rgba(88, 28, 135, 0.86);
  border: 1px solid #c084fc;
  border-radius: 8px;
  padding: 10px 16px;
  font-size: 13px;
  color: #f3e8ff;
  display: flex;
  align-items: center;
  gap: 8px;
}

.my-area {
  grid-area: player;
  display: grid;
  grid-template-columns: minmax(0, 1fr);
  grid-template-areas:
    "info"
    "hand"
    "status";
  align-items: center;
  justify-content: center;
  gap: 8px 18px;
  padding: 10px 14px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(0,0,0,0.24);
  border-radius: 8px;
  min-width: 0;
  position: relative;
}

.my-area--acting {
  grid-template-columns: minmax(150px, 220px) minmax(270px, auto) minmax(240px, 320px);
  grid-template-areas:
    "info info info"
    "discards hand action";
}

.my-info-bar {
  grid-area: info;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-wrap: wrap;
  gap: 10px;
  font-size: 13px;
  color: #ffe4e6;
}

.my-name {
  font-weight: 700;
}

.my-tokens {
  display: flex;
  gap: 4px;
  flex-wrap: wrap;
  justify-content: center;
}

.token-dot {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  border: 1.5px solid #fb7185;
  background: transparent;
}

.token-dot.filled {
  background: #fb7185;
}

.shield-badge {
  font-size: 11px;
  background: rgba(56, 189, 248, 0.15);
  border: 1px solid #38bdf8;
  color: #bae6fd;
  border-radius: 8px;
  padding: 3px 8px;
}

.my-discards {
  position: absolute;
  left: clamp(24px, 28%, 520px);
  bottom: 70px;
  height: 96px;
  width: 150px;
  display: flex;
  justify-content: center;
  pointer-events: none;
}

.discard-card {
  position: absolute;
  top: 0;
  left: 50%;
  margin-left: -32px;
  transition: transform 0.3s ease;
}

.my-hand {
  grid-area: hand;
  display: flex;
  gap: 14px;
  align-items: flex-end;
  justify-content: center;
  flex-wrap: wrap;
  width: 100%;
  transition: transform 0.22s ease;
}

.my-area--acting .my-hand {
  justify-content: flex-start;
}

.my-area--acting .my-discards {
  grid-area: discards;
  position: relative;
  left: auto;
  bottom: auto;
  width: min(100%, 220px);
  pointer-events: auto;
}

.waiting-msg,
.eliminated-msg {
  grid-area: status;
  justify-self: center;
  text-align: center;
}

.hand-card,
.chancellor-card-option {
  position: relative;
  padding: 0;
  border: 0;
  background: transparent;
  cursor: pointer;
  border-radius: 8px;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
}

.hand-card:hover,
.chancellor-card-option:hover {
  transform: translateY(-8px);
}

.hand-card--disabled {
  cursor: default;
  opacity: 0.82;
}

.hand-card--disabled:hover {
  transform: none;
}

.hand-card--selected,
.chancellor-card-option.selected {
  transform: translateY(-10px);
  box-shadow: 0 0 0 3px #fb7185, 0 8px 28px rgba(244, 63, 94, 0.35);
}

.hand-card--drawn::after {
  content: '';
  position: absolute;
  inset: -3px;
  border-radius: 10px;
  border: 2px dashed rgba(251, 191, 36, 0.6);
  pointer-events: none;
}

.card-label {
  display: block;
  margin-top: 6px;
  font-size: 11px;
  color: #fecdd3;
  text-align: center;
}

.card-label--new {
  color: #fde68a;
}

.action-panel {
  grid-area: action;
  display: grid;
  gap: 12px;
  background: rgba(0,0,0,0.38);
  border: 1px solid rgba(255,255,255,0.12);
  border-radius: 8px;
  padding: 14px;
  width: min(100%, 320px);
  justify-self: start;
  align-self: center;
}

.choice-row {
  display: grid;
  grid-template-columns: 64px repeat(3, minmax(64px, 1fr));
  align-items: stretch;
  gap: 8px;
  min-width: 0;
}

.action-label {
  font-size: 11px;
  color: #fecdd3;
  text-transform: uppercase;
  font-weight: 700;
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 0;
}

.choice-btn {
  font-size: 12px;
  min-width: 0;
  min-height: 38px;
  padding: 6px;
  border-radius: 6px;
  border: 1px solid #be123c;
  background: rgba(159, 18, 57, 0.18);
  color: #ffe4e6;
  cursor: pointer;
  max-width: none;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: center;
}

.choice-btn--active {
  background: #be123c;
  border-color: #fb7185;
  color: white;
}

.no-targets {
  font-size: 12px;
  color: #fb7185;
}

.confirm-btn {
  width: auto;
  min-width: 152px;
  justify-self: center;
  padding: 11px 14px;
  border-radius: 8px;
  background: #be123c;
  color: white;
  font-weight: 700;
  font-size: 14px;
  border: none;
  cursor: pointer;
}

.confirm-btn:hover:not(:disabled) {
  background: #e11d48;
}

.confirm-btn:disabled {
  opacity: 0.45;
  cursor: not-allowed;
}

.leave-game-btn {
  position: absolute;
  top: 14px;
  right: 14px;
  z-index: 20;
  min-width: 116px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(0,0,0,0.28);
  color: #fecdd3;
  border-radius: 8px;
  padding: 10px 12px;
  font-size: 12px;
  font-weight: 700;
  cursor: pointer;
  box-shadow: 0 10px 28px rgba(0,0,0,0.22);
  transition: background 0.16s ease, border-color 0.16s ease;
}

.leave-game-btn:hover {
  background: rgba(159, 18, 57, 0.34);
  border-color: rgba(251, 113, 133, 0.34);
}

.log-panel {
  position: absolute;
  top: 14px;
  left: 14px;
  z-index: 20;
  width: 132px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(0,0,0,0.28);
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 10px 28px rgba(0,0,0,0.28);
  transition: width 0.2s ease, background 0.2s ease;
}

.log-panel--open {
  width: min(340px, calc(100% - 28px));
  background: rgba(10, 2, 2, 0.94);
}

.log-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  background: rgba(255,255,255,0.06);
  border: 0;
  border-bottom: 1px solid rgba(255,255,255,0.08);
  color: #fecdd3;
  font-size: 12px;
  font-weight: 700;
  padding: 10px 12px;
  cursor: pointer;
  width: 100%;
  text-align: left;
}

.log-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 22px;
  height: 20px;
  border-radius: 8px;
  background: rgba(251, 113, 133, 0.18);
  color: #ffe4e6;
  font-size: 11px;
}

.log-entries {
  max-height: min(320px, calc(100dvh - 110px));
  overflow-y: auto;
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.log-entry {
  font-size: 12px;
  color: #fecdd3;
  line-height: 1.4;
  overflow-wrap: anywhere;
}

.log-entry--empty {
  color: #fda4af;
  font-style: italic;
}

.reference-panel {
  position: absolute;
  bottom: 14px;
  left: 14px;
  z-index: 20;
  width: 132px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(0,0,0,0.28);
  border-radius: 8px;
  overflow: hidden;
  box-shadow: 0 10px 28px rgba(0,0,0,0.28);
  transition: width 0.2s ease, background 0.2s ease;
}

.reference-panel--open {
  width: min(430px, calc(100% - 28px));
  background: rgba(10, 2, 2, 0.95);
}

.reference-toggle {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  background: rgba(255,255,255,0.06);
  border: 0;
  border-bottom: 1px solid rgba(255,255,255,0.08);
  color: #fecdd3;
  font-size: 12px;
  font-weight: 700;
  padding: 10px 12px;
  cursor: pointer;
  width: 100%;
  text-align: left;
}

.reference-count {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 24px;
  height: 20px;
  border-radius: 8px;
  background: rgba(251, 113, 133, 0.18);
  color: #ffe4e6;
  font-size: 11px;
}

.reference-entries {
  max-height: min(440px, calc(100dvh - 150px));
  overflow-y: auto;
  padding: 10px;
  display: grid;
  gap: 8px;
}

.reference-card {
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(255,255,255,0.035);
  border-radius: 8px;
  padding: 9px 10px;
}

.reference-card-head {
  display: grid;
  grid-template-columns: 24px minmax(0, 1fr) auto;
  align-items: center;
  gap: 8px;
}

.reference-value {
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

.reference-name {
  min-width: 0;
  color: #ffe4e6;
  font-size: 13px;
  font-weight: 800;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.reference-copy {
  color: #fda4af;
  font-size: 12px;
  font-weight: 800;
}

.reference-description {
  margin: 6px 0 0;
  color: #fecdd3;
  font-size: 12px;
  line-height: 1.35;
}

.chancellor-overlay {
  position: fixed;
  inset: 0;
  background: rgba(10, 2, 2, 0.94);
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 16px;
  z-index: 50;
  padding: 24px;
  overflow-y: auto;
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

.chancellor-title {
  font-size: 20px;
  font-weight: 700;
  color: #ffe4e6;
  margin: 0;
}

.chancellor-sub {
  font-size: 13px;
  color: #fecdd3;
  margin: 0;
  text-align: center;
}

.chancellor-cards {
  display: flex;
  gap: 18px;
  flex-wrap: wrap;
  justify-content: center;
}

.fade-enter-active,
.fade-leave-active,
.slide-up-enter-active,
.slide-up-leave-active,
.table-event-enter-active,
.table-event-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}

.fade-enter-from,
.fade-leave-to,
.slide-up-enter-from,
.slide-up-leave-to,
.table-event-enter-from,
.table-event-leave-to {
  opacity: 0;
  transform: translateY(10px);
}

.pop-enter-active {
  transition: all 0.2s ease;
}

.pop-leave-active {
  transition: all 0.15s ease;
}

.pop-enter-from,
.pop-leave-to {
  opacity: 0;
  transform: scale(0.96);
}

@media (min-width: 901px) {
  :deep(.card-face--lg) {
    width: 128px;
    height: 180px;
  }

  :deep(.card-face--lg .card-art-icon) {
    font-size: 40px;
  }

  :deep(.card-face--lg .card-name) {
    font-size: 10px;
    padding: 3px 6px;
  }

  :deep(.card-face--lg .card-desc) {
    font-size: 8px;
    line-height: 1.25;
    padding: 0 6px 6px;
  }
}

@media (min-width: 760px) and (max-height: 820px) {
  .game-table {
    grid-template-rows: auto minmax(118px, 0.7fr) auto;
    gap: 8px;
    padding: 10px;
  }

  .opponents-bar {
    gap: 8px;
    padding-top: 0;
    padding-bottom: 4px;
  }

  .table-center {
    min-height: 118px;
    gap: 8px;
    padding: 10px;
  }

  .pile-area {
    transform: scale(0.72);
    transform-origin: center;
    margin: -12px 0 -10px;
  }

  .turn-banner {
    font-size: 12px;
    padding: 6px 12px;
  }

  .tokens-info {
    font-size: 12px;
  }

  .my-info-bar {
    align-self: end;
  }

  .discard-card {
    margin-left: -24px;
  }

  .my-area--acting .my-hand {
    gap: 12px;
    align-items: end;
    justify-content: flex-start;
  }

  .card-label {
    margin-top: 4px;
  }

  .action-panel {
    align-self: center;
    width: min(100%, 300px);
    padding: 12px;
    gap: 8px;
  }

  .choice-row {
    grid-template-columns: 58px repeat(3, minmax(58px, 1fr));
    gap: 6px;
  }

  .choice-btn {
    min-height: 34px;
    padding: 5px;
  }

  .confirm-btn {
    min-width: 140px;
    padding: 9px 12px;
  }

  :deep(.card-face--sm) {
    width: 48px;
    height: 68px;
  }

  :deep(.card-face--sm .card-name),
  :deep(.card-face--sm .card-abbr) {
    display: none;
  }
}

@media (min-width: 901px) and (max-width: 1120px) {
  .my-area--acting {
    grid-template-columns: minmax(210px, auto) minmax(300px, 520px);
    grid-template-areas:
      "info info"
      "hand action"
      "discards action";
  }

  .my-area--acting .my-hand {
    justify-content: center;
  }
}

@media (max-width: 900px) {
  .game-table {
    height: auto;
    min-height: calc(100dvh - 2rem);
    grid-template-areas:
      "opponents"
      "center"
      "player";
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: auto auto auto;
    overflow-y: auto;
  }

  .table-center {
    min-height: 170px;
  }

  .my-area {
    display: flex;
    flex-direction: column;
  }

  .my-discards {
    position: relative;
    left: auto;
    bottom: auto;
    pointer-events: auto;
  }

  .log-panel {
    top: 10px;
    left: 10px;
  }

  .reference-panel {
    bottom: 10px;
    left: 10px;
  }

  .leave-game-btn {
    top: 10px;
    right: 10px;
  }

  .log-panel--open {
    width: min(320px, calc(100% - 20px));
  }

  .reference-panel--open {
    width: min(360px, calc(100% - 20px));
  }

  .log-entries,
  .reference-entries {
    max-height: 220px;
  }
}

@media (max-width: 560px) {
  .game-table {
    padding: 10px;
    gap: 10px;
  }

  .opponents-bar {
    justify-content: flex-start;
    padding-left: 118px;
    padding-right: 118px;
  }

  .pile-area {
    transform: scale(0.9);
    transform-origin: center;
  }

  .my-area {
    padding: 12px 8px;
  }

  .action-panel {
    padding: 10px;
  }

  .choice-row {
    grid-template-columns: 1fr 1fr;
    align-items: stretch;
  }

  .action-label {
    width: 100%;
    grid-column: 1 / -1;
  }
}
</style>
