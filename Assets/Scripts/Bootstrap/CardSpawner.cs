using Solitaire.Controllers;
using Solitaire.Views;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Bootstrap
{
    public sealed class CardSpawner : MonoBehaviour
    {
        [SerializeField] private CardView   _cardViewPrefab;
        [SerializeField] private Canvas     _canvas;
        [SerializeField] private StackView[] _stackViews;
        [SerializeField] private Button     _undoButton;
        [SerializeField] private float      _spacing = 120f;

        private InputController _inputController;
        private GameController  _gameController;

        private static readonly (Suit suit, Rank rank)[] Cards =
        {
            (Suit.Hearts, Rank.Ace),
            (Suit.Hearts, Rank.Two),
            (Suit.Hearts, Rank.Three),
            (Suit.Hearts, Rank.Four),
            (Suit.Hearts, Rank.Five),
        };

        private void Start()
        {
            // Build model layer
            // freeStack holds cards that aren't in any StackView yet,
            // so GameModel.FindStack can locate them for command creation.
            var freeStack   = new StackModel();
            var stackModels = new StackModel[_stackViews.Length + 1];
            stackModels[0]  = freeStack;
            for (int i = 0; i < _stackViews.Length; i++)
            {
                stackModels[i + 1] = new StackModel();
                _stackViews[i].Initialize(stackModels[i + 1]);
            }

            var gameModel = new GameModel();
            gameModel.Initialize(stackModels);

            // Build controller layer
            _inputController = new InputController();
            var undoController = new UndoController();
            _gameController    = new GameController(gameModel, undoController, _inputController);

            // Wire undo button
            _undoButton.onClick.AddListener(() => _inputController.NotifyUndoRequested());

            // Spawn cards
            float totalWidth = (Cards.Length - 1) * _spacing;
            float startX     = -totalWidth / 2f;

            for (int i = 0; i < Cards.Length; i++)
            {
                var (suit, rank) = Cards[i];
                var model = new CardModel(suit, rank);
                freeStack.Add(model);

                var card = Instantiate(_cardViewPrefab, _canvas.transform);
                card.transform.position = new Vector3(startX + i * _spacing, 0f, 0f);
                card.Initialize(model);
                card.SetInputController(_inputController);
            }
        }

        private void OnDestroy()
        {
            _gameController?.Dispose();
        }
    }
}
