<template>
  <div class="w-full max-w-lg space-y-4">

    <!-- Players status row -->
    <div class="grid grid-cols-2 gap-2 sm:grid-cols-4">
      <div
        v-for="p in state.gameState.players"
        :key="p.id"
        :class="playerCardClass(p)">
        <div class="text-xs font-semibold truncate">{{ p.name }}</div>
        <div class="text-xs opacity-70">{{ p.tokens }} 💌</div>
        <div v-if="p.isEliminated" class="text-xs text-red-400">eliminated</div>
        <div v-else-if="p.isProtected" class="text-xs text-sky-300">protected</div>
        <div v-else-if="isCurrentPlayer(p)" class="text-xs text-yellow-300">playing…</div>
        <!-- Show discards -->
        <div class="flex flex-wrap gap-0.5 mt-1">
          <span v-for="(d, i) in p.discards.slice(-4)" :key="i"
                class="text-[10px] bg-rose-900/60 px-1 rounded">{{ d.name[0] }}</span>
        </div>
      </div>
    </div>

    <!-- Priest reveal notification -->
    <Transition name="fade">
      <div v-if="state.priestReveal"
           class="bg-purple-900/80 border border-purple-600 rounded-xl px-4 py-3 text-center text-sm">
        <span class="text-purple-300">You peeked!</span>
        {{ getPlayerName(state.priestReveal.targetId) }} holds
        <strong class="text-purple-100">{{ state.priestReveal.card }}</strong>
      </div>
    </Transition>

    <!-- Deck / turn info -->
    <div class="flex justify-between items-center text-sm text-rose-400 px-1">
      <span>Draw pile: {{ state.gameState.drawPileCount }} cards</span>
      <span>{{ state.gameState.currentPlayerName }}'s turn</span>
      <span>Win at {{ state.gameState.roundsToWin }} 💌</span>
    </div>

    <!-- Your hand (only shown when it's your turn) -->
    <div v-if="isMyTurn && myPlayer" class="bg-rose-900/60 rounded-2xl p-4 space-y-3">
      <p class="text-rose-300 text-sm font-medium text-center">Your turn — choose a card to play</p>

      <div class="grid grid-cols-2 gap-3">
        <CardTile
          v-if="myPlayer.hand"
          :card="myPlayer.hand"
          :selected="selectedCard === myPlayer.hand.type"
          @click="selectCard(myPlayer.hand.type)"
          label="In hand"
        />
        <CardTile
          v-if="drawnCardInfo"
          :card="drawnCardInfo"
          :selected="selectedCard === drawnCardInfo.type"
          @click="selectCard(drawnCardInfo.type)"
          label="Just drawn"
        />
      </div>

      <!-- Target selection (if card needs a target) -->
      <div v-if="selectedCard && needsTarget(selectedCard)" class="space-y-2">
        <p class="text-rose-400 text-xs text-center">Choose a target</p>
        <div class="grid grid-cols-2 gap-2">
          <button
            v-for="p in validTargets"
            :key="p.id"
            :class="['rounded-xl px-3 py-2 text-sm border transition-colors',
              selectedTarget === p.id
                ? 'bg-rose-500 border-rose-400 text-white'
                : 'bg-rose-800/50 border-rose-700 text-rose-200 hover:border-rose-500']"
            @click="selectedTarget = p.id">
            {{ p.name }}
          </button>
        </div>
      </div>

      <!-- Guard guess -->
      <div v-if="selectedCard === 'Guard' && selectedTarget" class="space-y-2">
        <p class="text-rose-400 text-xs text-center">Guess their card</p>
        <div class="grid grid-cols-4 gap-1">
          <button
            v-for="ct in guardGuesses"
            :key="ct"
            :class="['rounded-lg px-2 py-1.5 text-xs border transition-colors',
              selectedGuess === ct
                ? 'bg-rose-500 border-rose-400 text-white'
                : 'bg-rose-800/50 border-rose-700 text-rose-300 hover:border-rose-500']"
            @click="selectedGuess = ct">
            {{ ct }}
          </button>
        </div>
      </div>

      <button
        @click="confirmPlay"
        :disabled="!canConfirm"
        class="w-full bg-rose-500 hover:bg-rose-400 disabled:opacity-40 disabled:cursor-not-allowed
               text-white font-semibold rounded-xl py-3 transition-colors">
        Play {{ selectedCard ?? '—' }}
      </button>
    </div>

    <div v-else-if="myPlayer && !myPlayer.isEliminated"
         class="text-center text-rose-500 text-sm py-4">
      Waiting for {{ state.gameState.currentPlayerName }} to play…
    </div>

    <div v-else-if="myPlayer?.isEliminated"
         class="text-center text-rose-600 text-sm py-4">
      You've been eliminated. Spectating…
    </div>

    <!-- Game log -->
    <div class="bg-rose-900/40 rounded-xl p-3 space-y-1 max-h-36 overflow-y-auto">
      <p v-for="(entry, i) in [...state.gameState.log].reverse()" :key="i"
         class="text-rose-400 text-xs">{{ entry }}</p>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useGameStore } from '../stores/gameStore'
import CardTile from './CardTile.vue'

const { state, myPlayer, isMyTurn, opponents, playCard } = useGameStore()

const selectedCard = ref(null)
const selectedTarget = ref(null)
const selectedGuess = ref(null)

const guardGuesses = ['Priest','Baron','Handmaid','Prince','King','Countess','Princess']

// Cards that are targeted against opponents
const targetedCards = ['Guard','Priest','Baron','King']
// Prince can also target self, handled separately
const optionalTargetCards = ['Prince']

function needsTarget(card) {
  return targetedCards.includes(card) || optionalTargetCards.includes(card)
}

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

// Placeholder — server sends DrawnCard separately; for now derive from yourState
const drawnCardInfo = computed(() => null) // TODO: wire up drawn card from server event

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

function playerCardClass(p) {
  const isCurrent = isCurrentPlayer(p)
  const isMe = p.id === state.myId
  return [
    'rounded-xl px-3 py-2 border text-left',
    p.isEliminated ? 'opacity-40 bg-rose-950/40 border-rose-900' :
    isCurrent ? 'bg-yellow-900/40 border-yellow-700' :
    isMe ? 'bg-rose-800/60 border-rose-600' :
    'bg-rose-900/40 border-rose-800'
  ]
}

const canConfirm = computed(() => {
  if (!selectedCard.value) return false
  if (needsTarget(selectedCard.value) && !selectedTarget.value) return false
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
</script>

<style scoped>
.fade-enter-active, .fade-leave-active { transition: opacity 0.4s; }
.fade-enter-from, .fade-leave-to { opacity: 0; }
</style>
