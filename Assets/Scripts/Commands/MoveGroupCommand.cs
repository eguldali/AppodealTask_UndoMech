using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Commands
{
    public sealed class MoveGroupCommand : ICommand
    {
        private readonly StackModel _from;
        private readonly StackModel _to;
        private readonly StackView  _fromView;
        private readonly StackView  _toView;

        private readonly IReadOnlyList<CardModel> _cards;
        private readonly IReadOnlyList<CardView>  _views;
        private readonly IReadOnlyList<Vector3>   _originPositions;
        private readonly IReadOnlyList<Vector3>   _targetPositions;
        private readonly IReadOnlyList<Transform> _originalParents;

        public MoveGroupCommand(
            StackModel from, StackModel to,
            StackView fromView, StackView toView,
            IReadOnlyList<CardView> views,
            IReadOnlyList<Vector3> originPositions,
            IReadOnlyList<Vector3> targetPositions)
        {
            _from            = from;
            _to              = to;
            _fromView        = fromView;
            _toView          = toView;
            _views           = views;
            _originPositions = originPositions;
            _targetPositions = targetPositions;

            var cards           = new CardModel[views.Count];
            var originalParents = new Transform[views.Count];
            for (int i = 0; i < views.Count; i++)
            {
                cards[i]           = views[i].CardModel;
                originalParents[i] = views[i].transform.parent;
            }
            _cards           = cards;
            _originalParents = originalParents;
        }

        public async UniTask Execute()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _fromView?.UnregisterCard(_views[i]);
                _from.Remove(_cards[i]);
                _to.Add(_cards[i]);
                _toView?.RegisterCard(_views[i]); // parent/list settled before animation starts
            }

            var tasks = new UniTask[_views.Count];
            for (int i = 0; i < _views.Count; i++)
                tasks[i] = _views[i].MoveToAsync(_targetPositions[i]);
            await UniTask.WhenAll(tasks);
        }

        public async UniTask Undo()
        {
            for (int i = 0; i < _views.Count; i++)
            {
                _toView?.UnregisterCard(_views[i]);
                _to.Remove(_cards[i]);
                _from.Add(_cards[i]);
                _views[i].transform.SetParent(_originalParents[i], true);
                _fromView?.RegisterCard(_views[i]); // parent/list settled before animation starts
            }

            var tasks = new UniTask[_views.Count];
            for (int i = 0; i < _views.Count; i++)
                tasks[i] = _views[i].MoveToAsync(_originPositions[i]);
            await UniTask.WhenAll(tasks);
        }
    }
}
