using System;
using System.Collections.Generic;
using UnityEngine;

namespace Solitaire.Data
{
    [CreateAssetMenu(fileName = "CardSpriteRegistry", menuName = "Solitaire/Card Sprite Registry")]
    public sealed class CardSpriteRegistry : ScriptableObject
    {
        [Serializable]
        public sealed class CardSpriteEntry
        {
            public Suit   suit;
            public Rank   rank;
            public Sprite sprite;
        }

        public List<CardSpriteEntry> entries;
        public Sprite                backSprite;

        private Dictionary<(Suit, Rank), Sprite> _lookup;

        public void Initialize()
        {
            _lookup = new Dictionary<(Suit, Rank), Sprite>(entries.Count);
            foreach (var entry in entries)
                _lookup[(entry.suit, entry.rank)] = entry.sprite;
        }

        public Sprite GetSprite(Suit suit, Rank rank)
        {
            if (_lookup.TryGetValue((suit, rank), out var sprite))
                return sprite;

            Debug.LogWarning($"[CardSpriteRegistry] No sprite for {suit} {rank}.");
            return null;
        }

        public Sprite GetBackSprite() => backSprite;
    }
}
