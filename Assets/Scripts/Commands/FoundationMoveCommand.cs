using Cysharp.Threading.Tasks;
using Solitaire.Models;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Commands
{
    public sealed class FoundationMoveCommand : ICommand
    {
        private readonly CardModel       _card;
        private readonly StackModel      _from;
        private readonly FoundationModel _to;
        private readonly WasteModel      _wasteFrom;
        private readonly CardView        _view;
        private readonly ICardContainer  _fromContainer;
        private readonly FoundationView  _toView;
        private readonly Vector3         _originPos;
        private readonly Vector3         _targetPos;
        private readonly Transform       _originalParent;

        private bool     _didFlipSourceTop;
        private CardView _sourceTopCard;

        public FoundationMoveCommand(
            CardModel card, StackModel from, FoundationModel to,
            CardView view, ICardContainer fromContainer, FoundationView toView,
            Vector3 originPos, Vector3 targetPos,
            WasteModel wasteFrom = null)
        {
            _card           = card;
            _from           = from;
            _to             = to;
            _wasteFrom      = wasteFrom;
            _view           = view;
            _fromContainer  = fromContainer;
            _toView         = toView;
            _originPos      = originPos;
            _targetPos      = targetPos;
            _originalParent = view.transform.parent;
        }

        public async UniTask Execute()
        {
            _fromContainer?.RemoveCard(_view);
            _wasteFrom?.Pop();
            _from?.Remove(_card);
            _to.Add(_card);
            _toView.RegisterCard(_view);
            await _view.MoveToAsync(_targetPos);

            var newTop = _fromContainer?.GetTopCardView();
            if (newTop != null && !newTop.CardModel.IsFaceUp)
            {
                _didFlipSourceTop = true;
                _sourceTopCard    = newTop;
                _sourceTopCard.SetFaceUp(true);
                _sourceTopCard.SetDraggable(true);
            }
        }

        public async UniTask Undo()
        {
            if (_didFlipSourceTop)
            {
                _sourceTopCard.SetFaceUp(false);
                _sourceTopCard.SetDraggable(false);
                _didFlipSourceTop = false;
            }

            _toView.UnregisterCard(_view);
            _to.Remove();
            _from?.Add(_card);
            _wasteFrom?.Push(_card);
            _view.transform.SetParent(_originalParent, true);
            await _view.MoveToAsync(_originPos);
            _fromContainer?.AddCard(_view);
            _view.SetDraggable(true);
        }
    }
}
