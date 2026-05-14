using System.Collections.Generic;

namespace Solitaire.Models
{
    public sealed class FoundationModel
    {
        private readonly List<CardModel> _cards = new();

        public int Count => _cards.Count;

        public void Add(CardModel card) => _cards.Add(card);

        public CardModel Remove()
        {
            if (_cards.Count == 0) return null;
            var top = _cards[^1];
            _cards.RemoveAt(_cards.Count - 1);
            return top;
        }

        public CardModel Peek() => _cards.Count > 0 ? _cards[^1] : null;
    }
}
