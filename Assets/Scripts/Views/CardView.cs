using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Solitaire.Controllers;
using Solitaire.Data;
using Solitaire.Models;
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
        private CardModel       _cardModel;
        private Canvas          _canvas;
        private CanvasGroup     _canvasGroup;
        private RectTransform   _rectTransform;
        private StackView       _currentStack;
        private Vector3         _originalPosition;
        private Transform       _originalParent;
        private List<CardView>  _dragGroup;
        private List<Vector3>   _dragOffsets;

        public CardModel                   CardModel         => _cardModel;
        public Vector3                     OriginalPosition  => _originalPosition;
        public List<CardView>              DragGroup         => _dragGroup ?? new List<CardView> { this };
        public StackView                   CurrentStack      => _currentStack;

        private void Awake()
        {
            _canvas        = GetComponentInParent<Canvas>();
            _canvasGroup   = GetComponent<CanvasGroup>();
            _rectTransform = GetComponent<RectTransform>();
        }

        public void Initialize(CardModel model, InputController inputController)
        {
            _cardModel        = model;
            _inputController  = inputController;
            _originalPosition = transform.position;

            _frontImage.sprite = _registry.GetSprite(model.Suit, model.Rank);
            _backImage.sprite  = _registry.GetBackSprite();
        }

        public void SetStack(StackView stack)              => _currentStack  = stack;
        public void SetInputController(InputController ic) => _inputController = ic;

        public void SetFaceUp(bool faceUp)
        {
            _frontImage.enabled =  faceUp;
            _backImage.enabled  = !faceUp;
            _cardModel.IsFaceUp =  faceUp;
        }

        public void SetDraggable(bool draggable) =>
            _canvasGroup.blocksRaycasts = draggable;

        public void SnapBack()
        {
            transform.SetParent(_originalParent);
            MoveToAsync(_originalPosition).Forget();
        }

        public UniTask MoveToAsync(Vector3 worldPos, float duration = 0.3f)
        {
            transform.DOKill();
            var tcs = new UniTaskCompletionSource();
            transform.DOMove(worldPos, duration)
                     .SetEase(Ease.OutCubic)
                     .SetLink(gameObject)
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
                _dragGroup[i]._originalParent   = _dragGroup[i].transform.parent;
                _dragOffsets.Add(_dragGroup[i].transform.position - transform.position);
                _dragGroup[i].transform.SetParent(_canvas.transform);
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

        public void OnEndDrag(PointerEventData eventData) => OnEndDragAsync(eventData).Forget();

        private async UniTaskVoid OnEndDragAsync(PointerEventData eventData)
        {
            foreach (var card in _dragGroup)
                card._canvasGroup.blocksRaycasts = true;

            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            StackView      targetStack    = null;
            FoundationView foundationView = null;
            foreach (var result in results)
            {
                if (targetStack    == null) targetStack    = result.gameObject.GetComponent<StackView>();
                if (foundationView == null) foundationView = result.gameObject.GetComponent<FoundationView>();
            }

            if (targetStack != null && _inputController != null)
            {
                _inputController.NotifyCardDropped(this, targetStack);
            }
            else if (foundationView != null && _inputController != null)
            {
                if (_dragGroup.Count > 1)
                {
                    foreach (var card in _dragGroup)
                    {
                        card.transform.SetParent(card._originalParent);
                        card.MoveToAsync(card.OriginalPosition).Forget();
                    }
                }
                else
                {
                    _inputController.NotifyCardDroppedOnFoundation(this, foundationView);
                }
            }
            else
            {
                foreach (var card in _dragGroup)
                {
                    card.transform.SetParent(card._originalParent);
                    card.MoveToAsync(card.OriginalPosition).Forget();
                }
            }
        }
    }
}
