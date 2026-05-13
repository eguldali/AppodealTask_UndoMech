using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Commands;

namespace Solitaire.Controllers
{
    public sealed class UndoController
    {
        private readonly Stack<ICommand> _history = new();
        private bool _isBusy = false;

        public bool CanUndo => _history.Count > 0;

        public async UniTask ExecuteAsync(ICommand command)
        {
            if (_isBusy) return;
            _isBusy = true;
            try
            {
                await command.Execute();
                _history.Push(command);
            }
            finally
            {
                _isBusy = false;
            }
        }

        public async UniTask UndoAsync()
        {
            if (_isBusy || !CanUndo) return;
            _isBusy = true;
            try
            {
                await _history.Pop().Undo();
            }
            finally
            {
                _isBusy = false;
            }
        }
    }
}
