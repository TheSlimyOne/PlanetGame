using System.Collections.Generic;
using Godot;



public partial class ResponsiveFileDialog : FileDialog
{
	Callable Callback;
	public void OpenFileDialog(Callable callback)
	{
		Callback = callback;
		Visible = true;
	}

	public void OnFileSelected(string path)
	{
		Callback.Call(path);
		Visible = false;
		Callback = default;
	}
}
