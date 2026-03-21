# RimMind Event Automation System

## Overview
The Event Automation System allows users to configure custom AI responses to game events. When specific events occur (raids, fires, mental breaks, etc.), RimMind automatically sends a user-defined prompt to the AI, which then responds using the existing 98+ tool suite.

## Architecture

### Core Components

1. **AutomationRule.cs** - Data structure for individual automation rules
   - `enabled` - Whether this rule is active
   - `customPrompt` - User's instruction text
   - `cooldownSeconds` - Minimum time between triggers

2. **EventAutomationManager.cs** - GameComponent that tracks cooldowns
   - Per-event-type cooldown tracking
   - Save/load persistent state
   - Singleton access via `Instance` property

3. **LetterAutomationPatch.cs** - Harmony Postfix on `LetterStack.ReceiveLetter`
   - Detects when letters arrive
   - Checks automation configuration
   - Enforces cooldowns
   - Sends prompts to ChatManager

4. **DefaultAutomationPrompts.cs** - Default prompt templates
   - Pre-written prompts for 30+ common events
   - Category organization for UI
   - Fallback generic prompts

5. **AutomationSettingsWindow.cs** - Configuration UI
   - Master enable/disable toggle
   - Per-event enable/disable
   - Custom prompt editor
   - Cooldown slider
   - Categorized event list

## User Workflow

1. **Enable Automation**
   - Open RimMind mod settings
   - Check "Enable Event Automation"
   - Click "Configure Automation Rules..."

2. **Configure Rules**
   - Select an event type (e.g., "RaidEnemy")
   - Check "Enabled"
   - Edit custom prompt (or use default)
   - Adjust cooldown (default 60s)
   - Save changes

3. **Automatic Operation**
   - When event occurs, RimMind checks if automation is enabled
   - If cooldown allows, sends custom prompt to AI
   - AI analyzes situation and executes tools
   - User sees notification: "RimMind automation: [Event]"

## Safety Mechanisms

1. **Opt-in by Default** - Master switch defaults to OFF
2. **Per-Event Control** - Users enable specific events, not all-or-nothing
3. **Cooldown System** - Prevents spam/loops (configurable per event)
4. **Thread Safety** - Uses MainThreadDispatcher for game state access
5. **Error Handling** - Try/catch wraps all automation logic (never crashes game)
6. **Chat Window Requirement** - Automation only works when chat is open

## Event Detection

### How It Works
Harmony Postfix patch on `LetterStack.ReceiveLetter`:
- RimWorld's Letter system handles all important game notifications
- Patch runs *after* letter is processed (safe, no interference)
- Detects event type from `Letter.def.defName`
- Auto-registers new event types with default prompts (disabled)

### Auto-Discovery
When a new letter type arrives that hasn't been seen before:
- Creates AutomationRule with default prompt
- Sets enabled = false (user must explicitly enable)
- Saves to settings for future configuration

## Example Automation Rules

### Raid Response
```
Event: RaidEnemy
Prompt: Draft all combat-capable colonists. Equip best available weapons 
(rifles to shooters, melee to brawlers). Position behind defensive structures. 
Close all exterior doors.
Cooldown: 60 seconds
```

### Fire Emergency
```
Event: FireStarted
Prompt: Assign 3 colonists to firefighting immediately. Forbid flammable items 
near fire area. Check for chemfuel storage nearby. Open vents if temperature is rising.
Cooldown: 30 seconds
```

### Mental Break
```
Event: MentalBreakExtreme
Prompt: Arrest the colonist immediately if violent. Move other colonists away 
from area. Lock doors if safe to do so.
Cooldown: 120 seconds
```

## Integration with Existing Systems

- **ChatManager** - Uses existing conversation system
- **ToolExecutor** - AI executes via existing 98+ tools
- **DebugLogger** - Logs all automation triggers/decisions
- **ProposalTracker** - AI actions still go through approval system
- **DirectivesTracker** - Colony directives apply to automation responses

## Performance Considerations

- **Minimal overhead** - Postfix patch only runs when letters arrive (~10/hour)
- **No polling** - Event-driven, not periodic checks
- **Background threading** - AI calls don't freeze game
- **Cooldowns** - Prevent excessive API usage

## Known Limitations

1. **Chat Window Must Be Open** - Automation requires ChatManager instance
   - Future: Could queue messages for next window open
2. **Letter Events Only** - Doesn't catch gradual issues (food slowly depleting)
   - Future: Add periodic state monitoring for proactive automation
3. **No Undo System** - AI actions are final (user must manually reverse)
   - Mitigation: ProposalTracker forbids AI blueprints by default

## Future Enhancements

- Alert-based automation (proactive monitoring)
- Incident-based hooks (more precise event detection)
- Context-aware cooldowns (shorter for critical events)
- Automation history viewer (review what AI did)
- Import/export automation profiles
- Community prompt library

## Technical Notes

### Thread Safety
All automation logic respects RimWorld's single-threaded architecture:
```csharp
MainThreadDispatcher.Enqueue(() => {
    // Game state access happens here (main thread)
    chatManager.SendMessage(prompt);
});
```

### Save Compatibility
All automation state is stored in:
- **RimMindSettings** - Global rules (per-player)
- **EventAutomationManager** - Cooldown state (per-save)

Both use proper `ExposeData()` serialization.

### Harmony Compatibility
- Postfix-only patch (safest approach)
- No game logic modification
- Wrapped in try/catch (never throws)
- Unique Harmony ID prevents conflicts

## Debugging

Enable debug logging:
- Open `RimMind/Logs/debug.log`
- Search for `[Automation]` entries
- See event detections, rule evaluations, cooldown checks

Example log output:
```
[Automation] Letter received: RaidEnemy - Pirate Raid
[Automation] Triggering automation for event: RaidEnemy
[Automation] Prompt: [EVENT AUTOMATION TRIGGERED]...
[Automation] Event RaidEnemy on cooldown (waiting 60s between triggers)
```

## Credits

Based on research document: `/home/node/.openclaw/workspace-rimmind/event-automation-research.md`
Implements Issue #72: https://github.com/Bwarhness/RimMind/issues/72
