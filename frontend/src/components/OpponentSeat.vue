<template>
  <div :class="['opponent-seat', isCurrent ? 'opponent-seat--active' : '', player.isEliminated ? 'opponent-seat--eliminated' : '']">
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

    <div class="opp-hand" v-if="!player.isEliminated">
      <div class="card-back-small" />
    </div>

    <div class="opp-info">
      <div class="opp-name-row">
        <span v-if="player.isAi" class="ai-icon" aria-label="AI player" title="AI player">🤖</span>
        <span class="opp-name">{{ player.name }}</span>
        <span v-if="isCurrent" class="opp-turn-dot" title="Current turn" />
      </div>

      <div class="opp-badges">
        <span v-if="player.isProtected" class="badge badge--shield">safe</span>
        <span v-if="player.isEliminated" class="badge badge--out">out</span>
      </div>

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
  player: { type: Object, required: true },
  isCurrent: { type: Boolean, default: false },
})

const { state } = useGameStore()
const maxTokens = computed(() => state.gameState?.roundsToWin ?? 7)

function discardStyle(index, total) {
  const spread = Math.min(total * 16, 64)
  const start = -spread / 2
  const step = total > 1 ? spread / (total - 1) : 0
  return {
    transform: `translateX(${start + index * step}px) rotate(${(index - (total - 1) / 2) * 4}deg)`,
    zIndex: index,
  }
}
</script>

<style scoped>
.opponent-seat {
  display: grid;
  justify-items: center;
  align-content: start;
  gap: 8px;
  width: 132px;
  min-height: 116px;
  padding: 10px;
  border-radius: 8px;
  border: 1px solid rgba(255,255,255,0.08);
  background: rgba(255,255,255,0.035);
  transition: border-color 0.2s ease, background 0.2s ease, box-shadow 0.2s ease;
  flex: 0 0 auto;
}

.opponent-seat--active {
  border-color: rgba(251, 191, 36, 0.5);
  background: rgba(251, 191, 36, 0.07);
  box-shadow: 0 0 18px rgba(251, 191, 36, 0.1);
}

.opponent-seat--eliminated {
  opacity: 0.45;
  filter: grayscale(0.6);
}

.opp-discards {
  position: relative;
  height: 92px;
  width: 110px;
}

.opp-discard {
  position: absolute;
  top: 0;
  left: 50%;
  margin-left: -32px;
}

.card-back-small {
  width: 42px;
  height: 58px;
  border-radius: 6px;
  background: linear-gradient(135deg, #7f1d1d 0%, #450a0a 50%, #7f1d1d 100%);
  border: 1.5px solid rgba(255, 200, 150, 0.22);
  box-shadow: 0 2px 6px rgba(0,0,0,0.5);
}

.opp-info {
  display: grid;
  justify-items: center;
  gap: 5px;
  min-width: 0;
  width: 100%;
}

.opp-name-row {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-width: 0;
  width: 100%;
}

.opp-name {
  font-size: 12px;
  font-weight: 700;
  color: #ffe4e6;
  white-space: nowrap;
  max-width: 104px;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ai-icon {
  font-size: 12px;
  line-height: 1;
  flex-shrink: 0;
}

.opp-turn-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: #fbbf24;
  flex-shrink: 0;
}

.opp-badges {
  display: flex;
  gap: 4px;
  min-height: 16px;
}

.badge {
  font-size: 10px;
  border-radius: 8px;
  padding: 2px 6px;
  line-height: 1;
  font-weight: 700;
}

.badge--shield {
  background: rgba(56,189,248,0.15);
  border: 1px solid #38bdf8;
  color: #bae6fd;
}

.badge--out {
  background: rgba(239,68,68,0.15);
  border: 1px solid #ef4444;
  color: #fecaca;
  text-transform: uppercase;
}

.opp-tokens {
  display: flex;
  gap: 3px;
  flex-wrap: wrap;
  justify-content: center;
  max-width: 92px;
}

.opp-token {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  border: 1px solid #fb7185;
  background: transparent;
}

.opp-token--filled {
  background: #fb7185;
}

@media (min-width: 901px) {
  .opponent-seat {
    width: 116px;
    min-height: 88px;
    gap: 5px;
    padding: 7px;
  }

  .opp-discards {
    height: 68px;
    width: 92px;
  }

  .opp-discard {
    margin-left: -24px;
  }

  .card-back-small {
    width: 34px;
    height: 48px;
  }

  .opp-name {
    max-width: 86px;
    font-size: 11px;
  }

  .opp-badges {
    min-height: 12px;
  }

  .badge {
    font-size: 9px;
    padding: 1px 5px;
  }
}
</style>
