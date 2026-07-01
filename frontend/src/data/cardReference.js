export const cardReference = [
  {
    type: 'Spy',
    name: 'Spy',
    value: 0,
    count: 2,
    description:
      'Playing a Spy marks you for a possible bonus. At the end of the round, if exactly one surviving player has played at least one Spy, that player gains 1 extra affection token before the normal round winner token is awarded.',
  },
  {
    type: 'Guard',
    name: 'Guard',
    value: 1,
    count: 6,
    description:
      'Choose another player who is not protected and name a card other than Guard. If that player has the named card in hand, they are eliminated. If the guess is wrong, nothing happens.',
  },
  {
    type: 'Priest',
    name: 'Priest',
    value: 2,
    count: 2,
    description:
      'Choose another player who is not protected and secretly look at their hand. The card is only revealed to you, not to the whole table.',
  },
  {
    type: 'Baron',
    name: 'Baron',
    value: 3,
    count: 2,
    description:
      'Choose another player who is not protected. You and that player compare the cards in your hands. The player with the lower value is eliminated. If the values are tied, neither player is eliminated.',
  },
  {
    type: 'Handmaid',
    name: 'Handmaid',
    value: 4,
    count: 2,
    description:
      'Until the start of your next turn, other players cannot choose you for card effects. Your protection ends when your next turn begins.',
  },
  {
    type: 'Prince',
    name: 'Prince',
    value: 5,
    count: 2,
    description:
      'Choose any player, including yourself. That player discards their hand and draws a replacement card. If they discard the Princess, they are eliminated. If the deck is empty, they take the set-aside card instead.',
  },
  {
    type: 'Chancellor',
    name: 'Chancellor',
    value: 6,
    count: 2,
    description:
      'Draw 2 cards from the deck if possible, then choose 1 card to keep from your hand and the drawn cards. Return the cards you do not keep to the bottom of the deck.',
  },
  {
    type: 'King',
    name: 'King',
    value: 7,
    count: 1,
    description:
      'Choose another player who is not protected. You and that player secretly trade the cards in your hands.',
  },
  {
    type: 'Countess',
    name: 'Countess',
    value: 8,
    count: 1,
    description:
      'If you have the Countess together with the King or Prince, you must play the Countess. You may also play it normally even when you are not forced to.',
  },
  {
    type: 'Princess',
    name: 'Princess',
    value: 9,
    count: 1,
    description:
      'The Princess has the highest value, but if you discard or play it for any reason, you are immediately eliminated from the round.',
  },
]

export const totalCardCount = cardReference.reduce((sum, card) => sum + card.count, 0)
