using Godot;
using SpacetimeDB.Types;

public partial class GameEventController : CanvasLayer
{
	private const float MessageDurationSeconds = 4f;

	private Label _messageLabel;
	private float _remainingMessageTime;
	private DbConnection _conn;

	public override void _Ready()
	{
		_messageLabel = new Label
		{
			Name = "Message",
			Position = new Vector2(24f, 24f),
		};
		_messageLabel.AddThemeFontSizeOverride("font_size", 24);
		AddChild(_messageLabel);

		GameSessionController.OnConnected += AttachToConnection;
		AttachToConnection();
	}

	public override void _ExitTree()
	{
		GameSessionController.OnConnected -= AttachToConnection;
		DetachFromConnection();
	}

	public override void _Process(double delta)
	{
		if (_remainingMessageTime <= 0f)
		{
			return;
		}

		_remainingMessageTime -= (float)delta;
		if (_remainingMessageTime <= 0f)
		{
			_messageLabel.Text = "";
		}
	}

	private void AttachToConnection()
	{
		var connection = GameSessionController.Conn;
		if (connection == null || connection == _conn)
		{
			return;
		}

		DetachFromConnection();
		_conn = connection;
		_conn.Db.GameEvent.OnInsert += GameEventOnInsert;
	}

	private void DetachFromConnection()
	{
		if (_conn == null)
		{
			return;
		}

		_conn.Db.GameEvent.OnInsert -= GameEventOnInsert;
		_conn = null;
	}

	private void GameEventOnInsert(EventContext context, GameEvent gameEvent)
	{
		_messageLabel.Text = gameEvent.Message;
		_messageLabel.Modulate = gameEvent.EventType switch
		{
			"delivery_completed" => Colors.Gold,
			"game_over" => Colors.OrangeRed,
			"topping_dropped" => Colors.OrangeRed,
			"topping_recovered" => Colors.LimeGreen,
			_ => Colors.White,
		};
		_remainingMessageTime = MessageDurationSeconds;
	}
}
