using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Solitaire.Commands;

namespace Solitaire.Controllers
{
    public sealed class UndoController
    {
        private readonly Stack<ICommand> _history = new();

        public bool CanUndo => _history.Count > 0;

        public async UniTask ExecuteAsync(ICommand command)
        {
            await command.Execute();
            _history.Push(command);
        }

        public async UniTask UndoAsync()
        {
            if (!CanUndo) return;
            await _history.Pop().Undo();
        }
    }
}
