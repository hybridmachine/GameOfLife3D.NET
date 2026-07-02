using System.Numerics;
using GameOfLife3D.NET.Rendering;

namespace GameOfLife3D.NET.Engine;

/// <summary>
/// CPU-side rigid-body physics simulation for the cinematic falling-cells
/// transition. Each live cell is treated as a sphere of <see cref="CellRadius"/>
/// for collision purposes (visually they remain cubes). Gravity, floor
/// contact, and cell-to-cell impulse collisions with friction &amp; restitution
/// are integrated at a fixed substep rate so the result is frame-rate
/// independent and recording-stable.
/// </summary>
public sealed class FallingCellsPhysics
{
    // ── Tunable constants ──────────────────────────────────────────────

    private const float Gravity = -6.0f;
    private const float Restitution = 0.3f;
    private const float Friction = 0.5f;
    private const float AirDrag = 0.02f;
    private const float CellRadius = 0.5f;
    private const float FloorY = -0.5f;
    private const float FixedDt = 1.0f / 120.0f;
    private const int MaxSubsteps = 4;
    private const int SolverIterations = 4;
    public const int MaxCells = 100_000;
    private const float InitialJitter = 0.5f;

    private static readonly float MinDist = CellRadius * 2f;
    private static readonly float MinDistSq = MinDist * MinDist;
    private static readonly float RestY = FloorY + CellRadius;
    private static readonly float HashCellSize = MinDist;

    // ── Simulation state ───────────────────────────────────────────────

    private Vector3[] _positions = [];
    private Vector3[] _velocities = [];
    private float[] _generationT = [];
    private bool[] _alive = [];
    private int _count;
    private int _activeCount;
    private float _accumulator;

    // Reusable spatial hash — cleared & repopulated every solver iteration.
    private readonly Dictionary<long, List<int>> _hash = new();
    private readonly Stack<List<int>> _listPool = new();

    public int Count => _count;
    public int ActiveCount => _activeCount;

    public void Initialize(ReadOnlySpan<Vector3> positions, ReadOnlySpan<float> generationT)
    {
        int count = Math.Min(positions.Length, MaxCells);
        count = Math.Min(count, generationT.Length);

        if (_positions.Length < count)
        {
            int cap = Math.Max(count, _positions.Length * 2);
            _positions = new Vector3[cap];
            _velocities = new Vector3[cap];
            _generationT = new float[cap];
            _alive = new bool[cap];
        }

        _count = count;
        _activeCount = count;
        _accumulator = 0f;

        for (int i = 0; i < count; i++)
        {
            _positions[i] = positions[i];
            _generationT[i] = generationT[i];
            _alive[i] = true;

            // Small random horizontal velocity breaks perfect symmetry so
            // vertically-aligned cells don't jitter on each other forever.
            _velocities[i] = new Vector3(
                (Random.Shared.NextSingle() * 2f - 1f) * InitialJitter,
                0f,
                (Random.Shared.NextSingle() * 2f - 1f) * InitialJitter);
        }
    }

    public void Step(float frameDelta)
    {
        if (_count == 0) return;

        _accumulator += Math.Min(frameDelta, 0.05f);
        int substeps = 0;
        while (_accumulator >= FixedDt && substeps < MaxSubsteps)
        {
            StepFixed(FixedDt);
            _accumulator -= FixedDt;
            substeps++;
        }
    }

    private void StepFixed(float dt)
    {
        Integrate(dt);
        ResolveFloor();
        for (int i = 0; i < SolverIterations; i++)
            ResolveCollisions();
    }

    private void Integrate(float dt)
    {
        float drag = 1.0f - AirDrag * dt;
        for (int i = 0; i < _count; i++)
        {
            if (!_alive[i]) continue;
            _velocities[i].Y += Gravity * dt;
            _velocities[i] *= drag;
            _positions[i] += _velocities[i] * dt;
        }
    }

    private void ResolveFloor()
    {
        for (int i = 0; i < _count; i++)
        {
            if (!_alive[i]) continue;
            if (_positions[i].Y < RestY)
            {
                _alive[i] = false;
                _activeCount--;
            }
        }
    }

    private void ResolveCollisions()
    {
        BuildSpatialHash();

        for (int i = 0; i < _count; i++)
        {
            if (!_alive[i]) continue;
            ref var pi = ref _positions[i];
            int cx = (int)MathF.Floor(pi.X / HashCellSize);
            int cy = (int)MathF.Floor(pi.Y / HashCellSize);
            int cz = (int)MathF.Floor(pi.Z / HashCellSize);

            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (!_hash.TryGetValue(HashKey(cx + dx, cy + dy, cz + dz), out var bucket))
                    continue;

                for (int b = 0; b < bucket.Count; b++)
                {
                    int j = bucket[b];
                    if (j <= i) continue;
                    ResolvePair(i, j);
                }
            }
        }
    }

    private void ResolvePair(int i, int j)
    {
        ref var pi = ref _positions[i];
        ref var pj = ref _positions[j];

        float dx = pj.X - pi.X;
        float dy = pj.Y - pi.Y;
        float dz = pj.Z - pi.Z;
        float distSq = dx * dx + dy * dy + dz * dz;

        if (distSq >= MinDistSq || distSq < 1e-8f) return;

        float dist = MathF.Sqrt(distSq);
        float invDist = 1.0f / dist;
        float nx = dx * invDist;
        float ny = dy * invDist;
        float nz = dz * invDist;

        // Positional correction — split the penetration equally.
        float halfPen = (MinDist - dist) * 0.5f;
        pi.X -= nx * halfPen; pi.Y -= ny * halfPen; pi.Z -= nz * halfPen;
        pj.X += nx * halfPen; pj.Y += ny * halfPen; pj.Z += nz * halfPen;

        // Impulse along the contact normal (equal masses).
        ref var vi = ref _velocities[i];
        ref var vj = ref _velocities[j];

        float rvx = vj.X - vi.X;
        float rvy = vj.Y - vi.Y;
        float rvz = vj.Z - vi.Z;

        float velAlongNormal = rvx * nx + rvy * ny + rvz * nz;
        if (velAlongNormal > 0f) return; // already separating

        float impulse = -(1.0f + Restitution) * velAlongNormal * 0.5f;
        vi.X -= impulse * nx; vi.Y -= impulse * ny; vi.Z -= impulse * nz;
        vj.X += impulse * nx; vj.Y += impulse * ny; vj.Z += impulse * nz;

        // Tangential friction.
        float tx = rvx - velAlongNormal * nx;
        float ty = rvy - velAlongNormal * ny;
        float tz = rvz - velAlongNormal * nz;
        float tLenSq = tx * tx + ty * ty + tz * tz;
        if (tLenSq < 1e-8f) return;

        float invTLen = 1.0f / MathF.Sqrt(tLenSq);
        tx *= invTLen; ty *= invTLen; tz *= invTLen;
        float frictionImpulse = (rvx * tx + rvy * ty + rvz * tz) * Friction * 0.5f;
        vi.X += frictionImpulse * tx; vi.Y += frictionImpulse * ty; vi.Z += frictionImpulse * tz;
        vj.X -= frictionImpulse * tx; vj.Y -= frictionImpulse * ty; vj.Z -= frictionImpulse * tz;
    }

    private void BuildSpatialHash()
    {
        foreach (var list in _hash.Values)
        {
            list.Clear();
            _listPool.Push(list);
        }
        _hash.Clear();

        for (int i = 0; i < _count; i++)
        {
            if (!_alive[i]) continue;
            ref var p = ref _positions[i];
            int cx = (int)MathF.Floor(p.X / HashCellSize);
            int cy = (int)MathF.Floor(p.Y / HashCellSize);
            int cz = (int)MathF.Floor(p.Z / HashCellSize);
            long key = HashKey(cx, cy, cz);

            if (!_hash.TryGetValue(key, out var list))
            {
                list = _listPool.Count > 0 ? _listPool.Pop() : new List<int>();
                _hash[key] = list;
            }
            list.Add(i);
        }
    }

    /// <summary>
    /// Packs three grid coordinates (each in roughly [-1024, 1024]) into a
    /// single 63-bit key so the spatial hash can use a primitive long instead
    /// of a tuple.
    /// </summary>
    private static long HashKey(int x, int y, int z)
    {
        return ((long)(x + 1024) << 42) | ((long)(y + 1024) << 21) | (long)(z + 1024);
    }

    /// <summary>
    /// Writes current positions (with the original generation index for colour)
    /// into the supplied instance buffer. Returns the number of instances
    /// written, clamped to <paramref name="maxCount"/>.
    /// </summary>
    public int WriteInstanceData(InstanceData[] buffer, int maxCount)
    {
        int written = 0;
        for (int i = 0; i < _count && written < maxCount; i++)
        {
            if (!_alive[i]) continue;
            buffer[written] = new InstanceData
            {
                Position = _positions[i],
                GenerationT = _generationT[i],
            };
            written++;
        }
        return written;
    }

    public void Clear()
    {
        _count = 0;
        _activeCount = 0;
        _accumulator = 0f;
        _hash.Clear();
    }
}
