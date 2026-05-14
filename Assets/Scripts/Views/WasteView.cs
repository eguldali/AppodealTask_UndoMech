using System.Collections.Generic;
using Solitaire.Models;
using UnityEngine;

namespace Solitaire.Views
{
    public sealed class WasteView : MonoBehaviour, ICardContainer
    {
        private WasteModel _wasteModel;

        private readonly List<CardView> _cardViews = new();

        public void Initialize(WasteModel wasteModel)
        {
            _wasteModel = wasteModel;
        }

        public void AddCard(CardView cardView)
        {
            _cardViews.Add(cardView);
            cardView.transform.SetParent(transform);
            cardView.transform.position = transform.position;
            cardView.SetStack(null);
            RefreshDraggability();
        }

        public void RemoveCard(CardView card)
        {
            _cardViews.Remove(card);
            RefreshDraggability();
        }

        public void RemoveTopCard()
        {
            if (_cardViews.Count == 0) return;
            _cardViews.RemoveAt(_cardViews.Count - 1);
            RefreshDraggability();
        }

        public CardView GetTopCardView() => _cardViews.Count > 0 ? _cardViews[^1] : null;

        public Vector3 GetTopSlotPosition() => transform.position;

        private void RefreshDraggability()
        {
            foreach (var card in _cardViews)
                card.SetDraggable(false);

            if (_cardViews.Count > 0)
                _cardViews[^1].SetDraggable(true);
        }
    }
}
