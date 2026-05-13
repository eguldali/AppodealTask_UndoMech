# Architecture Decision Record — Solitaire Undo Prototype

**Scope:** 2-3 stacks · 5-6 cards · drag-and-drop · multi-step undo  
**Stack:** Unity 6000.3.7f1 · DOTween · UniTask · Addressables · MVC + Command Pattern  
**Audience:** Senior Unity reviewer (~5-min read)

---

## System Map

```
GameBootstrapper (MonoBehaviour, scene root)
  │
  ├─ loads CardSpriteRegistry via Addressables
  ├─ constructs pure-C# controllers (no MonoBehaviour coupling)
  └─ wires StackView[] → injects registry + inputController

InputController ──event──▶ GameController ──creates──▶ MoveCardCommand
                                │                            │
                          UndoController               CardView.MoveToAsync()
                          Stack<ICommand>              (DOTween → UniTask)
                                │
                          GameModel / StackModel / CardModel
```

---

## ADR-001 · How GameController mediates without tight coupling

### Decision
`InputController` exposes typed C# `event` delegates. `GameController` subscribes in its constructor. Neither class imports the other's concrete type beyond the subscription site.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Direct method calls** | Simple | `InputController` must hold a `GameController` ref — bidirectional dependency |
| **B. C# events on InputController** ✅ | One-way dependency; easy to test with lambda stubs | Slightly more boilerplate than option A |
| **C. ScriptableObject event channels** | Fully decoupled across scenes | Overkill for a single-scene prototype; hides control flow |
| **D. Full message bus (UniRx / custom)** | Maximum flexibility | Heavy; unreadable for a 5-min review |

### Rationale
Events give the same decoupling benefit as an event bus with zero extra infrastructure. `InputController` never imports `GameController`. The dependency arrow is one-way: `GameController → InputController`.

```csharp
// InputController.cs
public sealed class InputController
{
    public event Action<CardView, StackView> OnCardDropped;
    public event Action                      OnUndoRequested;

    // Called by CardView.OnEndDrag and UndoButton.onClick
    internal void NotifyCardDropped(CardView card, StackView target) =>
        OnCardDropped?.Invoke(card, target);

    internal void NotifyUndoRequested() =>
        OnUndoRequested?.Invoke();
}

// GameController.cs
public sealed class GameController
{
    public GameController(GameModel model, UndoController undo, InputController input)
    {
        input.OnCardDropped    += HandleDrop;
        input.OnUndoRequested  += HandleUndo;
    }
}
```

---

## ADR-002 · Where animation responsibility lives

### Decision
`CardView` owns all DOTween calls and exposes two `UniTask` methods: `MoveToAsync` and `FlipAsync`. The command calls these methods and `await`s them — it never touches DOTween directly.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Command drives DOTween directly** | Command is self-contained | Command imports DOTween; animation logic leaks into business logic layer |
| **B. CardView owns DOTween, exposes UniTask** ✅ | Single responsibility; swap animation without touching commands | Command must hold a `CardView` ref (see ADR-004) |
| **C. Separate AnimationService** | Fully isolated | Extra indirection with no real benefit at this scope |

### Rationale
`CardView` is the visual representation of a card. Animating it is *view* work. The command stays animation-agnostic — it calls `await _cardView.MoveToAsync(position)` and blocks until the tween completes. Swapping DOTween for anything else touches only `CardView`.

```csharp
// CardView.cs
public UniTask MoveToAsync(Vector3 worldPos, float duration = 0.3f)
{
    return transform
        .DOMove(worldPos, duration)
        .SetEase(Ease.OutCubic)
        .ToUniTask();
}

public UniTask FlipAsync(bool faceUp, float duration = 0.15f)
{
    var seq = DOTween.Sequence();
    seq.Append(transform.DOScaleX(0f, duration * 0.5f));
    seq.AppendCallback(() => SetFaceSprite(faceUp));
    seq.Append(transform.DOScaleX(1f, duration * 0.5f));
    return seq.ToUniTask();
}
```

---

## ADR-003 · How CardSpriteRegistry is accessed globally

### Decision
`GameBootstrapper` loads the SO via Addressables at startup, calls `registry.Initialize()`, then passes it down by constructor/method parameter into each `StackView`, which passes it into each `CardView`. No global accessor exists.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Static singleton** | Simple | Hard to test; violates "no statics" constraint |
| **B. Zenject / VContainer DI** | Clean inversion of control | 30-min setup cost; framework knowledge required of reviewer |
| **C. Service Locator** | Avoids static field, still global | Hidden dependency; same testability problem as A |
| **D. Constructor/method injection via Bootstrapper** ✅ | Explicit dependencies; no framework; testable | Bootstrapper must wire everything at startup |

### Rationale
For a 2-hour prototype, DI-by-parameter is the most readable pattern. The reviewer sees exactly where `CardSpriteRegistry` comes from. Addressables load happens exactly once in `GameBootstrapper.StartAsync()`.

```csharp
// GameBootstrapper.cs
public sealed class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private AssetReferenceT<CardSpriteRegistry> _registryRef;
    [SerializeField] private StackView[]                         _stackViews;
    [SerializeField] private UndoButton                          _undoButton;

    private async UniTaskVoid Start()
    {
        var registry = await _registryRef.LoadAssetAsync().ToUniTask();
        registry.Initialize();

        var inputController = new InputController();
        var undoController  = new UndoController();
        var gameModel       = new GameModel();
        var gameController  = new GameController(gameModel, undoController, inputController);

        _undoButton.Bind(inputController);

        foreach (var stackView in _stackViews)
            stackView.Initialize(registry, inputController, gameModel);

        gameModel.Initialize(_stackViews);
    }
}
```

---

## ADR-004 · What MoveCardCommand holds as references

### Decision
`MoveCardCommand` stores: `CardModel`, source `StackModel`, destination `StackModel`, `CardView`, plus the two world positions captured **at command creation time**.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Model refs only** | Pure data; views updated separately | Requires a second observer system to sync views on undo; two units of work instead of one |
| **B. Model + View refs** ✅ | Command is the complete unit of work; Execute/Undo are self-contained | Command spans MVC layers — intentional given Command Pattern's role |
| **C. View refs only** | No model coupling in command | View must own model mutation — breaks MVC |

### Rationale
The Command Pattern's job is to encapsulate a *complete reversible action*. That action involves both state mutation (model) and visual feedback (view). Splitting them across two systems adds complexity with no benefit at this scope. Positions are captured at construction time, making `Undo()` safe even if the stack layout changes.

```csharp
// ICommand.cs
public interface ICommand
{
    UniTask Execute();
    UniTask Undo();
}

// MoveCardCommand.cs
public sealed class MoveCardCommand : ICommand
{
    private readonly CardModel  _card;
    private readonly StackModel _from;
    private readonly StackModel _to;
    private readonly CardView   _view;
    private readonly Vector3    _originPos;
    private readonly Vector3    _targetPos;

    public MoveCardCommand(
        CardModel card, StackModel from, StackModel to,
        CardView view, Vector3 originPos, Vector3 targetPos)
    {
        _card      = card;
        _from      = from;
        _to        = to;
        _view      = view;
        _originPos = originPos;
        _targetPos = targetPos;
    }

    public async UniTask Execute()
    {
        _from.Remove(_card);
        _to.Add(_card);
        await _view.MoveToAsync(_targetPos);
    }

    public async UniTask Undo()
    {
        _to.Remove(_card);
        _from.Add(_card);
        await _view.MoveToAsync(_originPos);
    }
}
```

---

## ADR-005 · Async execution strategy (UniTask + Forget)

### Decision
`UndoController` exposes `UniTask` methods. `GameController` calls them with `.Forget()`. The controller is not a MonoBehaviour, so it cannot be `async void` — `.Forget()` is the UniTask-idiomatic way to fire-and-forget while still surfacing exceptions to UniTaskScheduler.

```csharp
// UndoController.cs
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

// GameController.cs — call sites
private void HandleDrop(CardView card, StackView target)
{
    if (!_gameModel.IsValidMove(card.CardModel, target.StackModel)) return;

    var cmd = new MoveCardCommand(
        card.CardModel,
        _gameModel.FindStack(card.CardModel),
        target.StackModel,
        card,
        card.transform.position,
        target.GetNextSlotPosition()
    );

    _undoController.ExecuteAsync(cmd).Forget();
}

private void HandleUndo() => _undoController.UndoAsync().Forget();
```

---

## Folder & Class Structure

```
Assets/
├── Scripts/
│   ├── Bootstrap/
│   │   └── GameBootstrapper.cs        MonoBehaviour — wires everything, loads Addressables
│   │
│   ├── Commands/
│   │   ├── ICommand.cs                Interface: Execute() / Undo() → UniTask
│   │   └── MoveCardCommand.cs         Concrete command: model mutation + view animation
│   │
│   ├── Controllers/
│   │   ├── GameController.cs          Mediator: validates moves, creates commands
│   │   ├── InputController.cs         Event source: card drop / undo button
│   │   └── UndoController.cs          Stack<ICommand> — ExecuteAsync / UndoAsync
│   │
│   ├── Models/
│   │   ├── CardModel.cs               Suit, Rank, IsFaceUp (plain C#)
│   │   ├── StackModel.cs              List<CardModel>, Add/Remove, rule queries
│   │   └── GameModel.cs               All stacks, IsValidMove, FindStack
│   │
│   ├── Views/
│   │   ├── CardView.cs                MonoBehaviour: drag handlers, MoveToAsync, FlipAsync
│   │   ├── StackView.cs               MonoBehaviour: slot layout, GetNextSlotPosition
│   │   └── UndoButton.cs              MonoBehaviour: binds Button.onClick → InputController
│   │
│   └── Data/
│       ├── CardSpriteRegistry.cs      ScriptableObject: Suit+Rank → Sprite lookup
│       └── Enums/
│           ├── Suit.cs                Clubs, Diamonds, Hearts, Spades
│           └── Rank.cs                Ace..King
│
├── AddressableAssets/
│   └── CardSpriteRegistry.asset       Addressable SO (label: "Registry")
│
├── Art/
│   └── Cards/                         Card sprites (Addressable label: "Cards")
│
└── Scenes/
    └── Game.unity
```

---

## Class Responsibility Matrix

| Class | Layer | Knows About | Does Not Know About |
|---|---|---|---|
| `CardModel` | Model | Suit, Rank, IsFaceUp | Views, commands, Unity |
| `StackModel` | Model | `List<CardModel>` | Views, DOTween, positions |
| `GameModel` | Model | `StackModel[]`, move rules | Views, input, commands |
| `CardView` | View | DOTween, `CardModel` (read-only ref) | Commands, controllers, `GameModel` |
| `StackView` | View | `StackModel`, `CardView[]`, slot positions | Commands, `GameController` |
| `InputController` | Controller | C# events only | Game rules, models, views |
| `UndoController` | Controller | `ICommand`, `Stack<ICommand>` | Card types, views, models |
| `GameController` | Controller | All three other controllers, `GameModel` | DOTween, Addressables |
| `MoveCardCommand` | Command | `CardModel`, `StackModel` ×2, `CardView`, positions | `UndoController`, `GameController` |
| `CardSpriteRegistry` | Data | `Suit`, `Rank`, `Sprite` | Everything else |
| `GameBootstrapper` | Bootstrap | All of the above | Game rules (purely wiring) |

---

## Async Flow — Execute and Undo

```
EXECUTE
──────
CardView.OnEndDrag
  └─▶ InputController.NotifyCardDropped(cardView, targetStackView)
        └─▶ [event] GameController.HandleDrop()
              ├─ GameModel.IsValidMove()  →  false: return
              ├─ new MoveCardCommand(...)
              └─ UndoController.ExecuteAsync(cmd).Forget()
                    ├─ await cmd.Execute()
                    │     ├─ fromStack.Remove(cardModel)   [instant]
                    │     ├─ toStack.Add(cardModel)        [instant]
                    │     └─ await cardView.MoveToAsync()  [DOTween, 0.3s]
                    └─ _history.Push(cmd)

UNDO
────
UndoButton.onClick
  └─▶ InputController.NotifyUndoRequested()
        └─▶ [event] GameController.HandleUndo()
              └─ UndoController.UndoAsync().Forget()
                    ├─ cmd = _history.Pop()
                    └─ await cmd.Undo()
                          ├─ toStack.Remove(cardModel)    [instant]
                          ├─ fromStack.Add(cardModel)     [instant]
                          └─ await cardView.MoveToAsync() [DOTween, 0.3s, back to origin]
```

---

## Implementation Order (2-hour time-box)

| # | Task | Est. |
|---|---|---|
| 1 | Enums + `CardModel` + `StackModel` + `GameModel` | 15 min |
| 2 | `ICommand` + `MoveCardCommand` + `UndoController` | 15 min |
| 3 | `CardSpriteRegistry` SO + Addressable setup | 10 min |
| 4 | `CardView` — sprite display, drag handlers, `MoveToAsync` | 20 min |
| 5 | `StackView` — slot layout, `GetNextSlotPosition` | 10 min |
| 6 | `InputController` + `GameController` + `UndoButton` | 15 min |
| 7 | `GameBootstrapper` — wire everything, initial deal | 15 min |
| 8 | Scene setup, test undo chain, polish | 20 min |

**Total: ~2 hours**

---

## Non-functional checklist

- [x] No static singletons — registry injected by bootstrapper  
- [x] No coroutines — all async via `UniTask` + `.ToUniTask()` on DOTween sequences  
- [x] Undo is stack-based — pops `N` times for `N`-step undo  
- [x] `GameController` never imports DOTween  
- [x] `CardView` never imports `GameModel` or command classes  
- [x] Each class has one named responsibility (see matrix above)  
- [x] `ICommand` is the only cross-layer seam — both controllers and the bootstrapper depend on it  
