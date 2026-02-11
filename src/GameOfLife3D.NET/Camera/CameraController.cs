using System.Numerics;
using Silk.NET.Input;

namespace GameOfLife3D.NET.Camera;

public sealed class CameraController
{
    private Vector3 _target = new(0, 25, 0);
    private float _radius = 50f;
    private float _phi = MathF.PI / 3f;
    private float _theta = MathF.PI / 4f;

    private float _panSpeed = 0.1f;
    private float _rotateSpeed = 0.01f;
    private float _moveSpeed = 0.5f;
    private float _orbitSpeed = 0.02f;

    // Input state
    private readonly HashSet<Key> _keysDown = new();
    private bool _isDragging;
    private int _dragButton;
    private Vector2 _lastMouse;
    private bool _imGuiWantsMouse;
    private bool _imGuiWantsKeyboard;

    // Camera matrices
    private Matrix4x4 _viewMatrix;
    private Vector3 _cameraPosition;
    private float _aspectRatio = 16f / 9f;
    private float _fov = 60f * MathF.PI / 180f;

    public Matrix4x4 ViewMatrix => _viewMatrix;
    public Matrix4x4 ProjectionMatrix => Matrix4x4.CreatePerspectiveFieldOfView(_fov, _aspectRatio, 0.1f, 10000f);
    public Vector3 Position => _cameraPosition;
    public float AspectRatio { set => _aspectRatio = value; }

    public CameraController()
    {
        UpdateCameraPosition();
    }

    public void Initialize(IInputContext input)
    {
        foreach (var keyboard in input.Keyboards)
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyUp += OnKeyUp;
        }

        foreach (var mouse in input.Mice)
        {
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
            mouse.Scroll += OnScroll;
        }
    }

    public void SetImGuiCapture(bool wantsMouse, bool wantsKeyboard)
    {
        _imGuiWantsMouse = wantsMouse;
        _imGuiWantsKeyboard = wantsKeyboard;
    }

    public void Update(float deltaTime)
    {
        if (_imGuiWantsKeyboard) return;

        var forward = Vector3.Normalize(_target - _cameraPosition);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.UnitY;
        var move = Vector3.Zero;

        if (IsKeyDown(Key.W) || IsKeyDown(Key.Up)) move += forward;
        if (IsKeyDown(Key.S) || IsKeyDown(Key.Down)) move -= forward;
        if (IsKeyDown(Key.A) || IsKeyDown(Key.Left)) move -= right;
        if (IsKeyDown(Key.D) || IsKeyDown(Key.Right)) move += right;
        if (IsKeyDown(Key.R)) move += up;
        if (IsKeyDown(Key.F)) move -= up;

        if (IsKeyDown(Key.Q))
        {
            _theta -= _rotateSpeed * 2;
            UpdateCameraPosition();
        }
        if (IsKeyDown(Key.E))
        {
            _theta += _rotateSpeed * 2;
            UpdateCameraPosition();
        }

        if (IsKeyDown(Key.O)) { OrbitAroundY(-_orbitSpeed); }
        if (IsKeyDown(Key.P)) { OrbitAroundY(_orbitSpeed); }

        if (move.LengthSquared() > 0)
        {
            move = Vector3.Normalize(move) * _moveSpeed;
            _target += move;
            UpdateCameraPosition();
        }
    }

    private void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
    {
        _keysDown.Add(key);
    }

    private void OnKeyUp(IKeyboard keyboard, Key key, int scancode)
    {
        _keysDown.Remove(key);
    }

    private void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (_imGuiWantsMouse) return;
        _isDragging = true;
        _dragButton = (int)button;
        _lastMouse = new Vector2(mouse.Position.X, mouse.Position.Y);
    }

    private void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (_dragButton == (int)button)
        {
            _isDragging = false;
            _dragButton = -1;
        }
    }

    private void OnMouseMove(IMouse mouse, Vector2 position)
    {
        if (!_isDragging || _imGuiWantsMouse) return;

        float deltaX = position.X - _lastMouse.X;
        float deltaY = position.Y - _lastMouse.Y;
        _lastMouse = position;

        if (_dragButton == (int)MouseButton.Left)
        {
            // Orbit
            _theta -= deltaX * _rotateSpeed;
            _phi = Math.Clamp(_phi + deltaY * _rotateSpeed, 0.1f, MathF.PI - 0.1f);
            UpdateCameraPosition();
        }
        else if (_dragButton == (int)MouseButton.Right || _dragButton == (int)MouseButton.Middle)
        {
            // Pan
            ApplyPan(-deltaX * _panSpeed * 0.1f, deltaY * _panSpeed * 0.1f);
        }
    }

    private void OnScroll(IMouse mouse, ScrollWheel scroll)
    {
        if (_imGuiWantsMouse) return;
        float delta = scroll.Y > 0 ? 0.9f : 1.1f;
        _radius = Math.Clamp(_radius * delta, 1f, 1000f);
        UpdateCameraPosition();
    }

    private bool IsKeyDown(Key key) => _keysDown.Contains(key);

    private void ApplyPan(float rightAmount, float upAmount)
    {
        var forward = Vector3.Normalize(_target - _cameraPosition);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitY));
        var up = Vector3.Normalize(Vector3.Cross(right, forward));

        _target += right * rightAmount + up * upAmount;
        UpdateCameraPosition();
    }

    private void OrbitAroundY(float angle)
    {
        _theta += angle;
        UpdateCameraPosition();
    }

    private void UpdateCameraPosition()
    {
        // Spherical to Cartesian (Y-up)
        float sinPhi = MathF.Sin(_phi);
        float cosPhi = MathF.Cos(_phi);
        float sinTheta = MathF.Sin(_theta);
        float cosTheta = MathF.Cos(_theta);

        _cameraPosition = _target + new Vector3(
            _radius * sinPhi * sinTheta,
            _radius * cosPhi,
            _radius * sinPhi * cosTheta
        );

        _viewMatrix = Matrix4x4.CreateLookAt(_cameraPosition, _target, Vector3.UnitY);
    }

    public void Reset()
    {
        _target = new Vector3(0, 25, 0);
        _radius = 50f;
        _phi = MathF.PI / 3f;
        _theta = MathF.PI / 4f;
        UpdateCameraPosition();
    }

    public CameraState GetState() => new()
    {
        Target = _target,
        Distance = _radius,
        Phi = _phi,
        Theta = _theta,
    };

    public void SetState(CameraState state)
    {
        _target = state.Target;
        _radius = state.Distance;
        _phi = state.Phi;
        _theta = state.Theta;
        UpdateCameraPosition();
    }
}
