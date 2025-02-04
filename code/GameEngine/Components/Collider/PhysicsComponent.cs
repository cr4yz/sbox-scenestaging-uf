﻿using Sandbox;
using Sandbox.Diagnostics;
using System;

[Title( "Rigid Body" )]
[Category( "Physics" )]
[Icon( "panorama_fish_eye", "red", "white" )]
public class PhysicsComponent : BaseComponent, INetworkSerializable
{
	[Property] public bool Gravity { get; set; } = true;

	PhysicsBody _body;
	CollisionEventSystem _collisionEvents;

	internal PhysicsBody GetBody()
	{
		return _body;
	}

	public Vector3 Velocity
	{
		get => _body.Velocity;
		set => _body.Velocity = value;
	}

	public Vector3 AngularVelocity
	{
		get => _body.AngularVelocity;
		set => _body.AngularVelocity = value;
	}

	protected override void OnEnabled()
	{
		Assert.True( _body == null );
		Assert.NotNull( Scene, "Tried to create physics object but no scene" );
		Assert.NotNull( Scene.PhysicsWorld, "Tried to create physics object but no physics world" );

		_body = new PhysicsBody( Scene.PhysicsWorld );
		
		_body.UseController = false;
		_body.BodyType = PhysicsBodyType.Dynamic;
		_body.GameObject = GameObject;
		_body.GravityEnabled = Gravity;
		_body.Sleeping = false;
		_body.Transform = Transform.World;

		_collisionEvents?.Dispose();
		_collisionEvents = new CollisionEventSystem( _body );

		Transform.OnTransformChanged += OnLocalTransformChanged;

		UpdateColliders();
	}

	protected override void OnDisabled()
	{
		Transform.OnTransformChanged -= OnLocalTransformChanged;

		_body.Remove();
		_body = null;

		UpdateColliders();
	}

	bool isUpdatingPositionFromPhysics;

	protected override void OnFixedUpdate()
	{
		if ( _body is null ) return;

		if ( GameObject.IsProxy )
			return; 

		_body.GravityEnabled = Gravity;

		var bt = _body.Transform;

		isUpdatingPositionFromPhysics = true;
		Transform.World = bt.WithScale( Transform.Scale.x );
		isUpdatingPositionFromPhysics = false;
	}

	protected override void OnUpdate()
	{
		if ( GameObject.IsProxy )
		{
			_body.Transform = Transform.World;
			return;
		}
	}

	void OnLocalTransformChanged()
	{
		if ( isUpdatingPositionFromPhysics ) return;

		if ( _body is not null )
		{
			_body.Transform = Transform.World;
		}
	}

	/// <summary>
	/// Tell child colliders that our physics have changed
	/// </summary>
	void UpdateColliders()
	{
		foreach( var c in GameObject.Components.GetAll<Collider>( FindMode.EnabledInSelfAndDescendants ) )
		{
			c.OnPhysicsChanged();
		}
	}

	public void Write( ref ByteStream stream )
	{
		stream.Write( _body.Velocity );
		stream.Write( _body.AngularVelocity );
	}

	public void Read( ByteStream stream )
	{
		_body.Velocity = stream.Read<Vector3>();
		_body.AngularVelocity = stream.Read<Vector3>();
	}
}
