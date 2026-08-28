using Godot;

namespace PlanetGame.Util.DebugUIComponents;

public interface IDebugContainer
{
	void AddContent(Control control, int order = 0);
}