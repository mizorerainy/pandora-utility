# Pandora Unity Utility Package

A comprehensive Unity utility package providing networking solutions, configuration management, and other helpful tools for Unity game development.

## Features

- **AetherLink Network System**: Advanced networking utilities for Unity projects for quick 1–1 connection in Local Area Network.
- **Configuration System**: Type-safe, file-based configuration management with live-reloading
- **Editor Tools**: Development tools to enhance your Unity workflow

## Requirements

- Unity 2021.3 or later (6.0+ for **Build Utility**)
- Compatible with Unity Package Manager

## Installation

### Via Git URL (Recommended)

1. Open Unity and go to **Window → Package Manager**
2. Click the **+** button in the top-left corner
3. Select **Add package from git URL...**
4. Enter the following URL:
   ```
https://github.com/mizorerainy/pandora-unity-package.git#release/1.0.0
   ```
5. Click **Add**

### Via Package Manager (Local)

1. Clone or download this repository
2. Open Unity and go to **Window → Package Manager**
3. Click the **+** button and select **Add package from disk...**
4. Navigate to the package folder and select `package.json`

## Package Structure
```

├── Runtime/
│   ├── Network/
│   │   └── AetherLink/          # Advanced networking system
│   └── Config/                  # Configuration management system
├── Editor/                      # Editor-only utilities
├── Documentation~/              # Additional documentation (if available)
├── package.json                 # Package manifest
├── CHANGELOG.md                 # Version history
└── LICENSE                      # License information
```
## Configuration System

The Configuration System provides a robust, type-safe way to manage application settings with live-reloading capabilities.

### Getting Started

#### 1. Enable the Configuration System

The Configuration System is **disabled by default** to avoid conflicts with projects that don't need it.

**To enable it:**
1. Go to **Window → Pandora → Settings**
2. Check **"Enable Auto-Initialization"**
3. This will automatically add the `CONFIG_LOADER_AUTO_INIT` scripting define symbol

#### 2. Define Configuration Settings
```
csharp
public static class GameSettings
{
[Config("player_name", "Anonymous", "The player's display name")]
public static readonly ConfigEntry<string> PlayerName;

    [Config("max_fps", 60, "Maximum frame rate limit")]
    public static readonly ConfigEntry<int> MaxFPS;
    
    [Config("enable_debug", false, "Enable debug mode")]
    public static readonly ConfigEntry<bool> DebugMode;
}
```
#### 3. Access Configuration Values
```
csharp
void Start()
{
string playerName = GameSettings.PlayerName.Value;
int maxFps = GameSettings.MaxFPS.Value;
bool debugEnabled = GameSettings.DebugMode.Value;
}
```
#### 4. Configuration File

Once enabled, the system automatically creates a `config.txt` file in your project root with your settings.

### Manual Initialization (Advanced)

If you prefer full control over when the configuration system initializes:

1. Keep **"Enable Auto-Initialization"** unchecked in Pandora Settings
2. Call the initialization manually in your code:
```
csharp
// Async initialization (recommended)
await ConfigLoader.InitializeAsync();

// Or synchronous initialization
ConfigLoader.Initialize();
```
### Advanced Features

- **Live-Reloading**: Changes to `config.txt` are automatically applied in Unity Editor and standalone builds
- **Type Safety**: Strong typing with compile-time checking
- **Custom Parsers**: Support for complex types via `IConfigValueParser`
- **Initialization Modes**:
  - **Manual** (Default): Call `ConfigLoader.InitializeAsync()` manually
  - **Auto-Sync**: Enable via Pandora Settings for automatic synchronous initialization
  - **Auto-Async**: Enable auto-init + add `CONFIG_LOAD_ASYNC` define for faster async startup

### Configuration File Format

The system generates a human-readable configuration file:
```

# Application Configuration File
# Last saved: 7/21/2025 2:30:45 PM

#==================================================
# :: GameSettings Settings
#==================================================
# The player's display name
player_name=Anonymous
# Maximum frame rate limit
max_fps=60
# Enable debug mode
enable_debug=False
```
### Editor Integration

- **Pandora Settings**: Configure the config system via **Window → Pandora → Settings**
- **Menu Integration**: Access config file via **Pandora → Open Config File**
- **Auto-Discovery**: Automatically finds and registers all configuration settings
- **Error Handling**: Clear error messages for unsupported types and duplicate keys

## Documentation

- Check the `CHANGELOG.md` for version history and updates
- Browse the source code for detailed API usage
- Each script includes inline documentation and comments

## API Reference

### AetherLink Network System

The AetherLink system provides advanced networking capabilities for Unity projects. Key elements include:

- Master-Slave connection architecture
- Automatic network discovery on LAN
- Packet-based data transmission with headers
- Built-in serialization for common Unity types
- Network statistics and connection monitoring

### Configuration System API

Key classes and interfaces:

- `ConfigLoader`: Core configuration management
- `ConfigEntry<T>`: Type-safe configuration values
- `ConfigAttribute`: Metadata for configuration settings
- `IConfigValueParser`: Custom type conversion interface

*Complete API documentation available in `Documentation~/api-reference.md`*

## Contributing

We welcome contributions! Please feel free to submit issues, feature requests, or pull requests.

### Development Setup

1. Fork this repository
2. Clone your fork locally
3. Create a new branch for your feature
4. Make your changes and test thoroughly
5. Submit a pull request

## Versioning

This package follows [Semantic Versioning](https://semver.org/). See `CHANGELOG.md` for version history.

## License

This project is licensed under the terms specified in the `LICENSE` file.

## Support

- **Documentation**: Complete documentation available in `Documentation~/` folder
- **API Reference**: Detailed API documentation with examples
- **Tutorials**: Step-by-step guides for common scenarios

## Compatibility

- **Unity Version**: 2021.3+
- **Platforms**: All platforms supported by Unity
- **Rendering**: Compatible with Built-in, URP, and HDRP
- **Network**: Local Area Network (LAN) focused, supports 1-to-1 connections

---

*Made with ❤️ for the Unity community*