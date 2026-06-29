<template>
  <div :class="['card-face', `card-face--${size}`, `card-face--${card.type.toLowerCase()}`]">

    <!-- Corner value top-left -->
    <div class="card-corner card-corner--tl">
      <span class="card-value">{{ card.value }}</span>
      <span class="card-abbr">{{ abbr }}</span>
    </div>

    <!-- Center art area -->
    <div class="card-art">
      <img
          v-if="imageSrc"
          :src="imageSrc"
          :alt="card.name"
          class="card-img"
          @error="imgError = true"
      />
      <div v-else class="card-art-placeholder">
        <span class="card-art-icon">{{ icon }}</span>
      </div>
    </div>

    <!-- Card name -->
    <div class="card-name">{{ card.name }}</div>

    <!-- Description (only on lg) -->
    <div v-if="size === 'lg'" class="card-desc">{{ card.description }}</div>

    <!-- Corner value bottom-right (rotated) -->
    <div class="card-corner card-corner--br">
      <span class="card-value">{{ card.value }}</span>
      <span class="card-abbr">{{ abbr }}</span>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'

const props = defineProps({
  card: { type: Object, required: true },
  size: { type: String, default: 'md' }, // sm | md | lg
})

const imgError = ref(false)

// Placeholder icons per card type until real art is added
const icons = {
  Spy:        '🕵️',
  Guard:      '⚔️',
  Priest:     '📖',
  Baron:      '🤝',
  Handmaid:   '🌸',
  Prince:     '👑',
  Chancellor: '📜',
  King:       '♔',
  Countess:   '💎',
  Princess:   '🌹',
}

const icon = computed(() => icons[props.card.name] ?? '🃏')
const abbr = computed(() => props.card.name.slice(0, 2).toUpperCase())

// Swap this path to your real card images later
// e.g. /cards/guard.png, /cards/princess.png
const imageSrc = computed(() => {
  if (imgError.value) return null
  return null // Set to `/cards/${props.card.name.toLowerCase()}.png` when you have art
})
</script>

<style scoped>
.card-face {
  position: relative;
  border-radius: 10px;
  background: linear-gradient(160deg, #fdf6e3 0%, #f5e6c8 100%);
  border: 1px solid rgba(0,0,0,0.15);
  box-shadow: 0 4px 12px rgba(0,0,0,0.5), inset 0 1px 0 rgba(255,255,255,0.8);
  display: flex;
  flex-direction: column;
  align-items: center;
  overflow: hidden;
  user-select: none;
  color: #2c1810;
  font-family: 'Georgia', serif;
}

/* Sizes */
.card-face--sm  { width: 64px;  height: 90px; }
.card-face--md  { width: 96px;  height: 134px; }
.card-face--lg  { width: 150px; height: 210px; }

/* Colored top stripe per card */
.card-face::before {
  content: '';
  display: block;
  width: 100%;
  height: 6px;
  flex-shrink: 0;
}

.card-face--spy::before        { background: #6366f1; }
.card-face--guard::before      { background: #dc2626; }
.card-face--priest::before     { background: #7c3aed; }
.card-face--baron::before      { background: #b45309; }
.card-face--handmaid::before   { background: #db2777; }
.card-face--prince::before     { background: #0284c7; }
.card-face--chancellor::before { background: #059669; }
.card-face--king::before       { background: #ca8a04; }
.card-face--countess::before   { background: #9333ea; }
.card-face--princess::before   { background: #e11d48; }

/* Corners */
.card-corner {
  position: absolute;
  display: flex;
  flex-direction: column;
  align-items: center;
  line-height: 1;
  gap: 1px;
}

.card-corner--tl { top: 10px; left: 9px; }
.card-corner--br {
  bottom: 10px; right: 9px;
  transform: rotate(180deg);
}

.card-value {
  font-size: 16px;
  font-weight: 800;
  color: #1c0a00;
}

.card-face--sm .card-value { font-size: 13px; }
.card-face--lg .card-value { font-size: 20px; }

.card-abbr {
  font-size: 9px;
  font-weight: 600;
  color: #78350f;
  letter-spacing: 0.05em;
}

.card-face--sm .card-abbr { font-size: 8px; }

/* Art */
.card-art {
  flex: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  padding: 4px;
}

.card-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 4px;
}

.card-art-placeholder {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
  background: rgba(120, 53, 15, 0.06);
  border-radius: 4px;
}

.card-art-icon {
  font-size: 28px;
  filter: drop-shadow(0 1px 2px rgba(0,0,0,0.2));
}

.card-face--sm .card-art-icon  { font-size: 22px; }
.card-face--lg .card-art-icon  { font-size: 48px; }

/* Name */
.card-name {
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: #44180a;
  padding: 2px 4px 4px;
  width: 100%;
  text-align: center;
  flex-shrink: 0;
}

.card-face--sm .card-name { font-size: 8px; padding: 2px 3px 3px; }
.card-face--lg .card-name { font-size: 12px; padding: 4px 8px; }

/* Description */
.card-desc {
  font-size: 9px;
  line-height: 1.4;
  color: #78350f;
  text-align: center;
  padding: 0 8px 8px;
  flex-shrink: 0;
}
</style>