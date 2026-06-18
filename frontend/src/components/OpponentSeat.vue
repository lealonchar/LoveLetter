<template>
  <div :class="['opponent-seat', isCurrent ? 'opponent-seat--active' : '', player.isEliminated ? 'opponent-seat--eliminated' : '', orientation === 'vertical' ? 'opponent-seat--vertical' : '']">

    <!-- Discard fan (face up, small) -->
    <div class="opp-discards" v-if="player.discards?.length">
      <div
        v-for="(card, i) in player.discards.slice(-5)"
        :key="i"
        class="opp-discard"
        :style="discardStyle(i, Math.min(player.discards.length, 5))"
        :title="card.name">
        <CardFace :card="card" size="sm" />
      </div>
    </div>

    <!-- Face-down card (their current hand - hidden) -->
    <div class="opp-hand" v-if="!player.isEliminated">
      <div class="card-back-small" />
    </div>

    <!-- Player info -->
    <div class="opp-info">
      <div class="opp-name-row">
        <span class="opp-name">{{ player.isAi ? '🤖 ' : '' }}{{ player.name }}</span>
        <span v-if="isCurrent" class="opp-turn-dot" title="Current turn" />
      </div>

      <!-- Status badges -->
      <div class="opp-badges">
        <span v-if="player.isProtected" class="badge badge--shield">🛡</span>
        <span v-if="player.isEliminated" class="badge badge--out">out</span>
      </div>

      <!-- Tokens -->
      <div class="opp-tokens">
        <span
          v-for="n in maxTokens"
          :key="n"
          :class="['opp-token', n <= player.tokens ? 'opp-token--filled' : '']"
        />
      </div>
    </div>

  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useGameStore } from '../stores/gameStore'
import CardFace from './CardFace.vue'

const props = defineProps({
  player:      { type: Object,  required: true },
  isCurrent:   { type: Boolean, default: false },
  orientation: { type: String,  default: 'horizontal' },
})

const { state } = useGameStore()
const maxTokens = computed(() => state.gameState?.roundsToWin ?? 7)

function discardStyle(index, total) {
  const spread = Math.min(total * 18, 80)
  const start = -spread / 2
  const step = total > 1 ? spread / (total - 1) : 0
  return {
    transform: `translateX(${start + index * step}px) rotate(${(index - (total - 1) / 2) * 5}deg)`,
    zIndex: index,
  }
}
</script>

<style scoped>
.opponent-seat {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  padding: 10px;
  border-radius: 14px;
  border: 1px solid rgba(255,255,255,0.05);
  background: rgba(255,255,255,0.02);
  transition: all 0.3s ease;
  min-width: 100px;
}

.opponent-seat--active {
  border-color: rgba(251, 191, 36, 0.4);
  background: rgba(251, 191, 36, 0.04);
  box-shadow: 0 0 20px rgba(251, 191, 36, 0.08);
}

.opponent-seat--eliminated {
  opacity: 0.35;
  filter: grayscale(0.6);
}

.opponent-seat--vertical {
  flex-direction: row;
  min-width: unset;
  min-height: 100px;
}

/* Discard fan */
.opp-discards {
  position: relative;
  height: 52px;
  width: 90px;
  flex-shrink: 0;
}

.opp-discard {
  position: absolute;
  top: 0;
  left: 50%;
  margin-left: -24px;
}

/* Face-down hand card */
.card-back-small {
  width: 40px;
  height: 56px;
  border-radius: 6px;
  background: linear-gradient(135deg, #7f1d1d 0%, #450a0a 50%, #7f1d1d 100%);
  border: 1.5px solid rgba(255, 200, 150, 0.2);
  box-shadow: 0 2px 6px rgba(0,0,0,0.5);
  flex-shrink: 0;
}

/* Info section */
.opp-info {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.opp-name-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

.opp-name {
  font-size: 12px;
  font-weight: 700;
  color: #fda4af;
  white-space: nowrap;
  max-width: 100px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.opp-turn-dot {
  width: 7px; height: 7px;
  border-radius: 50%;
  background: #fbbf24;
  animation: pulse 1.2s ease infinite;
  flex-shrink: 0;
}

@keyframes pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.5; transform: scale(0.7); }
}

.opp-badges {
  display: flex;
  gap: 4px;
  min-height: 16px;
}

.badge {
  font-size: 10px;
  border-radius: 999px;
  padding: 1px 6px;
}

.badge--shield {
  background: rgba(56,189,248,0.15);
  border: 1px solid #38bdf8;
  color: #7dd3fc;
}

.badge--out {
  background: rgba(239,68,68,0.15);
  border: 1px solid #ef4444;
  color: #fca5a5;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  font-size: 9px;
  font-weight: 700;
}

/* Tokens */
.opp-tokens {
  display: flex;
  gap: 3px;
  flex-wrap: wrap;
  justify-content: center;
  max-width: 80px;
}

.opp-token {
  width: 7px; height: 7px;
  border-radius: 50%;
  border: 1px solid #9f1239;
  background: transparent;
  transition: background 0.3s;
}

.opp-token--filled { background: #f43f5e; }
</style>
