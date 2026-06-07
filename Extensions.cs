using Godot;

namespace SpacetimeDB.Types
{
	public sealed partial class DbVector3
	{
		public static implicit operator Vector3(DbVector3 value)
			=> new(value.X, value.Y, value.Z);

		public static implicit operator DbVector3(Vector3 value)
			=> new(value.X, value.Y, value.Z);
	}
}
