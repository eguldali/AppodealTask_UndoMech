using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Commands
{
    public sealed class GroupDragCommand : ICommand
    {
        private readonly ICommand       _mainCmd;
        private readonly List<CardView> _followers;
        private readonly ICardContainer _source;
        private readonly StackView      _target;
        private readonly Vector3[]      _followerOrigins;

        public GroupDragCommand(
            ICommand mainCmd,
            List<CardView> followers,
            ICardContainer source,
            StackView target)
        {
            _mainCmd   = mainCmd;
            _followers = followers;
            _source    = source;
            _target    = target;

            _followerOrigins = new Vector3[followers.Count];
            for (int i = 0; i < followers.Count; i++)
                _followerOrigins[i] = followers[i].OriginalPosition;
        }

        public async UniTask Execute()
        {
            foreach (var f in _followers)
                _source?.RemoveCard(f);

            await _mainCmd.Execute();

            foreach (var f in _followers)
                _target.AddCard(f);
        }

        public async UniTask Undo()
        {
            foreach (var f in _followers)
                _target.RemoveCard(f);

            // Kick off follower return animations before awaiting the main card —
            // all three animate concurrently, then we settle state once both finish.
            var followerTasks = new UniTask[_followers.Count];
            for (int i = 0; i < _followers.Count; i++)
                followerTasks[i] = _followers[i].MoveToAsync(_followerOrigins[i]);

            await _mainCmd.Undo();
            await UniTask.WhenAll(followerTasks);

            foreach (var f in _followers)
                _source?.AddCard(f);
        }
    }
}
