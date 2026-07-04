import { reactive, computed } from 'vue'
import { useSignalR } from '../composables/useSignalR'

const { connect, on, invoke, isConnected } = useSignalR()
let listenersRegistered = false

const STORAGE_KEYS = {
  playerId: 'loveletter.playerId',
  roomCode: 'loveletter.roomCode',
  playerName: 'loveletter.playerName',
}

function getOrCreatePlayerId() {
  let playerId = localStorage.getItem(STORAGE_KEYS.playerId)
  if (!playerId) {
    playerId = crypto.randomUUID()
    localStorage.setItem(STORAGE_KEYS.playerId, playerId)
  }
  return playerId
}

// Central reactive game state
const state = reactive({
  myName: '',
  myId: getOrCreatePlayerId(),
  roomCode: localStorage.getItem(STORAGE_KEYS.roomCode),
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
  if (listenersRegistered) return
  listenersRegistered = true

  on('GameStateUpdated', (dto) => {
    dto.log = dto.log ?? dto.Log ?? []
    state.gameState = dto
    if (dto.roomCode) {
      state.roomCode = dto.roomCode
      localStorage.setItem(STORAGE_KEYS.roomCode, dto.roomCode)
    }
    if (dto.yourState)
      state.myId = dto.yourState.id
    state.pendingError = null
  })

  on('RoomCreated', (code) => {
    state.roomCode = code
    localStorage.setItem(STORAGE_KEYS.roomCode, code)
  })

  on('ReconnectFailed', (_msg) => {
    clearRoomState()
    state.pendingError = _msg
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

  on('LeftRoom', () => {
    clearRoomState()
  })
}

function clearRoomState() {
  state.roomCode = null
  state.gameState = null
  state.priestReveal = null
  state.pendingError = null
  localStorage.removeItem(STORAGE_KEYS.roomCode)
}

// Actions
async function init() {
  state.isConnecting = true
  try {
    await connect()
    setupListeners()
    if (state.roomCode && state.myId)
      await invoke('ReconnectToRoom', state.roomCode, state.myId)
  } finally {
    state.isConnecting = false
  }
}

async function createRoom(name) {
  state.myName = name
  localStorage.setItem(STORAGE_KEYS.playerName, name)
  await invoke('CreateRoom', name, state.myId)
}

async function joinRoom(code, name) {
  const roomCode = code.toUpperCase()
  state.myName = name
  state.pendingError = null
  localStorage.setItem(STORAGE_KEYS.playerName, name)
  const joined = await invoke('JoinRoom', roomCode, name, state.myId)
  if (joined) {
    state.roomCode = roomCode
    localStorage.setItem(STORAGE_KEYS.roomCode, roomCode)
  }
}

async function addAiPlayer() {
  await invoke('AddAiPlayer', state.roomCode)
}

async function renameAiPlayer(aiPlayerId, name) {
  await invoke('RenameAiPlayer', state.roomCode, aiPlayerId, name)
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

async function leaveGame() {
  try {
    if (state.roomCode)
      await invoke('LeaveRoom')
  } finally {
    clearRoomState()
  }
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
    renameAiPlayer,
    startGame,
    playCard,
    startNextRound,
    resolveChancellor,
    leaveGame,
  }
}
