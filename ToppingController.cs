using System;
using Godot;
using SpacetimeDB.Types;

public partial class ToppingController : Node3D
{
	private const float PositionInterpolationSpeed = 14f;
	private const string PrototypeRootName = "ToppingProfilesSource";

	private readonly int _toppingId;
	private Vector3 _targetPosition;
	private MeshInstance3D _mesh;
	private Node3D _visualInstance;

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

		EnsureVisualInstance();

		var isVisible = State != ToppingState.WaitingAtSummit;
		_mesh.Visible = _visualInstance == null && isVisible;
		_mesh.MaterialOverride = new StandardMaterial3D
		{
			AlbedoColor = ResolveFallbackColor(ToppingName),
		};

		if (_visualInstance != null)
		{
			_visualInstance.Visible = isVisible;
		}
	}

	private void EnsureVisualInstance()
	{
		if (_visualInstance != null && string.Equals(_visualInstance.Name, ToppingName, StringComparison.Ordinal))
		{
			return;
		}

		if (_visualInstance != null)
		{
			_visualInstance.QueueFree();
			_visualInstance = null;
		}

		var prototype = FindPrototype(ToppingName);
		if (prototype == null)
		{
			return;
		}

		_visualInstance = prototype.Duplicate() as Node3D;
		if (_visualInstance == null)
		{
			return;
		}

		_visualInstance.Name = ToppingName;
		_visualInstance.Position = Vector3.Zero;
		AddChild(_visualInstance);
	}

	private Node3D FindPrototype(string toppingName)
	{
		var sceneRoot = GetTree()?.CurrentScene;
		if (sceneRoot == null)
		{
			return null;
		}

		var prototypeRoot = sceneRoot.FindChild(PrototypeRootName, true, false);
		if (prototypeRoot == null)
		{
			return null;
		}

		var targetName = CanonicalizeName(toppingName);
		return FindPrototypeRecursive(prototypeRoot, targetName);
	}

	private static Node3D FindPrototypeRecursive(Node root, string targetName)
	{
		foreach (var child in root.GetChildren())
		{
			if (child is Node3D childNode3D)
			{
				if (CanonicalizeName(childNode3D.Name) == targetName)
				{
					return childNode3D;
				}

				var nested = FindPrototypeRecursive(childNode3D, targetName);
				if (nested != null)
				{
					return nested;
				}
			}
			else if (child is Node childNode)
			{
				var nested = FindPrototypeRecursive(childNode, targetName);
				if (nested != null)
				{
					return nested;
				}
			}
		}

		return null;
	}

	private static Color ResolveFallbackColor(string toppingName)
	{
		return CanonicalizeName(toppingName) switch
		{
			"lettuce" => Colors.LimeGreen,
			"tomato" => Colors.Tomato,
			"cheese" => Colors.Gold,
			"bacon" => new Color("8b4513"),
			"topbread" => new Color("deb887"),
			"bottombread" => Colors.Peru,
			_ => Colors.Wheat,
		};
	}

	private static string CanonicalizeName(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return string.Empty;
		}

		var buffer = new System.Text.StringBuilder(value.Length);
		foreach (var character in value)
		{
			if (char.IsLetter(character))
			{
				buffer.Append(char.ToLowerInvariant(character));
			}
		}

		while (buffer.Length > 0 && char.IsDigit(buffer[^1]))
		{
			buffer.Length--;
		}

		return buffer.ToString();
	}
}
