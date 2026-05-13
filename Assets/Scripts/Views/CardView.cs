using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Controllers;
using Solitaire.Data;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Solitaire.Views
{
    [RequireComponent(typeof(CanvasGroup))]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CardView : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private CardSpriteRegistry _registry;
        [SerializeField] private Image              _frontImage;
        [SerializeField] private Image              _backImage;
        private InputController _inputController;

        private CardModel      _cardModel;
        private Canvas         _canvas;
        private CanvasGroup    _canvasGroup;
        private RectTransform  _rectTransform;
        private StackView      _currentStack;
        private Vector3        _originalPosition;
        private List<CardView> _dragGroup;
        private List<Vector3>  _dragOffsets;

        public CardModel CardModel     => _cardModel;
        public Vector3   OriginalPosition => _originalPosition;
        public IReadOnlyList<CardView> DragGroup => _dragGroup;

        private void Awake()
        {
            _canvas        = GetComponentInParent<Canvas>();
            _canvasGroup   = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Initialize(CardModel model)
        {
            _cardModel        = model;
            _originalPosition = transform.position;

            _frontImage.sprite = _registry.GetSprite(model.Suit, model.Rank);
            _backImage.sprite  = _registry.GetBackSprite();
        }

        public StackView CurrentStack => _currentStack;

        public void SetStack(StackView stack)              => _currentStack      = stack;
        public void SetInputController(InputController ic) => _inputController   = ic;

        private UniTaskCompletionSource _moveTcs;

        public UniTask MoveToAsync(Vector3 worldPos, float duration = 0.3f)
        {
            _moveTcs?.TrySetResult(); // let any awaiting continuation proceed immediately
            transform.DOKill();       // stop the in-progress tween
            var tcs = new UniTaskCompletionSource();
            _moveTcs = tcs;
            transform.DOMove(worldPos, duration)
                     .SetEase(Ease.OutCubic)
                     .OnComplete(() => tcs.TrySetResult());
            return tcs.Task;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragGroup   = _currentStack != null
                ? _currentStack.GetCardsFrom(this)
                : new List<CardView> { this };
            _dragOffsets = new List<Vector3>(_dragGroup.Count);

            for (int i = 0; i < _dragGroup.Count; i++)
            {
                _dragGroup[i]._originalPosition = _dragGroup[i].transform.position;
                _dragOffsets.Add(_dragGroup[i].transform.position - transform.position);
                _dragGroup[i].transform.SetAsLastSibling();
                _dragGroup[i]._canvasGroup.blocksRaycasts = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                _canvas.transform as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var worldPos);

            transform.position = worldPos;
            for (int i = 1; i < _dragGroup.Count; i++)
                _dragGroup[i].transform.position = worldPos + _dragOffsets[i];
        }

        public async void OnEndDrag(PointerEventData eventData)
        {
            foreach (var card in _dragGroup)
                card._canvasGroup.blocksRaycasts = true;

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            StackView targetStack = null;
            foreach (var result in results)
            {
                targetStack = result.gameObject.GetComponent<StackView>();
                if (targetStack != null) break;
            }

            if (targetStack != null)
            {
                if (_inputController != null)
                {
                    _inputController.NotifyCardDropped(this, targetStack);
                }
                else
                {
                    if (_currentStack != null) _currentStack.RemoveCardsFrom(this);
                    foreach (var card in _dragGroup)
                        targetStack.AddCard(card);
                }
            }
            else
            {
                for (int i = 0; i < _dragGroup.Count; i++)
                    SnapBackAsync(_dragGroup[i], i * 0.05f).Forget();
            }
        }

        private async UniTask SnapBackAsync(CardView card, float delaySeconds)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));
            await card.MoveToAsync(card._originalPosition);
        }
    }
}
