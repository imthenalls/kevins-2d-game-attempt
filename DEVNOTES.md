# Dev Notes

## Input System: Legacy Input.* crash at runtime

**Error**
```
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class,
but you have switched active Input handling to Input System package in Player Settings.
```

**Root cause**  
Legacy `Input.GetKeyDown()` / `Input.GetButtonDown()` calls were placed *outside* the
`#if ENABLE_INPUT_SYSTEM` block so they always compiled into the build. At runtime, when
the new Input System package is active, Unity throws because the old `Input` API is disabled.

**Fix**  
Move all legacy `Input.*` fallbacks into an `#else` branch so they only execute when the
Input System package is **not** active:

```csharp
#if ENABLE_INPUT_SYSTEM
    if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        return true;
    return false;
#else
    return Input.GetKeyDown(KeyCode.E);
#endif
```

Apply this pattern to every input helper method that has a legacy fallback.

**Files affected**  
`Assets/Scripts/PlayerInteractionController.cs` — all four input helpers:
- `WasInteractPressedThisFrame`
- `WasCancelPressedThisFrame`
- `WasChoiceUpPressedThisFrame`
- `WasChoiceDownPressedThisFrame`
