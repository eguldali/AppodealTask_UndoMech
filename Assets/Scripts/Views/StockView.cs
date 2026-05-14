using System.Collections.Generic;
using Solitaire.Controllers;
using Solitaire.Models;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Solitaire.Views
{
    public sealed class StockView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image _topCardImage;

        private StockModel      _stockModel;
        private InputController _inputController;

        private readonly List<CardView> _cardViews = new();

        public void Initialize(StockModel stockModel, InputController inputController)
        {
            _stockModel      = stockModel;
            _inputController = inputController;
        }

        public void AddCard(CardView cardView)
        {
            _cardViews.Add(cardView);
            cardView.transform.SetParent(transform);
            cardView.transform.position = transform.position;
            cardView.SetDraggable(false);
        }

        public void RemoveTopCard()
        {
            if (_cardViews.Count == 0) return;
            _cardViews.RemoveAt(_cardViews.Count - 1);
        }

        public CardView GetTopCardView() => _cardViews.Count > 0 ? _cardViews[^1] : null;

        public Vector3 GetTopSlotPosition() => transform.position;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_cardViews.Count == 0) return;
            _inputController.NotifyDrawRequested();
        }
    }
}
