using Godot;
using SpacetimeDB.Types;

public partial class SandwichController : Node3D
{
	public const string GroupName = "sandwich";

	private const float PositionInterpolationSpeed = 14f;
	private const float RotationInterpolationSpeed = 10f;

	private readonly int _sandwichId;
	private Vector3 _targetPosition;
	private float _targetTiltRadians;

	public bool Completed { get; private set; }
	public bool AtSummit { get; private set; }
	public int AttachedPlayerCount { get; private set; }

	public SandwichController(Sandwich sandwich)
	{
		_sandwichId = sandwich.Id;
		_targetPosition = sandwich.Position;
		Position = _targetPosition;
		ApplyNetworkState(sandwich);
	}

	public override void _Ready()
	{
		Name = $"Sandwich - {_sandwichId}";
		AddToGroup(GroupName);

		var mesh = new MeshInstance3D
		{
			Name = "PlaceholderMesh",
			Mesh = new BoxMesh { Size = new Vector3(3f, 0.35f, 3f) },
			MaterialOverride = new StandardMaterial3D { AlbedoColor = Colors.SaddleBrown },
		};
		AddChild(mesh);
	}

	public override void _Process(double delta)
	{
		var positionWeight = 1f - Mathf.Exp(-PositionInterpolationSpeed * (float)delta);
		var rotationWeight = 1f - Mathf.Exp(-RotationInterpolationSpeed * (float)delta);

		Position = Position.Lerp(_targetPosition, positionWeight);
		Rotation = new Vector3(
			Rotation.X,
			Rotation.Y,
			Mathf.LerpAngle(Rotation.Z, _targetTiltRadians, rotationWeight)
		);
	}

	public void ApplyNetworkState(Sandwich sandwich)
	{
		if (sandwich.Id != _sandwichId)
		{
			GD.PushError($"Cannot apply sandwich {sandwich.Id} state to sandwich {_sandwichId} controller.");
			return;
		}

		_targetPosition = sandwich.Position;
		_targetTiltRadians = Mathf.DegToRad(sandwich.Tilt);
		Completed = sandwich.Completed;
		AtSummit = sandwich.AtSummit;
		AttachedPlayerCount = sandwich.AttachedPlayerCount;
	}
}
