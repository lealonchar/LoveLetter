<template>
  <div class="game-table">
    <div class="game-toolbar">
      <div class="toolbar-group">
        <aside :class="['log-panel', logOpen ? 'log-panel--open' : '']" aria-label="Game log">
          <button type="button" class="log-toggle" @click="toggleLog">
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
      </div>

      <button type="button" class="leave-game-btn" @click="openLeaveConfirm">
        Leave game
      </button>
    </div>

    <div class="opponents-bar">
      <OpponentSeat
          v-for="p in opponents"
          :key="p.id"
          :player="p"
          :is-current="isCurrentPlayer(p)"
          @zoom-card="openCardZoom"
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
        <button
            v-for="(card, i) in myPlayer.discards"
            :key="i"
            type="button"
            class="discard-card"
            :style="discardStyle(i, myPlayer.discards.length)"
            :title="`${card.name} - double tap to zoom`"
            @click="handleVisibleCardTap(`my-discard-${i}`, card)">
          <CardFace :card="card" size="sm" />
        </button>
      </div>

      <div v-if="!isMyTurn && !isMyChancellorPending && myPlayer && !myPlayer.isEliminated" class="waiting-msg">
        Waiting for {{ state.gameState.currentPlayerName }}...
      </div>

      <div v-if="myPlayer?.isEliminated" class="eliminated-msg">
        You've been eliminated - spectating
      </div>

      <div v-if="myPlayer?.hand && !isMyChancellorPending && !myPlayer.isEliminated" class="my-hand">
        <div class="hand-card-wrap">
          <button
              type="button"
              :title="`${myPlayer.hand.name} - double tap to zoom`"
              :class="['hand-card', !isMyTurn ? 'hand-card--disabled' : '', selectedCardSlot === 'hand' ? 'hand-card--selected' : '']"
              @click="handleHandCardClick('hand', myPlayer.hand)">
            <CardFace :card="myPlayer.hand" size="lg" />
            <span class="card-label">In hand</span>
          </button>
        </div>

        <div v-if="state.gameState.drawnCard" class="hand-card-wrap">
          <button
              type="button"
              :title="`${state.gameState.drawnCard.name} - double tap to zoom`"
              :class="['hand-card hand-card--drawn', selectedCardSlot === 'drawn' ? 'hand-card--selected' : '']"
              @click="handleHandCardClick('drawn', state.gameState.drawnCard)">
            <CardFace :card="state.gameState.drawnCard" size="lg" />
            <span class="card-label card-label--new">Just drawn</span>
          </button>
        </div>
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

    <aside :class="['reference-panel', cardReferenceOpen ? 'reference-panel--open' : '']" aria-label="Card reference">
      <button type="button" class="reference-toggle" @click="toggleCardReference">
        <span>{{ cardReferenceOpen ? 'Close cards' : 'Cards' }}</span>
        <span class="reference-count">{{ totalCardCount }}</span>
      </button>
      <div v-if="cardReferenceOpen" class="reference-entries">
          <button
            v-for="card in cardReference"
            :key="card.type"
            type="button"
            class="reference-card"
            :title="`${card.name} - double tap to zoom`"
            @click="handleVisibleCardTap(`reference-${card.type}`, card)">
          <div class="reference-card-head">
            <span class="reference-value">{{ card.value }}</span>
            <span class="reference-name">{{ card.name }}</span>
            <span class="reference-copy">x{{ card.count }}</span>
          </div>
          <p class="reference-description">{{ card.description }}</p>
        </button>
      </div>
    </aside>

    <Transition name="slide-up">
      <div v-if="isMyChancellorPending" class="chancellor-overlay">
        <p class="chancellor-title">Choose a card to keep</p>
        <p class="chancellor-sub">The other {{ chancellorOptions.length - 1 }} return to the bottom of the deck</p>
        <div class="chancellor-cards">
          <button
              v-for="(card, index) in chancellorOptions"
              :key="`${card.type}-${index}`"
              type="button"
              :class="['chancellor-card-option', selectedChancellorIndex === index ? 'selected' : '']"
              :title="`${card.name} - double tap to zoom`"
              @click="handleChancellorCardClick(index, card)">
            <CardFace :card="card" size="lg" />
          </button>
        </div>
        <div v-if="selectedChancellorIndex !== null" class="chancellor-return-order">
          <p class="chancellor-order-title">Return order</p>
          <div class="chancellor-return-cards">
            <div
                v-for="(option, position) in chancellorReturnCards"
                :key="`${option.card.type}-${option.index}`"
                class="chancellor-return-card">
              <CardFace :card="option.card" size="sm" />
              <span class="return-position">
                {{ position === 0 ? 'Return first' : 'Return second' }}
              </span>
            </div>
          </div>
          <button
              v-if="chancellorReturnCards.length > 1"
              type="button"
              class="swap-order-btn"
              @click="swapChancellorReturnOrder">
            Swap order
          </button>
        </div>
        <button
            type="button"
            @click="confirmChancellor"
            :disabled="selectedChancellorIndex === null"
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

    <Transition name="pop">
      <div
          v-if="zoomedCard"
          class="card-zoom-backdrop"
          role="dialog"
          aria-modal="true"
          @click.self="closeCardZoom">
        <div class="card-zoom-modal">
          <button type="button" class="zoom-close-btn" @click="closeCardZoom">
            Close
          </button>
          <CardFace :card="zoomedCard" size="xl" />
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
const selectedCardSlot = ref(null)
const selectedTarget = ref(null)
const selectedGuess = ref(null)
const selectedChancellorIndex = ref(null)
const chancellorReturnOrder = ref([])
const logOpen = ref(false)
const cardReferenceOpen = ref(false)
const leaveConfirmOpen = ref(false)
const zoomedCard = ref(null)
const tableEvent = ref(null)
const tableEventKey = ref(0)
let tableEventTimer = null
let lastSeenLogEntry = null
let lastTappedCardKey = null
let lastCardTapAt = 0

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

const chancellorOptions = computed(() =>
    state.gameState?.chancellorOptions ?? []
)

const chancellorReturnCards = computed(() =>
    chancellorReturnOrder.value
        .map(index => ({ index, card: chancellorOptions.value[index] }))
        .filter(option => option.card)
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

watch(
    chancellorOptions,
    () => {
      selectedChancellorIndex.value = null
      chancellorReturnOrder.value = []
    }
)

watch(
    () => [
      state.gameState?.currentPlayerId_Index,
      myPlayer.value?.hand?.type,
      state.gameState?.drawnCard?.type,
      state.gameState?.phase,
    ].join(':'),
    () => {
      selectedCard.value = null
      selectedCardSlot.value = null
      selectedTarget.value = null
      selectedGuess.value = null
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

function selectCard(slot, type) {
  selectedCardSlot.value = slot
  selectedCard.value = type
  selectedTarget.value = null
  selectedGuess.value = null
}

function handleHandCardClick(slot, card) {
  if (handleCardDoubleTap(`hand-${slot}`, card))
    return

  if (isMyTurn.value)
    selectCard(slot, card.type)
}

function handleVisibleCardTap(key, card) {
  handleCardDoubleTap(key, card)
}

function handleChancellorCardClick(index, card) {
  if (handleCardDoubleTap(`chancellor-${index}`, card))
    return

  selectedChancellorIndex.value = index
  chancellorReturnOrder.value = chancellorOptions.value
      .map((_, optionIndex) => optionIndex)
      .filter(optionIndex => optionIndex !== index)
}

function handleCardDoubleTap(key, card) {
  const now = Date.now()
  const isDoubleTap = lastTappedCardKey === key && now - lastCardTapAt <= 360

  lastTappedCardKey = key
  lastCardTapAt = now

  if (!isDoubleTap)
    return false

  lastTappedCardKey = null
  lastCardTapAt = 0
  openCardZoom(card)
  return true
}

function swapChancellorReturnOrder() {
  if (chancellorReturnOrder.value.length < 2) return
  chancellorReturnOrder.value = [...chancellorReturnOrder.value].reverse()
}

function isCurrentPlayer(p) {
  return state.gameState?.players[state.gameState.currentPlayerId_Index]?.id === p.id
}

function getPlayerName(id) {
  return state.gameState?.players.find(p => p.id === id)?.name ?? 'Unknown'
}

function toggleLog() {
  logOpen.value = !logOpen.value
  if (logOpen.value)
    cardReferenceOpen.value = false
}

function toggleCardReference() {
  cardReferenceOpen.value = !cardReferenceOpen.value
  if (cardReferenceOpen.value)
    logOpen.value = false
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
  selectedCardSlot.value = null
  selectedTarget.value = null
  selectedGuess.value = null
}

async function confirmChancellor() {
  if (selectedChancellorIndex.value === null) return
  await resolveChancellor(selectedChancellorIndex.value, chancellorReturnOrder.value)
  selectedChancellorIndex.value = null
  chancellorReturnOrder.value = []
}

function openLeaveConfirm() {
  leaveConfirmOpen.value = true
}

function openCardZoom(card) {
  if (!card) return
  zoomedCard.value = card
}

function closeCardZoom() {
  zoomedCard.value = null
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
    "toolbar"
    "opponents"
    "center"
    "player";
  grid-template-columns: minmax(0, 1fr);
  grid-template-rows: auto auto minmax(120px, 0.8fr) auto;
  gap: 8px;
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
.game-toolbar,
.log-panel,
.reference-panel,
.leave-game-btn {
  position: relative;
  z-index: 1;
}

.game-toolbar {
  grid-area: toolbar;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
  min-width: 0;
  min-height: 38px;
  z-index: 30;
}

.toolbar-group {
  display: flex;
  align-items: flex-start;
  gap: 8px;
  min-width: 0;
}

.opponents-bar {
  grid-area: opponents;
  display: flex;
  gap: 10px;
  overflow-x: auto;
  padding: 0 0 4px;
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
    "discards"
    "status";
  align-items: center;
  justify-content: center;
  gap: 6px 18px;
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
  grid-area: discards;
  position: relative;
  left: auto;
  bottom: auto;
  height: 76px;
  width: min(100%, 170px);
  display: flex;
  justify-content: center;
  justify-self: center;
  margin-top: -4px;
  pointer-events: auto;
}

.discard-card {
  position: absolute;
  top: 0;
  left: 50%;
  margin-left: -25px;
  padding: 0;
  border: 0;
  background: transparent;
  border-radius: 8px;
  cursor: zoom-in;
  transition: transform 0.3s ease;
}

.my-discards :deep(.card-face--sm) {
  width: 50px;
  height: 70px;
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

.hand-card-wrap {
  position: relative;
  display: grid;
  justify-items: center;
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
  height: 86px;
  margin-top: 0;
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
  cursor: zoom-in;
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
  position: relative;
  flex: 0 0 auto;
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
  position: relative;
  flex: 0 0 auto;
  width: 132px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(0,0,0,0.28);
  border-radius: 8px;
  overflow: visible;
  box-shadow: 0 10px 28px rgba(0,0,0,0.28);
  transition: width 0.2s ease, background 0.2s ease;
}

.log-panel--open {
  width: 132px;
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
  position: absolute;
  top: calc(100% + 6px);
  left: 0;
  width: min(340px, calc(100vw - 28px));
  max-height: min(320px, calc(100dvh - 110px));
  overflow-y: auto;
  padding: 10px 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
  border: 1px solid rgba(255,255,255,0.1);
  background: rgba(10, 2, 2, 0.96);
  border-radius: 8px;
  box-shadow: 0 14px 34px rgba(0,0,0,0.34);
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
  left: 14px;
  bottom: 14px;
  z-index: 30;
  width: 132px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(0,0,0,0.28);
  border-radius: 8px;
  overflow: visible;
  box-shadow: 0 10px 28px rgba(0,0,0,0.28);
  transition: width 0.2s ease, background 0.2s ease;
}

.reference-panel--open {
  width: 132px;
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
  position: absolute;
  bottom: calc(100% + 6px);
  left: 0;
  width: min(430px, calc(100vw - 28px));
  max-height: min(440px, calc(100dvh - 150px));
  overflow-y: auto;
  padding: 10px;
  display: grid;
  gap: 8px;
  border: 1px solid rgba(255,255,255,0.1);
  background: rgba(10, 2, 2, 0.96);
  border-radius: 8px;
  box-shadow: 0 14px 34px rgba(0,0,0,0.34);
}

.reference-card {
  width: 100%;
  text-align: left;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(255,255,255,0.035);
  border-radius: 8px;
  padding: 9px 10px;
  cursor: zoom-in;
}

.reference-card:hover {
  border-color: rgba(251, 113, 133, 0.28);
  background: rgba(255,255,255,0.06);
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

.card-zoom-backdrop {
  position: fixed;
  inset: 0;
  z-index: 70;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 20px;
  background: rgba(10, 2, 2, 0.78);
  backdrop-filter: blur(7px);
}

.card-zoom-modal {
  position: relative;
  display: grid;
  justify-items: center;
  gap: 12px;
  max-width: min(100%, 360px);
}

.zoom-close-btn {
  justify-self: end;
  border: 1px solid rgba(251, 113, 133, 0.28);
  background: rgba(20, 5, 8, 0.9);
  color: #fecdd3;
  border-radius: 8px;
  padding: 8px 12px;
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
}

.zoom-close-btn:hover {
  background: rgba(159, 18, 57, 0.42);
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

.chancellor-return-order {
  width: min(100%, 420px);
  display: grid;
  gap: 10px;
  padding: 12px;
  border: 1px solid rgba(255,255,255,0.1);
  background: rgba(255,255,255,0.04);
  border-radius: 8px;
}

.chancellor-order-title {
  margin: 0;
  color: #ffe4e6;
  font-size: 12px;
  font-weight: 800;
  text-align: center;
  text-transform: uppercase;
}

.chancellor-return-cards {
  display: flex;
  flex-wrap: wrap;
  align-items: end;
  justify-content: center;
  gap: 14px;
}

.chancellor-return-card {
  display: grid;
  justify-items: center;
  gap: 7px;
}

.return-position {
  color: #fecdd3;
  font-size: 11px;
  font-weight: 800;
}

.swap-order-btn {
  justify-self: center;
  min-width: 116px;
  min-height: 36px;
  padding: 8px 12px;
  border: 1px solid rgba(251, 113, 133, 0.32);
  background: rgba(159, 18, 57, 0.26);
  color: #ffe4e6;
  border-radius: 8px;
  font-size: 12px;
  font-weight: 800;
  cursor: pointer;
}

.swap-order-btn:hover {
  background: rgba(190, 18, 60, 0.62);
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
    grid-template-rows: auto auto minmax(118px, 0.7fr) auto;
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
      "toolbar"
      "opponents"
      "center"
      "player";
    grid-template-columns: minmax(0, 1fr);
    grid-template-rows: auto auto auto auto;
    overflow-y: auto;
  }

  .game-toolbar {
    flex-wrap: wrap;
  }

  .toolbar-group {
    flex-wrap: wrap;
  }

  .table-center {
    min-height: 170px;
  }

  .my-area {
    display: flex;
    flex-direction: column;
  }

  .my-info-bar {
    order: 1;
  }

  .my-hand {
    order: 2;
  }

  .my-discards {
    order: 3;
    position: relative;
    left: auto;
    bottom: auto;
    pointer-events: auto;
  }

  .waiting-msg,
  .eliminated-msg {
    order: 4;
  }

  .log-entries {
    width: min(360px, calc(100vw - 20px));
    max-height: 220px;
  }

  .reference-panel {
    left: 10px;
    bottom: 10px;
  }

  .reference-entries {
    width: min(360px, calc(100vw - 20px));
    max-height: min(320px, calc(100dvh - 90px));
  }
}

@media (max-width: 560px) {
  .game-table {
    padding: 10px;
    gap: 10px;
  }

  .toolbar-group {
    gap: 6px;
  }

  .log-panel {
    width: 124px;
  }

  .reference-panel {
    width: 124px;
  }

  .leave-game-btn {
    min-width: 104px;
  }

  .log-entries {
    position: fixed;
    top: 58px;
    left: 10px;
    width: calc(100vw - 20px);
    max-height: min(420px, calc(100dvh - 90px));
    z-index: 40;
  }

  .reference-entries {
    position: fixed;
    left: 10px;
    bottom: 58px;
    width: calc(100vw - 20px);
    max-height: min(420px, calc(100dvh - 90px));
    z-index: 40;
  }

  .opponents-bar {
    justify-content: flex-start;
    padding-left: 0;
    padding-right: 0;
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
