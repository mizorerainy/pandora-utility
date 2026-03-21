# Changelog

All notable changes to the Pandora Network Utility Package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Configuration System
- Complete configuration management system with type-safe access
- ConfigLoader with synchronous and asynchronous initialization
- ConfigEntry<T> generic containers for strongly-typed configuration values
- ConfigAttribute for declarative configuration metadata with descriptions
- IConfigValueParser interface for custom type conversion support
- Automatic discovery of configuration settings across static classes
- Live-reloading via FileSystemWatcher for real-time config updates
- Thread-safe operations with proper concurrent access protection
- Self-healing configuration files that auto-add missing settings
- Dual format support: INI (.txt) and YAML (.yaml) configurations
- Manual initialization by default with optional auto-initialization
- Platform-specific configuration file paths
- Reset functionality to restore all settings to defaults
- Value change event system for reactive programming

#### AetherLink Network System  
- UDP/TCP hybrid networking solution for Unity projects
- Master-Slave architecture with automatic role assignment
- Auto-discovery system for peer detection on local networks
- Robust connection management with automatic reconnection
- Header-based packet system for structured data transmission
- Zero-allocation unmanaged struct serialization (replaced legacy object array serialization)
- Network statistics monitoring and performance tracking
- Heartbeat system for connection reliability
- Same-machine fallback for local development
- Packet integrity validation with checksum verification
- Unity MonoBehaviour integration with proper lifecycle management
- Full UniTask async/await support for non-blocking operations
- Cross-platform compatibility for all Unity networking platforms

#### Build Utilities
- Automated build pipeline system
- Multi-platform build management
- Configurable build variants and settings
- Post-build action support for deployment workflows
- Unity 6.0+ enhanced integration

#### Editor Tools & Integration
- Pandora Settings window for centralized configuration
- Package Setup system with automated dependency management
- Scripting define symbol management:
  - `CONFIG_LOADER_AUTO_INIT` for initialization control
  - `USE_YAML_CONFIG` for YAML format support
  - `HAVE_VYAML` auto-detection for VYaml package
  - `HAVE_CYSHARP_UNITASK` auto-detection for UniTask package
- One-click installation for optional dependencies
- Force recompile functionality for scripting defines
- Live configuration preview in Unity Editor

### Technical Features
- Full async/await support with UniTask integration
- Memory-optimized operations with minimal allocations
- Cross-platform compatibility testing
- Smart dependency detection and management
- Comprehensive error handling with detailed logging
- Performance-optimized file I/O and parsing

### Requirements
- Unity 2021.3 or later
- .NET Framework 4.7.1 or .NET Standard 2.0
- Newtonsoft.Json 3.0.2 (included)
- UniTask 2.0.0+ (optional, auto-installable)
- VYaml 0.27.1+ (optional, auto-installable)

### Documentation
- Comprehensive README with feature overview
- Complete inline API documentation
- Quick start guides and usage examples
- Best practices and performance recommendations
- Full UPM-compliant documentation structure (TableOfContents.md)

---