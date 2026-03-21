# Pandora Utility

A comprehensive Unity development toolkit that provides essential utilities for configuration management, build automation, and networking capabilities.

## Installation

You can install this package via Unity Package Manager (UPM) using the Git URL:

```
https://github.com/mizorerainy/pandora-utility.git
```

Alternatively, you can specify a version tag (e.g., `#v1.0.0`) at the end of the URL.

## Getting Started

### Configuration System
```csharp
// Define settings
public static class GameSettings
{
    [Config("PlayerName", "Anonymous", "The player's display name")]
    public static readonly ConfigEntry<string> PlayerName;
}

// Initialize (required by default)
await ConfigLoader.InitializeAsync();

// Use settings
string name = GameSettings.PlayerName.Value;
GameSettings.PlayerName.Value = "NewName"; // Auto-saves
```

### AetherLink Networking
```csharp
// Initialize AetherLink
AetherLink.Instance.Initialize(AetherLink.Settings.Default);
AetherLink.Instance.StartLink();

// Send data
AetherLink.Instance.SendData(1001, "Hello", 42, true);

// Receive data
await foreach(var packet in AetherLink.Instance.OnDataReceived())
{
    var objects = packet.ReadObjects();
    // Handle received data
}
```

## Features Overview

- **🔧 Configuration System**: Dual Format Support (INI/YAML), Automatic Discovery, Live Reloading, Type Safety, Custom Parsers.
- **🏗️ Build Utility**: Automated Build Pipeline, Platform Management, Build Configurations, Post-Build Actions.
- **🌐 Network Features (AetherLink)**: Cross-Platform Networking (UDP/TCP), Master-Slave Architecture, Real-time Communication, Auto-Discovery, Serialization System.

## Requirements

- Unity 2021.3 or later
- .NET Framework 4.7.1 or .NET Standard 2.0
- **Optional Dependencies**: UniTask (for async), VYaml (for YAML configs).

## Documentation & Support

Detailed documentation is available in the Wiki. For community support and discussions, check the Unity Forum and GitHub Issues.

## License

This package is part of the Pandora Utility framework.