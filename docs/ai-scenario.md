# AI-Authored Scenario

The RimMind scenario lets the player pitch a one-line story and have the AI author the full starting state — like a D&D session zero baked into RimWorld's new-colony flow.

## Player flow

1. New colony → world/site setup → **Page_SelectScenario**.
2. The picker now shows **RimMind: AI-Authored Story** alongside Crashlanded / Naked Brutality / etc.
3. Player picks it and clicks **Next**.
4. A modal dialog opens (`Dialog_RimMindCampaignPrompt`). One text-area: *describe your world, your story, and how things are right now*.
5. Player types a pitch (e.g. "Three nuns crashland on a tropical coast after their pilgrimage ship sinks"), clicks **Generate Starting State**.
6. AI returns a structured plan in ~5–30s depending on model. The dialog switches to a review screen with every field rendered as an editable `TextArea`. Player tweaks anything they want.
7. **Commit and continue** → dialog closes → click **Next** on the scenario page → flow advances through site → pawn config → game starts with the AI's pawns, items, and ideology.

## What the AI authors

The schema returned by `ScenarioPlanGenerator` covers:

### World (campaign frame)
- `setting` — vibe / atmosphere.
- `worldLore` — 2–3 paragraphs: geography, politics, history of the region.
- `ideologyName` + `ideologyDescription` — the colony's belief system. Applied at game start to the player faction's primary `Ideo` (requires Ideology DLC).
- `recentEvents` — what shook the world before the colony began.
- `techLevel` — tribal / medieval / industrial / spacer / mixed.
- `themes` — 3–6 motifs the storyteller honors.
- `incitingIncident` — what kicks the story off.
- `currentAct` — narrative phase label.
- `activeForces` — 3–5 factions/powers, each with 1–2 sentence description.
- `pendingThreat`, `opportunity` — story tension levers.
- `plantedSeeds` — narrative hooks the storyteller can pay off later.

### Party (also on the campaign frame — the "how did you all meet" questions)
- `colonyOrigin` — why these specific colonists ended up here.
- `howTheyMet` — circumstances of the party forming.
- `sharedGoal` — what binds them.
- `internalTension` — what divides them despite the alliance.

### Each pawn (kept tight by design)
- `firstName` / `nickName` / `lastName` / `gender` / `age` / `appearance`.
- `childhoodBackstory` / `adulthoodBackstory` — 1–2 paragraph prose.
- `definingMoment` — one pivotal event.
- `narrative` — one-line tagline.
- `traits` — 2–3 vanilla `TraitDef` defNames.
- `skills` — `SkillDef` → level (0–20, budget ~50 per pawn).
- `passions` — `SkillDef` → Minor/Major.

### Starting inventory
- `items[]` — `defName` + `count` (+ optional `stuff`). 8–15 entries.
- `biomeHint`, `seasonHint`, `locationHint` — recommendations shown to the player; not auto-forced on the starting-site picker.

## Architecture

### XML
- `Defs/ScenarioDefs/RimMind.xml` — the `RimMind_AIScenario` ScenarioDef. Inherits `ScenarioBase` so `surfaceLayer` and `playerFaction` come for free. Wires the standard `ScenPart_ConfigPage_ConfigureStartingPawns`, `ScenPart_PlayerPawnsArriveMethod` (DropPods), and `ScenPart_GameStartDialog`, plus our custom `RimMind_CampaignSetup` part.
- `Defs/ScenPartDefs/RimMind.xml` — declares the `RimMind_CampaignSetup` ScenPartDef pointing at `RimMind.Scenarios.ScenPart_RimMindCampaign`.

### C#
- `Source/RimMind/Scenarios/RimMindScenarioPlan.cs` — DTO: `CampaignFrame`, `List<PawnSpec>`, `List<ItemSpec>`, biome/season/location hints. All `IExposable`.
- `Source/RimMind/Scenarios/ScenarioPlanGenerator.cs` — builds the prompt, calls the configured LLM (`ClaudeCodeClient` / `AnthropicClient` / `CustomProviderClient` / `OpenRouterClient`), parses the JSON. Sets `enable_thinking=false` so reasoning models like Qwen3.5 and DeepSeek-R1 don't burn the token budget on internal thinking.
- `Source/RimMind/Scenarios/ScenPart_RimMindCampaign.cs` — the runtime ScenPart. Stores the `plan`. Overrides:
  - `DoEditInterface` — minimal in-place edit on the customize page (most editing happens in the dialog).
  - `Notify_NewPawnGenerating(Pawn, PawnGenerationContext.PlayerStarter)` — applies pawn specs (name, traits, skill levels, passions) to each player starter as it's rolled. Trait names are fuzzy-matched (defName or label, case-insensitive).
  - `PlayerStartingThings()` — yields `Thing`s for the AI's item list. Stuff is resolved via `DefDatabase<ThingDef>.GetNamedSilentFail`, falling back to `GenStuff.DefaultStuffFor` when the AI's stuff suggestion isn't a valid material for the def.
  - `PostGameStart()` — calls `ApplyIdeologyOverride()` which writes `IdeologyName` / `IdeologyDescription` into the player faction's primary `Ideo` via `Traverse.Create(playerIdeo).Field(...)`. No-op when Ideology DLC isn't active.
- `Source/RimMind/Scenarios/Dialog_RimMindCampaignPrompt.cs` — the modal popup that hosts the prompt input + review-with-edits. Two stages: prompt (single text-area + Generate) or review (every prose field as a `TextArea`, every short field also as a `TextArea`). Uses a lock-protected `pendingPlan` slot to receive the AI callback off the HTTP completion thread, then drains in `DoWindowContents`. Also drains `MainThreadDispatcher.Drain()` each frame — see the dispatcher note below.
- `Source/RimMind/Scenarios/Page_SelectScenario_DoNext_Patch.cs` — Harmony prefix on `Page.DoNext` (the base method — `Page_SelectScenario` doesn't declare its own). When the selected scenario contains a `ScenPart_RimMindCampaign` and the plan is empty, opens the dialog and returns false to abort the page transition. After the dialog commits, the next click on Next passes through normally because the plan now has content. Patching `Page.DoNext` (rather than `BeginScenarioConfiguration`, which is what actually assigns `this.next`) is critical: blocking `BeginScenarioConfiguration` leaves `this.next` null, and the subsequent `DoNext` call transitions to nothing and kicks the player back to the main menu.

### Translation keys
`Languages/<lang>/Keyed/RimMind.xml` carries `RimMind_Scenario_*` keys for every UI string the dialog uses. All 14 supported languages have the keys, English-fallback initially.

## Threading model

API responses come back on the HTTP completion thread. RimWorld is single-threaded; mutating game state from the HTTP thread will crash or corrupt.

`MainThreadDispatcher` is a `GameComponent` with a static queue. Its `GameComponentUpdate` drains the queue every frame — *but only when `Current.Game` exists*. Pre-game (on `Page_SelectScenario`), there is no Game and the queue never drains.

Fix: `MainThreadDispatcher.Drain()` is now a public static method, called both from `GameComponentUpdate` and from `Dialog_RimMindCampaignPrompt.DoWindowContents` (which runs every frame on the main thread regardless of game state).

If you add another pre-game UI that consumes API responses, call `MainThreadDispatcher.Drain()` from its `DoWindowContents` too.

## Persistence

The plan is saved in two places:

1. **On the ScenPart**: `ScenPart_RimMindCampaign.ExposeData()` calls `Scribe_Deep.Look(ref plan, "plan")`. The scenario is part of the save file, so the plan persists with the save.
2. **Mirrored to the storyteller**: `NarrativeEngine.FinalizeInit()` scans `Find.Scenario.AllParts` for our ScenPart and copies `plan.Campaign` into `state.Campaign`. The NarrativeEngine's `ExposeData` then deeply scribes the state, so the campaign frame survives even if scenario parts are ever reset.

Every campaign frame field is included in `CampaignFrame.BuildPromptContext()`, which is what the `DMPlanner` sends to the LLM when planning the next 3 story beats. That means the AI storyteller sees the world lore, ideology, themes, party origin, internal tension, etc. on every planning call — the lore is not decorative.

## Configuration the AI uses

Inherits the player's mod settings:
- **Provider**: `claudecode` / `anthropic` / `openrouter` / `custom`. The scenario request goes through the same client as chat.
- **Model**: `ActiveModelId` resolves per provider (`claudeCodeModelId` / `anthropicModelId` / `modelId` / `customModelId`).
- **Theme**: `selectedTheme` (default `chronicle`). `ChronicleThemeProvider.CampaignPrompt` is prepended to the scenario system prompt, so theme voice shapes the world generation.

The scenario request hardcodes:
- `temperature = 0.9` (creative).
- `max_tokens = 64000` (current API ceiling; will error on small-output models like Haiku or DeepSeek-R1 — switch model if that happens).
- `enable_thinking = false` (skips reasoning passes on Qwen3.5 / DeepSeek-R1; ignored by providers that don't support the field).

## Known limitations

- **Biome is a hint only.** The player still manually picks the starting tile on `Page_SelectStartingSite`. Auto-tile-pick that respects `biomeHint` is a future patch.
- **Pawn specs apply on initial generation only.** If the player clicks "Randomize all" on the configure-pawns page, our spec applies in slot order again — but per-pawn randomize on one slot may apply the wrong spec.
- **Backstory text is stored but not mapped to `BackstoryDef`.** Vanilla backstories still roll on the pawn; the AI's prose lives in the spec for storyteller context, not on the pawn record.
- **Ideology mutation is name + description only.** Memes, precepts, rituals, and structure stay whatever the player picked on `Page_ChooseIdeo`. A future patch could auto-pick memes that match the AI's themes.
- **Small-output models (Haiku 4.5, DeepSeek-R1, Gemini Flash) will reject `max_tokens=64000`.** If you hit a "max_tokens above limit" error, switch model.

## Adding a new field

1. Add the C# field to `CampaignFrame` or `PawnSpec`.
2. Add it to `ExposeData()` (`Scribe_Values.Look(ref ..., "<key>")`).
3. Add it to the JSON schema in `ScenarioPlanGenerator.BuildSystemPrompt()`.
4. Read it in `ScenarioPlanGenerator.Parse()`.
5. Render an editable row in `Dialog_RimMindCampaignPrompt.DrawReviewStage()` using `DrawEditable(...)`.
6. If the field should influence the storyteller's prompt, add it to `CampaignFrame.BuildPromptContext()`.

## Related code

- Storyteller theme system: `Source/RimMind/Storyteller/IThemeProvider.cs`, `ChronicleThemeProvider.cs`, `LotrThemeProvider.cs`, `ThemeRegistry.cs`.
- DM planning loop: `Source/RimMind/Storyteller/NarrativeEngine.cs`, `DMPlanner.cs`.
- Letter framing: `Source/RimMind/Storyteller/LetterFramingPatch.cs`, `LetterStackPatch.cs`, `PendingLetterFraming.cs`.

## Related GitHub issue

[#175 — AI Storyteller RFC](https://github.com/Bwarhness/RimMind/issues/175). The current implementation covers RFC Phases 0–4. Phase 5 (full Plan→Frame→Execute→Outcome→Replan loop) is partial — the planner runs, frames letters, and updates tension, but outcome detection is delegated to the Chronicle module.
