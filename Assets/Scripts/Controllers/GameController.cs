using System;
using Cysharp.Threading.Tasks;
using Solitaire.Commands;
using Solitaire.Models;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Controllers
{
    public sealed class GameController
    {
        private readonly GameModel                    _gameModel;
        private readonly UndoController               _undoController;
        private readonly InputController              _inputController;
        private readonly Action<CardView, StackView>  _onCardDropped;

        public GameController(
            GameModel gameModel,
            UndoController undoController,
            InputController inputController)
        {
            _gameModel       = gameModel;
            _undoController  = undoController;
            _inputController = inputController;

            _onCardDropped = (c, s) => HandleDropAsync(c, s).Forget();
            inputController.OnCardDropped   += _onCardDropped;
            inputController.OnUndoRequested += HandleUndo;
        }

        private async UniTaskVoid HandleDropAsync(CardView card, StackView targetStack)
        {
            if (targetStack.StackModel == null)
            {
                Debug.LogWarning("[GameController] StackView has no StackModel — call Initialize() on it.");
                return;
            }

            var fromStack = _gameModel.FindStack(card.CardModel);
            if (fromStack == null || fromStack == targetStack.StackModel) return;
            if (!_gameModel.IsValidMove(card.CardModel, targetStack.StackModel)) return;

            var group = (card.DragGroup != null && card.DragGroup.Count > 0)
                ? card.DragGroup
                : new[] { card };

            int baseIndex       = targetStack.CardCount;
            var originPositions = new Vector3[group.Count];
            var targetPositions = new Vector3[group.Count];
            for (int i = 0; i < group.Count; i++)
            {
                originPositions[i] = group[i].OriginalPosition;
                targetPositions[i] = targetStack.GetSlotPosition(baseIndex + i);
            }

            var cmd = new MoveGroupCommand(
                fromStack, targetStack.StackModel,
                card.CurrentStack, targetStack,
                group, originPositions, targetPositions);

            await _undoController.ExecuteAsync(cmd);
        }

        private void HandleUndo() => _undoController.UndoAsync().Forget();

        public void Dispose()
        {
            _inputController.OnCardDropped   -= _onCardDropped;
            _inputController.OnUndoRequested -= HandleUndo;
        }
    }
}
