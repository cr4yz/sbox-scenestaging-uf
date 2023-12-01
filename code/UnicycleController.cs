using Sandbox;
using System;

public sealed class UnicycleController : BaseComponent
{

	[Property]
	public CameraComponent Camera { get; set; }
	[Property]
	public PhysicsComponent Rigidbody { get; set; }
	[Property]
	public SkinnedModelRenderer Citizen { get; set; }
	[Property]
	public GameObject TiltRoot { get; set; }
	[Property]
	public float PedalStrength { get; set; } = 100.0f;
	[Property]
	public float BrakeStrength { get; set; } = 100.0f;
	[Property]
	public float TiltStrength { get; set; } = 100.0f;

	public float JumpDownTime { get; private set; }
	public bool Dead { get; private set; }
	public bool Grounded { get; private set; }

	float timeUntilRespawn;
	Vector3 velocitySmooth;
	Angles currentTilt = default;
	Angles tiltVelocity = default;
	Vector3 lastGroundNormal;

	public float SpringStiffness => 5f;
	public float DampingRatio => 2.5f;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		//Citizen.SceneObject.AnimationGraph = AnimationGraph.Load( "models/citizen_unicycle_frenzy.vanmgrph" );
	}

	protected override void OnUpdate()
	{
		if ( Dead )
		{
			TiltRoot.Enabled = false;
			Citizen.Enabled = false;
			Rigidbody.Velocity = 0;
			currentTilt = default;
			tiltVelocity = default;
			prevVelocity = default;

			if ( timeUntilRespawn > 0 )
			{
				timeUntilRespawn -= Time.Delta;

				if ( timeUntilRespawn <= 0 )
				{
					Respawn();
				}
			}
			return;
		}

		TiltRoot.Enabled = true;
		Citizen.Enabled = true;

		Citizen.SceneModel.AnimationGraph = AnimationGraph.Load( "models/citizen_unicycle_frenzy.vanmgrph" );

		var pos = Transform.Position;
		var mins = new Vector3( -2, 0, -2 );
		var maxs = new Vector3( 2, 48, 2 );
		var extents = (maxs - mins) / 2f;

		var tr = Scene.PhysicsWorld.Trace
			.Box( extents, pos + Vector3.Up * 4, pos + Vector3.Down * 8 )
			.WithTag( "solid" )
			.Run();

		Grounded = tr.Hit && tr.Normal.z > 0.1f/* && Rigidbody.Velocity.z < 260f*/;

		if ( Grounded )
		{
			var dir = Vector3.Dot( Rigidbody.Velocity, TiltRoot.Transform.Rotation.Forward ) >= 0;
			var pedalAdjust = Rigidbody.Velocity.Length * Time.Delta * .005f * (dir ? 1 : -1);
			pedalPosition += pedalAdjust;

			if ( dir && pedalPosition > 1 ) pedalPosition = 0;
			if ( !dir && pedalPosition < 0 ) pedalPosition = 1;

			if ( Input.Pressed( "attack1" ) || Input.Pressed( "attack2" ) )
			{
				var thing = TiltRoot.Transform.Rotation.Angles().WithPitch( 0 ).WithRoll( 0 );
				var fwd = Rotation.From( thing ).Forward;
				Rigidbody.Velocity += fwd * PedalStrength;
			}

			if ( Input.Down( "run" ) )
			{
				var velocityChange = Math.Clamp( Rigidbody.Velocity.Length - BrakeStrength, 0, float.PositiveInfinity );
				var desiredVelocity = Rigidbody.Velocity.Normal * velocityChange;
				Rigidbody.Velocity = Vector3.SmoothDamp( Rigidbody.Velocity, desiredVelocity, ref velocitySmooth, 0.3f, RealTime.Delta );
			}

			var dot = Vector3.Dot( Rigidbody.Velocity.Normal, tr.Normal );
			if ( lastGroundNormal != tr.Normal && dot < -.1f )
			{
				//Rigidbody.Velocity = ClipVelocity( Rigidbody.Velocity, tr.Normal );
			}
			var rot = Transform.Rotation.Angles().WithPitch( 0 );
			var right = Rotation.From( rot ).Right;
			Rigidbody.Velocity = ClipVelocity( Rigidbody.Velocity, right );

			lastGroundNormal = tr.Normal;
		}

		if ( Grounded && Rigidbody.Velocity.Length > 15f || ( !Grounded && Rigidbody.Velocity.WithZ( 0 ).Length < 15f ) )
		{
			var targetForward = Camera.Transform.Rotation.Angles().WithPitch( 0 ).ToRotation();
			Transform.Rotation = Rotation.Slerp( Transform.Rotation, targetForward, 2f * Time.Delta );
		}

		UpdateTilt();
		CheckForJump();

		if ( CheckForFall() )
		{
			Fall();
		}

		Transform.Position = Rigidbody.Transform.Position;

		if ( Rigidbody.Velocity.Length < 2f ) Rigidbody.AngularVelocity = Vector3.Zero;

		UpdateAnimation();
	}

	void UpdateTilt()
	{
		var velocityTilt = TiltFromVelocity(); // this is Angles struct (with pitch,yaw,roll)
		var inputTilt = GetInputTilt();// this is Angles (with pitch,yaw,roll)


		var targetTilt = velocityTilt + inputTilt;

		var springForce = (targetTilt - currentTilt) * SpringStiffness;
		var dampingForce = tiltVelocity * DampingRatio;
		var totalForce = springForce - dampingForce;

		tiltVelocity += totalForce * Time.Delta;
		currentTilt += tiltVelocity * Time.Delta;

		if ( Math.Abs( tiltVelocity.AsVector3().Length ) < .3f )
		{
			//tiltVelocity = default;
		}

		var smoothTilt = Angles.Lerp( currentTilt, targetTilt, 0.1f * Time.Delta );

		TiltRoot.Transform.Rotation = Rotation.From( Transform.Rotation.Angles() + smoothTilt );
	}

	bool CheckForFall()
	{
		if ( Grounded && MathF.Abs( currentTilt.pitch ) > 55 ) return true;
		if ( Grounded && MathF.Abs( currentTilt.roll ) > 35 ) return true;

		return false;
	}

	void CheckForJump()
	{
		if ( !Grounded )
		{
			JumpDownTime = 0;
			return;
		}

		if ( Input.Down( "jump" ) )
		{
			JumpDownTime += Time.Delta;
		}

		if ( Input.Released( "jump" ) )
		{
			var str = Math.Clamp( JumpDownTime / 0.75f, 0.5f, 1 );
			var dir = TiltRoot.Transform.Rotation.Up;
			Rigidbody.Velocity += dir * 350f * str;
			Grounded = false;
			JumpDownTime = 0;
		}
	}

	float currentBalanceX = 0.5f;
	float currentBalanceY = 0.5f;
	float slamPitch = 0f;
	float currentSlamPitch;
	float pedalPosition;
	void UpdateAnimation()
	{
		float normalizedPitch = (currentTilt.pitch / 45f) * 0.5f + 0.5f;
		float normalizedRoll = (currentTilt.roll / 45f) * 0.5f + 0.5f;

		if ( slamPitch > 0 )
		{
			slamPitch -= Time.Delta;

			if ( slamPitch < 0 ) slamPitch = 0;
		}

		currentSlamPitch = currentSlamPitch.LerpTo( slamPitch, 10f * Time.Delta );

		currentBalanceX = MathX.Lerp( currentBalanceX, normalizedPitch, Time.Delta * 4f );
		currentBalanceY = MathX.Lerp( currentBalanceY, normalizedRoll, Time.Delta * 4f );

		Citizen.SetLookDirection( "aim_head", Camera.Transform.Rotation.Forward );
		Citizen.Set( "b_unicycle_enable_foot_ik", false );
		Citizen.Set( "unicycle_balance_x", currentBalanceX + currentSlamPitch );
		Citizen.Set( "unicycle_balance_y", currentBalanceY );
		Citizen.Set( "unicycle_pedaling", pedalPosition );
	}


	Angles GetInputTilt()
	{
		Angles inputTilt = default;
		if ( Input.Down( "left" ) ) inputTilt += new Angles( 0, 0, -TiltStrength );
		if ( Input.Down( "right" ) ) inputTilt += new Angles( 0, 0, TiltStrength );
		if ( Input.Down( "backward" ) ) inputTilt += new Angles( -TiltStrength, 0, 0 );
		if ( Input.Down( "forward" ) ) inputTilt += new Angles( TiltStrength, 0, 0 );

		var stiffness = Math.Clamp( Rigidbody.Velocity.Length / 1200f, 0, 1f );

		return inputTilt * (1f - stiffness);
	}

	Vector3 prevVelocity = default;
	Angles TiltFromVelocity()
	{
		var localVelocity = TiltRoot.Transform.World.NormalToLocal( Rigidbody.Velocity );
		var prevLocalVelocity = TiltRoot.Transform.World.NormalToLocal( prevVelocity );

		var velocityChange = localVelocity - prevLocalVelocity;
		var velChange = velocityChange.Length;
		var zchange = Rigidbody.Velocity.z - prevVelocity.z;

		if ( zchange > 50 )
		{
			slamPitch = 0.5f;
		}

		prevVelocity = Rigidbody.Velocity;

		return new Angles( -velocityChange.x * 35f, 0, velocityChange.y * 5 );
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

	GameObject fuck;
	void Fall()
	{
		fuck?.Destroy();
		fuck = Scene.CreateObject();
		fuck.Transform.Position = Citizen.Transform.Position;
		fuck.Transform.Rotation = Citizen.Transform.Rotation;
		fuck.Transform.Scale = Citizen.Transform.Scale;

		var modelComponent = fuck.Components.Create<SkinnedModelRenderer>();
		modelComponent.Model = Citizen.Model;
		//modelComponent.SceneObject.UseAnimGraph = false;
		//modelComponent.CopyBonesFrom( Citizen.SceneObject );


		var phys = fuck.Components.Create<ModelPhysics>( false );
		phys.Model = Citizen.Model;
		phys.Renderer = modelComponent;
		phys.Enabled = true;

		Dead = true;
		timeUntilRespawn = 3f;
	}

	void Respawn()
	{
		fuck?.Destroy();
		fuck = null;

		Dead = false;
	}

}

public static class ModelExt
{
	public static void CopyBonesFrom( this SkinnedModelRenderer self, SceneModel from, float scale = 1.0f )
	{
		if ( self.Model.BoneCount != from.Model.BoneCount )
		{
			Log.Info( $"CopyBonesFrom: Bone count doesn't match - {self.Model.BoneCount} vs {from.Model.BoneCount}" );
			return;
		}

		for ( int i = 0; i < from.Model.BoneCount; i++ )
		{
			var tx = from.GetBoneWorldTransform( i );
			//self.SceneObject.SetBoneWorldTransform( i, tx );
		}
	}
}
