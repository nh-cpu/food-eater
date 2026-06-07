using System;
using System.Linq;
using SpacetimeDB;

[SpacetimeDB.Type]
public enum ToppingState
{
    Attached,
    Dropped,
    WaitingAtSummit,
    Placed,
}

public static partial class Module
{
    private const float TickSeconds = 0.05f;
    private const int SandwichId = 0;
    private const float PlayerCarryRadius = 2.25f;
    private const float SandwichCarryHeight = 2.6f;
    private const float StartRadiusFraction = 0.8f;
    private const float GroundedHeightTolerance = 0.05f;

    [Table(Accessor = "simulation_timer", Scheduled = nameof(Simulate), ScheduledAt = nameof(scheduled_at))]
    public partial struct SimulationTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;
        public ScheduleAt scheduled_at;
    }

    [Table(Accessor = "config", Public = true)]
    public partial struct Config
    {
        [PrimaryKey]
        public int id;
        public float world_radius;
        public float summit_height;
        public float sandwich_speed;
        public float gravity;
        public float recovery_distance;
        public float summit_distance;
        public float topping_drop_tilt;
        public float topping_drop_impact_speed;
        public float jump_impulse;
    }

    [Table(Accessor = "player", Public = true)]
    [Table(Accessor = "logged_out_player")]
    public partial struct Player
    {
        [PrimaryKey]
        public Identity identity;

        [Unique, AutoInc]
        public int player_id;

        public string name;
        public DbVector3 position;
        public DbVector3 attachment_offset;
        public DbVector3 input_direction;
        public bool jump_queued;
    }

    [Table(Accessor = "sandwich", Public = true)]
    public partial struct Sandwich
    {
        [PrimaryKey]
        public int id;
        public DbVector3 position;
        public DbVector3 velocity;
        public float tilt;
        public int attached_player_count;
        public bool at_summit;
        public bool completed;
        public ulong tick;
    }

    [Table(Accessor = "topping", Public = true)]
    public partial struct Topping
    {
        [PrimaryKey, AutoInc]
        public int topping_id;
        public string name;
        public int layer_order;
        public ToppingState state;
        public DbVector3 position;
        public DbVector3 velocity;
        public DbVector3 attached_offset;
        public int drop_count;
    }

    [Table(Accessor = "game_event", Public = true, Event = true)]
    public partial struct GameEvent
    {
        public Timestamp created_at;
        public string event_type;
        public int player_id;
        public int topping_id;
        public string message;
    }

    [Reducer(ReducerKind.Init)]
    public static void Init(ReducerContext ctx)
    {
        ctx.Db.config.Insert(new Config
        {
            id = 0,
            world_radius = TerrainHeightData.WorldRadius,
            summit_height = TerrainHeightData.SummitHeight,
            sandwich_speed = 5f,
            gravity = 12f,
            recovery_distance = 3f,
            summit_distance = 0f,
            topping_drop_tilt = 32f,
            topping_drop_impact_speed = 7f,
            jump_impulse = 8.5f,
        });

        ctx.Db.sandwich.Insert(CreateInitialSandwich(RequireConfig(ctx)));
        SeedToppings(ctx);

        ctx.Db.simulation_timer.Insert(new SimulationTimer
        {
            scheduled_at = new ScheduleAt.Interval(TimeSpan.FromMilliseconds(50)),
        });
    }

    [Reducer(ReducerKind.ClientConnected)]
    public static void Connect(ReducerContext ctx)
    {
        var loggedOut = ctx.Db.logged_out_player.identity.Find(ctx.Sender);
        if (loggedOut is not null)
        {
            ctx.Db.player.Insert(loggedOut.Value with
            {
                input_direction = DbVector3.Zero,
                jump_queued = false,
                position = ResolvePlayerPosition(
                    RequireSandwich(ctx),
                    loggedOut.Value.attachment_offset,
                    RequireConfig(ctx)
                ),
            });
            ctx.Db.logged_out_player.identity.Delete(ctx.Sender);
            return;
        }

        var config = RequireConfig(ctx);
        var player = ctx.Db.player.Insert(new Player
        {
            identity = ctx.Sender,
            name = "",
            position = ResolvePlayerPosition(RequireSandwich(ctx), DbVector3.Zero, config),
            attachment_offset = DbVector3.Zero,
            input_direction = DbVector3.Zero,
            jump_queued = false,
        });
        var attachmentOffset = AttachmentOffset(player.player_id);
        ctx.Db.player.identity.Update(player with
        {
            position = ResolvePlayerPosition(RequireSandwich(ctx), attachmentOffset, config),
            attachment_offset = attachmentOffset,
        });
    }

    [Reducer(ReducerKind.ClientDisconnected)]
    public static void Disconnect(ReducerContext ctx)
    {
        var player = RequirePlayer(ctx);
        ctx.Db.logged_out_player.Insert(player with
        {
            input_direction = DbVector3.Zero,
            jump_queued = false,
        });
        ctx.Db.player.identity.Delete(ctx.Sender);
    }

    [Reducer]
    public static void EnterGame(ReducerContext ctx, string name)
    {
        var player = RequirePlayer(ctx);
        var cleanName = name.Trim();
        if (cleanName.Length is < 1 or > 24)
        {
            throw new Exception("Player name must contain between 1 and 24 characters.");
        }

        ctx.Db.player.identity.Update(player with { name = cleanName });
    }

    [Reducer]
    public static void UpdatePlayerInput(ReducerContext ctx, DbVector3 direction, bool jumpRequested)
    {
        var player = RequirePlayer(ctx);
        if (!IsFinite(direction))
        {
            throw new Exception("Movement input must contain finite values.");
        }

        ctx.Db.player.identity.Update(player with
        {
            input_direction = ClampMagnitude(direction, 1f),
            jump_queued = player.jump_queued || jumpRequested,
        });
    }

    [Reducer]
    public static void TryRecoverTopping(ReducerContext ctx, int toppingId)
    {
        var player = RequirePlayer(ctx);
        var topping = ctx.Db.topping.topping_id.Find(toppingId)
            ?? throw new Exception("Topping not found.");
        var sandwich = RequireSandwich(ctx);
        var config = RequireConfig(ctx);

        if (topping.state != ToppingState.Dropped)
        {
            throw new Exception("Only dropped toppings can be recovered.");
        }

        if (DbVector3.Distance(player.position, topping.position) > config.recovery_distance)
        {
            throw new Exception("Player is too far from the topping.");
        }

        if (DbVector3.Distance(sandwich.position, topping.position) > config.recovery_distance * 2f)
        {
            throw new Exception("Bring the sandwich closer before recovering this topping.");
        }

        ctx.Db.topping.topping_id.Update(topping with
        {
            state = ToppingState.Attached,
            position = sandwich.position + topping.attached_offset,
            velocity = DbVector3.Zero,
        });
        Emit(ctx, "topping_recovered", player.player_id, topping.topping_id, $"{player.name} recovered {topping.name}.");
    }

    [Reducer]
    public static void ResetRun(ReducerContext ctx)
    {
        var player = RequirePlayer(ctx);
        if (string.IsNullOrWhiteSpace(player.name))
        {
            throw new Exception("Enter the game before resetting the run.");
        }

        foreach (var topping in ctx.Db.topping.Iter().ToList())
        {
            ctx.Db.topping.topping_id.Delete(topping.topping_id);
        }

        var config = RequireConfig(ctx);
        ctx.Db.sandwich.id.Update(CreateInitialSandwich(config));
        SeedToppings(ctx);

        foreach (var activePlayer in ctx.Db.player.Iter().ToList())
        {
            ctx.Db.player.identity.Update(activePlayer with
            {
                position = ResolvePlayerPosition(RequireSandwich(ctx), activePlayer.attachment_offset, config),
                input_direction = DbVector3.Zero,
                jump_queued = false,
            });
        }

        Emit(ctx, "run_reset", player.player_id, 0, $"{player.name} reset the delivery.");
    }

    [Reducer]
    public static void Simulate(ReducerContext ctx, SimulationTimer _timer)
    {
        var config = RequireConfig(ctx);
        var sandwich = RequireSandwich(ctx);
        var players = ctx.Db.player.Iter().ToList();
        var impacted = false;

        if (!sandwich.completed)
        {
            var previousGroundHeight = TerrainHeight(sandwich.position, config) + SandwichCarryHeight;
            var wasGrounded = sandwich.position.y <= previousGroundHeight + GroundedHeightTolerance;

            if (players.Count > 0)
            {
                var averageInput = DbVector3.Zero;
                foreach (var player in players)
                {
                    averageInput += player.input_direction;
                }
                averageInput /= players.Count;

                var disagreement = 0f;
                foreach (var player in players)
                {
                    disagreement += DbVector3.Distance(player.input_direction, averageInput);
                }
                disagreement /= players.Count;

                var horizontalInput = new DbVector3(averageInput.x, 0f, averageInput.z);
                var targetVelocity = ClampMagnitude(horizontalInput, 1f) * config.sandwich_speed;
                sandwich.velocity = DbVector3.Lerp(
                    new DbVector3(sandwich.velocity.x, 0f, sandwich.velocity.z),
                    targetVelocity,
                    0.35f
                );
                sandwich.tilt = Lerp(sandwich.tilt, disagreement * 45f, 0.2f);
            }
            else
            {
                sandwich.velocity = new DbVector3(
                    sandwich.velocity.x * 0.9f,
                    sandwich.velocity.y,
                    sandwich.velocity.z * 0.9f
                );
                sandwich.tilt = Lerp(sandwich.tilt, 0f, 0.1f);
            }

            sandwich.position += new DbVector3(sandwich.velocity.x, 0f, sandwich.velocity.z) * TickSeconds;
            sandwich.position = ClampToTerrainBounds(sandwich.position, config);
            var targetSandwichHeight = TerrainHeight(sandwich.position, config) + SandwichCarryHeight;
            var jumpRequested = players.Any(player => player.jump_queued);

            if (jumpRequested && wasGrounded)
            {
                sandwich.velocity.y = config.jump_impulse;
            }

            if (wasGrounded && !jumpRequested)
            {
                sandwich.position.y = targetSandwichHeight;
                sandwich.velocity.y = 0f;
            }
            else
            {
                sandwich.velocity.y -= config.gravity * TickSeconds;
                sandwich.position.y += sandwich.velocity.y * TickSeconds;

                if (sandwich.position.y <= targetSandwichHeight)
                {
                    impacted = sandwich.velocity.y < -config.topping_drop_impact_speed;
                    sandwich.position.y = targetSandwichHeight;
                    sandwich.velocity.y = 0f;
                }
            }

            sandwich.attached_player_count = players.Count;
            sandwich.tick++;

            if (impacted || sandwich.tilt >= config.topping_drop_tilt)
            {
                DropTopTopping(ctx, sandwich);
                sandwich.tilt *= 0.45f;
            }

            sandwich.at_summit = false;
            sandwich.completed = false;
        }

        ClearJumpQueues(ctx, players);
        ctx.Db.sandwich.id.Update(sandwich);
        UpdateAttachedPlayerPositions(ctx, sandwich, config);
        UpdateAttachedToppingPositions(ctx, sandwich);
        UpdateDroppedToppings(ctx, config);
    }

    private static Config RequireConfig(ReducerContext ctx)
        => ctx.Db.config.id.Find(0) ?? throw new Exception("Config not found.");

    private static Sandwich RequireSandwich(ReducerContext ctx)
        => ctx.Db.sandwich.id.Find(SandwichId) ?? throw new Exception("Sandwich not found.");

    private static Player RequirePlayer(ReducerContext ctx)
        => ctx.Db.player.identity.Find(ctx.Sender) ?? throw new Exception("Player not found.");

    private static Sandwich CreateInitialSandwich(Config config)
    {
        var horizontalPosition = new DbVector3(TerrainHeightData.CenterX, 0f, TerrainHeightData.CenterZ);
        return new Sandwich
        {
            id = SandwichId,
            position = horizontalPosition + new DbVector3(
                0f,
                TerrainHeight(horizontalPosition, config) + SandwichCarryHeight,
                0f
            ),
            velocity = DbVector3.Zero,
        };
    }

    private static void SeedToppings(ReducerContext ctx)
    {
        InsertTopping(ctx, "Bottom Bread", 0, new DbVector3(0f, 0f, 0f), ToppingState.Attached);
        InsertTopping(ctx, "Lettuce", 1, new DbVector3(0f, 0.35f, 0f), ToppingState.Attached);
        InsertTopping(ctx, "Tomato", 2, new DbVector3(0f, 0.65f, 0f), ToppingState.Attached);
        InsertTopping(ctx, "Cheese", 3, new DbVector3(0f, 0.95f, 0f), ToppingState.Attached);
        InsertTopping(ctx, "Top Bread", 4, new DbVector3(0f, 1.3f, 0f), ToppingState.Attached);
    }

    private static void InsertTopping(
        ReducerContext ctx,
        string name,
        int layerOrder,
        DbVector3 offset,
        ToppingState state,
        DbVector3? position = null
    )
    {
        var sandwich = RequireSandwich(ctx);
        ctx.Db.topping.Insert(new Topping
        {
            name = name,
            layer_order = layerOrder,
            state = state,
            position = position ?? sandwich.position + offset,
            velocity = DbVector3.Zero,
            attached_offset = offset,
        });
    }

    private static void DropTopTopping(ReducerContext ctx, Sandwich sandwich)
    {
        var topping = ctx.Db.topping.Iter()
            .Where(row => row.state == ToppingState.Attached && row.layer_order > 0)
            .OrderByDescending(row => row.layer_order)
            .FirstOrDefault();

        if (topping.topping_id == 0)
        {
            return;
        }

        ctx.Db.topping.topping_id.Update(topping with
        {
            state = ToppingState.Dropped,
            position = sandwich.position + topping.attached_offset,
            velocity = sandwich.velocity,
            drop_count = topping.drop_count + 1,
        });
        Emit(ctx, "topping_dropped", 0, topping.topping_id, $"{topping.name} fell off the sandwich.");
    }

    private static void UpdateAttachedToppingPositions(ReducerContext ctx, Sandwich sandwich)
    {
        foreach (var topping in ctx.Db.topping.Iter().Where(row =>
            row.state is ToppingState.Attached or ToppingState.Placed
        ).ToList())
        {
            ctx.Db.topping.topping_id.Update(topping with
            {
                position = sandwich.position + topping.attached_offset,
            });
        }
    }

    private static void UpdateDroppedToppings(ReducerContext ctx, Config config)
    {
        foreach (var topping in ctx.Db.topping.Iter().Where(row => row.state == ToppingState.Dropped).ToList())
        {
            var velocity = topping.velocity;
            velocity.y -= config.gravity * TickSeconds;

            var position = topping.position + velocity * TickSeconds;
            var terrainHeight = TerrainHeight(position, config);
            if (position.y <= terrainHeight)
            {
                position.y = terrainHeight;
                velocity = DbVector3.Zero;
            }
            position = ClampToTerrainBounds(position, config);

            ctx.Db.topping.topping_id.Update(topping with
            {
                position = position,
                velocity = velocity,
            });
        }
    }

    private static void ClearJumpQueues(ReducerContext ctx, System.Collections.Generic.List<Player> players)
    {
        foreach (var player in players.Where(player => player.jump_queued))
        {
            ctx.Db.player.identity.Update(player with { jump_queued = false });
        }
    }

    private static void UpdateAttachedPlayerPositions(ReducerContext ctx, Sandwich sandwich, Config config)
    {
        foreach (var player in ctx.Db.player.Iter().ToList())
        {
            ctx.Db.player.identity.Update(player with
            {
                position = ResolvePlayerPosition(sandwich, player.attachment_offset, config),
            });
        }
    }

    private static DbVector3 AttachmentOffset(int index)
    {
        var angle = index * 2.3999632f;
        return new DbVector3(
            MathF.Cos(angle) * PlayerCarryRadius,
            0f,
            MathF.Sin(angle) * PlayerCarryRadius
        );
    }

    private static DbVector3 ClampMagnitude(DbVector3 value, float maximum)
    {
        var magnitude = value.Magnitude;
        return magnitude > maximum ? value / magnitude * maximum : value;
    }

    private static bool IsFinite(DbVector3 value)
        => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

    private static float Lerp(float from, float to, float weight) => from + (to - from) * weight;

    private static DbVector3 ClampToTerrainBounds(DbVector3 position, Config config)
    {
        position.x = Math.Clamp(position.x, TerrainHeightData.MinX, TerrainHeightData.MaxX);
        position.z = Math.Clamp(position.z, TerrainHeightData.MinZ, TerrainHeightData.MaxZ);

        position.y = Math.Clamp(position.y, 0f, config.summit_height + SandwichCarryHeight + 10f);
        return position;
    }

    private static float TerrainHeight(DbVector3 position, Config config)
        => TerrainHeightData.SampleHeight(position.x, position.z);

    private static DbVector3 ResolvePlayerPosition(
        Sandwich sandwich,
        DbVector3 attachmentOffset,
        Config config
    )
    {
        var horizontalPosition = new DbVector3(
            sandwich.position.x + attachmentOffset.x,
            0f,
            sandwich.position.z + attachmentOffset.z
        );
        horizontalPosition = ClampToTerrainBounds(horizontalPosition, config);
        var sandwichGroundHeight = TerrainHeight(sandwich.position, config) + SandwichCarryHeight;
        var airborneOffset = MathF.Max(0f, sandwich.position.y - sandwichGroundHeight);
        horizontalPosition.y = TerrainHeight(horizontalPosition, config) + airborneOffset;
        return horizontalPosition;
    }

    private static void Emit(
        ReducerContext ctx,
        string eventType,
        int playerId,
        int toppingId,
        string message
    )
    {
        ctx.Db.game_event.Insert(new GameEvent
        {
            created_at = ctx.Timestamp,
            event_type = eventType,
            player_id = playerId,
            topping_id = toppingId,
            message = message,
        });
    }
}
