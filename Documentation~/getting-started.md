# Getting Started with Pandora Unity Utility Package

This guide will help you install, configure, and start using the Pandora Unity Utility Package in your Unity project. Follow this step-by-step guide to set up networking capabilities with the AetherLink system.

## Table of Contents

- [System Requirements](#system-requirements)
- [Installation](#installation)
- [First Setup](#first-setup)
- [Your First Network Connection](#your-first-network-connection)
- [Project Configuration](#project-configuration)
- [Verification and Testing](#verification-and-testing)
- [Next Steps](#next-steps)

---

## System Requirements

### Unity Version Compatibility
- **Minimum**: Unity 2021.3 LTS
- **Recommended**: Unity 2022.3 LTS or later
- **Build Utility Features**: Unity 6.0+ (for advanced editor tools)

### Platform Support
- **Windows** (Standalone, UWP)
- **macOS** (Standalone)
- **Linux** (Standalone)
- **iOS** (Mobile)
- **Android** (Mobile)

### Rendering Pipeline Compatibility
- Built-in Render Pipeline
- Universal Render Pipeline (URP)
- High Definition Render Pipeline (HDRP)

### Network Requirements
- **Local Area Network**: Required for device discovery
- **UDP/TCP Support**: Standard Unity networking capabilities
- **Firewall Access**: May require firewall configuration for LAN connections

---

## Installation

### Method 1: Git URL (Recommended)

1. **Open Package Manager**
    - In Unity, go to `Window` → `Package Manager`

2. **Add Package from Git**
    - Click the `+` button in the top-left corner
    - Select `Add package from git URL...`

3. **Enter Repository URL**
```
https://github.com/mizorerainy/pandora-utility.git#release/1.0.0
```

4. **Install Package**
    - Click `Add` and wait for the installation to complete
    - Unity will automatically resolve dependencies

### Method 2: Local Installation

1. **Download Package**
    - Clone or download the repository from GitHub
    - Extract to a local folder

2. **Add to Unity**
    - In Unity Package Manager, click `+` → `Add package from disk...`
    - Navigate to the downloaded folder
    - Select `package.json`

### Method 3: Manual Installation (Advanced)

1. **Clone Repository**
```bash
git clone https://github.com/mizorerainy/pandora-utility.git#release/1.0.0
```

2. **Add to Packages Folder**
   - Copy the package folder to `YourProject/Packages/`
   - Unity will automatically detect and import it

### Verify Installation

After installation, verify the package is properly installed:

1. **Check Package Manager**
   - Open `Window` → `Package Manager`
   - Switch to "In Project" view
   - Look for "Pandora Utility" in the list

2. **Check Assembly Definitions**
   - Ensure `PandoraUtility.Runtime.Network` appears in your assemblies
   - Verify no compilation errors in the Console

---

## First Setup

### Import Sample Projects

1. **Open Package Manager**
   - Find "Pandora Utility" in your installed packages

2. **Import Samples**
   - Expand the "Samples" section
   - Click "Import" next to "Basic Connection Sample"
   - Repeat for other samples you want to explore

3. **Locate Imported Samples**
   - Samples will be imported to `Assets/Samples/Pandora Utility/[version]/`

### Configure Project Settings

1. **Player Settings**
   - Go to `Edit` → `Project Settings` → `Player`
   - Under "Configuration", ensure "Internet Client" is enabled
   - For mobile platforms, verify network permissions are set

2. **Network Settings**
   - No additional Unity network settings required
   - AetherLink handles network configuration internally

---

## Your First Network Connection

### Quick Test Setup

Create two simple scripts to test basic connectivity:

1. **Create Master Device Script**
 ```csharp
 using UnityEngine;
 using PandoraUtility.Network.AetherLink;

 public class NetworkMaster : MonoBehaviour
 {
     void Start()
     {
         // Configure as Master
         var settings = Settings.Default;
         settings.LinkMode = Mode.Master;
         settings.Port = 7777;
         
         // Initialize and start
         AetherLink.Instance.Initialize(settings);
         AetherLink.Instance.StartLink();
         
         Debug.Log("Master started - waiting for connection...");
     }
 }
 ```

2. **Create Slave Device Script**
 ```csharp
 using UnityEngine;
 using PandoraUtility.Network.AetherLink;

 public class NetworkSlave : MonoBehaviour
 {
     void Start()
     {
         // Configure as Slave
         var settings = Settings.Default;
         settings.LinkMode = Mode.Slave;
         settings.Port = 7777;
         settings.TargetIP = "192.168.1.100"; // Master's IP
         
         // Initialize and start
         AetherLink.Instance.Initialize(settings);
         AetherLink.Instance.StartLink();
         
         Debug.Log("Slave started - attempting connection...");
     }
 }
 ```

### Test the Connection

1. **Setup Two Devices or Unity Instances**
   - Run Master script on one device/instance
   - Run Slave script on another device/instance
   - Ensure both are on the same network

2. **Monitor Connection**
   - Watch Unity Console for connection messages
   - Successful connection will show in both consoles

---

## Project Configuration

### Network Configuration

1. **Firewall Settings**
   - Ensure chosen port (default 7777) is open
   - Configure Windows Firewall or macOS firewall as needed
   - For router connections, configure port forwarding

2. **IP Address Configuration**
   - Find Master device IP using `ipconfig` (Windows) or `ifconfig` (Mac/Linux)
   - Update Slave configuration with Master's IP address

### Performance Configuration

1. **Frame Rate Settings**
   - Consider target frame rate for your application
   - Network updates typically don't require 60 FPS

2. **Quality Settings**
   - Network performance is independent of graphics quality
   - Adjust based on your application needs

### Development vs Production

1. **Development Setup**
   - Use localhost (127.0.0.1) for single-machine testing
   - Enable verbose logging for debugging

2. **Production Setup**
   - Configure actual network IPs
   - Disable debug logging for performance
   - Test on target hardware and network conditions

---

## Verification and Testing

### Connection Test Checklist

- [ ] Package installed without errors
- [ ] Sample scripts compile successfully
- [ ] Master device starts without errors
- [ ] Slave device connects to Master
- [ ] Console shows connection success messages
- [ ] No firewall blocking network traffic

### Common Connection Issues

**Master not starting**
- Check if port is already in use
- Verify firewall permissions
- Ensure valid network adapter

**Slave cannot connect**
- Verify Master IP address is correct
- Check network connectivity (ping test)
- Confirm both devices use same port number

**Compilation errors**
- Verify Unity version compatibility
- Check assembly definition references
- Reimport package if necessary

---

## Next Steps

### Learning Path

1. **Immediate Next Steps**
   - [Simple Connection Tutorial](tutorials/simple-connection.md) - Detailed connection setup
   - [Data Serialization Tutorial](tutorials/data-serialization.md) - Send custom data

2. **Intermediate Development**
   - [Advanced Scenarios](tutorials/advanced-scenarios.md) - Complex networking patterns
   - [API Reference](api/) - Complete technical documentation

3. **Production Readiness**
   - Error handling and recovery patterns
   - Performance optimization techniques
   - Platform-specific considerations

### Sample Project Exploration

1. **Basic Connection Sample**
   - Simple Master/Slave setup
   - Connection event handling
   - Basic data exchange

2. **Data Serialization Sample**
   - Built-in type serialization
   - Custom type implementation
   - Complex data structures

3. **Advanced Features Sample**
   - Async stream API usage
   - Managed event handlers
   - Error recovery patterns

### Key Documentation

- **[API Reference](api/)** - Complete class and method documentation
- **[Tutorials](tutorials/)** - Step-by-step learning guides
- **[Troubleshooting](tutorials/troubleshooting.md)** - Common issues and solutions

---

## Development Tips

### Best Practices

1. **Start Simple**
   - Begin with basic connection examples
   - Add complexity gradually
   - Test frequently during development

2. **Network Testing**
   - Test on actual network hardware
   - Verify different network conditions
   - Test connection recovery scenarios

3. **Code Organization**
   - Separate networking logic from game logic
   - Use events for loose coupling
   - Implement proper error handling

### Performance Considerations

1. **Data Optimization**
   - Minimize data sent over network
   - Use appropriate data types
   - Consider compression for large data

2. **Update Frequency**
   - Don't update every frame unless necessary
   - Batch multiple updates when possible
   - Use fixed update intervals for consistency

### Debugging Tools

1. **Unity Console**
   - Monitor AetherLink debug messages
   - Check for connection state changes
   - Watch for error messages

2. **Network Monitoring**
   - Use system network monitors
   - Check port usage with netstat
   - Monitor network traffic patterns

---

## Getting Help

If you encounter issues during setup:

1. **Documentation Resources**
   - [Troubleshooting Guide](tutorials/troubleshooting.md)
   - [FAQ Section](tutorials/troubleshooting.md#frequently-asked-questions)
   - [API Documentation](api/)

2. **Community Support**
   - [GitHub Issues](https://github.com/mizorerainy/pandora-unity-package/issues)
   - Search existing issues before creating new ones
   - Provide detailed information when reporting problems

3. **Information to Include**
   - Unity version
   - Package version
   - Platform and OS version
   - Network configuration details
   - Error messages and stack traces

---

## What's Next?

You're now ready to build networked Unity applications with Pandora! Here are your next steps:

1. **Practice Basic Connections** - Get comfortable with Master/Slave setup
2. **Explore Data Serialization** - Learn to send custom data between devices
3. **Study Sample Projects** - See real implementations in action
4. **Build Your First Project** - Apply what you've learned to your own ideas

Ready to dive deeper? Continue with the [Simple Connection Tutorial](tutorials/simple-connection.md) to build your first complete networked application.

---

*Welcome to the Pandora Unity Utility Package community. Happy networking!*