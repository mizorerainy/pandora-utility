
# Changelog

All notable changes to the Pandora Unity Utility Package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

#### Configuration System
- **NEW**: Complete configuration management system with type-safe access
- **ConfigLoader**: Core configuration engine with synchronous and asynchronous initialization
- **ConfigEntry<T>**: Generic, type-safe configuration value containers
- **ConfigAttribute**: Declarative configuration metadata system
- **IConfigValueParser**: Interface for custom type conversion support
- **Automatic Discovery**: Reflection-based automatic detection of configuration settings
- **Live-Reloading**: Real-time configuration updates via FileSystemWatcher (Editor & Standalone)
- **Thread-Safe Operations**: Concurrent access protection with proper locking mechanisms
- **Editor Integration**: "Pandora → Open Config File" menu item for easy access

#### Configuration Features
- **Automatic Initialization**: Configurable via Scripting Define Symbols:
  - Default: Synchronous initialization before a scene is loaded
  - `CONFIG_LOADER_MANUAL_INIT`: Disable automatic initialization
  - `CONFIG_LOAD_ASYNC`: Enable asynchronous initialization for faster startup
- **Human-Readable Config Files**: Generated `config.txt` with comments and grouping
- **Error Handling**: Comprehensive error reporting and fallback to default values
- **Type Support**: Built-in support for primitives, strings, enums, and extensible custom types

#### AetherLink Network System
- Advanced networking utilities for Unity projects
- Master-Slave connection architecture
- Automatic network discovery on LAN
- Packet-based data transmission with headers
- Built-in serialization for common Unity types
- Network statistics and connection monitoring

#### Editor Tools
- Development tools to enhance Unity workflow
- Build utilities (Unity 6.0+ required)

### Technical Improvements
- **Async/Await Support**: Full .NET Task-based asynchronous operations
- **Memory Efficient**: Minimal allocations and optimized reflection usage
- **Cross-Platform**: Compatible with all Unity-supported platforms
- **Dependency-Free**: No external dependencies, uses built-in .NET libraries

### Documentation
- Comprehensive inline code documentation
- README.md updated with configuration system examples
- API reference documentation
- Usage examples and best practices

---

## Development Notes

### Configuration System Usage
