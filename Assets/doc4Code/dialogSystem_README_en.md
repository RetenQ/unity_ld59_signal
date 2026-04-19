# dialogSystem Guide (English)

This document explains how `Assets/dialogSystem.cs` works at runtime and how other scripts should call it.

## 1. Purpose and Features

`dialogSystem` is a global singleton dialog controller that supports:

- Open/close dialog UI
- Update dialog text at runtime (English text)
- Word-by-word typewriter effect
- Persistent lifetime across scene changes (`DontDestroyOnLoad`)
- Debug hotkeys: `O / P / K`

---

## 2. Runtime Initialization Flow

### 2.1 Auto-mount on manager object

After scene load, `EnsureMountedOnSystemManager()` runs:

1. If `dialogSystem.Instance` already exists, return.
2. If any `dialogSystem` already exists in the scene, return.
3. Otherwise recursively search for `SytemManager` or `SystemManager`.
4. If found, attach `dialogSystem` to it.
5. If not found, create a fallback `SystemManager` and attach `dialogSystem`.

### 2.2 Singleton and persistence

Inside `Awake()`:

1. If another instance already exists, destroy this one.
2. Set `Instance = this`.
3. Call `DontDestroyOnLoad(gameObject)` to keep it across scenes.

---

## 3. UI Binding and Recovery

### 3.1 Binding priority

`Awake()` / `OpenDialog()` use this order:

1. Try Inspector bindings (`dialogRoot/dialogText`).
2. If invalid, try recovering from prefab via `TrySetupFromPrefab()`.
3. If still invalid and `allowRuntimeFallbackUI=true`, create fallback UI in code.
4. If still invalid, log an error.

### 3.2 Important prefab recovery detail

`dialogPrefab` target: `Assets/Scenes/Prefeb/DialogSystem.prefab`.

To avoid “duplicate script instance gets destroyed and UI is lost”:

- If prefab root has `dialogSystem`, instantiate only its `DialogCanvas` child (UI only).
- Otherwise instantiate the full prefab.

---

## 4. Dialog Display Logic (Typewriter)

### 4.1 Display flow

`OpenDialog(text)`:

1. Validate/recover bindings when needed.
2. Update `currentFullText` if new text is provided.
3. Show dialog: `dialogRoot.SetActive(true)`.
4. Start word-by-word typewriter through `RestartTypewriter()`.

### 4.2 Safe coroutine mechanism

To prevent `MissingReferenceException` after UI is replaced/destroyed:

- Cache a `Text` snapshot (`activeTypingTarget`) when typing starts.
- Check target validity on each step before writing.
- Call `StopTypingSafely()` before close/hide/rebuild operations.

---

## 5. Public API (for other scripts)

Recommended static API:

```csharp
// Open and type text word-by-word
dialogSystem.Open("Test It");

// Update text (if dialog is already open, typing restarts)
dialogSystem.SetText("Hello , this is the test !");

// Close dialog
dialogSystem.Close();
```

Instance method usage (secondary option):

```csharp
if (dialogSystem.Instance != null)
{
    dialogSystem.Instance.OpenDialog("Test It");
    dialogSystem.Instance.UpdateDialogText("Hello");
    dialogSystem.Instance.CloseDialog();
}
```

---

## 6. Debug Keys (Play Mode)

- `O`: open and show `Test It`
- `P`: close dialog
- `K`: update text to `Hello , this is the test !`

Supported input backends:

- Legacy Input (`Input.GetKeyDown`)
- New Input System (`Keyboard.current`)

---

## 7. Troubleshooting

### 7.1 Pressing `O` does nothing

Check:

1. You are in Play mode.
2. `Game` view has focus.
3. `enableDebugKeys` is `true`.
4. Console contains any `[dialogSystem]` errors.

### 7.2 Inspector shows UI Binding Missing

The system can auto-recover, but verify:

- `dialogPrefab` points to `Assets/Scenes/Prefeb/DialogSystem.prefab`
- In Play mode, `SytemManager/SystemManager` contains `DialogCanvas -> DialogPanel -> DialogText`

