using Godot;
using SpacetimeDB.Types;

public partial class PlayerInputController : Node
{
	private const float SendIntervalSeconds = 1f / 20f;

	private float _timeSinceLastSend;
	private Vector3 _lastSentDirection;
	private bool _jumpQueued;
	private bool _restartChordActive;

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
		var jumpRequested = _jumpQueued;
		_jumpQueued = false;

		if (direction.IsEqualApprox(_lastSentDirection) && direction == Vector3.Zero && !jumpRequested)
		{
			return;
		}

		_lastSentDirection = direction;
		GameSessionController.Conn.Reducers.UpdatePlayerInput((DbVector3)direction, jumpRequested);
	}

	public override void _Input(InputEvent @event)
	{
		if (@event.IsActionPressed("jump"))
		{
			_jumpQueued = true;
		}

		if (!GameSessionController.IsConnected())
		{
			return;
		}

		var restartPressed = Input.IsKeyPressed(Key.I) && Input.IsKeyPressed(Key.O);
		if (restartPressed && !_restartChordActive)
		{
			_restartChordActive = true;
			GameSessionController.Conn.Reducers.ResetRun();
		}
		else if (!restartPressed)
		{
			_restartChordActive = false;
		}
	}

	private static Vector3 ReadHorizontalDirection()
	{
		var localDirection = Vector3.Zero;

		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))
		{
			localDirection.Z -= 1f;
		}
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))
		{
			localDirection.Z += 1f;
		}
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))
		{
			localDirection.X -= 1f;
		}
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right))
		{
			localDirection.X += 1f;
		}

		if (localDirection == Vector3.Zero)
		{
			return Vector3.Zero;
		}

		var cameraController = PlayerCameraController.LocalInstance;
		if (cameraController == null)
		{
			return localDirection.Normalized();
		}

		var forward = cameraController.GetFlattenedForward();
		var right = cameraController.GetFlattenedRight();
		var worldDirection = forward * -localDirection.Z + right * localDirection.X;
		return worldDirection.Normalized();
	}
}
