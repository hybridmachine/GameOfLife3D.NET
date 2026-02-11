namespace GameOfLife3D.NET.Engine;

public class GameState
{
    public int GridSize { get; set; }
    public bool Toroidal { get; set; }
    public string RuleName { get; set; } = "conway";
    public int[]? BirthRule { get; set; }
    public int[]? SurvivalRule { get; set; }
    public int GenerationCount { get; set; }
    public bool[]? Gen0Cells { get; set; }
}
