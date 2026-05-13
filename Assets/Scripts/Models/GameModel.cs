namespace Solitaire.Models
{
    public sealed class GameModel
    {
        private StackModel[] _stacks;

        public void Initialize(StackModel[] stacks)
        {
            _stacks = stacks;
        }

        public StackModel FindStack(CardModel card)
        {
            foreach (var stack in _stacks)
                if (stack.Contains(card)) return stack;

            return null;
        }

        public bool IsValidMove(CardModel card, StackModel destination)
        {
            var source = FindStack(card);
            return source != null && source != destination;
        }
    }
}
