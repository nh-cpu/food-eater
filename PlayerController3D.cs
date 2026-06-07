using Godot;
using SpacetimeDB.Types;

public partial class PlayerController3D : Node3D
{
	private const float PositionInterpolationSpeed = 14f;

	private readonly int _playerId;
	private readonly bool _isLocalPlayer;
	private Vector3 _targetPosition;

	public int PlayerId => _playerId;
	public string Username { get; private set; }
	public bool IsLocalPlayer => _isLocalPlayer;
	public Vector3 AttachmentOffset { get; private set; }

	public PlayerController3D(Player player)
	{
		_playerId = player.PlayerId;
		_isLocalPlayer = player.Identity == GameSessionController.LocalIdentity;
		Username = player.Name;
		AttachmentOffset = player.AttachmentOffset;
		_targetPosition = player.Position;
		Position = _targetPosition;
	}

	public override void _Ready()
	{
		Name = $"Player - {Username}";
		AddPlaceholderVisual();
	}

	public override void _Process(double delta)
	{
		var weight = 1f - Mathf.Exp(-PositionInterpolationSpeed * (float)delta);
		Position = Position.Lerp(_targetPosition, weight);
	}

	public void ApplyNetworkState(Player player)
	{
		if (player.PlayerId != _playerId)
		{
			GD.PushError(
				$"Cannot apply player {player.PlayerId} state to player {_playerId} controller."
			);
			return;
		}

		Username = player.Name;
		AttachmentOffset = player.AttachmentOffset;
		_targetPosition = player.Position;
		Name = $"Player - {Username}";
	}

	private void AddPlaceholderVisual()
	{
		var meshInstance = new MeshInstance3D
		{
			Name = "PlaceholderMesh",
			Position = new Vector3(0f, 0.9f, 0f),
			Mesh = new CapsuleMesh
			{
				Radius = 0.4f,
				Height = 1.8f,
			},
		};

		var material = new StandardMaterial3D
		{
			AlbedoColor = IsLocalPlayer ? Colors.DodgerBlue : Colors.Orange,
		};
		meshInstance.MaterialOverride = material;

		AddChild(meshInstance);
	}
}
