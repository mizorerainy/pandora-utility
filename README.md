# Pandora Unity Utility Package

A comprehensive Unity utility package providing networking solutions and other helpful tools for Unity game development.

## Features

- **AetherLink Network System**: Advanced networking utilities for Unity projects for quick 1–1 connection in Local Area Network.
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
   https://github.com/mizorerainy/pandora-unity-package.git
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
│   └── Network/
│       └── AetherLink/          # Advanced networking system
├── Editor/                      # Editor-only utilities
├── Documentation~/              # Additional documentation (if available)
├── package.json                 # Package manifest
├── CHANGELOG.md                 # Version history
└── LICENSE                      # License information
```
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

*Made with ❤️ for the Unity community*# pandora-utility
