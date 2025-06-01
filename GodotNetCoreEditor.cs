#if TOOLS
using Godot;
using System;

[Tool]
public partial class GodotNetCoreEditor : EditorPlugin
{
	public override void _EnterTree() {
		AddAutoloadSingleton("NetviewManager", "res://addons/GodotNetCore/GodotNetCore/Netview/NetviewManager.cs");
	}

	public override void _ExitTree()
	{
		RemoveAutoloadSingleton("NetviewManager");
	}
}
#endif
