# Pandora Unity Utility Package Documentation

Welcome to the comprehensive documentation for the Pandora Unity Utility Package! This package provides advanced networking solutions and development tools to enhance your Unity game development workflow.

## Table of Contents

- [Quick Start](#quick-start)
- [Core Features](#core-features)
- [Documentation Sections](#documentation-sections)
- [API Reference](#api-reference)
- [Examples & Tutorials](#examples--tutorials)
- [Support & Community](#support--community)

---

## Quick Start

New to Pandora? Start here:

1. **[Getting Started](getting-started.md)** - Install via Package Manager or Git URL
2. **[AetherLink Setup](tutorials/aetherlink-setup.md)** - Setting up the network components
3. **[Basic Networking Tutorial](tutorials/basic-networking.md)** - Send and receive unmanaged data structs

---

## Core Features

### AetherLink Network System
Advanced networking utilities designed for quick 1-to-1 connections in Local Area Network environments.

**Key Capabilities:**
- Simple LAN connection establishment (Master/Slave architecture)
- Automatic network discovery and connection
- Built-in and custom data serialization
- Async stream API with UniTask integration
- Managed event handlers with automatic cleanup
- Connection resilience and error recovery
- Performance optimizations for real-time applications

### Editor Tools
Development utilities to enhance your Unity workflow and productivity.

**Includes:**
- Build utilities (Unity 6.0+)
- Development helpers
- AetherLink inspector tools
- Network debugging utilities
- Packet Simulation Profiles (ScriptableObjects for Editor-time network testing)

---

## Documentation Sections

### Getting Started
- **[Getting Started](getting-started.md)** - Package installation and setup
- **[System Requirements](getting-started.md#system-requirements)** - Unity versions and platform compatibility
- **[First Steps](getting-started.md#first-steps)** - Basic configuration

### Tutorials
- **[AetherLink Setup](tutorials/aetherlink-setup.md)** - Basic master-slave manager setup
- **[Basic Networking](tutorials/basic-networking.md)** - Unmanaged struct serialization and zero-allocation
- **[Advanced Scenarios](examples/advanced-scenarios.md)** - Async streams, managed handlers, complex patterns

### API Documentation
- **[AetherLink API](api-reference.md)** - Core networking class reference and configuration

---

## API Reference

### Core Namespaces
- **`PandoraUtility.Network.AetherLink`** - Main networking functionality
- **`PandoraUtility.Network.Interfaces`** - Serialization and event interfaces
- **`PandoraUtility.Editor`** - Editor-only utilities

### Key Classes
- **AetherLink** - Primary networking interface and singleton
- **Settings** - Network configuration and connection parameters
- **PacketResponse** - Network packet handling

**[Complete API Reference](api-reference.md)**

---

## Examples & Tutorials

### Beginner Level
- [AetherLink Setup](tutorials/aetherlink-setup.md) - Setting up the Network Manager
- [Basic Networking](tutorials/basic-networking.md) - Connecting and exchanging unmanaged structs

### Intermediate Level
- [Custom Data Types](tutorials/basic-networking.md#custom-type-serialization) - Unmanaged Struct Serialization
- [Troubleshooting](tutorials/troubleshooting.md) - Connection and data analysis

### Advanced Level
- [Async Stream API](examples/advanced-scenarios.md#async-stream-api) - Reactive event handling with UniTask
- [Managed Handlers](examples/advanced-scenarios.md#managed-event-handlers) - Automatic cleanup and error handling
- [Performance Optimization](examples/advanced-scenarios.md#performance-optimization) - High-throughput scenarios
- [Error Recovery](examples/advanced-scenarios.md#error-recovery-and-resilience) - Building robust applications

---

## Sample Projects

Explore practical implementations in the **Samples~** directory:

- **Basic Connection Sample** - Simple master-slave setup
- **Data Serialization Sample** - Custom type serialization examples
- **Real-time Sync Sample** - Game state synchronization
- **Advanced Features Sample** - Async patterns and managed handlers
- **AetherLink Simulation Profile Sample** - Pre-configured simulation profile asset (`SampleLoginProfile.asset`)

Import samples via Package Manager: **Window > Package Manager > Pandora Utility > Samples**

---

## Development & Contributing

### For Contributors
- **[Contributing Guidelines](contributing.md)** - How to contribute to the project
- **[Development Setup](contributing.md#development-setup)** - Local development environment
- **[Code Standards](contributing.md#code-standards)** - Coding conventions and practices

### Architecture Overview
- **Master/Slave Connection Model** - Simple 1-to-1 LAN networking
- **Event-Driven Design** - UnityEvent and async stream patterns
- **Zero-Allocation Serialization** - Efficient unmanaged struct support
- **Unity Integration** - Inspector support and lifecycle management

---

## System Requirements

### Unity Compatibility
- **Unity Version**: 2021.3 LTS or later
- **Build Utility**: Unity 6.0+ (for advanced build features)
- **Platforms**: All Unity-supported platforms
- **Rendering**: Compatible with Built-in, URP, and HDRP

### Optional Dependencies
- **UniTask**: Required for async stream API features
- **Newtonsoft.Json**: Included (3.0.2) for internal serialization

### Network Requirements
- **Local Area Network**: Required for device discovery
- **UDP Communication**: Used for connection establishment
- **TCP Communication**: Used for reliable data transfer

---

## Support & Community

### Getting Help
- **[Troubleshooting Guide](tutorials/troubleshooting.md)** - Common issues and solutions
- **[FAQ](tutorials/troubleshooting.md#frequently-asked-questions)** - Frequently asked questions
- **GitHub Issues** - Report bugs and request features

### Documentation Status
- **Installation & Setup** - Complete
- **Basic Usage** - Complete  
- **Data Serialization** - Complete
- **Advanced Features** - Complete
- **API Reference** - Complete
- **Sample Projects** - Complete

---

## Performance & Limitations

### Optimal Use Cases
- **Local Area Network**: Designed for LAN environments
- **1-to-1 Connections**: Master-slave architecture
- **Real-time Applications**: Games, interactive experiences
- **Rapid Prototyping**: Quick networking setup

### Current Limitations
- **Single Connection**: One master, one slave at a time
- **LAN Only**: No WAN/internet routing support
- **Platform Specific**: May have platform-specific networking constraints

---

## Version Information

- **Current Version**: 1.0.0
- **Unity Compatibility**: 2021.3+
- **Last Updated**: 2025
- **License**: See LICENSE file

---

## Additional Resources

- **[Changelog](../CHANGELOG.md)** - Version history and updates
- **[License](../LICENSE)** - Usage terms and conditions
- **[GitHub Repository](https://github.com/your-username/pandora-unity-package)** - Source code and releases

---

## What's Next?

1. **New to networking?** Start with the [Getting Started Guide](getting-started.md)
2. **Ready to connect?** Try the [AetherLink Setup Tutorial](tutorials/aetherlink-setup.md)
3. **Need custom data?** Learn [Basic Networking](tutorials/basic-networking.md)
4. **Building something complex?** Explore [Advanced Scenarios](examples/advanced-scenarios.md)
5. **Issues?** Check out the [Troubleshooting](tutorials/troubleshooting.md)

---

*Documentation for Pandora Unity Utility Package - Making Unity networking simple and powerful*