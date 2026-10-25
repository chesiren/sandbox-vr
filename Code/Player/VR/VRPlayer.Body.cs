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

	bool _ikApplied;

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
				_ikApplied = false;
			}

			return;
		}

		renderer.SetIk( "hand_left", HandTarget( LeftHand, LeftHandIkOffset ) );
		renderer.SetIk( "hand_right", HandTarget( RightHand, RightHandIkOffset ) );
		_ikApplied = true;
	}

	Transform HandTarget( Transform grip, Angles offset )
	{
		var rotation = grip.Rotation * offset.ToRotation();
		return new Transform( grip.Position + grip.Rotation * HandIkPositionOffset, rotation );
	}
}
