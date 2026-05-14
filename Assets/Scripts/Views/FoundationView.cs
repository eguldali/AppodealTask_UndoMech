using System.Collections.Generic;
using Solitaire.Models;
using UnityEngine;

namespace Solitaire.Views
{
    public sealed class FoundationView : MonoBehaviour, ICardContainer
    {
        private FoundationModel _foundationModel;

        private readonly List<CardView> _cardViews = new();

        public void Initialize(FoundationModel foundationModel)
        {
            _foundationModel = foundationModel;
        }

        public void AddCard(CardView cardView)
        {
            _cardViews.Add(cardView);
            cardView.transform.SetParent(transform);
            cardView.transform.position = transform.position;
            cardView.SetDraggable(false);
        }

        public void RegisterCard(CardView cardView)
        {
            _cardViews.Add(cardView);
            cardView.transform.SetParent(transform, true);
            cardView.SetDraggable(false);
        }

        public void UnregisterCard(CardView cardView) => _cardViews.Remove(cardView);

        public void RemoveCard(CardView card) => _cardViews.Remove(card);

        public void RemoveTopCard()
        {
            if (_cardViews.Count == 0) return;
            _cardViews.RemoveAt(_cardViews.Count - 1);
        }

        public CardView GetTopCardView() => _cardViews.Count > 0 ? _cardViews[^1] : null;

        public Vector3 GetTopSlotPosition() => transform.position;
    }
}
