# Creating Custom Configuration Parsers

The Pandora Configuration System natively supports primitive types, strings, enums, common Unity types like `Vector3` and `Color`, as well as **Arrays (`T[]`)** out-of-the-box. Array configuration supports inline syntax `[1, 2, 3]` and standard YAML sequence lists.

However, you might want to use strong types like `System.Net.IPAddress` or `System.Uri` in your configuration files.

To avoid bloat, these are not built into the core library, but they are incredibly easy to add via parsers.

## The IConfigValueParser Interface

To support a custom type for reading/writing from disk (YAML or INI), you implement `IConfigValueParser`.

## The IConfigEditorParser Interface

To support visualizing and editing the custom type inside the Pandora Config Editor Window, you implement `IConfigEditorParser`. 

*Note: You must wrap editor-specific code inside `#if UNITY_EDITOR` blocks so it compiles in full game builds.*

## Full Example: IPAddress Parser

Here is a full example of a parser that handles `System.Net.IPAddress`:

```csharp
using System;
using System.Net;
using MizoreRainy.Pandora.ConfigUtility;

#if UNITY_EDITOR
using MizoreRainy.Pandora.ConfigUtility.Editor;
using UnityEditor;
using UnityEngine;
#endif

public class IPAddressConfigParser : IConfigValueParser
#if UNITY_EDITOR
    , IConfigEditorParser
#endif
{
    // Tell the system this parser handles IPAddress
    public bool CanParse(Type type) => type == typeof(IPAddress);

    // Convert string from Config to IPAddress object
    public object Parse(string value, Type type)
    {
        if (IPAddress.TryParse(value, out IPAddress ip))
            return ip;
        return IPAddress.Loopback;
    }

    // Convert IPAddress back to string to save in YAML
    public string ConvertToString(object value, Type type)
    {
        if (value is IPAddress ip) return ip.ToString();
        return string.Empty;
    }

#if UNITY_EDITOR
    // Tell the Editor GUI how to draw this type
    public object DrawEditorGui(GUIContent label, object currentValue)
    {
        IPAddress currentIp = currentValue as IPAddress ?? IPAddress.Loopback;
        string input = EditorGUILayout.TextField(label, currentIp.ToString());
        
        if (IPAddress.TryParse(input, out IPAddress newIp)) return newIp;
        return currentIp; // Retain current on invalid edit
    }
#endif
}
```

## Registering Your Custom Parser

Once your parser class exists, you must register it with the system **before** configuration objects are accessed.

You can do this using `ConfigLoader.RegisterParser()`:

```csharp
using UnityEditor;
using UnityEngine;
using MizoreRainy.Pandora.ConfigUtility;

public static class ConfigRegistrar
{
#if UNITY_EDITOR
    [InitializeOnLoadMethod]
#endif
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RegisterParsers()
    {
        ConfigLoader.RegisterParser(new IPAddressConfigParser());
    }
}
```

## Ready-to-Use Bonus Parsers

You can find complete copy-paste examples for `IPAddress` and `Uri` inside the Pandora package at:
`Samples~/Config System/Bonus Parsers/BonusParsers.cs`
