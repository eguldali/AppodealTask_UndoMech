using System.Collections.Generic;

namespace Solitaire.Models
{
    public sealed class StackModel
    {
        private readonly List<CardModel> _cards = new();

        public int Count => _cards.Count;

        public void Add(CardModel card)      => _cards.Add(card);
        public void Remove(CardModel card)   => _cards.Remove(card);
        public bool Contains(CardModel card) => _cards.Contains(card);

        public CardModel Peek() => _cards.Count > 0 ? _cards[^1] : null;
    }
}
