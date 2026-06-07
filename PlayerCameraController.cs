using Godot;

public partial class PlayerCameraController : Node3D
{
	private const float FollowSpeed = 6f;
	private static readonly Vector3 FollowOffset = new(0f, 9f, 14f);

	private SandwichController _target;
	private Camera3D _camera;

	public override void _Ready()
	{
		_camera = new Camera3D
		{
			Name = "Camera",
			Current = true,
			Fov = 65f,
		};
		AddChild(_camera);
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

		var desiredPosition = _target.GlobalPosition + FollowOffset;
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
}
