# Architecture Decision Record — Solitaire Undo Prototype

**Scope:** 3 tableau columns · stock / waste / foundation · drag-and-drop · multi-step undo · Phase 2: draw, flip, SetDraggable  
**Stack:** Unity 6000.3.7f1 · DOTween · UniTask · Addressables · MVC + Command Pattern  
**Audience:** Senior Unity reviewer (~5-min read)

---

## System Map

```
GameBootstrapper (MonoBehaviour, scene root)
  │
  ├─ loads CardSpriteRegistry via Addressables
  ├─ constructs pure-C# controllers (no MonoBehaviour coupling)
  ├─ wires StackView[], StockView, WasteView, FoundationView
  └─ deals initial cards (not a Command — not undoable)

InputController ──OnCardDropped──▶ GameController ──creates──▶ MoveCardCommand
                │                        │                           │
                │                  UndoController              CardView.MoveToAsync()
                │                  Stack<ICommand>              (DOTween → UniTask)
                │                        │
                ├──OnDrawRequested──▶ HandleDraw() ──creates──▶ DrawCardCommand
                │                                                (instant — no DOTween)
                └──OnUndoRequested──▶ HandleUndo()
                                            │
                              GameModel / StackModel / StockModel
                              WasteModel / FoundationModel / CardModel
```

---

## Scene Layout

```
[ Column 1 ]    [ Column 2 ]    [ Column 3 ]    [ Stock ]  [ Waste ]  [ Foundation ]
  face-up         face-down       face-down      click       face-up    drop only
                  face-up         face-down      to draw
                                  face-up
```

- Top face-up card in every tableau column: `SetDraggable(true)`
- All face-down cards: `SetDraggable(false)`
- Stock cards: `SetDraggable(false)` — click to draw, never drag
- Waste top card: `SetDraggable(true)` — others `SetDraggable(false)`
- Foundation: cards are deposited, never dragged out

No placement rules. Any card can go anywhere.

---

## ADR-001 · How GameController mediates without tight coupling

### Decision
`InputController` exposes typed C# `event` delegates. `GameController` subscribes in its constructor. Neither class imports the other's concrete type beyond the subscription site.

**Phase 2 addition:** `OnDrawRequested` event added to `InputController`. `StockView.OnPointerClick` calls `NotifyDrawRequested()`.

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
    public event Action                      OnDrawRequested;   // Phase 2

    internal void NotifyCardDropped(CardView card, StackView target) =>
        OnCardDropped?.Invoke(card, target);

    internal void NotifyUndoRequested() =>
        OnUndoRequested?.Invoke();

    internal void NotifyDrawRequested() =>                      // Phase 2
        OnDrawRequested?.Invoke();
}

// GameController.cs
public sealed class GameController
{
    public GameController(GameModel model, UndoController undo, InputController input)
    {
        input.OnCardDropped   += HandleDrop;
        input.OnUndoRequested += HandleUndo;
        input.OnDrawRequested += HandleDraw;   // Phase 2
    }
}
```

---

## ADR-002 · Where animation and visual-state responsibility lives

### Decision
`CardView` owns all DOTween calls and exposes `UniTask` methods. Phase 2 adds **instant** synchronous operations: `SetFaceUp(bool)` and `SetDraggable(bool)`. These return `void` — no `UniTask` wrapping.

**Rule:** drag-and-drop moves → `MoveToAsync` (DOTween, 0.3 s). Stock draw and card flip → instant, no DOTween.

`FlipAsync` (DOTween scale sequence from Phase 1 design) is superseded by `SetFaceUp` — instant flip is the correct choice for this prototype.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Command drives DOTween directly** | Self-contained | DOTween leaks into business logic layer |
| **B. CardView owns DOTween, exposes UniTask** ✅ | SRP; swap animation without touching commands | Command must hold a `CardView` ref (see ADR-004) |
| **C. Separate AnimationService** | Fully isolated | Extra indirection with no benefit at this scope |

### Rationale
`CardView` is the visual representation of a card — animating and toggling its visuals is *view* work. Commands stay animation-agnostic: they call `await _view.MoveToAsync(pos)` or `_view.SetFaceUp(true)` and never touch DOTween or sprites directly.

```csharp
// CardView.cs — Phase 1
public UniTask MoveToAsync(Vector3 worldPos, float duration = 0.3f) =>
    transform.DOMove(worldPos, duration).SetEase(Ease.OutCubic).ToUniTask();

// CardView.cs — Phase 2 additions (instant, synchronous)
public void SetFaceUp(bool faceUp)
{
    _frontImage.enabled =  faceUp;
    _backImage.enabled  = !faceUp;
    _card.IsFaceUp      =  faceUp;
}

public void SetDraggable(bool draggable) =>
    _canvasGroup.blocksRaycasts = draggable;
```

---

## ADR-003 · How CardSpriteRegistry is accessed globally

### Decision
`GameBootstrapper` loads the SO via Addressables at startup, calls `registry.Initialize()`, then passes it down by constructor/method parameter into each view. No global accessor exists.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Static singleton** | Simple | Hard to test; violates "no statics" constraint |
| **B. Zenject / VContainer DI** | Clean inversion of control | 30-min setup cost; framework knowledge required of reviewer |
| **C. Service Locator** | Avoids static field, still global | Hidden dependency; same testability problem as A |
| **D. Constructor/method injection via Bootstrapper** ✅ | Explicit dependencies; no framework; testable | Bootstrapper must wire everything at startup |

### Rationale
For a prototype, DI-by-parameter is the most readable pattern. The reviewer sees exactly where `CardSpriteRegistry` comes from. Addressables load happens exactly once in `GameBootstrapper.StartAsync()`.

```csharp
// GameBootstrapper.cs
public sealed class GameBootstrapper : MonoBehaviour
{
    [SerializeField] private AssetReferenceT<CardSpriteRegistry> _registryRef;
    [SerializeField] private StackView[]    _stackViews;
    [SerializeField] private StockView      _stockView;
    [SerializeField] private WasteView      _wasteView;
    [SerializeField] private FoundationView _foundationView;
    [SerializeField] private UndoButton     _undoButton;

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

        _stockView.Initialize(gameModel.StockModel, inputController);
        _wasteView.Initialize(gameModel.WasteModel, registry);
        _foundationView.Initialize(gameModel.FoundationModel, gameModel);

        DealInitialCards(gameModel, registry);   // not a Command; initial state is not undoable
    }
}
```

---

## ADR-004 · What MoveCardCommand holds as references

### Decision
`MoveCardCommand` stores: `CardModel`, source `StackModel`, destination `StackModel`, `CardView`, source `StackView` (Phase 2), two world positions captured at command creation time, and lazy flip fields `_didFlipSourceTop` / `_sourceTopCard`.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Model refs only** | Pure data | Requires a separate observer system to sync views on undo |
| **B. Model + View refs** ✅ | Command is the complete unit of work; Execute/Undo are self-contained | Command spans MVC layers — intentional given Command Pattern's role |
| **C. View refs only** | No model coupling in command | View must own model mutation — breaks MVC |

### Rationale
The Command Pattern's job is to encapsulate a *complete reversible action*. That includes both state mutation (model) and visual feedback (view). Positions are captured at construction time, making `Undo()` safe even if the stack layout changes later. The source `StackView` ref is needed by Phase 2 flip logic (see ADR-007).

```csharp
// MoveCardCommand.cs — Phase 2
public sealed class MoveCardCommand : ICommand
{
    private readonly CardModel  _card;
    private readonly StackModel _from;
    private readonly StackModel _to;
    private readonly CardView   _view;
    private readonly StackView  _fromView;   // needed to resolve new top CardView post-move
    private readonly Vector3    _originPos;
    private readonly Vector3    _targetPos;

    private bool     _didFlipSourceTop;
    private CardView _sourceTopCard;

    public MoveCardCommand(
        CardModel card, StackModel from, StackModel to,
        CardView view, StackView fromView,
        Vector3 originPos, Vector3 targetPos)
    {
        _card      = card;
        _from      = from;
        _to        = to;
        _view      = view;
        _fromView  = fromView;
        _originPos = originPos;
        _targetPos = targetPos;
    }

    public async UniTask Execute()
    {
        _from.Remove(_card);
        _to.Add(_card);
        await _view.MoveToAsync(_targetPos);

        var newTop = _fromView.GetTopCardView();
        if (newTop != null && !newTop.CardModel.IsFaceUp)
        {
            _didFlipSourceTop = true;
            _sourceTopCard    = newTop;
            _sourceTopCard.SetFaceUp(true);
            _sourceTopCard.SetDraggable(true);
        }
    }

    public async UniTask Undo()
    {
        if (_didFlipSourceTop)
        {
            _sourceTopCard.SetFaceUp(false);
            _sourceTopCard.SetDraggable(false);
        }
        _to.Remove(_card);
        _from.Add(_card);
        await _view.MoveToAsync(_originPos);
    }
}
```

---

## ADR-005 · Async execution strategy (UniTask + Forget)

### Decision
`UndoController` exposes `UniTask` methods. `GameController` calls them with `.Forget()`. The controller is not a MonoBehaviour, so it cannot be `async void` — `.Forget()` is the UniTask-idiomatic way to fire-and-forget while still surfacing exceptions to `UniTaskScheduler`.

`DrawCardCommand.Execute/Undo` has a synchronous body and returns `UniTask.CompletedTask` — no special case needed at the `UndoController` call site.

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
    var cmd = new MoveCardCommand(
        card.CardModel,
        _gameModel.FindStack(card.CardModel),
        target.StackModel,
        card,
        _gameModel.FindStackView(card.CardModel),
        card.transform.position,
        target.GetNextSlotPosition()
    );
    _undoController.ExecuteAsync(cmd).Forget();
}

private void HandleDraw()                           // Phase 2
{
    var topCard = _stockView.GetTopCardView();
    if (topCard == null) return;
    var cmd = new DrawCardCommand(
        topCard,
        _gameModel.StockModel,
        _gameModel.WasteModel,
        _stockView.GetTopSlotPosition(),
        _wasteView.GetTopSlotPosition()
    );
    _undoController.ExecuteAsync(cmd).Forget();
}

private void HandleUndo() => _undoController.UndoAsync().Forget();
```

---

## ADR-006 · SetDraggable ownership

### Decision
Each `ICommand` calls `SetDraggable` on only the `CardView` instances it directly affects. No global refresh loop over all cards.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Global refresh after every command** | Easy to reason about in isolation | O(n) loop touches cards the command never affected |
| **B. Command-owned, surgical** ✅ | Zero wasted work; coupling between action and side effect is explicit | Each command author must call it for every affected card |
| **C. StackView auto-observes model changes** | View manages its own draggability | Requires observer/event wiring; hides coupling from command to view |

### Rationale
Commands already hold references to every `CardView` they affect. Calling `SetDraggable` in `Execute` and reversing it in `Undo` is two lines per command — no infrastructure required. A global loop would touch every card in the scene on every action even when only one card changed. The invariant is simple: when a command runs, it leaves exactly the affected cards in the correct draggable state.

---

## ADR-007 · Flip responsibility in MoveCardCommand

### Decision
`MoveCardCommand` is responsible for detecting, executing, and reversing the source-top flip. It stores `_didFlipSourceTop` and `_sourceTopCard` as private mutable fields. `Undo()` reads these fields before reversing the move.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. GameController checks after ExecuteAsync** | Command stays simple | Flip is outside the undo record — `Undo()` misses the reversal |
| **B. StackView auto-flips on model change** | View self-manages | Undo requires the view to detect the reverse case; fragile at boundaries |
| **C. MoveCardCommand owns it** ✅ | Complete reversible unit; Execute/Undo are symmetric | Command is slightly larger; must hold a source `StackView` ref |

### Rationale
The undo stack stores commands. For undo to be correct, every side effect of `Execute` must be reversed in `Undo`. The source-top flip is a side effect of moving a card — it must live inside the command. `GameController` triggering it externally would break the invariant that a single `_history.Pop().Undo()` fully reverses the action.

---

## ADR-008 · Stock order preservation via undo stack

### Decision
Stock order is preserved **implicitly** by undo stack ordering. `DrawCardCommand.Undo()` pushes the drawn card back to `StockModel`. Because the undo stack is LIFO, the last-drawn card is always undone first — matching the natural reverse order of the draw pile.

### Options considered

| Option | Pro | Con |
|---|---|---|
| **A. Explicit index tracking in StockModel** | Self-documenting at the model level | Extra state that must stay in sync with every undo operation |
| **B. Implicit LIFO ordering via undo stack** ✅ | Zero extra state; correctness is structural | Relies on callers not bypassing the undo stack |

### Rationale
The undo stack *is* the reverse-order record. Draws are pushed in chronological order; undos pop in reverse. No additional index bookkeeping is needed — the invariant "most recent draw is always undone first" is guaranteed by the stack data structure itself, not by application logic.

---

## DrawCardCommand — Code

```csharp
// DrawCardCommand.cs
public sealed class DrawCardCommand : ICommand
{
    private readonly CardView   _drawnCard;
    private readonly StockModel _stock;
    private readonly WasteModel _waste;
    private readonly Vector3    _stockPos;
    private readonly Vector3    _wastePos;

    public DrawCardCommand(
        CardView drawnCard, StockModel stock, WasteModel waste,
        Vector3 stockPos, Vector3 wastePos)
    {
        _drawnCard = drawnCard;
        _stock     = stock;
        _waste     = waste;
        _stockPos  = stockPos;
        _wastePos  = wastePos;
    }

    public UniTask Execute()
    {
        _stock.Pop();
        _waste.Push(_drawnCard.CardModel);
        _drawnCard.SetFaceUp(true);
        _drawnCard.transform.position = _wastePos;
        _drawnCard.SetDraggable(true);
        return UniTask.CompletedTask;
    }

    public UniTask Undo()
    {
        _waste.Pop();
        _stock.Push(_drawnCard.CardModel);
        _drawnCard.SetFaceUp(false);
        _drawnCard.transform.position = _stockPos;
        _drawnCard.SetDraggable(false);
        return UniTask.CompletedTask;
    }
}
```

---

## Folder & Class Structure

```
Assets/
├── Scripts/
│   ├── Bootstrap/
│   │   └── GameBootstrapper.cs        MonoBehaviour — wires everything, loads Addressables, deals initial cards
│   │
│   ├── Commands/
│   │   ├── ICommand.cs                Interface: Execute() / Undo() → UniTask
│   │   ├── MoveCardCommand.cs         Drag move: model mutation + DOTween animation + source-top flip
│   │   └── DrawCardCommand.cs         Stock draw: instant move + flip + draggable toggle  [Phase 2]
│   │
│   ├── Controllers/
│   │   ├── GameController.cs          Mediator: validates moves, creates commands, handles draw
│   │   ├── InputController.cs         Event source: card drop / undo / draw
│   │   └── UndoController.cs          Stack<ICommand> — ExecuteAsync / UndoAsync
│   │
│   ├── Models/
│   │   ├── CardModel.cs               Suit, Rank, IsFaceUp (plain C#)
│   │   ├── StackModel.cs              List<CardModel>, Add/Remove
│   │   ├── StockModel.cs              Stack<CardModel>, Push/Pop  [Phase 2]
│   │   ├── WasteModel.cs              Stack<CardModel>, Push/Pop  [Phase 2]
│   │   ├── FoundationModel.cs         List<CardModel>, Add/Remove  [Phase 2]
│   │   └── GameModel.cs               All model refs, IsValidMove, FindStack/FindStackView
│   │
│   ├── Views/
│   │   ├── CardView.cs                MonoBehaviour: drag handlers, MoveToAsync, SetFaceUp, SetDraggable
│   │   ├── StackView.cs               MonoBehaviour: tableau slot layout, GetNextSlotPosition, GetTopCardView
│   │   ├── StockView.cs               MonoBehaviour: IPointerClickHandler → NotifyDrawRequested  [Phase 2]
│   │   ├── WasteView.cs               MonoBehaviour: drop target, shows top drawn card  [Phase 2]
│   │   ├── FoundationView.cs          MonoBehaviour: drop target only, no dragging out  [Phase 2]
│   │   └── UndoButton.cs              MonoBehaviour: binds Button.onClick → InputController
│   │
│   └── Data/
│       ├── CardSpriteRegistry.cs      ScriptableObject: Suit+Rank → Sprite lookup
│       └── Enums/
│           ├── Suit.cs                Hearts, Spades (prototype uses subset)
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
| `StackModel` | Model | `List<CardModel>`, Add/Remove | Views, DOTween, positions |
| `StockModel` | Model | `Stack<CardModel>`, Push/Pop | Views, commands |
| `WasteModel` | Model | `Stack<CardModel>`, Push/Pop | Views, commands |
| `FoundationModel` | Model | `List<CardModel>`, Add/Remove | Views, commands |
| `GameModel` | Model | All models, IsValidMove, FindStack, FindStackView | Views, input, commands |
| `CardView` | View | DOTween, `CardModel`, `CanvasGroup`, drag handlers | Commands, controllers, `GameModel` |
| `StackView` | View | `StackModel`, `CardView[]`, slot positions, GetTopCardView | Commands, `GameController` |
| `StockView` | View | `StockModel`, `InputController` (notify only), GetTopCardView | Commands, game rules |
| `WasteView` | View | `WasteModel`, top card draggability, GetTopSlotPosition | Commands, game rules |
| `FoundationView` | View | `FoundationModel` | Dragging, commands |
| `UndoButton` | View | `InputController` (notify only) | Everything else |
| `InputController` | Controller | C# events only | Game rules, models, views |
| `UndoController` | Controller | `ICommand`, `Stack<ICommand>` | Card types, views, models |
| `GameController` | Controller | All controllers, `GameModel`, view refs for HandleDraw | DOTween, Addressables |
| `MoveCardCommand` | Command | `CardModel`, `StackModel`×2, `CardView`, `StackView` (source), positions | `UndoController`, `GameController` |
| `DrawCardCommand` | Command | `CardView`, `StockModel`, `WasteModel`, positions | `UndoController`, `GameController` |
| `CardSpriteRegistry` | Data | `Suit`, `Rank`, `Sprite` | Everything else |
| `GameBootstrapper` | Bootstrap | All of the above | Game rules (purely wiring + initial deal) |

---

## Async Flow — Execute and Undo

```
MoveCardCommand — EXECUTE
─────────────────────────
CardView.OnEndDrag
  └─▶ InputController.NotifyCardDropped(cardView, targetStackView)
        └─▶ [event] GameController.HandleDrop()
              ├─ GameModel.IsValidMove()  →  false: return
              ├─ new MoveCardCommand(...)
              └─ UndoController.ExecuteAsync(cmd).Forget()
                    ├─ await cmd.Execute()
                    │     ├─ from.Remove(card)                    [instant]
                    │     ├─ to.Add(card)                         [instant]
                    │     ├─ await view.MoveToAsync(_targetPos)   [DOTween, 0.3 s]
                    │     └─ if source new-top is face-down:
                    │           _sourceTopCard.SetFaceUp(true)    [instant]
                    │           _sourceTopCard.SetDraggable(true) [instant]
                    └─ _history.Push(cmd)

MoveCardCommand — UNDO
──────────────────────
UndoButton.onClick
  └─▶ InputController.NotifyUndoRequested()
        └─▶ [event] GameController.HandleUndo()
              └─ UndoController.UndoAsync().Forget()
                    ├─ cmd = _history.Pop()
                    └─ await cmd.Undo()
                          ├─ if _didFlipSourceTop:
                          │     _sourceTopCard.SetFaceUp(false)    [instant]
                          │     _sourceTopCard.SetDraggable(false) [instant]
                          ├─ to.Remove(card)                       [instant]
                          ├─ from.Add(card)                        [instant]
                          └─ await view.MoveToAsync(_originPos)    [DOTween, 0.3 s]

DrawCardCommand — EXECUTE
─────────────────────────
StockView.OnPointerClick
  └─▶ InputController.NotifyDrawRequested()
        └─▶ [event] GameController.HandleDraw()
              ├─ stockView.GetTopCardView()  →  null: return
              ├─ new DrawCardCommand(...)
              └─ UndoController.ExecuteAsync(cmd).Forget()
                    ├─ await cmd.Execute()               — returns UniTask.CompletedTask
                    │     ├─ stock.Pop()                 [instant]
                    │     ├─ waste.Push(card)            [instant]
                    │     ├─ cardView.SetFaceUp(true)    [instant]
                    │     ├─ transform.position = _wastePos [instant]
                    │     └─ cardView.SetDraggable(true) [instant]
                    └─ _history.Push(cmd)

DrawCardCommand — UNDO
──────────────────────
(same undo entry point as above)
  └─ await cmd.Undo()                        — returns UniTask.CompletedTask
        ├─ waste.Pop()                        [instant]
        ├─ stock.Push(card)                   [instant]
        ├─ cardView.SetFaceUp(false)          [instant]
        ├─ transform.position = _stockPos     [instant]
        └─ cardView.SetDraggable(false)       [instant]
```

---

## Implementation Order

| # | Task | Phase | Est. |
|---|---|---|---|
| 1 | Enums + `CardModel` + `StackModel` + `GameModel` | 1 | done |
| 2 | `ICommand` + `MoveCardCommand` + `UndoController` | 1 | done |
| 3 | `CardSpriteRegistry` SO + Addressable setup | 1 | done |
| 4 | `CardView` — sprite display, drag handlers, `MoveToAsync` | 1 | done |
| 5 | `StackView` — slot layout, `GetNextSlotPosition` | 1 | done |
| 6 | `InputController` + `GameController` + `UndoButton` | 1 | done |
| 7 | `GameBootstrapper` — wire everything, initial deal | 1 | done |
| 8 | Scene setup, test undo chain, polish | 1 | done |
| 9 | `StockModel` + `WasteModel` + `FoundationModel` | 2 | 10 min |
| 10 | `CardView.SetFaceUp` + `CardView.SetDraggable` | 2 | 10 min |
| 11 | `DrawCardCommand` | 2 | 15 min |
| 12 | Update `MoveCardCommand` with flip fields + logic | 2 | 10 min |
| 13 | `StockView` + `WasteView` + `FoundationView` | 2 | 20 min |
| 14 | `InputController.OnDrawRequested` + `GameController.HandleDraw` | 2 | 10 min |
| 15 | `GameBootstrapper` Phase 2: deal scene layout, wire new views | 2 | 15 min |
| 16 | Scene layout, test draw + undo chain end-to-end | 2 | 20 min |

**Phase 2 total: ~1.5 hours**

---

## Non-functional checklist

- [x] No static singletons — registry injected by bootstrapper
- [x] No coroutines — all async via `UniTask` + `.ToUniTask()` on DOTween sequences
- [x] Undo is stack-based — pops `N` times for `N`-step undo
- [x] `GameController` never imports DOTween
- [x] `CardView` never imports `GameModel` or command classes
- [x] Each class has one named responsibility (see matrix above)
- [x] `ICommand` is the only cross-layer seam — both controllers and the bootstrapper depend on it
- [x] No global card refresh loop — `SetDraggable` called surgically per command (ADR-006)
- [x] No DOTween for stock draw or flip — instant operations only (ADR-002)
- [x] Stock order preserved structurally by LIFO undo stack — no index bookkeeping (ADR-008)
- [x] Flip side effects live inside `MoveCardCommand` — undo is always symmetric (ADR-007)
