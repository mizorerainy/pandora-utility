# Changelog

All notable changes to the Pandora Network Utility Package will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.3] - 2026-05-18

### Added

#### Configuration System
- Added `public static void SaveSync()` to `ConfigLoader` for synchronous saving outside of async contexts.
- Added `SetValueAndSave(T _newValue)` to `ConfigEntry<T>` allowing inline value assignments and immediate file saving.
- Added a safety warning to avoid calling `SaveSync` during the `Update()` loop to prevent file system I/O throttling.

## [1.2.2] - 2026-04-07

#### Build Utilities
- Added a `PandoraBuilder` class with a `public static bool BuildProfileByName` API enabling headless, programmatic build triggering without EditorWindow dependencies.

## [1.2.1] - 2026-04-05

### Changed

#### Configuration System
- Migrated `PandoraSettings` from a local `EditorPrefs` dependent methodology to a project-shared `ScriptableObject`. The settings are automatically generated at `Assets/Editor/Pandora/PandoraSettings.asset` to keep it external to the UPM package itself.
- Optimized settings loading by utilizing a fast `EditorPrefs` path cache combined with `AssetDatabase.FindAssets` fallback scanning, keeping initialization lighting speed while resisting assets being moved.

## [1.2.0] - 2026-03-30

### Optimized

#### Configuration System
- Optimized Config System performance by bypassing initialization and `FileSystemWatcher` thread creation entirely when zero configuration settings exist.
- Introduced `PANDORA_DISABLE_CONFIG_WATCHER` Script Define Symbol to easily disable background file monitoring for production release performance.
- Introduced `CONFIG_ALLOW_REFLECTION` to defensively block slow runtime Assembly parsing when missing `PandoraConfigCache.asset`.
- Expanded the Pandora Settings Window (UI) with dedicated toggles to safely control these optimizations.

## [1.1.0] - 2026-03-27

### Added

#### Build Utilities
- Integrated a polymorphic post-build architecture utilizing `[SerializeReference]` for flexible, OS-agnostic post-build automation logic natively within the Unity Editor.
- Introduced `ManagedPostBuildTask` base abstract class for powerful extension of the build pipeline logic.
- Implemented native `ReorderableList` in the Build Manager UI, fully supporting drag-and-drop task reordering.
- Re-architected the legacy hard-coded string-based folder copy feature into a dedicated polymorphic `CopyFilesTask`.
- Added an initial `Samples~` directory containing `SampleBuildLoggerTask`, demonstrating ASCII file tree reporting of build outputs in the internal console.
- Added generic property attributes (`[FileSelector]`, `[FolderSelector]`, `[PathSelector]`, and `[UrlButton]`) to completely replace hard-coded custom UI layout iteration logic.
- Transformed the `ZipAndUploadToGoogleDrive` sample script into a full built-in utility Action inside the package's internal `Tasks/` directory.
- Added automatic browser URL opening logic to the Google Drive uploader utilizing `EditorApplication.delayCall`.

### Fixed

#### Editor UI
- Fixed an issue where the Managed Profile UI would aggressively cycle through profiles if multiple managed definitions mapped to an identical underlying Unity Build Profile.
- Prevented unexpected profile foldout state resets upon Unity domain reloads by dynamically linking the foldout session states to persistent string identifiers.
- Addressed inflexible UI profile pinning by allowing developers to explicitly force shared duplicate configuration profiles to properly adopt the 'Active Profile' rendering slot.

## [1.0.0] - 2026-03-21

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
- Extracted global state into an isolated `ConfigRegistry` for improved testability and mockability
- Zero-reflection initialization using `UnityEditor.TypeCache` and a runtime `ConfigTypeCacheSO` scriptable object
- Editor-time validation for early feedback on unsupported configuration types
- Added native array support (`T[]`) for configuration values, supporting both inline brackets `[]` and YAML standard sequence lists `- item`
- Updated Config Editor UI to natively support visualizing, re-arranging, and editing arrays

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
- Packet Simulation Profiles with ScriptableObjects for Editor-time network testing

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