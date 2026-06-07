using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Godot;

[Tool]
public partial class ToppingProfileExporter : EditorScript
{
	private const string SourceNodeName = "ToppingProfilesSource";
	private const string OutputPath = "res://Shared/ToppingShapeData.generated.cs";
	private const float MinimumToppingMass = 0.35f;
	private const float ToppingVelocityRetention = 0.15f;
	private const float ToppingEdgeGrip = 2.5f;

	public override void _Run()
	{
		var sceneRoot = EditorInterface.Singleton.GetEditedSceneRoot();
		if (sceneRoot == null)
		{
			GD.PushError("No edited scene root found.");
			return;
		}

		var sourceRoot = sceneRoot.FindChild(SourceNodeName, true, false) as Node3D;
		if (sourceRoot == null)
		{
			GD.PushError($"Could not find a Node3D named '{SourceNodeName}' in the edited scene.");
			return;
		}

		var groupedProfiles = new Dictionary<string, List<ToppingShapeProfile>>(StringComparer.OrdinalIgnoreCase);
		foreach (var child in sourceRoot.GetChildren())
		{
			if (child is not Node3D toppingRoot)
			{
				continue;
			}

			if (!TryBuildProfile(toppingRoot, out var profile))
			{
				continue;
			}

			var canonicalName = CanonicalizeProfileName(profile.Name);
			if (!groupedProfiles.TryGetValue(canonicalName, out var profileGroup))
			{
				profileGroup = new List<ToppingShapeProfile>();
				groupedProfiles.Add(canonicalName, profileGroup);
			}

			profileGroup.Add(profile with { Name = canonicalName });
		}

		if (groupedProfiles.Count == 0)
		{
			GD.PushError($"'{SourceNodeName}' does not contain any child topping roots with meshes.");
			return;
		}

		var profiles = new List<ToppingShapeProfile>(groupedProfiles.Count);
		foreach (var group in groupedProfiles)
		{
			profiles.Add(AggregateProfiles(group.Key, group.Value));
		}

		var output = BuildGeneratedFile(profiles);
		var absolutePath = ProjectSettings.GlobalizePath(OutputPath);
		Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
		File.WriteAllText(absolutePath, output, Encoding.UTF8);
		GD.Print($"Topping shape data exported to {OutputPath}");
	}

	private static bool TryBuildProfile(Node3D toppingRoot, out ToppingShapeProfile profile)
	{
		var meshes = new List<MeshInstance3D>();
		CollectMeshNodes(toppingRoot, meshes);
		if (meshes.Count == 0)
		{
			profile = default;
			return false;
		}

		var hasBounds = false;
		var min = Vector3.Zero;
		var max = Vector3.Zero;

		var rootInverse = toppingRoot.GlobalTransform.AffineInverse();
		foreach (var meshNode in meshes)
		{
			if (meshNode.Mesh == null)
			{
				continue;
			}

			foreach (var localPoint in GetRootLocalAabbCorners(meshNode, rootInverse))
			{
				if (!hasBounds)
				{
					min = localPoint;
					max = localPoint;
					hasBounds = true;
					continue;
				}

				min = new Vector3(
					MathF.Min(min.X, localPoint.X),
					MathF.Min(min.Y, localPoint.Y),
					MathF.Min(min.Z, localPoint.Z)
				);
				max = new Vector3(
					MathF.Max(max.X, localPoint.X),
					MathF.Max(max.Y, localPoint.Y),
					MathF.Max(max.Z, localPoint.Z)
				);
			}
		}

		if (!hasBounds)
		{
			profile = default;
			return false;
		}

		var size = max - min;
		var halfWidth = MathF.Max(0.05f, MathF.Max(MathF.Abs(min.X), MathF.Abs(max.X)));
		var halfDepth = MathF.Max(0.05f, MathF.Max(MathF.Abs(min.Z), MathF.Abs(max.Z)));
		var minY = min.Y;
		var maxY = MathF.Max(minY + 0.04f, max.Y);
		var footprint = MathF.Max(0.01f, size.X * size.Z);
		var mass = MathF.Max(MinimumToppingMass, footprint * (maxY - minY) * 2.5f);
		var friction = ToppingVelocityRetention;
		var edgeGrip = ToppingEdgeGrip;

		profile = new ToppingShapeProfile(
			toppingRoot.Name,
			halfWidth,
			halfDepth,
			minY,
			maxY,
			mass,
			friction,
			edgeGrip
		);
		return true;
	}

	private static void CollectMeshNodes(Node node, List<MeshInstance3D> meshes)
	{
		if (node is MeshInstance3D meshNode)
		{
			meshes.Add(meshNode);
		}

		foreach (var child in node.GetChildren())
		{
			if (child is Node childNode)
			{
				CollectMeshNodes(childNode, meshes);
			}
		}
	}

	private static IEnumerable<Vector3> GetRootLocalAabbCorners(MeshInstance3D meshNode, Transform3D rootInverse)
	{
		var meshAabb = meshNode.Mesh.GetAabb();
		var meshTransform = meshNode.GlobalTransform;
		var min = meshAabb.Position;
		var max = meshAabb.End;

		yield return rootInverse * (meshTransform * new Vector3(min.X, min.Y, min.Z));
		yield return rootInverse * (meshTransform * new Vector3(max.X, min.Y, min.Z));
		yield return rootInverse * (meshTransform * new Vector3(min.X, max.Y, min.Z));
		yield return rootInverse * (meshTransform * new Vector3(max.X, max.Y, min.Z));
		yield return rootInverse * (meshTransform * new Vector3(min.X, min.Y, max.Z));
		yield return rootInverse * (meshTransform * new Vector3(max.X, min.Y, max.Z));
		yield return rootInverse * (meshTransform * new Vector3(min.X, max.Y, max.Z));
		yield return rootInverse * (meshTransform * new Vector3(max.X, max.Y, max.Z));
	}

	private static string BuildGeneratedFile(List<ToppingShapeProfile> profiles)
	{
		var builder = new StringBuilder();
		builder.AppendLine("// Auto-generated by ToppingProfileExporter.cs");
		builder.AppendLine("public static partial class ToppingShapeData");
		builder.AppendLine("{");
		builder.AppendLine("    public static ToppingShapeProfile GetProfile(string name)");
		builder.AppendLine("        => name switch");
		builder.AppendLine("        {");

		foreach (var profile in profiles)
		{
			builder.AppendLine(
				$"            \"{profile.Name}\" => new ToppingShapeProfile(\"{profile.Name}\", {Format(profile.HalfWidth)}f, {Format(profile.HalfDepth)}f, {Format(profile.MinY)}f, {Format(profile.MaxY)}f, {Format(profile.Mass)}f, {Format(profile.Friction)}f, {Format(profile.EdgeGrip)}f),"
			);
		}

		builder.AppendLine("            _ => Default,");
		builder.AppendLine("        };");
		builder.AppendLine("}");
		return builder.ToString();
	}

	private static string Format(float value)
		=> value.ToString("0.######", CultureInfo.InvariantCulture);

	private static string CanonicalizeProfileName(string name)
	{
		if (string.IsNullOrWhiteSpace(name))
		{
			return name;
		}

		var end = name.Length;
		while (end > 0 && char.IsDigit(name[end - 1]))
		{
			end--;
		}

		while (end > 0)
		{
			var trailing = name[end - 1];
			if (trailing == ' ' || trailing == '_' || trailing == '-')
			{
				end--;
				continue;
			}

			break;
		}

		return end > 0 ? name[..end] : name;
	}

	private static ToppingShapeProfile AggregateProfiles(string canonicalName, List<ToppingShapeProfile> profiles)
	{
		if (profiles.Count == 1)
		{
			return profiles[0] with { Name = canonicalName };
		}

		var halfWidth = 0f;
		var halfDepth = 0f;
		var minY = 0f;
		var maxY = 0f;
		var mass = 0f;
		var friction = 0f;
		var edgeGrip = 0f;
		var initializedY = false;

		foreach (var profile in profiles)
		{
			halfWidth += profile.HalfWidth;
			halfDepth += profile.HalfDepth;
			if (!initializedY)
			{
				minY = profile.MinY;
				maxY = profile.MaxY;
				initializedY = true;
			}
			else
			{
				minY = MathF.Min(minY, profile.MinY);
				maxY = MathF.Max(maxY, profile.MaxY);
			}
			mass += profile.Mass;
			friction += profile.Friction;
			edgeGrip += profile.EdgeGrip;
		}

		var count = profiles.Count;
		return new ToppingShapeProfile(
			canonicalName,
			halfWidth / count,
			halfDepth / count,
			minY,
			maxY,
			mass / count,
			friction / count,
			edgeGrip / count
		);
	}
}
