using Cysharp.Threading.Tasks;
using Solitaire.Models;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Commands
{
    public sealed class DrawCardCommand : ICommand
    {
        private readonly CardView   _drawnCard;
        private readonly StockModel _stock;
        private readonly WasteModel _waste;
        private readonly StockView  _stockView;
        private readonly WasteView  _wasteView;
        private readonly Vector3    _stockPos;
        private readonly Vector3    _wastePos;

        public DrawCardCommand(
            CardView drawnCard, StockModel stock, WasteModel waste,
            StockView stockView, WasteView wasteView,
            Vector3 stockPos, Vector3 wastePos)
        {
            _drawnCard = drawnCard;
            _stock     = stock;
            _waste     = waste;
            _stockView = stockView;
            _wasteView = wasteView;
            _stockPos  = stockPos;
            _wastePos  = wastePos;
        }

        public UniTask Execute()
        {
            _stock.Pop();
            _stockView.RemoveTopCard();
            _waste.Push(_drawnCard.CardModel);
            _wasteView.AddCard(_drawnCard);
            _drawnCard.SetFaceUp(true);
            _drawnCard.transform.position = _wastePos;
            _drawnCard.SetDraggable(true);
            return UniTask.CompletedTask;
        }

        public UniTask Undo()
        {
            _waste.Pop();
            _wasteView.RemoveTopCard();
            _stock.Push(_drawnCard.CardModel);
            _stockView.AddCard(_drawnCard);
            _drawnCard.SetFaceUp(false);
            _drawnCard.transform.position = _stockPos;
            _drawnCard.SetDraggable(false);
            return UniTask.CompletedTask;
        }
    }
}
