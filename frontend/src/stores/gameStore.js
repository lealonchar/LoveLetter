import { reactive, computed } from 'vue'
import { useSignalR } from '../composables/useSignalR'

const { connect, on, invoke, isConnected } = useSignalR()

// Central reactive game state
const state = reactive({
  myName: '',
  myId: null,
  roomCode: null,
  gameState: null,
  priestReveal: null,  // { targetId, card } from Priest effect
  pendingError: null,
  isConnecting: false,
})

// Computed helpers
const myPlayer = computed(() =>
    state.gameState?.yourState ?? null
)

const isMyTurn = computed(() => {
  if (!state.gameState) return false
  const currentPlayer = state.gameState.players[state.gameState.currentPlayerId_Index]
  if (currentPlayer?.id !== state.myId) return false
  // Don't show play UI if waiting for Chancellor resolution from someone else
  if (state.gameState.pendingAction === 'Chancellor' &&
      state.gameState.chancellorOptions?.length === 0)
    return false
  return true
})

const activePlayers = computed(() =>
    state.gameState?.players.filter(p => !p.isEliminated) ?? []
)

const opponents = computed(() =>
    activePlayers.value.filter(p => p.id !== state.myId)
)

// SignalR listeners
function setupListeners() {
  on('GameStateUpdated', (dto) => {
    state.gameState = dto
    if (!state.myId && dto.yourState)
      state.myId = dto.yourState.id
    state.pendingError = null
  })

  on('RoomCreated', (code) => {
    state.roomCode = code
  })

  on('PriestReveal', (targetId, card) => {
    state.priestReveal = { targetId, card }
    setTimeout(() => { state.priestReveal = null }, 5000)
  })

  on('Error', (msg) => {
    state.pendingError = msg
    setTimeout(() => { state.pendingError = null }, 4000)
  })

  on('PlayerLeft', (_id) => {
    // GameStateUpdated will follow
  })
}

// Actions
async function init() {
  state.isConnecting = true
  try {
    await connect()
    setupListeners()
  } finally {
    state.isConnecting = false
  }
}

async function createRoom(name) {
  state.myName = name
  await invoke('CreateRoom', name)
}

async function joinRoom(code, name) {
  state.myName = name
  state.roomCode = code.toUpperCase()
  await invoke('JoinRoom', code.toUpperCase(), name)
}

async function addAiPlayer() {
  await invoke('AddAiPlayer', state.roomCode)
}

async function startGame() {
  await invoke('StartGame', state.roomCode)
}

async function playCard(cardType, targetId = null, guessedCard = null) {
  await invoke('PlayCard', state.roomCode, cardType, targetId, guessedCard)
}

async function startNextRound() {
  await invoke('StartNextRound', state.roomCode)
}

async function resolveChancellor(cardType) {
  await invoke('ResolveChancellor', state.roomCode, cardType)
}

export function useGameStore() {
  return {
    state,
    myPlayer,
    isMyTurn,
    activePlayers,
    opponents,
    isConnected,
    init,
    createRoom,
    joinRoom,
    addAiPlayer,
    startGame,
    playCard,
    startNextRound,
    resolveChancellor,
  }
}