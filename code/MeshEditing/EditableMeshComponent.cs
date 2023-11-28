
using Sandbox;
using System.Collections.Generic;

public class EditableMeshComponent : BaseComponent, BaseComponent.ExecuteInEditor
{

	[Property, HideInEditor]
	public EditableMesh Mesh { get; set; }

	Model model;
	PhysicsBody physicsBody;

	public override void OnEnabled()
	{
		base.OnEnabled();

		Mesh ??= EditableMesh.Cube( new Vector3( 128 ) );
		Mesh.UpdateMeshData();

		physicsBody = new( Scene.PhysicsWorld );
		model = Model.Builder
			.AddMesh( Mesh.Mesh )
			.Create();

		if ( !TryGetComponent<ModelComponent>( out var mr, false ) )
			mr = GameObject.AddComponent<ModelComponent>();

		mr.Model = model;

		Mesh.OnMeshChanged = GenerateCollisionMesh;
		GenerateCollisionMesh();
	}

	void GenerateCollisionMesh()
	{
		physicsBody.EnableTraceAndQueries = true;
		physicsBody.ClearShapes();
		physicsBody.Transform = Transform.World;
		var shape = physicsBody.AddMeshShape( Mesh.Vertexes.Select( x => x.Vertex.Position ).ToList(), Mesh.Indices );
		shape.Tags.SetFrom( GameObject.Tags );
	}

	public override void OnDisabled()
	{
		base.OnDisabled();

		physicsBody?.Remove();
		physicsBody = null;
	}

	Dictionary<int, object> hack;
	public override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( Mesh == null ) return;

		if ( Mesh.Selection.Any() )
		{
			var center = Mesh.GetSelectionCenter();
			using ( Gizmo.Scope( "Position", center ) )
			{
				if ( Gizmo.Control.Position( "vertmove", center, out var newPos ) )
				{
					Mesh.TranslateSelection( newPos - center );
				}
			}
		}

		hack ??= new();

		foreach ( var part in Mesh.Parts )
		{
			var das = HashCode.Combine( part.A, part.B, part.C, part.D );

			if ( !hack.ContainsKey( das ) )
			{
				hack[das] = new object();
			}

			using ( Gizmo.Scope( $"part-{das}" ) )
			{
				Gizmo.Object = hack[das];

				switch ( part.Type )
				{
					case MeshPartTypes.Face:
						var posA = Mesh.Positions[Mesh.Vertexes[part.A].PositionIndex];
						var posB = Mesh.Positions[Mesh.Vertexes[part.B].PositionIndex];
						var posC = Mesh.Positions[Mesh.Vertexes[part.C].PositionIndex];
						var posD = Mesh.Positions[Mesh.Vertexes[part.D].PositionIndex];

						var center = (posA + posB + posC + posD) / 4;
						var box = new BBox( center, 5f );

						Gizmo.Draw.Color = Color.White;
						Gizmo.Draw.SolidBox( box );

						Gizmo.Hitbox.BBox( box );
						Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.White;
						Gizmo.Draw.Color = Gizmo.IsSelected ? Color.Green : Gizmo.Draw.Color;
						Gizmo.Draw.SolidBox( box );
						break;
					case MeshPartTypes.Vertex:
						var pos = Mesh.Positions[Mesh.Vertexes[part.A].PositionIndex];

						var vertbox = new BBox()
						{
							Mins = pos - 2,
							Maxs = pos + 2
						};

						Gizmo.Hitbox.BBox( vertbox );
						Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.White;
						Gizmo.Draw.Color = Gizmo.IsSelected ? Color.Green : Gizmo.Draw.Color;
						Gizmo.Draw.SolidBox( vertbox );
						break;
					case MeshPartTypes.Edge:
						var edgeA = Mesh.Positions[Mesh.Vertexes[part.A].PositionIndex];
						var edgeB = Mesh.Positions[Mesh.Vertexes[part.B].PositionIndex];

						using ( Gizmo.Hitbox.LineScope() )
						{
							Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.White;
							Gizmo.Draw.Color = Gizmo.IsSelected ? Color.Green : Gizmo.Draw.Color;
							Gizmo.Draw.Line( edgeA, edgeB );
						}

						break;
				}

				if ( Gizmo.IsPressed )
				{
					Gizmo.Select();
				}

				if ( Gizmo.IsPressed && Gizmo.HasPressed && part.Type == MeshPartTypes.Face )
				{
					Mesh.ExtrudeSelection( 64f );
				}

				part.Selected = Gizmo.IsSelected;
			}
		}
	}

}
