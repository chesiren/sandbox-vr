using Sandbox.VR;

public sealed partial class VRPlayer
{
	/// <summary>
	/// How long a shift dash takes.
	/// </summary>
	[Property, Group( "VR" )] public float ShiftDuration { get; set; } = 0.15f;

	/// <summary>
	/// Launch speed of the teleport arc.
	/// </summary>
	[Property, Group( "VR" )] public float TeleportArcSpeed { get; set; } = 700f;

	/// <summary>
	/// Hand speed to walk speed, for the arm swinger.
	/// </summary>
	[Property, Group( "VR" )] public float ArmSwingScale { get; set; } = 2.5f;

	Vector2 _moveStick;
	bool _aimingTarget;
	Vector3? _target;
	LineRenderer _arc;

	Vector3 _shiftFrom;
	Vector3 _shiftTo;
	TimeSince _shiftTime;
	bool _shifting;


	void UpdateLocomotionInput( VRController off )
	{
		_moveStick = off.Joystick.Value;

		var settings = VRSettings.Current;
		var targeted = settings.Movement is VRSettings.MovementType.Teleport or VRSettings.MovementType.Shift;

		if ( !targeted || Player.IsNoclipping || Player.IsSeated )
		{
			_aimingTarget = false;
			_target = null;
			UpdateArc( null );
			return;
		}

		// Push the stick to aim, let go to go
		if ( _moveStick.Length > 0.6f )
		{
			_aimingTarget = true;
			_target = TraceTarget( off.AimTransform, out var points );
			UpdateArc( points );
		}
		else if ( _aimingTarget )
		{
			_aimingTarget = false;
			UpdateArc( null );

			if ( _target is { } target )
			{
				if ( settings.Movement == VRSettings.MovementType.Teleport )
				{
					MoveTo( target );
				}
				else
				{
					_shiftFrom = WorldPosition;
					_shiftTo = target;
					_shiftTime = 0;
					_shifting = true;
				}

				VRMode.Pulse( off, 0.3f );
			}

			_target = null;
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( !IsLocalVR ) return;
		if ( !Controller.IsValid() || !Controller.Mode.IsValid() ) return;

		Controller.UpdateDucking( WantsDuck );

		if ( _shifting )
		{
			var t = (_shiftTime / ShiftDuration).Clamp( 0f, 1f );
			MoveTo( Vector3.Lerp( _shiftFrom, _shiftTo, t ) );
			Controller.WishVelocity = 0;

			if ( t >= 1f ) _shifting = false;
			return;
		}

		Controller.WishVelocity = CalculateWishVelocity();
	}

	Vector3 CalculateWishVelocity()
	{
		var settings = VRSettings.Current;
		var off = VRMode.OffHand;
		var input = new Vector3( _moveStick.y, -_moveStick.x, 0 );
		if ( input.Length < 0.15f ) input = 0;

		var direction = settings.Direction == VRSettings.DirectionType.Hand
			? off.AimTransform.Rotation
			: Head.Rotation;

		if ( Player.IsNoclipping )
		{
			// Fly where you look or point, including up and down
			return Controller.Mode.UpdateMove( direction, input );
		}

		var flat = Rotation.FromYaw( direction.Yaw() );

		switch ( settings.Movement )
		{
			case VRSettings.MovementType.Continuous:
				return Controller.Mode.UpdateMove( flat, input );

			case VRSettings.MovementType.ArmSwinger:
			{
				// Hold A on either hand and swing that arm
				var speed = 0f;
				if ( Input.VR.LeftHand.ButtonA.IsPressed ) speed += Input.VR.LeftHand.Velocity.Length;
				if ( Input.VR.RightHand.ButtonA.IsPressed ) speed += Input.VR.RightHand.Velocity.Length;

				return flat.Forward * (speed * ArmSwingScale).Clamp( 0f, Controller.RunSpeed );
			}

			default:
				// Teleport and shift don't walk
				return 0;
		}
	}

	/// <summary>
	/// Throws a ballistic arc from the hand and returns where the player could stand, if anywhere.
	/// </summary>
	Vector3? TraceTarget( Transform hand, out List<Vector3> points )
	{
		points = new List<Vector3>();

		var position = hand.Position;
		var velocity = hand.Rotation.Forward * TeleportArcSpeed;
		var gravity = Vector3.Down * 800f;
		const float step = 1f / 30f;

		points.Add( position );

		for ( var i = 0; i < 90; i++ )
		{
			var next = position + velocity * step;
			velocity += gravity * step;

			var tr = Scene.Trace.Ray( position, next )
				.IgnoreGameObjectHierarchy( GameObject )
				.WithoutTags( "player", "trigger" )
				.Run();

			if ( tr.Hit )
			{
				points.Add( tr.HitPosition );
				return IsStandable( tr ) ? tr.HitPosition : null;
			}

			points.Add( next );
			position = next;
		}

		return null;
	}

	bool IsStandable( SceneTraceResult tr )
	{
		if ( tr.Normal.z < 0.7f ) return false;

		// Make sure the body fits there
		var spot = tr.HitPosition + Vector3.Up * 2f;
		var fit = Controller.TraceBody( spot, spot, 1f, 1f );
		return !fit.StartedSolid && !fit.Hit;
	}

	void UpdateArc( List<Vector3> points )
	{
		if ( points is null || points.Count < 2 )
		{
			if ( _arc.IsValid() ) _arc.Enabled = false;
			return;
		}

		if ( !_arc.IsValid() )
		{
			var go = new GameObject( true, "VR Arc" );
			go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
			_arc = go.AddComponent<LineRenderer>();
			_arc.UseVectorPoints = true;
			_arc.Width = 1.5f;
			_arc.Opaque = false;
			_arc.CastShadows = false;
		}

		_arc.Enabled = true;
		_arc.Color = _target.HasValue ? Color.Cyan : Color.Red;
		_arc.VectorPoints = points;
	}

	protected override void OnDisabled()
	{
		if ( _arc.IsValid() ) _arc.GameObject.Destroy();
	}
}
