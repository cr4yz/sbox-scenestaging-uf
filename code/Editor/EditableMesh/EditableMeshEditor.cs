
[CanEdit( typeof( TestShit ) )]
public class MeshPartEditor : Widget
{

	public MeshPartEditor( Widget parent = null, TestShit target = null ) : base( parent )
	{
		Layout = Layout.Column();
		Layout.Spacing = 4;
		Layout.Margin = 10;

		Layout.Add( new Label( target.Part.Type.ToString() ) );

		if ( target.Part.Type == MeshPartTypes.Face )
		{
			var extrudeButton = Layout.Add( new Button( "Extrude" ) );
			extrudeButton.Clicked = () => target.Mesh.ExtrudeSelection( 64 );
		}

		Layout.AddStretchCell( 1 );
	}

}

[CustomEditor( typeof( EditableMesh ) )]
public class EditableMeshEditor : ControlWidget
{

	public EditableMeshEditor( SerializedProperty property ) : base( property )
	{
		Layout = Layout.Column();
		Layout.Spacing = 2;

		//var button = Layout.Add( new Button( "Extrude Selection" ) );
		//button.Pressed = () =>
		//{
		//	var mesh = property.GetValue<EditableMesh>( null );
		//	if ( mesh == null ) return;

		//	mesh.ExtrudeSelection( 64f );
		//};
	}

}
