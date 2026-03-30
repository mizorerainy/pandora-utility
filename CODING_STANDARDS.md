# Pandora Coding & Documentation Standards

This document outlines the standard conventions for documentation and code structure within the Pandora UPM package. Adhering to these standards ensures maintainability, readable tooling, and consistent APIs.

## 1. Documentation Practices

### Public and Protected APIs
All `public` and `protected` classes, methods, fields, and properties **must** include XML `/// <summary>` documentation. The grammar format should strictly follow these rules:

- **Classes & Structs**: Must begin with "Represents a..." or "Provides..." or "Defines..."
  - *Example:* `/// <summary>Represents a configuration entry for a specific field.</summary>`
- **Methods**: Must begin with an active third-person verb (e.g., "Gets", "Initializes", "Validates", "Loads").
  - *Example:* `/// <summary>Initializes the configuration system synchronously.</summary>`
- **Properties**: Must begin with "Gets..." or "Gets or sets...".
  - *Example:* `/// <summary>Gets a value indicating whether the system is initialized.</summary>`

### Private and Internal Members
Avoid XML bloat for purely `private` or `internal` helper functions, variables, and properties unless they contain complex logic that warrants thorough documentation. 
Instead, use standard inline double-slash `//` comments or single-line XML summaries where explanation adds clear value. 

## 2. Structural Conventions

Any C# class or struct exceeding **50 lines of code** must be structured using standard `#region` blocks. 
These regions ensure clean traversal and code-folding, which is especially important for complex Unity constructs like `MonoBehaviour` and `EditorWindow`.

The standard structure (in strict order from top to bottom) is:

1. `#region Fields & Properties`
   - Static fields/properties, then instance fields/properties.
2. `#region Events`
   - Any `event Action` or delegate declarations.
3. `#region Unity Lifecycle & Initialization`
   - `OnEnable`, `Awake`, `Start`, `Update`, `OnDestroy`, custom `Initialize()` or setup phase methods.
4. `#region Public API`
   - All publicly accessible methods and functional entry points.
5. `#region Internal & Interface Implementations`
   - Private helper methods, internal logic flow, native Unity editor callbacks, or explicit interface implementations.
6. `#region Nested Types`
   - Any private embedded enums, structs, or helper classes bound to the main class.

Missing or empty regions can be safely skipped. `partial` classes should adhere to only the regions that fit the scope of their specific file.
