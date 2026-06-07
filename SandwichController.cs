using Godot;
using SpacetimeDB.Types;

public partial class SandwichController : Node3D
{
	public const string GroupName = "sandwich";

	private const float PositionInterpolationSpeed = 14f;
	private const float RotationInterpolationSpeed = 10f;

	private readonly int _sandwichId;
	private Vector3 _targetPosition;
	private float _targetPitchRadians;
	private float _targetYawRadians;
	private float _targetRollRadians;

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
	}

	public override void _Process(double delta)
	{
		var positionWeight = 1f - Mathf.Exp(-PositionInterpolationSpeed * (float)delta);
		var rotationWeight = 1f - Mathf.Exp(-RotationInterpolationSpeed * (float)delta);

		Position = Position.Lerp(_targetPosition, positionWeight);
		Rotation = new Vector3(
			Mathf.LerpAngle(Rotation.X, _targetPitchRadians, rotationWeight),
			Mathf.LerpAngle(Rotation.Y, _targetYawRadians, rotationWeight),
			Mathf.LerpAngle(Rotation.Z, _targetRollRadians, rotationWeight)
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
		_targetPitchRadians = Mathf.DegToRad(sandwich.Pitch);
		_targetYawRadians = Mathf.DegToRad(sandwich.Yaw);
		_targetRollRadians = Mathf.DegToRad(sandwich.Roll);
		Completed = sandwich.Completed;
		AtSummit = sandwich.AtSummit;
		AttachedPlayerCount = sandwich.AttachedPlayerCount;
	}
}
