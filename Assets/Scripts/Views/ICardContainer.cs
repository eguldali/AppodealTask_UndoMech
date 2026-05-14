using UnityEngine;

namespace Solitaire.Views
{
    public interface ICardContainer
    {
        void     AddCard(CardView card);
        void     RemoveCard(CardView card);
        Vector3  GetTopSlotPosition();
        CardView GetTopCardView();
    }
}
