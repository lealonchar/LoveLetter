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
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'
import { useGameStore } from '../stores/gameStore'

const { createRoom: create, joinRoom: join } = useGameStore()
const name = ref('')
const code = ref('')
const mode = ref('create')

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
