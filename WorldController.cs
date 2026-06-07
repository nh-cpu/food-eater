using Godot;
public partial class WorldController : Node3D
{
	public override void _Ready()
	{
		AddLighting();
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

}
