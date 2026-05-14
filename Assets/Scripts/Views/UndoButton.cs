using Solitaire.Controllers;
using UnityEngine;
using UnityEngine.UI;

namespace Solitaire.Views
{
    [RequireComponent(typeof(Button))]
    public sealed class UndoButton : MonoBehaviour
    {
        private Button _button;

        private void Awake() => _button = GetComponent<Button>();

        public void Bind(InputController inputController)
        {
            _button.onClick.AddListener(() => inputController.NotifyUndoRequested());
        }
    }
}
