# Pandora Utility

A comprehensive Unity development toolkit that provides essential utilities for configuration management, build automation, and networking capabilities.

## Features Overview

### 🔧 Configuration System
- **Dual Format Support**: Both INI (.txt) and YAML (.yaml) configuration files
- **Automatic Discovery**: Finds and registers configuration settings across your project
- **Live Reloading**: Real-time config updates in Editor and builds
- **Type Safety**: Strongly-typed configuration entries with compile-time checking
- **Custom Parsers**: Support for complex types through extensible parsing system

### 🏗️ Build Utility
- **Automated Build Pipeline**: Streamlined build process with customizable settings
- **Platform Management**: Easy switching and building for multiple platforms
- **Build Configurations**: Manage different build variants and settings
- **Post-Build Actions**: Automated post-processing and deployment tasks

### 🌐 Network Features (AetherLink)
- **Cross-Platform Networking**: UDP/TCP hybrid networking solution
- **Master-Slave Architecture**: Automatic role assignment and management
- **Real-time Communication**: Low-latency data transmission
- **Auto-Discovery**: Automatic peer discovery on local networks
- **Connection Management**: Robust connection handling with reconnection support
- **Serialization System**: Built-in object serialization for network transmission

## Quick Start

### Configuration System
```csharp
// Define settings
public static class GameSettings // Needed to be static class
{
    [Config("PlayerName", "Anonymous", "The player's display name")]
    public static readonly ConfigEntry<string> PlayerName;// Needed to be static field
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
var settings = AetherLink.Settings.Default;
AetherLink.Instance.Initialize(settings);
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


## Package Structure

### Core Components
- **ConfigLoader**: Configuration management system
- **AetherLink**: Network communication framework
- **Build Utilities**: Automated build and deployment tools
- **Editor Tools**: Unity Editor integration and windows

### Assembly Definitions
- `MizoreRainy.Pandora.Runtime.*`: Runtime components
- `MizoreRainy.Pandora.Editor.*`: Editor-only tools and utilities
- `MizoreRainy.Pandora.*.AetherLink`: Network-specific modules

## Configuration

### Settings Window
Access package settings via **Window → Pandora → Settings**:
- **Config Loader**: Enable/disable auto-initialization
- **Format Options**: Switch between INI and YAML formats
- **Dependencies**: Install required packages (VYaml, UniTask)

### Build Setup
Configure build settings via **Window → Pandora → Build Tools**:
- **Target Platforms**: Select build targets
- **Build Configurations**: Manage build variants
- **Output Settings**: Configure build output paths

### Network Configuration
Set up AetherLink via **Window → Pandora → Network**:
- **Connection Settings**: Configure network parameters
- **Discovery Options**: Set up peer discovery
- **Protocol Settings**: UDP/TCP configuration options

## Requirements

### Core Requirements
- Unity 2021.3 or later
- .NET Framework 4.7.1 or .NET Standard 2.0

### Optional Dependencies
- **UniTask**: Required for async operations (auto-installable)
- **VYaml**: Required for YAML configuration support (auto-installable)

### Supported Platforms
- **Editor**: Full feature set available
- **Standalone**: Windows, macOS, Linux
- **Mobile**: iOS, Android (limited networking features)
- **Console**: PlayStation, Xbox, Nintendo Switch

## Getting Started

1. **Import the Package**: Add Pandora Utility to your Unity project
2. **Run Package Setup**: Use **Window → Pandora → Package Setup** to install dependencies
3. **Configure Settings**: Access **Window → Pandora → Settings** to configure features
4. **Initialize Systems**: Call initialization methods for the features you need

## Documentation

Detailed documentation for each component:
- **Configuration System**: Complete guide to setting up and using the config system
- **AetherLink Networking**: Network programming guide and API reference
- **Build Utilities**: Build automation and deployment workflows
- **Editor Tools**: Extending and customizing the Unity Editor integration

## Support

- **Unity Forum**: Community support and discussions
- **GitHub Issues**: Bug reports and feature requests
- **Documentation Wiki**: Comprehensive guides and tutorials

## License

This package is part of the Pandora Utility framework.