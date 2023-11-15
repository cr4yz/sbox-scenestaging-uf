
using Sandbox;

public sealed class ThirdPersonCamera : BaseComponent
{

	[Property]
	public GameObject Target { get; set; }
	[Property]
	public float Distance { get; set; } = 560; // inches is our scale
	[Property]
	public float Smoothing { get; set; } = 25f;

	private Vector3 _desiredPosition;
	private Vector2 _lookAngles;

	public override void Update()
	{
		if ( Target == null ) return;

		_lookAngles.x -= Mouse.Delta.x * .01f;
		_lookAngles.y += Mouse.Delta.y * .01f;
		_lookAngles.y = Math.Clamp( _lookAngles.y, -35, 60 ); // Limit vertical angle

		var rotation = Rotation.From( _lookAngles.y, _lookAngles.x, 0 );
		_desiredPosition = Target.Transform.Position - (rotation * Vector3.Forward * Distance);
		_desiredPosition += Vector3.Up * 250;

		Transform.Position = Vector3.Lerp( Transform.Position, _desiredPosition, Smoothing * Time.Delta );
		Transform.Rotation = Rotation.LookAt( Target.Transform.Position - Transform.Position );
	}

}
