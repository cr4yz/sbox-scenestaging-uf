using Sandbox;
using System;

public sealed class UnicycleController : BaseComponent
{

	[Property]
	public CameraComponent Camera { get; set; }
	[Property]
	public PhysicsComponent Rigidbody { get; set; }
	[Property]
	public AnimatedModelComponent Citizen { get; set; }
	[Property]
	public GameObject TiltRoot { get; set; }
	[Property]
	public float PedalStrength { get; set; } = 100.0f;
	[Property]
	public float BrakeStrength { get; set; } = 100.0f;
	[Property]
	public float TiltStrength { get; set; } = 100.0f;

	public bool Grounded { get; private set; }

	Vector3 velocitySmooth;
	Angles currentTilt = default;
	Angles tiltVelocity = default; // Rate of change of tilt

	public float SpringStiffness => 50f;
	public float DampingRatio => 1.5f;

	public override void OnEnabled()
	{
		base.OnEnabled();

		Citizen.SceneObject.AnimationGraph = AnimationGraph.Load( "models/citizen_unicycle_frenzy.vanmgrph" );
	}

	public override void Update()
	{
		var pos = Transform.Position;
		var mins = new Vector3( -16, 0, -16 );
		var maxs = new Vector3( 16, 48, 16 );
		var extents = ( maxs - mins ) / 2f;

		var tr = Scene.PhysicsWorld.Trace
			.Box( extents, pos, pos + Vector3.Down * 17.5f )
			.WithTag( "solid" )
			.Run();

		Grounded = tr.Hit;

		Log.Error( Grounded );

		if ( Grounded )
		{
			if ( Input.Pressed( "attack1" ) || Input.Pressed( "attack2" ) )
			{
				Rigidbody.Velocity += Camera.Transform.Rotation.Forward * PedalStrength;
			}

			if ( Input.Down( "run" ) )
			{
				var velocityChange = Math.Clamp( Rigidbody.Velocity.Length - BrakeStrength, 0, float.PositiveInfinity );
				var desiredVelocity = Rigidbody.Velocity.Normal * velocityChange;
				Rigidbody.Velocity = Vector3.SmoothDamp( Rigidbody.Velocity, desiredVelocity, ref velocitySmooth, 0.3f, RealTime.Delta );
			}
		}

		UpdateTilt();

		Transform.Position = Rigidbody.Transform.Position;

		if ( Grounded )
		{
			var dot = Vector3.Dot( Rigidbody.Velocity, tr.Normal );
			if ( dot < 0 ) Rigidbody.Velocity = ClipVelocity( Rigidbody.Velocity, tr.Normal );
			Rigidbody.Velocity = ClipVelocity( Rigidbody.Velocity, Camera.Transform.Rotation.Right );
		}

		if ( Rigidbody.Velocity.Length < 2f ) Rigidbody.AngularVelocity = Vector3.Zero;

		UpdateAnimation();
	}

	void UpdateAnimation()
	{
		Citizen.SetLookDirection( "aim_head", Camera.Transform.Rotation.Forward );
		Citizen.Set( "b_unicycle_enable_foot_ik", false );
	}

	void UpdateTilt()
	{
		var velocityTilt = CalculateTargetTiltBasedOnVelocity();
		var inputTilt = GetInputTilt();
		var targetTilt = velocityTilt + inputTilt;

		var springForce = (targetTilt - currentTilt) * SpringStiffness;
		var dampingForce = tiltVelocity * DampingRatio;
		var totalForce = springForce - dampingForce;

		tiltVelocity += totalForce * Time.Delta;
		currentTilt += tiltVelocity * Time.Delta;

		TiltRoot.Transform.Rotation = Rotation.From( Camera.Transform.Rotation.Angles().WithPitch( 0 ) + currentTilt );
	}


	Angles GetInputTilt()
	{
		Angles inputTilt = default;
		if ( Input.Down( "left" ) ) inputTilt += new Angles( 0, 0, -TiltStrength ) * Time.Delta;
		if ( Input.Down( "right" ) ) inputTilt += new Angles( 0, 0, TiltStrength ) * Time.Delta;
		if ( Input.Down( "backward" ) ) inputTilt += new Angles( -TiltStrength, 0, 0 ) * Time.Delta;
		if ( Input.Down( "forward" ) ) inputTilt += new Angles( TiltStrength, 0, 0 ) * Time.Delta;

		return inputTilt;
	}

	Angles CalculateTargetTiltBasedOnVelocity()
	{
		return default;
		float factor = 0.1f;
		Angles tilt = new Angles( Rigidbody.Velocity.x * factor, 0, Rigidbody.Velocity.z * factor );

		return tilt;
	}

	Angles SmoothTilt( Angles current, Angles target, float deltaTime )
	{
		float tiltSmoothFactor = 1.0f; 
		return Angles.Lerp( current, target, tiltSmoothFactor * deltaTime );
	}

	Vector3 ClipVelocity( Vector3 vel, Vector3 norm, float overbounce = 1.0f )
	{
		var backoff = Vector3.Dot( vel, norm ) * overbounce;
		var o = vel - (norm * backoff);

		var adjust = Vector3.Dot( o, norm );
		if ( adjust >= 1.0f ) return o;

		adjust = MathF.Min( adjust, -1.0f );
		o -= norm * adjust;

		return o;
	}

}
