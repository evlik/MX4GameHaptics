# MX4 Game Haptics

Transform your Logitech MX Master 4 mouse into a game haptic feedback device. Captures Xbox controller vibration from games and converts it to haptic feedback on your mouse via direct HID++ protocol.

## How It Works

```
┌──────────┐    ┌─────────────────────┐    ┌─────────────┐
│   Game   │───▶│  MX4HapticService   │───▶│ MX Master 4 │
│ (XInput) │    │  (ViGEm + HID++)    │    │  (Haptics)  │
└──────────┘    └─────────────────────┘    └─────────────┘
```

1. **ViGEm Virtual Controller** - Creates a virtual Xbox 360 controller that games recognize
2. **Vibration Capture** - Intercepts rumble/vibration commands from games
3. **HID++ Protocol** - Sends haptic feedback directly to MX Master 4 via Bluetooth

## Requirements

- Windows 10/11
- .NET 8.0 SDK
- [ViGEmBus Driver](https://github.com/nefarius/ViGEmBus/releases) - Virtual gamepad driver
- Logitech MX Master 4 mouse (connected via Bluetooth)

## Quick Start

```bash
# Install ViGEmBus driver first!

# Run the service
dotnet run --project tools/MX4HapticService

# (Optional) Configure settings
dotnet run --project tools/HapticConfigurator
```

## Projects

### MX4HapticService
Standalone system tray application.

```bash
dotnet run --project tools/MX4HapticService
```

**Tray Menu:**
- Start/Stop service
- Test ViGEm Latency
- Reload Config
- Open Configurator
- Exit

### HapticConfigurator
GUI for configuring haptic settings.

```bash
dotnet run --project tools/HapticConfigurator
```

**Settings:**
- Waveform selection (wave, knock, collision, etc.)
- Intensity scaling (0.5x - 2.0x)
- Min/Max haptic levels
- Fixed or variable pulse interval (1-500ms)

## Configuration

Settings stored in: `%APPDATA%\MX4GameHaptics\haptic-config.json`

### Simple Mode (Recommended)
- Single waveform + direct intensity control
- Variable or fixed pulse interval
- Best for most games

### Advanced Mode (PDM)
- Multiple waveform zones based on intensity
- Per-zone interval curves
- Fine-tuned haptic response

## Available Waveforms

| Name | Best For |
|------|----------|
| wave | Engine rumble, continuous vibration |
| knock | Gunshots, impacts |
| sharp_collision | Hard hits |
| damp_collision | Soft impacts |
| subtle_collision | Light feedback |
| firework | Explosions |

## Building

```bash
# Build service
dotnet build tools/MX4HapticService

# Build configurator
dotnet build tools/HapticConfigurator
```

## Troubleshooting

**No haptic feedback**
- Ensure MX Master 4 is connected via Bluetooth (not USB receiver)
- Check ViGEmBus driver is installed
- Click "Connect Mouse" in configurator to test connection

**High latency**
- Use "Test ViGEm Latency" in tray menu
- Normal: < 5ms
- If > 50ms: check other virtual controller software

**Game doesn't see controller**
- Ensure MX4HapticService is running
- Check Windows sees "Xbox 360 Controller" in devices

**"Get an app to open this ms-gamebar link" popup**
- Windows tries to open Xbox Game Bar when a virtual Xbox controller is detected
- **Solution**: Install Xbox Game Bar from Microsoft Store
- Alternative: Apply registry fix from `tools/DisableGameBar.reg` (run as Administrator)

## License

MIT
