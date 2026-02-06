# Character Switching Implementation Guide

## Current Architecture Analysis

### Key Components:
1. **`Player.cs`** - Represents a joined player (networked)
   - Currently has a single `_agentPrefab` field
   - Manages `ActiveAgent` reference
   - Handles weapon slot persistence

2. **`PlayerAgent.cs`** - The actual character controller
   - Handles movement, input, camera
   - Contains `Weapons`, `Health`, `KCC` components
   - Networked component

3. **`Gameplay.cs`** - Manages player spawning/despawning
   - Spawns agents using `player.AgentPrefab`
   - Handles respawning on death
   - Uses spawn points for positioning

4. **`ChangeCharacter.cs`** - Simple local script (not networked)
   - Currently just switches between transforms locally
   - Not integrated with the networked system

## Implementation Strategy

### Option 1: Multiple Agent Prefabs (Recommended)
**Best for:** Different character types with different abilities/stats/models

**Approach:**
- Create multiple `PlayerAgent` prefabs (e.g., `PlayerAgent_Fast.prefab`, `PlayerAgent_Tank.prefab`)
- Store selected character index in `Player` class (networked)
- Modify `Gameplay.SpawnPlayerAgent()` to use the selected prefab
- Add UI for character selection

**Pros:**
- Clean separation of character types
- Easy to balance different characters
- Can have different models, stats, abilities

**Cons:**
- Requires creating multiple prefabs
- More setup work

### Option 2: Single Prefab with Swappable Visuals
**Best for:** Same gameplay, different appearances

**Approach:**
- Keep single `PlayerAgent` prefab
- Add character model/visual selection to `PlayerBody`
- Store character visual index in `Player` class
- Swap visual representation on spawn/switch

**Pros:**
- Simpler implementation
- Less prefab management
- Good for cosmetic-only differences

**Cons:**
- Harder to have different stats/abilities
- All characters share same prefab settings

### Option 3: Hybrid Approach
**Best for:** Multiple character types with some shared components

**Approach:**
- Multiple prefabs for different character types
- Shared base components
- Character selection stored in `Player`
- Visual customization within each prefab

## Recommended Implementation (Option 1)

### Step 1: Modify `Player.cs`
Add networked character selection:

```csharp
[Networked]
public int SelectedCharacterIndex { get; private set; }

[SerializeField]
private PlayerAgent[] _agentPrefabs; // Array of character prefabs

public PlayerAgent GetAgentPrefab()
{
    if (_agentPrefabs == null || _agentPrefabs.Length == 0)
        return _agentPrefab; // Fallback to single prefab
    
    int index = Mathf.Clamp(SelectedCharacterIndex, 0, _agentPrefabs.Length - 1);
    return _agentPrefabs[index] != null ? _agentPrefabs[index] : _agentPrefab;
}

[Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
public void RPC_SelectCharacter(int characterIndex)
{
    if (characterIndex < 0 || characterIndex >= _agentPrefabs.Length)
        return;
    
    SelectedCharacterIndex = characterIndex;
    
    // If agent is already spawned, respawn with new character
    if (ActiveAgent != null && HasStateAuthority)
    {
        Context.Gameplay?.RequestCharacterSwitch(this);
    }
}
```

### Step 2: Modify `Gameplay.cs`
Update spawning to use selected character:

```csharp
protected void SpawnPlayerAgent(Player player)
{
    DespawnPlayerAgent(player);
    
    // Use GetAgentPrefab() instead of AgentPrefab
    var agentPrefab = player.GetAgentPrefab();
    var agent = SpawnAgent(player.Object.InputAuthority, agentPrefab) as PlayerAgent;
    player.AssignAgent(agent);
    
    agent.Health.FatalHitTaken += OnFatalHitTaken;
    OnPlayerAgentSpawned(agent);
}

// Add method for switching character mid-game
public void RequestCharacterSwitch(Player player)
{
    if (HasStateAuthority == false)
        return;
    
    // Store current position/rotation
    if (player.ActiveAgent != null)
    {
        var position = player.ActiveAgent.transform.position;
        var rotation = player.ActiveAgent.transform.rotation;
        
        DespawnPlayerAgent(player);
        
        // Spawn new character at same location
        var agentPrefab = player.GetAgentPrefab();
        var agent = SpawnAgent(player.Object.InputAuthority, agentPrefab) as PlayerAgent;
        agent.transform.SetPositionAndRotation(position, rotation);
        player.AssignAgent(agent);
        
        agent.Health.FatalHitTaken += OnFatalHitTaken;
        OnPlayerAgentSpawned(agent);
    }
}
```

### Step 3: Create Character Selection UI
Create a UI component similar to `UIWeapons`:

```csharp
// Scripts/UI/Widgets/UICharacterSelection.cs
namespace Projectiles.UI
{
    public class UICharacterSelection : UIBehaviour
    {
        [SerializeField]
        private UICharacterList _characterThumbnails;
        
        private Player _localPlayer;
        
        public void UpdateCharacters(Player player)
        {
            _localPlayer = player;
            // Update UI with available characters
            // Show selected character
        }
        
        public void OnCharacterSelected(int index)
        {
            if (_localPlayer != null)
            {
                _localPlayer.RPC_SelectCharacter(index);
            }
        }
    }
}
```

### Step 4: Input Handling
Add character switching input in `PlayerInput.cs`:

```csharp
// Add to GameplayInput struct
public byte CharacterSwitchButton; // 0 = no switch, 1-9 = character index

// In OnInput() method
if (keyboard.tabKey.wasPressedThisFrame)
{
    // Cycle through characters
    // Or open character selection menu
}
```

### Step 5: Character Selection Menu
- Create a menu scene/panel for character selection
- Show character thumbnails, stats, abilities
- Allow selection before joining game or during respawn
- Store selection in `Player` networked property

## Alternative: Simple In-Game Switching

If you want simpler switching without UI:

### Modify `PlayerInput.cs`:
```csharp
// Add character switch input
if (keyboard.leftBracketKey.wasPressedThisFrame)
{
    // Switch to previous character
    if (HasInputAuthority && _agent != null && _agent.Owner != null)
    {
        int currentIndex = _agent.Owner.SelectedCharacterIndex;
        int newIndex = (currentIndex - 1 + characterCount) % characterCount;
        _agent.Owner.RPC_SelectCharacter(newIndex);
    }
}

if (keyboard.rightBracketKey.wasPressedThisFrame)
{
    // Switch to next character
    if (HasInputAuthority && _agent != null && _agent.Owner != null)
    {
        int currentIndex = _agent.Owner.SelectedCharacterIndex;
        int newIndex = (currentIndex + 1) % characterCount;
        _agent.Owner.RPC_SelectCharacter(newIndex);
    }
}
```

## Network Considerations

1. **State Authority**: Only server can spawn/despawn network objects
2. **RPCs**: Use RPCs to request character changes from client to server
3. **Synchronization**: Character selection should be networked property
4. **Respawn Timing**: Consider when switching is allowed (e.g., only on death, or anytime)

## Testing Checklist

- [ ] Character selection persists across network
- [ ] Switching works for local player
- [ ] Switching works for remote players (visual update)
- [ ] Character stats/abilities are correct after switch
- [ ] Weapon persistence works correctly
- [ ] No network errors or desyncs
- [ ] Performance is acceptable

## Next Steps

1. Decide on character switching approach (Option 1, 2, or 3)
2. Create multiple character prefabs (if using Option 1)
3. Implement networked character selection in `Player.cs`
4. Update `Gameplay.cs` to use selected character
5. Create UI for character selection
6. Add input handling for switching
7. Test thoroughly in multiplayer
