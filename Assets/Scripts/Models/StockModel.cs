using System.Collections.Generic;

namespace Solitaire.Models
{
    public sealed class StockModel
    {
        private readonly Stack<CardModel> _cards = new();

        public int  Count   => _cards.Count;
        public bool IsEmpty => _cards.Count == 0;

        public StockModel() { }

        public StockModel(IEnumerable<CardModel> cards)
        {
            foreach (var card in cards)
                _cards.Push(card);
        }

        public CardModel Peek() => _cards.Count > 0 ? _cards.Peek() : null;

        public CardModel Pop() => _cards.Count > 0 ? _cards.Pop() : null;

        public void Push(CardModel card) => _cards.Push(card);
    }
}
