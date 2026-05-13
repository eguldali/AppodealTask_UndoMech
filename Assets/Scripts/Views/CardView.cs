using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Solitaire.Views
{
    public sealed class CardView : MonoBehaviour
    {
        public UniTask MoveToAsync(Vector3 worldPos, float duration = 0.3f)
        {
            return UniTask.CompletedTask;
        }
    }
}
