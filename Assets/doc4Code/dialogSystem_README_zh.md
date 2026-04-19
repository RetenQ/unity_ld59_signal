# dialogSystem 使用说明（中文）

本文档说明 `Assets/dialogSystem.cs` 的运行逻辑，以及其它代码如何调用它。

## 1. 目标与能力

`dialogSystem` 是一个全局单例对话框系统，支持：

- 对话框开关（显示/隐藏）
- 动态更新英文文本
- 按“单词”逐步显示（打字机效果）
- 场景切换后持续存在（`DontDestroyOnLoad`）
- 调试热键：`O / P / K`

---

## 2. 运行时初始化逻辑

### 2.1 自动挂载到管理对象

在场景加载后，`EnsureMountedOnSystemManager()` 会执行：

1. 若已有 `dialogSystem.Instance`，直接返回（避免重复）。
2. 若场景里已存在任意 `dialogSystem`，直接返回。
3. 否则在当前场景中递归查找 `SytemManager` 或 `SystemManager`。
4. 找到后自动挂 `dialogSystem`。
5. 若没找到，会新建一个 `SystemManager` 作为兜底并挂载。

### 2.2 单例与常驻

`Awake()` 中：

1. 若已存在其它实例，销毁自己（防重复）。
2. `Instance = this`。
3. `DontDestroyOnLoad(gameObject)`，跨场景保留。

---

## 3. UI 绑定与恢复逻辑

### 3.1 绑定优先级

`Awake()` / `OpenDialog()` 会执行以下顺序：

1. 尝试读取 Inspector 绑定的 `dialogRoot/dialogText`。
2. 若绑定失效，尝试从 prefab 恢复（`TrySetupFromPrefab()`）。
3. 若仍失败且 `allowRuntimeFallbackUI=true`，创建代码兜底 UI（黑色方框 + 文本）。
4. 若最终仍失败，输出错误日志。

### 3.2 prefab 恢复的重要细节

`dialogPrefab` 使用：`Assets/Scenes/Prefeb/DialogSystem.prefab`。

为了避免“重复脚本实例被单例销毁，连 UI 一起没了”的问题：

- 如果 prefab 根节点带 `dialogSystem` 组件，则只实例化其 `DialogCanvas` 子节点（纯 UI）。
- 否则可直接实例化整个 prefab。

---

## 4. 对话显示逻辑（打字机）

### 4.1 显示流程

`OpenDialog(text)`：

1. 校验并恢复绑定（必要时）。
2. 有新文本就更新 `currentFullText`。
3. `dialogRoot.SetActive(true)`。
4. `RestartTypewriter()` 开始逐词显示。

### 4.2 安全协程机制

为防止 UI 被销毁后协程仍写旧 `Text`（`MissingReferenceException`）：

- 协程启动时保存 `activeTypingTarget` 快照。
- 每步写入前都检查目标是否仍有效且仍是当前目标。
- 在 `CloseDialog()` / `ApplyHiddenState()` / 重建 UI 前，统一调用 `StopTypingSafely()` 停止协程。

---

## 5. 对外调用 API（给其它代码）

推荐使用静态 API：

```csharp
// 打开并显示文本（逐词）
dialogSystem.Open("Test It");

// 更新文本（若当前已打开，会重新开始逐词显示）
dialogSystem.SetText("Hello , this is the test !");

// 关闭对话框
dialogSystem.Close();
```

也可拿实例方法（不推荐作为主方式）：

```csharp
if (dialogSystem.Instance != null)
{
    dialogSystem.Instance.OpenDialog("Test It");
    dialogSystem.Instance.UpdateDialogText("Hello");
    dialogSystem.Instance.CloseDialog();
}
```

---

## 6. 调试按键（Play 模式）

- `O`：打开并显示 `Test It`
- `P`：关闭对话框
- `K`：更新为 `Hello , this is the test !`

已兼容：

- 旧输入系统（`Input.GetKeyDown`）
- 新 Input System（`Keyboard.current`）

---

## 7. 常见问题排查

### 7.1 按 `O` 没反应

先检查：

1. 是否在 Play 模式。
2. `Game` 视图是否有焦点。
3. `enableDebugKeys` 是否为 `true`。
4. Console 是否有 `[dialogSystem]` 开头错误日志。

### 7.2 Inspector 显示 UI Binding Missing

系统会尝试自动修复，但建议手工确认：

- `dialogPrefab` 指向 `Assets/Scenes/Prefeb/DialogSystem.prefab`
- 场景运行后 `SytemManager/SystemManager` 下存在 `DialogCanvas -> DialogPanel -> DialogText`

