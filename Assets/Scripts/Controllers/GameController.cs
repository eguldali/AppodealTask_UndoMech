using System;
using System.Collections.Generic;
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
        private readonly StockView                    _stockView;
        private readonly WasteView                    _wasteView;
        private readonly FoundationView               _foundationView;

        public GameController(
            GameModel gameModel,
            UndoController undoController,
            InputController inputController,
            StockView stockView,
            WasteView wasteView,
            FoundationView foundationView)
        {
            _gameModel       = gameModel;
            _undoController  = undoController;
            _inputController = inputController;
            _stockView       = stockView;
            _wasteView       = wasteView;
            _foundationView  = foundationView;

            _onCardDropped = (c, s) => HandleDropAsync(c, s).Forget();
            inputController.OnCardDropped            += _onCardDropped;
            inputController.OnUndoRequested          += HandleUndo;
            inputController.OnDrawRequested          += HandleDraw;
            inputController.OnCardDroppedOnFoundation += HandleFoundationDrop;
        }

        private async UniTaskVoid HandleDropAsync(CardView card, StackView targetStack)
        {
            if (targetStack.StackModel == null)
            {
                Debug.LogWarning("[GameController] StackView has no StackModel — call Initialize() on it.");
                return;
            }

            var fromContainer = FindSourceContainer(card);
            var fromStack     = _gameModel.FindStack(card.CardModel);

            if (fromStack == targetStack.StackModel || !_gameModel.IsValidMove(card.CardModel, targetStack.StackModel))
            {
                foreach (var c in card.DragGroup)
                    c.SnapBack();
                return;
            }

            var dragGroup = card.DragGroup;
            var followers = new List<CardView>(dragGroup.Count - 1);
            for (int i = 0; i < dragGroup.Count; i++)
                if (dragGroup[i] != card) followers.Add(dragGroup[i]);

            var wasteFrom = fromContainer is WasteView ? _gameModel.WasteModel : null;
            var mainCmd = new MoveCardCommand(
                card.CardModel, fromStack, targetStack.StackModel,
                fromContainer, targetStack,
                card, card.OriginalPosition, targetStack.GetSlotPosition(targetStack.CardCount),
                wasteFrom);

            ICommand cmd = followers.Count > 0
                ? new GroupDragCommand(mainCmd, followers, fromContainer, targetStack,
                                       fromStack, targetStack.StackModel)
                : mainCmd;
            await _undoController.ExecuteAsync(cmd);
        }

        private void HandleUndo() => _undoController.UndoAsync().Forget();

        private void HandleDraw()
        {
            var topCard = _stockView.GetTopCardView();
            if (topCard == null) return;

            var cmd = new DrawCardCommand(
                topCard,
                _gameModel.StockModel,
                _gameModel.WasteModel,
                _stockView,
                _wasteView,
                _stockView.GetTopSlotPosition(),
                _wasteView.GetTopSlotPosition()
            );
            _undoController.ExecuteAsync(cmd).Forget();
        }

        private void HandleFoundationDrop(CardView card, FoundationView foundation)
        {
            var fromContainer = FindSourceContainer(card);
            var fromStack     = _gameModel.FindStack(card.CardModel);
            var wasteFrom     = fromContainer is WasteView ? _gameModel.WasteModel : null;

            var cmd = new FoundationMoveCommand(
                card.CardModel,
                fromStack,
                _gameModel.FoundationModel,
                card,
                fromContainer,
                foundation,
                card.OriginalPosition,
                foundation.GetTopSlotPosition(),
                wasteFrom);
            _undoController.ExecuteAsync(cmd).Forget();
        }

        private ICardContainer FindSourceContainer(CardView card)
        {
            if (card.CurrentStack != null) return card.CurrentStack;
            if (_wasteView.GetTopCardView() == card) return _wasteView;
            return null;
        }

        public void Dispose()
        {
            _inputController.OnCardDropped            -= _onCardDropped;
            _inputController.OnUndoRequested          -= HandleUndo;
            _inputController.OnDrawRequested          -= HandleDraw;
            _inputController.OnCardDroppedOnFoundation -= HandleFoundationDrop;
        }
    }
}
