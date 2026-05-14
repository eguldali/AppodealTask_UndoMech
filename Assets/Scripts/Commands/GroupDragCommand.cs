using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Models;
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
        private readonly StackModel     _fromStack;
        private readonly StackModel     _toStack;
        private readonly Vector3[]      _followerOrigins;

        public GroupDragCommand(
            ICommand mainCmd,
            List<CardView> followers,
            ICardContainer source,
            StackView target,
            StackModel fromStack,
            StackModel toStack)
        {
            _mainCmd   = mainCmd;
            _followers = followers;
            _source    = source;
            _target    = target;
            _fromStack = fromStack;
            _toStack   = toStack;

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
            {
                _fromStack?.Remove(f.CardModel);
                _toStack.Add(f.CardModel);
                _target.AddCard(f);
            }
        }

        public async UniTask Undo()
        {
            foreach (var f in _followers)
                _target.RemoveCard(f);

            var followerTasks = new UniTask[_followers.Count];
            for (int i = 0; i < _followers.Count; i++)
                followerTasks[i] = _followers[i].MoveToAsync(_followerOrigins[i]);

            await _mainCmd.Undo();
            await UniTask.WhenAll(followerTasks);

            foreach (var f in _followers)
            {
                _toStack.Remove(f.CardModel);
                _fromStack?.Add(f.CardModel);
                _source?.AddCard(f);
            }
        }
    }
}
