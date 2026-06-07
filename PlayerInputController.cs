using Godot;
using SpacetimeDB.Types;

public partial class PlayerInputController : Node
{
	private const float SendIntervalSeconds = 1f / 20f;

	private float _timeSinceLastSend;
	private Vector3 _lastSentDirection;

	public override void _Process(double delta)
	{
		if (!GameSessionController.IsConnected())
		{
			return;
		}

		_timeSinceLastSend += (float)delta;
		if (_timeSinceLastSend < SendIntervalSeconds)
		{
			return;
		}

		_timeSinceLastSend = 0f;
		var direction = ReadHorizontalDirection();

		if (direction.IsEqualApprox(_lastSentDirection) && direction == Vector3.Zero)
		{
			return;
		}

		_lastSentDirection = direction;
		GameSessionController.Conn.Reducers.UpdatePlayerInput((DbVector3)direction);
	}

	private static Vector3 ReadHorizontalDirection()
	{
		var direction = Vector3.Zero;

		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
		{
			direction.Z -= 1f;
		}
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
		{
			direction.Z += 1f;
		}
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
		{
			direction.X -= 1f;
		}
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
		{
			direction.X += 1f;
		}

		return direction.Normalized();
	}
}
