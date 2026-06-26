# Gerty for Unity

You have access to a Unity Editor running the Gerty MCP server. This lets you **see** what's in Unity scenes and **execute** C# code directly in the Editor.

## IMPORTANT: When to Use unity_execute vs Writing Files

**Use `unity_execute` for:**
- Editor operations: creating GameObjects, modifying scenes, setting up hierarchies
- One-off tasks: moving objects, changing materials, adjusting lighting
- Queries: inspecting object properties, listing scene contents
- Testing: quick experiments that don't need to persist as code

**Use normal file writing (Write tool) for:**
- MonoBehaviour scripts (PlayerController, EnemyAI, etc.)
- ScriptableObjects
- Editor scripts and tools
- Any C# code that should be part of the shipped project

**Example - Creating a player:**
1. Write `Assets/Scripts/PlayerController.cs` using the **Write tool** (this is permanent project code)
2. Call `unity_compile_permanent_script` with path `Assets/Scripts/PlayerController.cs` - triggers import
3. Wait 2-3 seconds, then call `unity_check_compile_status` - poll until status is "ready"
4. Use `unity_execute` to create the Player GameObject and attach the component:
   ```csharp
   var player = AIHelper.CreatePrimitive(PrimitiveType.Capsule, "Player", new Vector3(0, 1, 0));
   player.AddComponent<PlayerController>();
   ```

**Never use `unity_execute` to write script files** - that's using a screwdriver as a hammer. Write files directly, compile them with `unity_compile_permanent_script`, poll `unity_check_compile_status`, then use `unity_execute` to wire things up in the scene.

## Tools

### unity_find
Search for GameObjects in the currently open scene(s) using natural language.

**Parameters:**
- `query` (required): What you're looking for (e.g., "main camera", "player", "red cube"), or `"*"` to list ALL objects
- `limit` (optional): Max results (default: 10, use higher values with `"*"`)

**Returns:** Matches with name, path, type, description, and similarity score.

**Examples:**
- `unity_find(query="main camera")` - Find objects matching "main camera"
- `unity_find(query="*", limit=50)` - List all objects in the scene (up to 50)

**Note:** This searches the scene hierarchy only. For project assets (prefabs, materials, textures), use your file system tools to browse the Assets folder, then `unity_asset_info` to get details.

### unity_asset_info
Get detailed information about Unity assets by path.

**Parameters:**
- `paths` (required): Array of asset paths relative to project root (e.g., `["Assets/Prefabs/Player.prefab", "Assets/Materials/Red.mat"]`)

**Returns:** Rich descriptions for each asset:
- Prefabs: components, hierarchy, materials, vertex count
- Materials: shader, colors, textures
- Textures: dimensions, format, type (normal map, etc.)
- Audio: duration, channels, sample rate
- And more...

**Workflow for finding assets:**
1. Use `Glob` to find files by pattern: `Assets/**/*Enemy*.prefab`
2. Use `unity_asset_info` to get detailed info on interesting matches
3. Use `unity_execute` to work with the assets in the scene

### unity_execute
Run temporary, one-off C# code in Unity Editor for immediate actions.

**Parameters:**
- `code` (required): C# code (becomes body of `AIExecute()` method) - NOT saved to disk
- `description` (optional): For logging

**Use for:** Creating GameObjects, modifying scenes, querying properties, one-off editor tasks.
**Do NOT use for:** Writing permanent script files (use Write tool + `unity_compile_permanent_script` instead).

**Pre-imported namespaces:**
```csharp
System, System.Collections, System.Collections.Generic, System.Linq,
System.Text, System.IO, UnityEngine, UnityEditor, UnityEditor.SceneManagement,
Gerty  // Contains AIHelper
```

**Output:** Use `Debug.Log()` - all console output is captured and returned.

### unity_compile_permanent_script
After writing permanent C# script file(s) using the Write tool, call this to trigger import and compilation.

**Parameters:**
- `script_paths` (required): Array of paths relative to project root (e.g., `["Assets/Scripts/PlayerController.cs"]` or `["Assets/Scripts/Player.cs", "Assets/Scripts/Enemy.cs"]`)

**Returns:** Confirmation that import was triggered. The server will briefly disconnect during compilation.

**Important:** This triggers a Unity domain reload which temporarily disconnects the MCP server. After calling this, you MUST poll `unity_check_compile_status` until it returns "ready" before proceeding.

### unity_check_compile_status
Check if Unity is currently compiling. Use this to poll after `unity_compile_permanent_script`.

**Parameters:** None

**Returns:** Status:
- `compiling` - Unity is still compiling, wait and poll again
- `updating` - Asset database is updating, wait and poll again
- `ready` - Compilation succeeded, you can now use `AddComponent<T>()`
- `error` - Compilation failed with errors listed. Fix the script and try again.

### unity_scene_view
Capture a PNG screenshot of the Unity Scene view. You can see the image in the response. By default the screenshot includes the editor gizmos the user is currently seeing (selection outlines, colliders, custom Handles, icons, etc.). Optionally moves the Scene view camera first so you can inspect any part of the scene. Use this to visually verify edits, confirm placement, or debug layout issues.

**Parameters (all optional):**
- `look_at`: GameObject name to frame. Easiest way to focus on something specific (e.g. `"Player"` or `"RedCube"`). Uses the object's renderer bounds.
- `pivot`: Manual world-space pivot `[x, y, z]` the camera orbits around. Ignored if `look_at` is set.
- `rotation_euler`: Manual camera euler rotation in degrees `[x, y, z]`. Ignored if `look_at` is set.
- `size`: Scene view zoom / distance from pivot (smaller = closer). Ignored if `look_at` is set.
- `restore_view`: If `true` (default), restore the Scene view to its previous pivot/rotation/size after the screenshot, so the user's camera isn't hijacked. Set `false` to leave the camera where you moved it. Ignored if no camera move is requested.
- `show_gizmos`: If `true` (default), include editor gizmos in the screenshot — matches what the user is currently seeing in the Scene view. Set `false` for a clean render with no gizmos, handles, or selection outlines.
- `width`: Screenshot width in pixels (default 1024, max 4096).
- `height`: Screenshot height in pixels (default 768, max 4096).

**Returns:** A text summary of the camera state plus a PNG image block you can see.

**Examples:**
- `unity_scene_view()` — screenshot of the current Scene view (with gizmos) without moving the camera.
- `unity_scene_view(look_at="Player")` — frame the Player GameObject, then screenshot.
- `unity_scene_view(pivot=[0,1,0], rotation_euler=[30,45,0], size=8)` — top-down-ish angle on the origin.
- `unity_scene_view(show_gizmos=False)` — clean render with no gizmos or handles.

**Tips:**
- The Scene view tab must be open in the Editor (it usually is).
- For token efficiency, stick to the 1024x768 default unless you need more detail.
- Combine with `unity_execute`: make a change, then `unity_scene_view` to verify it visually.

**Workflow for adding permanent scripts:**
1. Write script file(s) with Write tool
2. Call `unity_compile_permanent_script` with all paths - triggers import (server will disconnect briefly)
3. Wait 2-3 seconds
4. Call `unity_check_compile_status` - repeat every 2 seconds until status is "ready" or "error"
5. If "error", fix the issues and go back to step 2
6. If "ready", use `unity_execute` to attach components: `gameObject.AddComponent<YourScript>()`

## AIHelper Library

Quick reference for the `AIHelper` static class:

```csharp
// Find objects in scene
AIHelper.FindInScene<T>(name)         // Find component by GO name
AIHelper.FindGameObject(name)          // Find GO (partial match)
AIHelper.FindAllWithComponent<T>()     // All GOs with component
AIHelper.FindAllByTag(tag)             // By tag
AIHelper.FindAllByLayer(layer)         // By layer

// Assets
AIHelper.LoadAsset<T>(path)            // Load by path
AIHelper.FindAssets<T>(filter)         // Search assets

// Create
AIHelper.CreateEmpty(name, parent)
AIHelper.CreatePrimitive(type, name, position)
AIHelper.CreateMaterial(color, name)

// Prefabs
AIHelper.InstantiatePrefab(path, position)
AIHelper.InstantiatePrefab(path, position, rotation)

// Selection
AIHelper.GetSelectedObjects()
AIHelper.SetSelection(gameObjects)
AIHelper.SelectAndFrame(gameObjects)

// Logging
AIHelper.Log(message)
AIHelper.LogJson(object)
AIHelper.LogGameObject(go)

// Scene
AIHelper.MarkSceneDirty()
AIHelper.SaveAllScenes()
```

## Workflow Pattern

1. **Find first** - Use `unity_find` to search the scene, or `Glob` + `unity_asset_info` for assets
2. **Check nulls** - Always verify objects exist before using them
3. **Log output** - Use `Debug.Log()` to confirm what was done
4. **Small steps** - Multiple focused executions beat one giant script

## Example: Create a red cube

```csharp
var cube = AIHelper.CreatePrimitive(PrimitiveType.Cube, "RedCube", new Vector3(0, 1, 0));
var renderer = cube.GetComponent<Renderer>();
renderer.sharedMaterial = AIHelper.CreateMaterial(Color.red, "RedMaterial");
AIHelper.SelectAndFrame(cube);
Debug.Log($"Created {cube.name} at {cube.transform.position}");
```

## Example: Find and modify objects

```csharp
var lights = AIHelper.FindAllWithComponent<Light>();
foreach (var light in lights)
{
    light.intensity *= 1.5f;
    Debug.Log($"Increased {light.name} intensity to {light.intensity}");
}
AIHelper.MarkSceneDirty();
```

## Common Pitfalls

- **`Camera.main` can be null** - Use `GameObject.Find("Main Camera")` with null check instead
- **Scripts need compilation time** - After `unity_compile_permanent_script`, always poll `unity_check_compile_status` until "ready" before using `AddComponent<T>()`.
- **Shaders may not exist** - `Shader.Find("Sprites/Default")` can return null. Use `Shader.Find("Standard")` or check for null.
- **Server disconnects during compile** - This is normal. The MCP server restarts automatically after Unity finishes compiling. Just retry your request.

## Limitations

- No undo - use version control
- Runs on main thread - may briefly freeze editor for large operations
- Sandboxed - no process execution or file access outside project
- Best in Edit mode, not Play mode
