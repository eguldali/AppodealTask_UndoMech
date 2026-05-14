using System;
using Solitaire.Views;

namespace Solitaire.Controllers
{
    public sealed class InputController
    {
        public event Action<CardView, StackView>      OnCardDropped;
        public event Action                           OnUndoRequested;
        public event Action                           OnDrawRequested;
        public event Action<CardView, FoundationView> OnCardDroppedOnFoundation;

        public void NotifyCardDropped(CardView card, StackView target) =>
            OnCardDropped?.Invoke(card, target);

        public void NotifyUndoRequested() =>
            OnUndoRequested?.Invoke();

        public void NotifyDrawRequested() =>
            OnDrawRequested?.Invoke();

        public void NotifyCardDroppedOnFoundation(CardView card, FoundationView foundation) =>
            OnCardDroppedOnFoundation?.Invoke(card, foundation);
    }
}
