namespace GameOfLife3D.NET.Engine;

public sealed class GameEngine
{
    private const int MaxGenerations = 1000;

    private int _gridSize;
    private readonly List<Generation> _generations = new();
    private bool _toroidal;
    private HashSet<int> _birthRule = new([3]);
    private HashSet<int> _survivalRule = new([2, 3]);
    private string _currentRuleName = "conway";

    public int GridSize => _gridSize;
    public bool IsToroidal => _toroidal;
    public string RuleName => _currentRuleName;
    public int GenerationCount => _generations.Count;
    public IReadOnlyList<Generation> Generations => _generations;

    public GameEngine(int gridSize = 50)
    {
        _gridSize = gridSize;
    }

    public string RuleString
    {
        get
        {
            var b = string.Join("", _birthRule.Order());
            var s = string.Join("", _survivalRule.Order());
            return $"B{b}/S{s}";
        }
    }

    public int[] BirthRule => [.. _birthRule.Order()];
    public int[] SurvivalRule => [.. _survivalRule.Order()];

    public void SetGridSize(int size)
    {
        _gridSize = size;
        _generations.Clear();
    }

    public void SetRule(string ruleKey)
    {
        if (RulePresets.All.TryGetValue(ruleKey, out var rule))
        {
            _birthRule = new HashSet<int>(rule.Birth);
            _survivalRule = new HashSet<int>(rule.Survival);
            _currentRuleName = ruleKey;
        }
    }

    public void SetCustomRule(int[] birth, int[] survival)
    {
        _birthRule = new HashSet<int>(birth);
        _survivalRule = new HashSet<int>(survival);
        _currentRuleName = "custom";
    }

    public void SetToroidal(bool enabled) => _toroidal = enabled;

    public void InitializeFromPattern(bool[,] pattern)
    {
        _generations.Clear();
        var grid = new bool[_gridSize, _gridSize];

        int patRows = pattern.GetLength(0);
        int patCols = pattern.GetLength(1);
        int startX = (_gridSize - patRows) / 2;
        int startY = (_gridSize - patCols) / 2;

        for (int i = 0; i < patRows; i++)
        {
            for (int j = 0; j < patCols; j++)
            {
                int gx = startX + i;
                int gy = startY + j;
                if (gx >= 0 && gx < _gridSize && gy >= 0 && gy < _gridSize)
                {
                    grid[gx, gy] = pattern[i, j];
                }
            }
        }

        AddGeneration(grid);
    }

    public void InitializeRandom(float density = 0.3f)
    {
        _generations.Clear();
        var grid = new bool[_gridSize, _gridSize];
        var rng = Random.Shared;

        for (int x = 0; x < _gridSize; x++)
            for (int y = 0; y < _gridSize; y++)
                grid[x, y] = rng.NextSingle() < density;

        AddGeneration(grid);
    }

    public void ComputeGenerations(int count)
    {
        if (_generations.Count == 0)
            throw new InvalidOperationException("No initial generation set.");

        int target = Math.Min(count, MaxGenerations);
        for (int i = _generations.Count; i < target; i++)
        {
            var next = ComputeNextGeneration(_generations[i - 1].Cells);
            AddGeneration(next);
        }
    }

    public bool ComputeSingleGeneration()
    {
        if (_generations.Count == 0 || _generations.Count >= MaxGenerations)
            return false;

        var next = ComputeNextGeneration(_generations[^1].Cells);
        AddGeneration(next);
        return true;
    }

    public Generation? GetGeneration(int index)
    {
        if (index >= 0 && index < _generations.Count)
            return _generations[index];
        return null;
    }

    public void Clear() => _generations.Clear();

    public GameState ExportState() => new()
    {
        GridSize = _gridSize,
        Toroidal = _toroidal,
        RuleName = _currentRuleName,
        BirthRule = BirthRule,
        SurvivalRule = SurvivalRule,
        GenerationCount = _generations.Count,
        Gen0Cells = _generations.Count > 0 ? SerializeGrid(_generations[0].Cells) : null,
    };

    public void ImportState(GameState state)
    {
        _gridSize = state.GridSize;
        _toroidal = state.Toroidal;

        if (state.RuleName != "custom" && RulePresets.All.ContainsKey(state.RuleName))
            SetRule(state.RuleName);
        else if (state.BirthRule != null && state.SurvivalRule != null)
            SetCustomRule(state.BirthRule, state.SurvivalRule);

        _generations.Clear();
        if (state.Gen0Cells != null)
        {
            var grid = DeserializeGrid(state.Gen0Cells, _gridSize);
            AddGeneration(grid);
            if (state.GenerationCount > 1)
                ComputeGenerations(state.GenerationCount);
        }
    }

    private void AddGeneration(bool[,] grid)
    {
        var liveCells = new List<Vector2Int>();
        for (int x = 0; x < _gridSize; x++)
            for (int y = 0; y < _gridSize; y++)
                if (grid[x, y])
                    liveCells.Add(new Vector2Int(x, y));

        _generations.Add(new Generation(_generations.Count, grid, liveCells));
    }

    private bool[,] ComputeNextGeneration(bool[,] current)
    {
        var next = new bool[_gridSize, _gridSize];
        for (int x = 0; x < _gridSize; x++)
        {
            for (int y = 0; y < _gridSize; y++)
            {
                int neighbors = CountLiveNeighbors(current, x, y);
                next[x, y] = current[x, y]
                    ? _survivalRule.Contains(neighbors)
                    : _birthRule.Contains(neighbors);
            }
        }
        return next;
    }

    private int CountLiveNeighbors(bool[,] grid, int x, int y)
    {
        int count = 0;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;

                int nx = x + dx;
                int ny = y + dy;

                if (_toroidal)
                {
                    nx = (nx + _gridSize) % _gridSize;
                    ny = (ny + _gridSize) % _gridSize;
                    if (grid[nx, ny]) count++;
                }
                else
                {
                    if (nx >= 0 && nx < _gridSize && ny >= 0 && ny < _gridSize && grid[nx, ny])
                        count++;
                }
            }
        }
        return count;
    }

    private static bool[]? SerializeGrid(bool[,] grid)
    {
        int rows = grid.GetLength(0);
        int cols = grid.GetLength(1);
        var flat = new bool[rows * cols];
        for (int x = 0; x < rows; x++)
            for (int y = 0; y < cols; y++)
                flat[x * cols + y] = grid[x, y];
        return flat;
    }

    private static bool[,] DeserializeGrid(bool[] flat, int size)
    {
        var grid = new bool[size, size];
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                grid[x, y] = flat[x * size + y];
        return grid;
    }
}

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
