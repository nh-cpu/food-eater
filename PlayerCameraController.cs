using Godot;

public partial class PlayerCameraController : Node3D
{
	public static PlayerCameraController LocalInstance { get; private set; }

	private const float FollowSpeed = 8f;
	private const float RotationSensitivity = 0.01f;
	private const float ZoomStep = 1.25f;
	private const float MinDistance = 6f;
	private const float MaxDistance = 24f;
	private const float MinPitch = -1.2f;
	private const float MaxPitch = -0.2f;

	private SandwichController _target;
	private Camera3D _camera;
	private float _yaw = 0.35f;
	private float _pitch = -0.55f;
	private float _distance = 14f;
	private bool _isOrbiting;

	public override void _Ready()
	{
		LocalInstance = this;

		_camera = new Camera3D
		{
			Name = "Camera",
			Current = true,
			Fov = 65f,
		};
		AddChild(_camera);
	}

	public override void _ExitTree()
	{
		if (LocalInstance == this)
		{
			LocalInstance = null;
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Right)
			{
				_isOrbiting = mouseButton.Pressed;
				Input.MouseMode = _isOrbiting ? Input.MouseModeEnum.Captured : Input.MouseModeEnum.Visible;
			}

			if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelUp)
			{
				_distance = Mathf.Max(MinDistance, _distance - ZoomStep);
			}
			else if (mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.WheelDown)
			{
				_distance = Mathf.Min(MaxDistance, _distance + ZoomStep);
			}
		}

		if (_isOrbiting && @event is InputEventMouseMotion mouseMotion)
		{
			_yaw -= mouseMotion.Relative.X * RotationSensitivity;
			_pitch = Mathf.Clamp(_pitch - mouseMotion.Relative.Y * RotationSensitivity, MinPitch, MaxPitch);
		}
	}

	public override void _Process(double delta)
	{
		if (!IsInstanceValid(_target))
		{
			_target = FindSandwich();
		}

		if (!IsInstanceValid(_target))
		{
			return;
		}

		var orbitBasis = Basis.FromEuler(new Vector3(_pitch, _yaw, 0f));
		var desiredOffset = orbitBasis * new Vector3(0f, 0f, _distance);
		var desiredPosition = _target.GlobalPosition + desiredOffset;
		var weight = 1f - Mathf.Exp(-FollowSpeed * (float)delta);
		GlobalPosition = GlobalPosition.Lerp(desiredPosition, weight);
		LookAt(_target.GlobalPosition + Vector3.Up, Vector3.Up);
	}

	private SandwichController FindSandwich()
	{
		foreach (var node in GetTree().GetNodesInGroup(SandwichController.GroupName))
		{
			if (node is SandwichController sandwich)
			{
				return sandwich;
			}
		}

		return null;
	}

	public Vector3 GetFlattenedForward()
	{
		var forward = -GlobalBasis.Z;
		forward.Y = 0f;
		return forward.Normalized();
	}

	public Vector3 GetFlattenedRight()
	{
		var right = GlobalBasis.X;
		right.Y = 0f;
		return right.Normalized();
	}
}
