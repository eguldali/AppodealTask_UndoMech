using Cysharp.Threading.Tasks;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Commands
{
    public sealed class MoveCardCommand : ICommand
    {
        private readonly CardModel  _card;
        private readonly StackModel _from;
        private readonly StackModel _to;
        private readonly StackView  _fromView;
        private readonly StackView  _toView;
        private readonly CardView   _view;
        private readonly Vector3    _originPos;
        private readonly Vector3    _targetPos;
        private readonly Transform  _originalParent;

        public MoveCardCommand(
            CardModel card, StackModel from, StackModel to,
            StackView fromView, StackView toView,
            CardView view, Vector3 originPos, Vector3 targetPos)
        {
            _card           = card;
            _from           = from;
            _to             = to;
            _fromView       = fromView;
            _toView         = toView;
            _view           = view;
            _originPos      = originPos;
            _targetPos      = targetPos;
            _originalParent = view.transform.parent;
        }

        public async UniTask Execute()
        {
            if (_fromView != null) _fromView.UnregisterCard(_view);
            _from.Remove(_card);
            _to.Add(_card);
            await _view.MoveToAsync(_targetPos);
            if (_toView != null) _toView.RegisterCard(_view);
        }

        public async UniTask Undo()
        {
            if (_toView != null) _toView.UnregisterCard(_view);
            _to.Remove(_card);
            _from.Add(_card);
            _view.transform.SetParent(_originalParent, true);
            await _view.MoveToAsync(_originPos);
            if (_fromView != null) _fromView.RegisterCard(_view);
        }
    }
}
