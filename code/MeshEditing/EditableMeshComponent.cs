
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

		physicsBody = new( Scene.PhysicsWorld );

		Mesh.OnMeshChanged = () =>
		{
			model = Model.Builder
				.AddMesh( Mesh.Mesh )
				.Create();

			if ( !TryGetComponent<ModelComponent>( out var mr, false ) )
				mr = GameObject.AddComponent<ModelComponent>();

			mr.Model = model;

			GenerateCollisionMesh();
		};

		Mesh.UpdateMeshData();
	}

	void GenerateCollisionMesh()
	{
		physicsBody.EnableTraceAndQueries = true;
		physicsBody.ClearShapes();
		physicsBody.Transform = Transform.World;
		var shape = physicsBody.AddMeshShape( Mesh.Vertexes.Select( x => x.Position ).ToList(), Mesh.Indices );
		shape.Tags.SetFrom( GameObject.Tags );
	}

	public override void OnDisabled()
	{
		base.OnDisabled();

		physicsBody?.Remove();
		physicsBody = null;
	}

	Dictionary<int, TestShit> hack;
	public override void DrawGizmos()
	{
		base.DrawGizmos();

		if ( Mesh == null ) return;

		if ( Mesh.Selection.Any() )
		{
			var center = Mesh.CalculateCenter( Mesh.Selection );
			using ( Gizmo.Scope( "position", center ) )
			{
				if ( Gizmo.Control.Position( "position", center, out var newPos, null, 0f ) )
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
				hack[das] = new TestShit();
			}

			hack[das].Part = part;
			hack[das].Mesh = Mesh;

			using ( Gizmo.ObjectScope( hack[das], new Transform( 0, Rotation.Identity, 1 ) ) )
			{
				switch ( part.Type )
				{
					case MeshPartTypes.Face:
						var posA = Mesh.Vertexes[part.A].Position;
						var posB = Mesh.Vertexes[part.B].Position;
						var posC = Mesh.Vertexes[part.C].Position;
						var posD = Mesh.Vertexes[part.D].Position;

						var center = (posA + posB + posC + posD) / 4;
						var box = new BBox( center, 8f );
						var end = center + Mesh.Vertexes[part.A].Normal * 20f;

						Gizmo.Draw.Color = Color.White;
						Gizmo.Draw.SolidBox( box );

						Gizmo.Hitbox.BBox( box );
						Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.White;
						Gizmo.Draw.Color = Gizmo.IsSelected ? Color.Green : Gizmo.Draw.Color;
						Gizmo.Draw.SolidBox( box );

						break;
					case MeshPartTypes.Vertex:
						var pos = Mesh.Vertexes[part.A].Position;

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
						var edgeA = Mesh.Vertexes[part.A].Position;
						var edgeB = Mesh.Vertexes[part.B].Position;

						using ( Gizmo.Hitbox.LineScope() )
						{
							Gizmo.Draw.Color = Gizmo.IsHovered ? Color.Yellow : Color.White;
							Gizmo.Draw.Color = part.Selected ? Color.Green : Gizmo.Draw.Color;
							Gizmo.Draw.Line( edgeA, edgeB );
						}

						break;
				}

				if ( Gizmo.IsPressed && Gizmo.HasClicked )
				{
					Gizmo.Select();
				}

				part.Selected = Gizmo.IsSelected;
			}
		}
	}

}

public class TestShit
{
	public MeshPart Part;
	public EditableMesh Mesh;
}
