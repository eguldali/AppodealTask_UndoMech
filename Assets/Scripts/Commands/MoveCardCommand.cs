using Cysharp.Threading.Tasks;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Commands
{
    public sealed class MoveCardCommand : ICommand
    {
        private readonly CardModel _card;
        private readonly StackModel _from;
        private readonly StackModel _to;
        private readonly CardView _view;
        private readonly Vector3 _originPos;
        private readonly Vector3 _targetPos;

        public MoveCardCommand(
            CardModel card, StackModel from, StackModel to,
            CardView view, Vector3 originPos, Vector3 targetPos)
        {
            _card      = card;
            _from      = from;
            _to        = to;
            _view      = view;
            _originPos = originPos;
            _targetPos = targetPos;
        }

        public async UniTask Execute()
        {
            _from.Remove(_card);
            _to.Add(_card);
            await _view.MoveToAsync(_targetPos);
        }

        public async UniTask Undo()
        {
            _to.Remove(_card);
            _from.Add(_card);
            await _view.MoveToAsync(_originPos);
        }
    }
}
