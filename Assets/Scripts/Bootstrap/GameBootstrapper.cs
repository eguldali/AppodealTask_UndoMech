using System;
using System.Collections.Generic;
using Solitaire.Controllers;
using Solitaire.Data.Enums;
using Solitaire.Models;
using Solitaire.Views;
using UnityEngine;

namespace Solitaire.Bootstrap
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [SerializeField] private CardView       _cardViewPrefab;
        [SerializeField] private Canvas         _canvas;
        [SerializeField] private StackView[]    _stackViews;
        [SerializeField] private StockView      _stockView;
        [SerializeField] private WasteView      _wasteView;
        [SerializeField] private FoundationView _foundationView;
        [SerializeField] private UndoButton     _undoButton;

        private GameController _gameController;

        private void Start()
        {
            // STEP 1 — Create models
            var gameModel = new GameModel();
            gameModel.Initialize();

            // STEP 2 — Create controllers
            var inputController = new InputController();
            var undoController  = new UndoController();
            _gameController     = new GameController(
                gameModel, undoController, inputController,
                _stockView, _wasteView, _foundationView);

            // STEP 3 — Bind undo button
            _undoButton.Bind(inputController, undoController);

            // STEP 4 — Initialize views
            for (int i = 0; i < _stackViews.Length; i++)
                _stackViews[i].Initialize(gameModel.StackModels[i]);

            _stockView.Initialize(gameModel.StockModel, inputController);
            _wasteView.Initialize(gameModel.WasteModel);
            _foundationView.Initialize(gameModel.FoundationModel);

            // STEP 5 — Create and shuffle deck
            var deck = BuildDeck();
            Shuffle(deck);

            // STEP 6 — Deal to tableau columns
            int deckIndex = 0;

            // Column 0: 1 face-up
            DealCard(deck[deckIndex++], true, _stackViews[0], inputController);

            // Column 1: 1 face-down, 1 face-up
            DealCard(deck[deckIndex++], false, _stackViews[1], inputController);
            DealCard(deck[deckIndex++], true,  _stackViews[1], inputController);

            // Column 2: 2 face-down, 1 face-up
            DealCard(deck[deckIndex++], false, _stackViews[2], inputController);
            DealCard(deck[deckIndex++], false, _stackViews[2], inputController);
            DealCard(deck[deckIndex++], true,  _stackViews[2], inputController);

            // STEP 7 — Remaining cards to stock
            for (int i = deckIndex; i < deck.Count; i++)
            {
                var cardModel = deck[i];
                cardModel.IsFaceUp = false;
                gameModel.StockModel.Push(cardModel);

                var cardView = Instantiate(_cardViewPrefab, _canvas.transform);
                cardView.Initialize(cardModel, inputController);
                _stockView.AddCard(cardView);
                cardView.SetDraggable(false);
            }
        }

        private void DealCard(CardModel cardModel, bool faceUp, StackView stackView,
            InputController inputController)
        {
            cardModel.IsFaceUp = faceUp;
            stackView.StackModel.Add(cardModel);

            var cardView = Instantiate(_cardViewPrefab, _canvas.transform);
            cardView.Initialize(cardModel, inputController);
            stackView.AddCard(cardView);
            cardView.SetFaceUp(faceUp);
            cardView.SetDraggable(faceUp);
        }

        private static List<CardModel> BuildDeck()
        {
            var deck = new List<CardModel>(13);
            foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                deck.Add(new CardModel(Suit.Hearts, rank));
            return deck;
        }

        private static void Shuffle(List<CardModel> deck)
        {
            for (int i = deck.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (deck[i], deck[j]) = (deck[j], deck[i]);
            }
        }

        private void OnDestroy()
        {
            _gameController?.Dispose();
        }
    }
}
