# MX4GameHaptics - Waveform Reference

This document describes all 15 haptic waveforms available through the Logi Options+ SDK.

## Waveform Table

| Index | Name | Enum | Description | Best For |
|-------|------|------|-------------|----------|
| 0 | sharp_collision | `SharpCollision` | Sharp, sudden impact sensation | Collisions, hits, crashes |
| 1 | sharp_state_change | `SharpStateChange` | Sharp state transition | Mode switches, toggles |
| 2 | knock | `Knock` | Knocking sensation | Notifications, taps, gunshots |
| 3 | damp_collision | `DampCollision` | Dampened/soft impact | Soft landings, cushioned hits |
| 4 | mad | `Mad` | Aggressive vibration | Intense/angry moments |
| 5 | ringing | `Ringing` | Ringing sensation | Alerts, warnings, bells |
| 6 | subtle_collision | `SubtleCollision` | Subtle, light impact | Minor interactions, UI clicks |
| 7 | completed | `Completed` | Completion confirmation | Task completion, checkpoints |
| 8 | jingle | `Jingle` | Melodic pattern | Achievements, rewards |
| 9 | damp_state_change | `DampStateChange` | Soft state change | Ambient feedback, low intensity |
| 10 | firework | `Firework` | Firework burst | Explosions, celebrations |
| 11 | happy_alert | `HappyAlert` | Happy, positive alert | Rewards, pickups, positive events |
| 12 | wave | `Wave` | Wave-like continuous | Engine rumble, ambient vibration |
| 13 | angry_alert | `AngryAlert` | Aggressive alert | Damage, danger, negative events |
| 14 | square | `Square` | Square wave pattern | Rhythmic feedback |

## Motor Value Mapping

The default mapper translates XInput motor values (0-255 normalized) to waveforms:

| Condition | Waveform | Typical Game Event |
|-----------|----------|-------------------|
| L > 200 AND R > 200 | `SharpCollision` | Major impact, crash |
| L > 150 AND R > 150 | `Firework` | Explosion |
| L > 150 | `DampCollision` | Heavy left feedback |
| R > 150 | `Knock` | Sharp right feedback |
| L > 80 | `Wave` | Engine rumble |
| R > 80 | `SubtleCollision` | Light feedback |
| Any > 0 | `DampStateChange` | Minimal feedback |

## Game Event Recommendations

### Racing Games
- **Collision**: `SharpCollision` (0)
- **Engine rumble**: `Wave` (12)
- **Wall scrape**: `DampCollision` (3)
- **Boost**: `Firework` (10)

### Shooter Games
- **Gunshot**: `Knock` (2)
- **Explosion**: `Firework` (10)
- **Damage taken**: `AngryAlert` (13)
- **Reload**: `SubtleCollision` (6)

### Action/Adventure
- **Attack hit**: `SharpCollision` (0)
- **Block**: `DampCollision` (3)
- **Item pickup**: `Jingle` (8)
- **Quest complete**: `Completed` (7)

## Preset Sequences

Composite effects combine multiple waveforms:

### explosion
1. `SharpCollision` at 0ms
2. `Firework` at 40ms
3. `Wave` at 80ms

### gunshot
1. `Knock` at 0ms
2. `SubtleCollision` at 30ms

### damage
1. `SharpCollision` at 0ms
2. `AngryAlert` at 50ms

### heal
1. `HappyAlert` at 0ms
2. `Completed` at 100ms

### achievement
1. `HappyAlert` at 0ms
2. `Jingle` at 50ms
3. `Completed` at 150ms

## Testing Waveforms

Use the test CLI to try waveforms:

```bash
# By index
mx4test waveform 0

# By name
mx4test waveform sharp_collision

# Play all waveforms
mx4test test

# Play a sequence
mx4test sequence explosion
```
