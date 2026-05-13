using Solitaire.Data.Enums;

namespace Solitaire.Models
{
    public sealed class CardModel
    {
        public Suit Suit     { get; }
        public Rank Rank     { get; }
        public bool IsFaceUp { get; set; }

        public CardModel(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }
    }
}
