using Godot;
using SpacetimeDB.Types;

public partial class PlayerController3D : Node3D
{
	public const string GroupName = "Players";
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
		_isLocalPlayer = player.Identity.Equals(GameSessionController.LocalIdentity);
		Username = player.Name;
		AttachmentOffset = player.AttachmentOffset;
		_targetPosition = player.Position;
		Position = _targetPosition;
	}

	public override void _Ready()
	{
		Name = $"Player - {Username}";
		AddToGroup(GroupName);
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
		var bodyMaterial = new StandardMaterial3D
		{
			AlbedoColor = GetPlayerColor(),
		};
		var meshInstance = new MeshInstance3D
		{
			Name = "PlaceholderMesh",
			Position = new Vector3(0f, 0.9f, 0f),
			Mesh = new CapsuleMesh
			{
				Radius = 0.4f,
				Height = 1.8f,
			},
			MaterialOverride = bodyMaterial,
		};

		var faceMarker = new MeshInstance3D
		{
			Name = "FrontFaceMarker",
			Position = new Vector3(0f, 1.15f, -0.36f),
			Mesh = new SphereMesh
			{
				Radius = 0.09f,
				Height = 0.18f,
			},
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = Colors.Red,
			},
		};

		AddChild(meshInstance);
		AddChild(faceMarker);
	}

	private Color GetPlayerColor()
	{
		return ParsePlayerSlot(Username) switch
		{
			var slot when slot >= 0 && slot % 2 == 0 => Colors.Goldenrod,
			var slot when slot >= 0 => Colors.DodgerBlue,
			_ => Colors.LightGray,
		};
	}

	private static int ParsePlayerSlot(string username)
	{
		if (string.IsNullOrWhiteSpace(username) || !username.StartsWith("Player "))
		{
			return -1;
		}

		return int.TryParse(username[7..], out var index) ? index - 1 : -1;
	}
}
