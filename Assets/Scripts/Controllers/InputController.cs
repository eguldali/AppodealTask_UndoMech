using System;
using Solitaire.Views;

namespace Solitaire.Controllers
{
    public sealed class InputController
    {
        public event Action<CardView, StackView> OnCardDropped;
        public event Action                      OnUndoRequested;

        public void NotifyCardDropped(CardView card, StackView target) =>
            OnCardDropped?.Invoke(card, target);

        public void NotifyUndoRequested() =>
            OnUndoRequested?.Invoke();
    }
}
