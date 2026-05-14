using Solitaire.Controllers;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Views
{
    [RequireComponent(typeof(Button))]
    public sealed class UndoButton : MonoBehaviour
    {
        private Button _button;
        private UndoController _undoController;

        private void Awake() => _button = GetComponent<Button>();

        public void Bind(InputController inputController, UndoController undoController)
        {
            _undoController = undoController;
            _button.onClick.AddListener(() => inputController.NotifyUndoRequested());
            _undoController.OnHistoryChanged += RefreshInteractable;
            RefreshInteractable();
        }

        private void OnDestroy()
        {
            if (_undoController != null)
                _undoController.OnHistoryChanged -= RefreshInteractable;
        }

        private void RefreshInteractable() =>
            _button.interactable = _undoController.CanUndo;
    }
}
