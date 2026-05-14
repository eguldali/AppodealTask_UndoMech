using System.Collections.Generic;
using Solitaire.Models;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Views
{
    [RequireComponent(typeof(Image))]
    public sealed class StackView : MonoBehaviour, ICardContainer
    {
        [SerializeField] private Transform _slotContainer;

        private readonly List<CardView> _cardViews = new();
        private StackModel _stackModel;

        public StackModel StackModel => _stackModel;

        public void Initialize(StackModel stackModel)
        {
            _stackModel = stackModel;
        }

        public void AddCard(CardView cardView)
        {
            var position = GetNextSlotPosition();
            _cardViews.Add(cardView);
            cardView.transform.SetParent(_slotContainer);
            cardView.transform.position = position;
            cardView.SetStack(this);
        }

        public void RemoveCard(CardView cardView)
        {
            _cardViews.Remove(cardView);
        }

        public void RegisterCard(CardView cardView)
        {
            _cardViews.Add(cardView);
            cardView.transform.SetParent(_slotContainer, true);
            cardView.SetStack(this);
        }

        public void UnregisterCard(CardView cardView)
        {
            _cardViews.Remove(cardView);
            cardView.SetStack(null);
        }

        public List<CardView> GetCardsFrom(CardView card)
        {
            var index = _cardViews.IndexOf(card);
            if (index < 0) return new List<CardView> { card };
            return _cardViews.GetRange(index, _cardViews.Count - index);
        }

        public void RemoveCardsFrom(CardView card)
        {
            var index = _cardViews.IndexOf(card);
            if (index < 0) return;
            _cardViews.RemoveRange(index, _cardViews.Count - index);
        }

        public CardView GetTopCardView()         => _cardViews.Count > 0 ? _cardViews[^1] : null;
        public int     CardCount                => _cardViews.Count;
        public Vector3 GetSlotPosition(int i)   => _slotContainer.position + Vector3.down * (i * 30f);
        public Vector3 GetNextSlotPosition()    => GetSlotPosition(_cardViews.Count);
        public Vector3 GetTopSlotPosition()     => GetNextSlotPosition();
    }
}
