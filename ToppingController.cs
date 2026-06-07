using Godot;
using SpacetimeDB.Types;

public partial class ToppingController : Node3D
{
	private const float PositionInterpolationSpeed = 14f;

	private readonly int _toppingId;
	private Vector3 _targetPosition;
	private MeshInstance3D _mesh;

	public string ToppingName { get; private set; }
	public ToppingState State { get; private set; }

	public ToppingController(Topping topping)
	{
		_toppingId = topping.ToppingId;
		_targetPosition = topping.Position;
		Position = _targetPosition;
		ToppingName = topping.Name;
		State = topping.State;
	}

	public override void _Ready()
	{
		Name = $"Topping - {ToppingName}";
		_mesh = new MeshInstance3D
		{
			Name = "PlaceholderMesh",
			Mesh = new BoxMesh { Size = new Vector3(2.7f, 0.2f, 2.7f) },
		};
		AddChild(_mesh);
		UpdateVisual();
	}

	public override void _Process(double delta)
	{
		var weight = 1f - Mathf.Exp(-PositionInterpolationSpeed * (float)delta);
		Position = Position.Lerp(_targetPosition, weight);
	}

	public void ApplyNetworkState(Topping topping)
	{
		if (topping.ToppingId != _toppingId)
		{
			GD.PushError($"Cannot apply topping {topping.ToppingId} state to topping {_toppingId} controller.");
			return;
		}

		ToppingName = topping.Name;
		State = topping.State;
		_targetPosition = topping.Position;
		Name = $"Topping - {ToppingName}";
		UpdateVisual();
	}

	private void UpdateVisual()
	{
		if (_mesh == null)
		{
			return;
		}

		_mesh.Visible = State != ToppingState.WaitingAtSummit;
		_mesh.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = ToppingName switch
			{
				"Lettuce" => Colors.LimeGreen,
				"Tomato" => Colors.Tomato,
				"Cheese" => Colors.Gold,
				_ => Colors.Wheat,
			},
		};
	}
}
