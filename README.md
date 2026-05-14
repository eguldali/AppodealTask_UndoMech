# Solitaire – Undo Move System Prototype

Unity 2D prototype built for the Appodeal Senior Unity Developer case study.  
**Stack:** Unity 6000.3.7f1 · DOTween · UniTask · ScriptableObject · MVC + Command Pattern

---

## What I Built

### Phase 1 — Core Undo System
A minimal Solitaire setup with drag-and-drop card movement and a fully stack-based multi-step undo system.

- 3 tableau columns with cascading card layout
- Drag-and-drop between columns — single card and group drag supported
- Multi-step undo via `Stack<ICommand>` — unlimited history, not just last move
- Clean MVC architecture with Command Pattern for all reversible actions
- `UndoController` with `_isBusy` concurrency guard (prevents double-undo on fast taps)

### Phase 2 — Stock / Waste / Foundation
Extended the prototype with a more realistic Solitaire scene layout.

- **Stock pile** — click to draw cards one at a time
- **Waste pile** — drawn cards appear here; only the top card is draggable
- **Foundation pile** — drop target for collected cards; single cards only
- **Face-down card flip** — when the top card of a column is moved away, the newly exposed face-down card automatically flips face-up. This flip is part of the command and is fully reversed on undo
- **Stock order preservation** — undoing draws restores the stock in the exact original order (guaranteed structurally by LIFO undo stack, no extra bookkeeping)

### Architecture Highlights

```
GameBootstrapper
  └── InputController ──events──▶ GameController
                                       │
                              UndoController
                              Stack<ICommand>
                                       │
                    ┌──────────────────┼───────────────────┐
             MoveCardCommand    DrawCardCommand    GroupDragCommand
                    │
              CardView.MoveToAsync()   (DOTween → UniTask)
```

**Key decisions:**

| Decision | Choice | Reason |
|---|---|---|
| Undo mechanism | `Stack<ICommand>` | Extensible, symmetric Execute/Undo, no state snapshots |
| Controller coupling | C# events on `InputController` | One-way dependency, no circular imports |
| Sprite lookup | `CardSpriteRegistry` ScriptableObject | No naming convention dependency, Inspector-driven |
| Async pattern | `UniTask` throughout, no coroutines | Cleaner async chains, proper fire-and-forget with `.Forget()` |
| Card interactivity | `SetDraggable(bool)` per-card | Surgical updates — only affected cards toggled, no global loop |
| View polymorphism | `ICardContainer` interface | `StackView`, `WasteView`, `FoundationView` share one contract |
| Animation | DOTween with `.SetLink(gameObject)` | Auto-kills on destroy, no dangling callbacks |

---

## Architecture Decision Record

A full ADR covering all major design decisions is available in [`ARCHITECTURE.md`](ARCHITECTURE.md).

It documents five key decisions with options, trade-offs, and rationale:
- Controller decoupling via C# events
- Animation ownership (CardView vs Command)
- Sprite registry access pattern
- Command reference scope (Model + View)
- Async execution strategy (UniTask + Forget)

Phase 2 adds three additional ADRs:
- SetDraggable ownership (surgical per-command vs global refresh)
- Flip responsibility inside MoveCardCommand
- Stock order preservation via LIFO undo stack

---

## Project Structure

```
Assets/Scripts/
├── Bootstrap/        GameBootstrapper — wires everything, deals initial cards
├── Commands/         ICommand, MoveCardCommand, DrawCardCommand, GroupDragCommand
├── Controllers/      GameController, InputController, UndoController
├── Models/           CardModel, StackModel, StockModel, WasteModel, FoundationModel, GameModel
├── Views/            CardView, StackView, StockView, WasteView, FoundationView, UndoButton
└── Data/             CardSpriteRegistry (ScriptableObject), Suit/Rank enums
```

---

## How to Run

1. Open the project in **Unity 6000.3.7f1**
2. Open `Assets/Scenes/GameScene.unity`
3. Press **Play**

Controls:
- **Drag** cards between columns
- **Click** the stock pile to draw a card
- **Drag** waste or column cards to other columns or the foundation
- **Undo button** — reverts any number of moves in sequence

---

## What I Would Improve With More Time

**Architecture**
- `CardView` currently handles display, drag gesture, group assembly, raycasting, and animation — this violates SRP. I would extract a `CardDragHandler` component to isolate input from display
- `CardSpriteRegistry` is directly serialized on the `CardView` prefab. In production it would be loaded via Addressables at runtime and released when no longer needed. Addressables integration was scoped during the architecture phase but excluded to keep the prototype minimal — the package and asset groups remain in the project as groundwork for a future build

**Gameplay**
- Proper Klondike rules — red/black alternation, descending rank validation
- Win condition detection
- Full 52-card deck (Hearts + Spades only for prototype)
- Stock cycling — reshuffle waste pile back into stock when exhausted

**Polish**
- Card flip animation (DOTween scale sequence) instead of instant swap
- Smooth fan layout for group drags
- Sound effects on move, flip, undo, and win
- Mobile touch input via Unity's new Input System (prototype uses pointer events)

**Code Quality**
- Unit tests for `MoveCardCommand.Execute/Undo` symmetry and `UndoController` concurrency guard
- `OnValidate` guards on all `[SerializeField]` fields to surface missing references at edit time rather than runtime

---

## AI Workflow

AI tools (Claude via Cowork + Claude Code) were used throughout this project. Here is an honest breakdown of where and how.

### Architecture Design
I used Claude to generate a full Architecture Decision Record (ADR) before writing any code. I provided the task requirements, tech stack constraints (DOTween, UniTask, Addressables, MVC), and scope limits. Claude produced ADR entries for five key decisions — controller coupling, animation ownership, sprite registry access, command references, and async strategy. I reviewed each option table, pushed back on two decisions (removed IPlayerAction as unnecessary abstraction, simplified Addressables usage), and locked the architecture before touching Unity.

**Prompt approach:** I gave explicit constraints ("no static singletons", "no coroutines", "5-minute reviewer read") and named the options I wanted evaluated. Vague prompts produce vague ADRs.

### Code Generation — Layer by Layer
Each architectural layer was generated with a targeted Claude Code prompt. I wrote the prompts myself based on the ADR, specifying exact file paths, method signatures, and what the method should and should not know about. Generation order followed dependency order: Models → Commands → Views → Controllers → Bootstrapper.

I did not generate everything at once and paste it in. Each layer was reviewed before the next was prompted.

**Example prompt pattern:**
```
Create Assets/Scripts/Commands/DrawCardCommand.cs
- Implements ICommand
- Constructor: CardView, StockModel, WasteModel, Vector3 stockPos, Vector3 wastePos
- Execute(): instant, no DOTween, return UniTask.CompletedTask
- Undo(): full reversal of Execute
No animation. No MonoBehaviour.
```

### Code Review
I ran two structured code reviews using an `engineering:code-review` prompt against all 27 scripts. The reviews caught:
- `async void` event handlers (fixed → `UniTaskVoid` + `.Forget()`)
- Missing concurrency guard in `UndoController` (fixed → `_isBusy` flag)
- Fallback path in `OnEndDrag` that bypassed the Command Pattern (removed)
- Missing `SetLink` on DOTween tweens (fixed)
- `GroupDragCommand` not transferring follower `CardModel`s between `StackModel`s — identified here, fixed in a subsequent refactor pass
- Dead `MoveGroupCommand` class — identified here, removed in the same refactor pass

I triaged findings by severity — fixed Critical and Major issues, documented Minor ones in this README as future improvements.

### Bug Fixing
When runtime bugs appeared (stock only drawing one card, dropped cards not joining stacks, z-ordering during drag, foundation not accepting drops), I described the symptom to Claude, it identified the root cause, and I reviewed the proposed fix before applying it. I pushed back on two fixes that were more complex than needed and proposed simpler alternatives.

### What I Wrote Myself
- All prompt engineering (what to ask, how to constrain it, what to reject)
- Scope decisions throughout (what to cut, what to keep)
- Architectural pushback (removing IPlayerAction, simplifying Addressables, SetDraggable instead of global refresh)
- Bug reproduction and symptom description
- This README
