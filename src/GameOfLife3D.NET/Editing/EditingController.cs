using System.Numerics;
using GameOfLife3D.NET.Engine;
using GameOfLife3D.NET.Rendering;

namespace GameOfLife3D.NET.Editing;

public enum EditTool
{
    Toggle,
    Draw,
    Erase,
}

public sealed class EditingController
{
    private readonly GameEngine _engine;
    private readonly Renderer3D _renderer;
    private readonly GridRayCaster _rayCaster;

    public bool IsActive { get; private set; }
    public int BrushSize { get; set; } = 1;
    public EditTool CurrentTool { get; set; } = EditTool.Toggle;
    public int PatternRotation { get; private set; } // 0, 90, 180, 270

    public EditingController(GameEngine engine, Renderer3D renderer, GridRayCaster rayCaster)
    {
        _engine = engine;
        _renderer = renderer;
        _rayCaster = rayCaster;
    }

    public bool TryActivate(bool isPlaying, int displayStart)
    {
        // Editing only available when paused and viewing generation 0
        if (isPlaying || displayStart != 0)
            return false;

        IsActive = true;
        return true;
    }

    public void Deactivate()
    {
        IsActive = false;
        _renderer.ClearPreviewCells();
    }

    public void RotatePattern()
    {
        PatternRotation = (PatternRotation + 90) % 360;
    }

    public void HandleClick(float screenX, float screenY, Matrix4x4 view, Matrix4x4 proj, int viewportW, int viewportH, int gridSize)
    {
        if (!IsActive) return;

        var cell = _rayCaster.ScreenToGrid(screenX, screenY, view, proj, viewportW, viewportH, gridSize);
        if (cell == null) return;

        ApplyBrush(cell.Value, gridSize);
    }

    public void HandleMouseMove(float screenX, float screenY, Matrix4x4 view, Matrix4x4 proj, int viewportW, int viewportH, int gridSize)
    {
        if (!IsActive) return;

        var cell = _rayCaster.ScreenToGrid(screenX, screenY, view, proj, viewportW, viewportH, gridSize);
        if (cell == null)
        {
            _renderer.ClearPreviewCells();
            return;
        }

        UpdatePreview(cell.Value, gridSize);
    }

    private void ApplyBrush(Vector2Int center, int gridSize)
    {
        var gen0 = _engine.GetGeneration(0);
        if (gen0 == null) return;

        int halfBrush = BrushSize / 2;
        for (int dx = -halfBrush; dx <= halfBrush; dx++)
        {
            for (int dz = -halfBrush; dz <= halfBrush; dz++)
            {
                int gx = center.X + dx;
                int gz = center.Y + dz;
                if (gx < 0 || gx >= gridSize || gz < 0 || gz >= gridSize)
                    continue;

                bool newState = CurrentTool switch
                {
                    EditTool.Toggle => !gen0.Cells[gx, gz],
                    EditTool.Draw => true,
                    EditTool.Erase => false,
                    _ => !gen0.Cells[gx, gz],
                };

                _engine.SetCellInGen0(gx, gz, newState);
            }
        }

        _renderer.InvalidateState();
    }

    private void UpdatePreview(Vector2Int center, int gridSize)
    {
        var previewList = new List<InstanceData>();
        float halfSize = gridSize / 2f;
        int halfBrush = BrushSize / 2;

        for (int dx = -halfBrush; dx <= halfBrush; dx++)
        {
            for (int dz = -halfBrush; dz <= halfBrush; dz++)
            {
                int gx = center.X + dx;
                int gz = center.Y + dz;
                if (gx < 0 || gx >= gridSize || gz < 0 || gz >= gridSize)
                    continue;

                previewList.Add(new InstanceData
                {
                    Position = new Vector3(gx - halfSize, 0, gz - halfSize),
                    GenerationT = -1.0f, // Marker for preview
                });
            }
        }

        if (previewList.Count > 0)
        {
            _renderer.SetPreviewCells(previewList.ToArray());
        }
        else
        {
            _renderer.ClearPreviewCells();
        }
    }
}
