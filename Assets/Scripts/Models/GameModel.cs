namespace Solitaire.Models
{
    public sealed class GameModel
    {
        public StackModel[]    StackModels     { get; private set; }
        public StockModel      StockModel      { get; private set; }
        public WasteModel      WasteModel      { get; private set; }
        public FoundationModel FoundationModel { get; private set; }

        public void Initialize()
        {
            StackModels     = new[] { new StackModel(), new StackModel(), new StackModel() };
            StockModel      = new StockModel();
            WasteModel      = new WasteModel();
            FoundationModel = new FoundationModel();
        }

        public StackModel FindStack(CardModel card)
        {
            foreach (var stack in StackModels)
                if (stack.Contains(card)) return stack;
            return null;
        }

        public bool IsValidMove(CardModel card, StackModel destination)
        {
            var source = FindStack(card);
            return source != destination;  // null source = waste card, always valid
        }
    }
}
