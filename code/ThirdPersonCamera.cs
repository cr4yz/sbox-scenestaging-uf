
using Sandbox;

public sealed class ThirdPersonCamera : BaseComponent
{

	[Property]
	public GameObject Target { get; set; }
	[Property]
	public float Distance { get; set; } = 260; 

	private Vector2 _lookAngles;

	protected override void OnUpdate()
	{
		if ( Target == null ) return;

		_lookAngles.x -= Mouse.Delta.x * .01f;
		_lookAngles.y += Mouse.Delta.y * .01f;
		_lookAngles.y = Math.Clamp( _lookAngles.y, -35, 65 );

		var targetRot = Rotation.From( _lookAngles.y, _lookAngles.x, 0 );
		var center = Target.Transform.Position + Vector3.Up * 40;
		var targetPos = center + targetRot.Forward * -Distance;

		Transform.Position = targetPos;
		Transform.Rotation = targetRot;
	}

}
