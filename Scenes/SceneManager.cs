 using Godot;
using System;

public partial class SceneManager : Node
{
	public object[] SceneData;

	public void SwitchScene(PackedScene to, params object[] sceneData)
	{
		SceneData = sceneData;
		

	}


}
