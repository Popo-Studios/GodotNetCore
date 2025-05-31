#if TOOLS
using Godot;
using System;

[Tool]
public partial class GodotNetCoreEditor : EditorPlugin
{
	public override void _EnterTree() {
		AddAutoloadSingleton("NetviewMaster", "res://addons/GodotNetCore/GodotNetCore/Netview/NetviewMaster.cs");
	}

	public override void _ExitTree()
	{
		RemoveAutoloadSingleton("NetviewMaster");
	}
}
#endif
