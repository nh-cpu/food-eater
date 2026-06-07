using System;
using SpacetimeDB;
using SpacetimeDB.Types;
using Godot;

public partial class GameSessionController : Node
{
	public static event Action OnConnected;
	public static event Action OnSubscriptionApplied;
	
	[Export]    
	private string ServerUrl { get; set; } = "http://localhost:3000";
	
	[Export]
	private string DatabaseName { get; set; } = "food-eater";

	[Export]
	private string DefaultPlayerName { get; set; } = "3Blave";

	private static GameSessionController Instance { get; set; }
	public static Identity LocalIdentity { get; private set; }
	public static DbConnection Conn { get; private set; }
	private bool UsePersistedToken => !IsLocalServerUrl(ServerUrl);

	public GameSessionController()
	{
		var builder = DbConnection.Builder()
			.OnConnect(HandleConnect)
			.OnConnectError(HandleConnectError)
			.OnDisconnect(HandleDisconnect)
			.WithUri(ServerUrl)
			.WithDatabaseName(DatabaseName);

		if (UsePersistedToken && AuthToken.TryGetToken(out var authToken))
		{
			builder = builder.WithToken(authToken);
		}

		Conn = builder.Build();
		STDBUpdateManager.Add(Conn);
	}

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _ExitTree()
	{
		Disconnect();

		if (Instance == this)
		{
			Instance = null;
		}
	}

	public static bool IsConnected() => Conn != null && Conn.IsActive;

	private void Disconnect()
	{
		STDBUpdateManager.Remove(Conn, true);
		Conn = null;
	}

	// Called when we connect to SpacetimeDB and receive our client identity
private void HandleConnect(DbConnection conn, Identity identity, string token)
{
	GD.Print("Connected.");
	if (UsePersistedToken)
	{
		AuthToken.SaveToken(token);
	}
	LocalIdentity = identity;

	OnConnected?.Invoke();

	AddChild(new NetworkEntitySpawner(conn));

	// Request all tables
	Conn.SubscriptionBuilder()
		.OnApplied(HandleSubscriptionApplied)
		.SubscribeToAllTables();
}
	private void HandleConnectError(Exception ex)
	{
		GD.PrintErr($"Connection error: {ex}");
	}

	private void HandleDisconnect(DbConnection _conn, Exception ex)
	{
		GD.Print("Disconnected.");
		if (ex != null)
		{
			GD.PrintErr(ex);
		}
	}

private void HandleSubscriptionApplied(SubscriptionEventContext ctx)
	{
		GD.Print("Subscription applied!");
		OnSubscriptionApplied?.Invoke();

		ctx.Reducers.EnterGame(DefaultPlayerName);
	}

	private static bool IsLocalServerUrl(string serverUrl)
	{
		if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri))
		{
			return false;
		}

		return uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
			|| uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);
	}
}
