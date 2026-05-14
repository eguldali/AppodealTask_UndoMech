using System.Collections.Generic;

namespace Solitaire.Models
{
    public sealed class WasteModel
    {
        private readonly Stack<CardModel> _cards = new();

        public int  Count   => _cards.Count;
        public bool IsEmpty => _cards.Count == 0;

        public CardModel Peek() => _cards.Count > 0 ? _cards.Peek() : null;

        public CardModel Pop() => _cards.Count > 0 ? _cards.Pop() : null;

        public void Push(CardModel card) => _cards.Push(card);
    }
}
