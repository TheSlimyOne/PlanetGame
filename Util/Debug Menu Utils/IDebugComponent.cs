namespace PlanetGame.Util.DebugUIComponents;
public interface IDebugComponent
{
    public string TechnicalName { get; protected set; }
	bool IsTemplate { get; set; }

	protected static string GetTechnicalName(string name)
	{
		return name.ToLower().Replace(" ", "_");
	}
}
