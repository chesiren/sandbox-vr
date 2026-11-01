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
	/// How far the model's eyes are in front of its feet, plus a little so its head stays behind the headset.
	/// </summary>
	[Property, Group( "VR Body" )] public float ModelEyeForward { get; set; } = 8.5f;

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

	/// <summary>
	/// On every peer, pins the body's hands to the synced controller poses, so everyone sees the VR player's
	/// real hands - and the weapon in them, since the world model hangs off the hand.
	/// </summary>
	protected override void OnUpdate()
	{
		var renderer = Controller.IsValid() ? Controller.Renderer : null;
		if ( !renderer.IsValid() ) return;

		if ( !IsVR )
		{
			if ( _ikApplied )
			{
				renderer.ClearIk( "hand_left" );
				renderer.ClearIk( "hand_right" );
				ReleaseFingers();
				renderer.LocalScale = 1f;
				_ikApplied = false;
			}

			return;
		}

		renderer.SetIk( "hand_left", HandTarget( LeftHand, LeftHandIkOffset, LeftHandIkPositionOffset ) );
		renderer.SetIk( "hand_right", HandTarget( RightHand, RightHandIkOffset, RightHandIkPositionOffset ) );
		UpdateFingers( renderer );
		_ikApplied = true;

		if ( IsLocalVR && !_headHidden )
			HideOwnHead( renderer );
	}

	/// <summary>
	/// After the controller has placed the body for the frame: size it to the player and step it back so its eyes,
	/// not its feet, are under the headset - otherwise the camera ends up in its neck and shoulders.
	/// </summary>
	protected override void OnPreRender()
	{
		if ( !IsVR ) return;

		var renderer = Controller.IsValid() ? Controller.Renderer : null;
		if ( !renderer.IsValid() ) return;

		renderer.LocalScale = BodyScale;

		var forward = renderer.WorldRotation.Forward.WithZ( 0 ).Normal;
		renderer.WorldPosition -= forward * ModelEyeForward * BodyScale;

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

		// With a menu up the gun is just in the way of the laser, or of the wheel - hide it on our side only
		var hide = IsLocalVR && (SpawnMenuHost.IsOpen || WheelOpen);
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
