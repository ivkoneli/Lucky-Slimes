# Gerty for Unity

Gerty connects Claude Code to your Unity Editor, letting you control Unity with natural language. Search your scenes, create objects, modify properties, and write scripts - all by chatting with Claude.

## Quick Start

1. **Open your Unity project** - Gerty starts automatically and configures everything
2. **Open a terminal** in your Unity project's root folder (the folder containing `Assets/`)
3. **Run `claude`** - When prompted, allow the "gerty" MCP server
4. **Start building!**

That's it. Claude now has access to your Unity Editor.

## What Can You Do?

Try asking Claude things like:

**Explore your project:**
- "What's in my scene?"
- "Find all the enemy prefabs"
- "Show me materials that use transparency"

**Create and modify objects:**
- "Create a red cube at position 0, 2, 0"
- "Add a Rigidbody to the Player"
- "Make all the lights twice as bright"
- "Parent these objects under a new empty called 'Environment'"

**Write scripts:**
- "Write a PlayerController script with WASD movement and jumping"
- "Create a health system with a HealthBar UI"
- "Add a script that makes this object rotate slowly"

**Scene setup:**
- "Set up basic lighting for an outdoor scene"
- "Create spawn points in a grid pattern"
- "Organize the hierarchy - group all the environment objects"

## How It Works

Gerty runs an MCP (Model Context Protocol) server inside the Unity Editor. When you run Claude Code in your project folder, it automatically discovers Gerty and gains access to:

- **unity_find** - Semantic search across your scenes and assets
- **unity_execute** - Run C# code directly in the Editor
- **unity_compile_permanent_script** - Compile scripts you've written
- **unity_check_compile_status** - Check if Unity is done compiling

Claude also receives skill instructions that teach it Unity best practices, so it knows when to write permanent scripts vs. execute one-off commands.

## Requirements

- Unity 2023.1 or newer
- [Claude Code CLI](https://docs.anthropic.com/en/docs/claude-code)

## Configuration

Open **Window > Gerty** to view the status panel:

- **Server Status** - Check if Gerty is running
- **Claude Code Integration** - Verify config files are set up
- **Search Index** - See how many objects are indexed

Most users won't need to change anything - the defaults work well.

---

## Advanced

### Manual Claude Configuration

Gerty automatically creates these files in your project root:

- `.mcp.json` - MCP server connection config with auth token
- `.claude/skills/gerty/SKILL.md` - Instructions for Claude
- `.claude/agents/gerty-unity.md` - Sub-agent definition

If you need to regenerate these, click **"Setup Claude Config"** in the Gerty window.

### Settings

In the Gerty window's **Settings** tab:

- **Port** - Default is 4689. Change if you have a conflict.
- **Auto-start on Editor Load** - Enabled by default
- **Auto-setup Claude Config** - Automatically create config files
- **Execution Timeout** - How long code can run (default 30s)
- **Enable Sandboxing** - Restricts file system access in executed code

### AIHelper.cs

The `Editor/Helpers/AIHelper.cs` file contains helper functions that Claude uses. You can add your own functions here - Claude will see them and can use them in `unity_execute` calls.

### Troubleshooting

**Claude can't connect to the server**
- Check that Gerty is running (Window > Gerty shows "Running")
- Make sure you're running `claude` from the Unity project root folder
- Try clicking "Setup Claude Config" to regenerate the config files

**Server won't start**
- Check if port 4689 is already in use
- Try changing the port in Settings

**Scripts aren't compiling**
- The server briefly disconnects during Unity compilation - this is normal
- Claude should automatically poll `unity_check_compile_status` and wait

**Search results seem outdated**
- Click "Rebuild Index" in the Gerty window
- The index auto-updates but marks itself as "dirty" until the next search

## Support

Questions or issues? Email us at [team@gerty.dev](mailto:team@gerty.dev)
