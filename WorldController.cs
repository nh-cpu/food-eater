using Godot;
using SpacetimeDB.Types;

public partial class WorldController : Node3D
{
	private const int TerrainResolution = 48;
	private bool _worldCreated;

	public override void _Ready()
	{
		GameSessionController.OnSubscriptionApplied += CreateWorldFromConfig;
		CreateWorldFromConfig();
	}

	public override void _ExitTree()
	{
		GameSessionController.OnSubscriptionApplied -= CreateWorldFromConfig;
	}

	private void CreateWorldFromConfig()
	{
		if (_worldCreated || !GameSessionController.IsConnected())
		{
			return;
		}

		var config = GameSessionController.Conn.Db.Config.Id.Find(0);
		if (config == null)
		{
			return;
		}

		_worldCreated = true;
		AddLighting();
		AddGround(config);
		AddSummitMarker(config);
	}

	private void AddLighting()
	{
		AddChild(new DirectionalLight3D
		{
			Name = "Sun",
			RotationDegrees = new Vector3(-55f, -30f, 0f),
			ShadowEnabled = true,
			LightEnergy = 1.2f,
		});

		var environment = new WorldEnvironment
		{
			Name = "Environment",
			Environment = new Environment
			{
				BackgroundMode = Environment.BGMode.Color,
				BackgroundColor = new Color("87ceeb"),
				AmbientLightSource = Environment.AmbientSource.Color,
				AmbientLightColor = Colors.White,
				AmbientLightEnergy = 0.45f,
			},
		};
		AddChild(environment);
	}

	private void AddGround(Config config)
	{
		var ground = new MeshInstance3D
		{
			Name = "Mountain",
			Mesh = BuildTerrainMesh(config),
			MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color("6b8e23") },
		};
		AddChild(ground);

		var boundary = new MeshInstance3D
		{
			Name = "WorldBoundary",
			Mesh = new TorusMesh
			{
				InnerRadius = config.WorldRadius - 0.15f,
				OuterRadius = config.WorldRadius + 0.15f,
			},
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = Colors.Goldenrod,
				EmissionEnabled = true,
				Emission = Colors.Goldenrod,
			},
		};
		AddChild(boundary);
	}

	private void AddSummitMarker(Config config)
	{
		var summit = new MeshInstance3D
		{
			Name = "SummitMarker",
			Position = new Vector3(0f, config.SummitHeight, 0f),
			Mesh = new CylinderMesh
			{
				TopRadius = config.SummitDistance,
				BottomRadius = config.SummitDistance,
				Height = 0.5f,
			},
			MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = Colors.Gold,
				EmissionEnabled = true,
				Emission = Colors.Gold,
			},
		};
		AddChild(summit);
	}

	private static ArrayMesh BuildTerrainMesh(Config config)
	{
		var surfaceTool = new SurfaceTool();
		surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

		var step = (config.WorldRadius * 2f) / TerrainResolution;
		for (var x = 0; x < TerrainResolution; x++)
		{
			for (var z = 0; z < TerrainResolution; z++)
			{
				var x0 = -config.WorldRadius + x * step;
				var x1 = x0 + step;
				var z0 = -config.WorldRadius + z * step;
				var z1 = z0 + step;

				var p00 = TerrainPoint(x0, z0, config);
				var p10 = TerrainPoint(x1, z0, config);
				var p01 = TerrainPoint(x0, z1, config);
				var p11 = TerrainPoint(x1, z1, config);

				AddTerrainTriangle(surfaceTool, p00, p10, p11);
				AddTerrainTriangle(surfaceTool, p00, p11, p01);
			}
		}

		surfaceTool.GenerateNormals();
		return surfaceTool.Commit();
	}

	private static void AddTerrainTriangle(SurfaceTool surfaceTool, Vector3 a, Vector3 b, Vector3 c)
	{
		surfaceTool.AddVertex(a);
		surfaceTool.AddVertex(b);
		surfaceTool.AddVertex(c);
	}

	private static Vector3 TerrainPoint(float x, float z, Config config)
	{
		var horizontalDistance = Mathf.Sqrt(x * x + z * z);
		var normalizedDistance = Mathf.Clamp(horizontalDistance / config.WorldRadius, 0f, 1f);
		var heightFactor = 1f - normalizedDistance;
		var y = config.SummitHeight * heightFactor * heightFactor;
		return new Vector3(x, y, z);
	}
}
