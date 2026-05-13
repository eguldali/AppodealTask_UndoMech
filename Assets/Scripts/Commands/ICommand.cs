using Cysharp.Threading.Tasks;

namespace Solitaire.Commands
{
    public interface ICommand
    {
        UniTask Execute();
        UniTask Undo();
    }
}
