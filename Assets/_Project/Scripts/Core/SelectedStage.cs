// Value-only handoff: no scene object or asset lifetime dependencies.
public sealed class SelectedStage
{
    public string Id { get; }
    public string SceneName { get; }
    public int WinCoins { get; }
    public SelectedStage(string id, string sceneName, int winCoins)
    { Id = id; SceneName = sceneName; WinCoins = winCoins; }
}
