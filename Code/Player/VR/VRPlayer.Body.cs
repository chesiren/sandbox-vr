public sealed partial class VRPlayer
{
	/// <summary>
	/// Rotation from a controller's grip pose to the body's left hand bone.
	/// </summary>
	[Property, Group( "VR Body" )] public Angles LeftHandIkOffset { get; set; } = new( 0, 0, 90 );

	/// <summary>
	/// Rotation from a controller's grip pose to the body's right hand bone.
	/// </summary>
	[Property, Group( "VR Body" )] public Angles RightHandIkOffset { get; set; } = new( 0, 0, -90 );

	/// <summary>
	/// Where the hand bone sits relative to the grip pose, in the grip's space.
	/// </summary>
	[Property, Group( "VR Body" )] public Vector3 HandIkPositionOffset { get; set; } = new( -3, 0, -1 );

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

		renderer.SetIk( "hand_left", HandTarget( LeftHand, LeftHandIkOffset ) );
		renderer.SetIk( "hand_right", HandTarget( RightHand, RightHandIkOffset ) );
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

	Transform HandTarget( Transform grip, Angles offset )
	{
		var rotation = grip.Rotation * offset.ToRotation();
		return new Transform( grip.Position + grip.Rotation * HandIkPositionOffset, rotation );
	}
}
