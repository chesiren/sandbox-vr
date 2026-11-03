public sealed partial class VRPlayer
{
	/// <summary>
	/// Rotation from a controller's grip pose to the body's left hand bone.
	/// </summary>
	[Property, Group( "VR Body" )] public Angles LeftHandIkOffset { get; set; } = new( 80.10f, -60.74f, 19.47f );

	/// <summary>
	/// Rotation from a controller's grip pose to the body's right hand bone.
	/// </summary>
	[Property, Group( "VR Body" )] public Angles RightHandIkOffset { get; set; } = new( 80.10f, 60.74f, 160.53f );

	/// <summary>
	/// Where the left hand bone sits relative to the grip pose, in the grip's space.
	/// </summary>
	[Property, Group( "VR Body" )] public Vector3 LeftHandIkPositionOffset { get; set; } = new( -3.47f, 2.11f, 7.47f );

	/// <summary>
	/// Where the right hand bone sits relative to the grip pose, in the grip's space.
	/// </summary>
	[Property, Group( "VR Body" )] public Vector3 RightHandIkPositionOffset { get; set; } = new( -3.47f, -2.11f, 7.47f );

	/// <summary>
	/// Eye height of the body model standing at scale 1.
	/// </summary>
	[Property, Group( "VR Body" )] public float ModelEyeHeight { get; set; } = 63.7f;

	/// <summary>
	/// How far the model's head bone is kept behind the headset, at scale 1, so the face isn't in front of the eyes.
	/// </summary>
	[Property, Group( "VR Body" )] public float HeadBehind { get; set; } = 3f;

	/// <summary>
	/// How far the head or the hands can turn from the body before the body turns after them at once, in degrees.
	/// </summary>
	[Property, Group( "VR Body" )] public float BodyTurnAngle { get; set; } = 30f;

	/// <summary>
	/// How long the head can stay turned a little from the body before the body squares up to it, in seconds.
	/// </summary>
	[Property, Group( "VR Body" )] public float BodySettleTime { get; set; } = 0.5f;

	/// <summary>
	/// How quickly the body catches up with the head once it turns.
	/// </summary>
	[Property, Group( "VR Body" )] public float BodyTurnSpeed { get; set; } = 12f;

	/// <summary>
	/// The body is scaled so its eyes are at the player's real standing eye height.
	/// </summary>
	[Sync] public float BodyScale { get; private set; } = 1f;

	/// <summary>
	/// Highest the headset has been above the floor - the player's standing eye height.
	/// </summary>
	float _standingHeight;

	bool _ikApplied;

	float _lastHeadHeight;

	/// <summary>
	/// Takes the current head height as standing height, for the local VR player.
	/// </summary>
	public static void RecalibrateHeight()
	{
		if ( Local is not { } vr ) return;

		vr._standingHeight = vr.Head.Position.z - vr.WorldPosition.z;
	}

	void UpdateBodyScale()
	{
		var headHeight = Head.Position.z - WorldPosition.z;
		var headSpeed = MathF.Abs( headHeight - _lastHeadHeight ) / MathF.Max( Time.Delta, 0.001f );
		_lastHeadHeight = headHeight;

		// Only trust a steady, plausible head height - not the headset on a desk, or lifted over the head while
		// putting it on. A height that's too tall makes looking down count as crouching.
		if ( headHeight > 30f && headHeight < 80f && headSpeed < 10f )
			_standingHeight = MathF.Max( _standingHeight, headHeight );

		if ( _standingHeight > 0f )
			BodyScale = (_standingHeight / ModelEyeHeight).Clamp( 0.6f, 1.2f );
	}

	protected override void OnUpdate()
	{
		if ( IsVR || !_ikApplied ) return;

		var renderer = Controller.IsValid() ? Controller.Renderer : null;
		if ( !renderer.IsValid() ) return;

		renderer.ClearIk( "hand_left" );
		renderer.ClearIk( "hand_right" );
		ReleaseFingers();
		renderer.LocalScale = 1f;
		_ikApplied = false;
		_bodyTurned = false;
	}

	/// <summary>
	/// On every peer, once the controller has animated the body like any player's: its hands on the synced controller
	/// poses, so everyone sees the VR player's real hands, and it turned, crouched and sized from the headset.
	/// See <see cref="VRBodySystem"/>.
	/// </summary>
	public void PoseBody()
	{
		var renderer = Controller.IsValid() ? Controller.Renderer : null;
		if ( !renderer.IsValid() ) return;

		renderer.LocalScale = BodyScale;

		if ( IsLocalVR && !_headHidden )
			HideOwnHead( renderer );

		// The weapon's on the controller, see PlaceHeldWeapon - a hold pose only pulls the arms away from the hands
		renderer.Set( "holdtype", "none" );

		// A seat has its own pose and turns us with it
		if ( Player.IsSeated )
		{
			_bodyTurned = false;
			_headOffset = null;
			_duck = 0f;
		}
		else
		{
			TurnBody( renderer );
			PlaceUnderHead( renderer );
			renderer.Set( "duck", MathF.Max( renderer.GetFloat( "duck" ), _duck ) );
		}

		// Last: the engine takes these relative to the body as it is now, so turning or moving it after carried them off
		renderer.SetIk( "hand_left", HandTarget( LeftHand, LeftHandIkOffset, LeftHandIkPositionOffset ) );
		renderer.SetIk( "hand_right", HandTarget( RightHand, RightHandIkOffset, RightHandIkPositionOffset ) );
		UpdateFingers( renderer );
		_ikApplied = true;
	}

	float _bodyYaw;
	bool _bodyTurned;
	bool _bodyTurning;
	TimeSince _timeOffSquare;

	/// <summary>
	/// The body stays put while the head glances about, and turns after it when the head or both hands go far enough
	/// round, when walking, or when the head stays a little turned. Not while looking down, where the head's yaw means
	/// little - at the hands, or at the feet.
	/// </summary>
	void TurnBody( SkinnedModelRenderer renderer )
	{
		var head = Head.Rotation;
		var headYaw = head.Yaw();

		if ( !_bodyTurned )
		{
			_bodyYaw = headYaw;
			_bodyTurned = true;
		}

		var difference = Angles.NormalizeAngle( headYaw - _bodyYaw );
		var walking = Controller.Velocity.WithZ( 0 ).Length > 30f;

		if ( MathF.Abs( difference ) < 8f )
			_timeOffSquare = 0;

		if ( MathF.Abs( difference ) > BodyTurnAngle || HandsTurned() || walking || _timeOffSquare > BodySettleTime )
			_bodyTurning = true;
		else if ( MathF.Abs( difference ) < 3f )
			_bodyTurning = false;

		var lookingDown = head.Forward.z < -0.75f;
		var turn = _bodyTurning && (walking || !lookingDown) ? difference * (Time.Delta * BodyTurnSpeed).Clamp( 0f, 1f ) : 0f;
		_bodyYaw += turn;

		renderer.WorldRotation = Rotation.FromYaw( _bodyYaw );

		// Shuffles the feet while turning on the spot, and keeps the head and eyes on where the headset looks
		renderer.Set( "move_rotationspeed", turn / MathF.Max( Time.Delta, 0.001f ) );

		var look = renderer.WorldRotation.Inverse * head.Forward;
		renderer.Set( "aim_eyes", look );
		renderer.Set( "aim_head", look );
		renderer.Set( "aim_body", look );
	}

	/// <summary>
	/// Both hands well round to one side of the body, the way they go when turning to face something.
	/// </summary>
	bool HandsTurned()
	{
		var reach = ((LeftHand.Position + RightHand.Position) * 0.5f - Head.Position).WithZ( 0 );
		if ( reach.Length < 12f * BodyScale ) return false;

		return MathF.Abs( Angles.NormalizeAngle( Rotation.LookAt( reach ).Yaw() - _bodyYaw ) ) > BodyTurnAngle;
	}

	Vector3? _headOffset;
	float _duck;

	/// <summary>
	/// Moves the body so its head is under the headset. Where the head is over the feet changes with crouching, leaning
	/// and looking about, so it's taken from the last pose rather than assumed.
	/// </summary>
	void PlaceUnderHead( SkinnedModelRenderer renderer )
	{
		if ( _headOffset is not { } offset ) return;

		var rotation = renderer.WorldRotation;
		var target = Head.Position - rotation.Forward * HeadBehind * BodyScale;
		var position = target - rotation * (offset * BodyScale);

		renderer.WorldPosition = position.WithZ( renderer.WorldPosition.z );
	}

	/// <summary>
	/// Once the bones are worked out, measures where the body's head ended up: for placing it under the headset, and
	/// for bending the knees to bring it to the headset's height. The player only crouches for real - lower and
	/// smaller - past the crouch height setting.
	/// </summary>
	protected override void OnPreRender()
	{
		if ( !IsVR ) return;

		var renderer = Controller.IsValid() ? Controller.Renderer : null;
		if ( !renderer.IsValid() ) return;

		if ( !Player.IsSeated && renderer.TryGetBoneTransform( "head", out var bone ) )
		{
			// In the model's own units, flat, and eased so a twitch of the animation doesn't shake the whole body
			var offset = renderer.WorldTransform.PointToLocal( bone.Position ).WithZ( 0 );
			_headOffset = _headOffset is { } last ? last.LerpTo( offset, (Time.Delta * 10f).Clamp( 0f, 1f ) ) : offset;

			// Ducking all the way takes the head down about 40% of the model's height
			var tooHigh = bone.Position.z - Head.Position.z;
			_duck = (_duck + tooHigh / (25f * BodyScale) * (Time.Delta * 6f).Clamp( 0f, 1f )).Clamp( 0f, 1f );
		}

		PlaceHeldWeapon();
	}

	/// <summary>
	/// The world model hangs off the hand's hold bone, which the animation moves around per hold type - so with the
	/// hand on the controller it pointed anywhere. Put it straight on the controller instead: grip in the palm,
	/// barrel along where the controller aims, which is also where it shoots.
	/// </summary>
	void PlaceHeldWeapon()
	{
		var weapon = Player.GetComponent<PlayerInventory>()?.ActiveWeapon;
		if ( !weapon.IsValid() || !weapon.WorldModel.IsValid() )
		{
			ShowHiddenWeaponParts();
			return;
		}

		weapon.WorldModel.WorldTransform = new Transform( DominantHand.Position, Aim.Rotation );

		// What sits on the weapon goes with it, from the same pose at the same moment, or it trails behind
		if ( IsLocalVR )
		{
			Scene.Get<VRWeaponAmmo>()?.PlaceNow();
			Scene.Get<VRScope>()?.PlaceNow();
		}

		// With a menu up the gun is just in the way of the laser, or of the wheel - hide it on our side only
		var hide = IsLocalVR && (SpawnMenuHost.IsOpen || WheelOpen || VRKeyboard.IsOpen);
		if ( hide && _hiddenWeaponParts.Count == 0 )
		{
			// Models, but also glows, particles and lights (the physgun's glow)
			foreach ( var part in weapon.WorldModel.GetComponentsInChildren<Component>() )
			{
				if ( part is not (Renderer or Light) ) continue;

				part.Enabled = false;
				_hiddenWeaponParts.Add( part );
			}
		}
		else if ( !hide )
		{
			ShowHiddenWeaponParts();
		}
	}

	// Only what we hid comes back - some weapons keep parts of their model switched off on purpose
	readonly List<Component> _hiddenWeaponParts = new();

	void ShowHiddenWeaponParts()
	{
		foreach ( var model in _hiddenWeaponParts )
		{
			if ( model.IsValid() ) model.Enabled = true;
		}

		_hiddenWeaponParts.Clear();
	}

	bool _headHidden;

	/// <summary>
	/// Puts back the head <see cref="HideOwnHead"/> shrank away.
	/// </summary>
	public void ShowOwnHead()
	{
		if ( !_headHidden ) return;

		var head = Controller.IsValid() ? Controller.Renderer?.GetBoneObject( "head" ) : null;
		if ( head.IsValid() ) head.LocalScale = 1f;

		_headHidden = false;
	}

	/// <summary>
	/// Our own head (and the hats and hair hanging off it) would sit right in front of the headset. The model has no
	/// headless body group, so shrink the head bone away - only on our side, everyone else still sees it.
	/// </summary>
	void HideOwnHead( SkinnedModelRenderer renderer )
	{
		renderer.CreateBoneObjects = true;

		var head = renderer.GetBoneObject( "head" );
		if ( !head.IsValid() ) return;

		head.Flags |= GameObjectFlags.ProceduralBone;
		head.LocalScale = 0.01f;
		_headHidden = true;
	}

	/// <summary>
	/// The hand bone for a controller grip. The offsets put the hand's hold bone - what weapons hang off - in the palm,
	/// pointing where the controller aims.
	/// </summary>
	Transform HandTarget( Transform grip, Angles rotationOffset, Vector3 positionOffset )
	{
		var rotation = grip.Rotation * rotationOffset.ToRotation();
		return new Transform( grip.Position + grip.Rotation * positionOffset * BodyScale, rotation );
	}
}
