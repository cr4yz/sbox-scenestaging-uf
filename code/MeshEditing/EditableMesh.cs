
using Sandbox;
using System.Collections.Generic;

public class EditableMesh
{

	public List<Vector3> Positions { get; set; }
	public List<SimpleVertexWrapper> Vertexes { get; set; }
	public List<int> Indices { get; set; }

	List<MeshPart> parts = new();

	public IEnumerable<MeshPart> Parts => parts;
	public IEnumerable<MeshPart> Selection => parts.Where( x => x.Selected );

	public Mesh Mesh { get; private set; }

	public Action OnMeshChanged;

	public Vector3 GetSelectionCenter()
	{
		Vector3 center = default;
		int count = default;

		foreach ( var part in Selection )
		{
			switch ( part.Type )
			{
				case MeshPartTypes.Face:
					center += Positions[Vertexes[part.A].PositionIndex];
					center += Positions[Vertexes[part.B].PositionIndex];
					center += Positions[Vertexes[part.C].PositionIndex];
					center += Positions[Vertexes[part.D].PositionIndex];
					count += 4;
					break;
				case MeshPartTypes.Vertex:
					center += Positions[Vertexes[part.A].PositionIndex];
					count += 1;
					break;
				case MeshPartTypes.Edge:
					center += Positions[Vertexes[part.A].PositionIndex];
					center += Positions[Vertexes[part.B].PositionIndex];
					count += 2;
					break;
			}
		}

		if ( count == 0 ) return default;

		return center / count;
	}

	float timeSinceExtrude;
	public void ExtrudeSelection( float distance )
	{
		// I don't think what I've done makes this easy
	}

	public void TranslateSelection( Vector3 translation )
	{
		List<int> positions = new();

		foreach ( var part in Selection )
		{
			switch ( part.Type )
			{
				case MeshPartTypes.Face:
					positions.Add( Vertexes[part.A].PositionIndex );
					positions.Add( Vertexes[part.B].PositionIndex );
					positions.Add( Vertexes[part.C].PositionIndex );
					positions.Add( Vertexes[part.D].PositionIndex );
					break;
				case MeshPartTypes.Vertex:
					positions.Add( Vertexes[part.A].PositionIndex );
					break;
				case MeshPartTypes.Edge:
					positions.Add( Vertexes[part.A].PositionIndex );
					positions.Add( Vertexes[part.B].PositionIndex );
					break;
			}
		}

		if ( !positions.Any() ) return;

		foreach ( var pos in positions )
		{
			var newPosition = Positions[pos] + translation;
			UpdateVertexPosition( pos, newPosition );
		}

		UpdateMeshData();

		OnMeshChanged?.Invoke();
	}

	void UpdateVertexPosition( int positionIndex, Vector3 newPosition )
	{
		Positions[positionIndex] = newPosition;

		foreach ( var vertexWrapper in Vertexes )
		{
			if ( vertexWrapper.PositionIndex != positionIndex ) 
				continue;

			var vertex = vertexWrapper.Vertex;
			vertex.Position = newPosition;
			vertexWrapper.Vertex = vertex;
		}
	}

	public void UpdateMeshData()
	{
		if ( Mesh == null )
		{
			Mesh = new Mesh( Material.Load( "materials/dev/reflectivity_30.vmat" ) );
			Mesh.CreateVertexBuffer<SimpleVertex>( Vertexes.Count, SimpleVertex.Layout, Vertexes.Select( x => (SimpleVertex)x.Vertex ).ToArray() );
			Mesh.CreateIndexBuffer( Indices.Count, Indices.ToArray() );
		}
		else
		{
			Mesh.SetVertexBufferSize( Vertexes.Count );
			Mesh.SetIndexBufferSize( Indices.Count );

			Mesh.SetVertexBufferData( Vertexes.Select( v => (SimpleVertex)v.Vertex ).ToList(), 0 );
			Mesh.SetIndexBufferData( Indices.ToList(), 0 );
		}

		Mesh.Bounds = BBox.FromPoints( Positions );

		parts.Clear();
		parts.AddRange( FindQuads() );

		var edges = FindEdges();
		var distinctEdges = edges
			.GroupBy( edge => new { Min = Math.Min( Vertexes[edge.A].PositionIndex, Vertexes[edge.B].PositionIndex ), Max = Math.Max( Vertexes[edge.A].PositionIndex, Vertexes[edge.B].PositionIndex ) } )
			.Select( group => group.First() )
			.ToList();

		parts.AddRange( distinctEdges );

		var uniqueVerts = Vertexes.DistinctBy( x => x.PositionIndex );
		foreach ( var v in uniqueVerts )
		{
			parts.Add( new MeshPart()
			{
				A = v.PositionIndex,
				Type = MeshPartTypes.Vertex
			} );
		}
	}

	IEnumerable<MeshPart> FindQuads()
	{
		var sharedEdges = new Dictionary<(int, int), int>();
		var quadFaces = new List<MeshPart>();

		for ( int i = 0; i < Indices.Count; i += 3 )
		{
			if ( i + 2 >= Indices.Count ) break;

			int[] triangleIndices = { Indices[i], Indices[i + 1], Indices[i + 2] };

			for ( int j = 0; j < 3; j++ )
			{
				int indexA = triangleIndices[j];
				int indexB = triangleIndices[(j + 1) % 3];

				var edge = (indexA < indexB) ? (indexA, indexB) : (indexB, indexA);

				if ( sharedEdges.ContainsKey( edge ) )
				{
					int otherTriangleIndex = sharedEdges[edge];

					int[] otherTriangleIndices = { Indices[otherTriangleIndex], Indices[otherTriangleIndex + 1], Indices[otherTriangleIndex + 2] };
					int thirdVertex = otherTriangleIndices.FirstOrDefault( v => !triangleIndices.Contains( v ) );
					int fourthVertex = triangleIndices.FirstOrDefault( v => !otherTriangleIndices.Contains( v ) );

					quadFaces.Add( new MeshPart() { A = indexA, B = indexB, C = thirdVertex, D = fourthVertex, Type = MeshPartTypes.Face } );
				}
				else
				{
					sharedEdges[edge] = i;
				}
			}
		}

		return quadFaces;
	}

	IEnumerable<MeshPart> FindEdges()
	{
		var uniqueEdges = new HashSet<(int, int)>();

		for ( int i = 0; i < Indices.Count; i += 3 )
		{
			if ( i + 2 >= Indices.Count ) break;

			int[] triangleIndices = { Indices[i], Indices[i + 1], Indices[i + 2] };

			for ( int j = 0; j < 3; j++ )
			{
				int indexA = triangleIndices[j];
				int indexB = triangleIndices[(j + 1) % 3];

				var edge = (indexA < indexB) ? (indexA, indexB) : (indexB, indexA);

				if ( !uniqueEdges.Add( edge ) )
				{
					uniqueEdges.Remove( edge );
				}
			}
		}

		return uniqueEdges.Select( x => new MeshPart() { A = x.Item1, B = x.Item2, Type = MeshPartTypes.Edge } );
	}

	public static EditableMesh Cube( Vector3 size )
	{
		var result = new EditableMesh();
		result.Vertexes = new();
		result.Indices = new();
		result.Positions = new List<Vector3>()
		{
			new Vector3(-0.5f, -0.5f, 0.5f) * size,
			new Vector3(-0.5f, 0.5f, 0.5f) * size,
			new Vector3(0.5f, 0.5f, 0.5f) * size,
			new Vector3(0.5f, -0.5f, 0.5f) * size,
			new Vector3(-0.5f, -0.5f, -0.5f) * size,
			new Vector3(-0.5f, 0.5f, -0.5f) * size,
			new Vector3(0.5f, 0.5f, -0.5f) * size,
			new Vector3(0.5f, -0.5f, -0.5f) * size,
		};

		var faceIndices = new int[]
		{
				0, 1, 2, 3,
				7, 6, 5, 4,
				0, 4, 5, 1,
				1, 5, 6, 2,
				2, 6, 7, 3,
				3, 7, 4, 0,
		};

		var uAxis = new Vector3[]
		{
				Vector3.Forward,
				Vector3.Left,
				Vector3.Left,
				Vector3.Forward,
				Vector3.Right,
				Vector3.Backward,
		};

		var vAxis = new Vector3[]
		{
				Vector3.Left,
				Vector3.Forward,
				Vector3.Down,
				Vector3.Down,
				Vector3.Down,
				Vector3.Down,
		};

		for ( var i = 0; i < 6; ++i )
		{
			var tangent = uAxis[i];
			var binormal = vAxis[i];
			var normal = Vector3.Cross( tangent, binormal );

			for ( var j = 0; j < 4; ++j )
			{
				var vertexIndex = faceIndices[(i * 4) + j];
				var pos = result.Positions[vertexIndex];

				result.Vertexes.Add( new SimpleVertexWrapper()
				{
					Vertex = new()
					{
						Position = pos,
						Normal = normal,
						Tangent = tangent,
						Texcoord = Planar( pos / 32, uAxis[i], vAxis[i] )
					},
					PositionIndex = vertexIndex,
				} );
			}

			result.Indices.Add( i * 4 + 0 );
			result.Indices.Add( i * 4 + 2 );
			result.Indices.Add( i * 4 + 1 );
			result.Indices.Add( i * 4 + 2 );
			result.Indices.Add( i * 4 + 0 );
			result.Indices.Add( i * 4 + 3 );
		}

		result.UpdateMeshData();

		return result;
	}

	static Vector2 Planar( Vector3 pos, Vector3 uAxis, Vector3 vAxis )
	{
		return new Vector2()
		{
			x = Vector3.Dot( uAxis, pos ),
			y = Vector3.Dot( vAxis, pos )
		};
	}

}

public class MeshPart
{

	public int A, B, C, D;
	public bool Selected;
	public MeshPartTypes Type;

}

public enum MeshPartTypes
{
	Vertex,
	Face,
	Edge
}

public struct SimpleVertex_S
{
	public Vector3 Position { get; set; }
	public Vector3 Normal { get; set; }
	public Vector3 Tangent { get; set; }
	public Vector2 Texcoord { get; set; }

	public static explicit operator SimpleVertex( SimpleVertex_S vertex )
	{
		return new SimpleVertex
		{
			position = vertex.Position,
			normal = vertex.Normal,
			tangent = vertex.Tangent,
			texcoord = vertex.Texcoord
		};
	}

	public static explicit operator SimpleVertex_S( SimpleVertex vertex )
	{
		return new SimpleVertex_S()
		{
			Position = vertex.position,
			Normal = vertex.normal,
			Tangent = vertex.tangent,
			Texcoord = vertex.texcoord
		};
	}

}

public class SimpleVertexWrapper
{
	public SimpleVertex_S Vertex { get; set; }
	public int PositionIndex { get; set; }
}
