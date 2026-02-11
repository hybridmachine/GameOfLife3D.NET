namespace GameOfLife3D.NET.Engine;

public sealed record Rule(string Name, int[] Birth, int[] Survival);

public static class RulePresets
{
    public static readonly Dictionary<string, Rule> All = new()
    {
        ["conway"] = new("Conway's Life", [3], [2, 3]),
        ["highlife"] = new("HighLife", [3, 6], [2, 3]),
        ["daynight"] = new("Day & Night", [3, 6, 7, 8], [3, 4, 6, 7, 8]),
        ["seeds"] = new("Seeds", [2], []),
        ["lifewithoutdeath"] = new("Life without Death", [3], [0, 1, 2, 3, 4, 5, 6, 7, 8]),
        ["diamoeba"] = new("Diamoeba", [3, 5, 6, 7, 8], [5, 6, 7, 8]),
        ["2x2"] = new("2x2", [3, 6], [1, 2, 5]),
        ["morley"] = new("Morley", [3, 6, 8], [2, 4, 5]),
        ["anneal"] = new("Anneal", [4, 6, 7, 8], [3, 5, 6, 7, 8]),
    };
}
