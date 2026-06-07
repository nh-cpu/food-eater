using System.Collections.Generic;
using Godot;
using SpacetimeDB.Types;

public partial class NetworkEntitySpawner : Node3D
{
	private readonly Dictionary<int, PlayerController3D> _players = new();
	private readonly Dictionary<int, SandwichController> _sandwiches = new();
	private readonly Dictionary<int, ToppingController> _toppings = new();
	private DbConnection _conn;

	public NetworkEntitySpawner(DbConnection conn)
	{
		_conn = conn;
	}

	public override void _EnterTree()
	{
		_conn.Db.Player.OnInsert += PlayerOnInsert;
		_conn.Db.Player.OnUpdate += PlayerOnUpdate;
		_conn.Db.Player.OnDelete += PlayerOnDelete;

		_conn.Db.Sandwich.OnInsert += SandwichOnInsert;
		_conn.Db.Sandwich.OnUpdate += SandwichOnUpdate;
		_conn.Db.Sandwich.OnDelete += SandwichOnDelete;

		_conn.Db.Topping.OnInsert += ToppingOnInsert;
		_conn.Db.Topping.OnUpdate += ToppingOnUpdate;
		_conn.Db.Topping.OnDelete += ToppingOnDelete;
	}

	public override void _ExitTree()
	{
		if (_conn == null)
		{
			return;
		}

		_conn.Db.Player.OnInsert -= PlayerOnInsert;
		_conn.Db.Player.OnUpdate -= PlayerOnUpdate;
		_conn.Db.Player.OnDelete -= PlayerOnDelete;

		_conn.Db.Sandwich.OnInsert -= SandwichOnInsert;
		_conn.Db.Sandwich.OnUpdate -= SandwichOnUpdate;
		_conn.Db.Sandwich.OnDelete -= SandwichOnDelete;

		_conn.Db.Topping.OnInsert -= ToppingOnInsert;
		_conn.Db.Topping.OnUpdate -= ToppingOnUpdate;
		_conn.Db.Topping.OnDelete -= ToppingOnDelete;

		_conn = null;
	}

	private void PlayerOnInsert(EventContext context, Player player)
	{
		if (_players.ContainsKey(player.PlayerId))
		{
			return;
		}

		var controller = new PlayerController3D(player);
		_players.Add(player.PlayerId, controller);
		AddChild(controller);
	}

	private void PlayerOnUpdate(EventContext context, Player oldPlayer, Player newPlayer)
	{
		if (_players.TryGetValue(newPlayer.PlayerId, out var controller))
		{
			controller.ApplyNetworkState(newPlayer);
			return;
		}

		PlayerOnInsert(context, newPlayer);
	}

	private void PlayerOnDelete(EventContext context, Player player)
	{
		if (_players.Remove(player.PlayerId, out var controller))
		{
			controller.QueueFree();
		}
	}

	private void SandwichOnInsert(EventContext context, Sandwich sandwich)
	{
		if (_sandwiches.ContainsKey(sandwich.Id))
		{
			return;
		}

		var controller = new SandwichController(sandwich);
		_sandwiches.Add(sandwich.Id, controller);
		AddChild(controller);
	}

	private void SandwichOnUpdate(EventContext context, Sandwich oldSandwich, Sandwich newSandwich)
	{
		if (_sandwiches.TryGetValue(newSandwich.Id, out var controller))
		{
			controller.ApplyNetworkState(newSandwich);
			return;
		}

		SandwichOnInsert(context, newSandwich);
	}

	private void SandwichOnDelete(EventContext context, Sandwich sandwich)
	{
		if (_sandwiches.Remove(sandwich.Id, out var controller))
		{
			controller.QueueFree();
		}
	}

	private void ToppingOnInsert(EventContext context, Topping topping)
	{
		if (_toppings.ContainsKey(topping.ToppingId))
		{
			return;
		}

		var controller = new ToppingController(topping);
		_toppings.Add(topping.ToppingId, controller);
		AddChild(controller);
	}

	private void ToppingOnUpdate(EventContext context, Topping oldTopping, Topping newTopping)
	{
		if (_toppings.TryGetValue(newTopping.ToppingId, out var controller))
		{
			controller.ApplyNetworkState(newTopping);
			return;
		}

		ToppingOnInsert(context, newTopping);
	}

	private void ToppingOnDelete(EventContext context, Topping topping)
	{
		if (_toppings.Remove(topping.ToppingId, out var controller))
		{
			controller.QueueFree();
		}
	}
}
