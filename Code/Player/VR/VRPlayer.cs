using Sandbox.VR;

/// <summary>
/// The VR side of a player. On the owner it reads the headset and controllers, keeps the play area under the
/// player and drives the <see cref="PlayerController"/>; on every peer it holds the synced head and hand poses,
/// so desktop players see where a VR player's head and hands are, and the host can check reach from the hands.
/// Inert for desktop players: <see cref="IsVR"/> stays false and nothing here runs.
/// </summary>
public sealed partial class VRPlayer : Component, ICameraModifier
{
	[RequireComponent] public Player Player { get; set; }

	PlayerController Controller => Player.Controller;

	/// <summary>
	/// True when the owner of this player is in VR.
	/// </summary>
	[Sync] public bool IsVR { get; private set; }

	// Poses are relative to the player's root, so they stay small and interpolate with the player.
	[Sync] Transform HeadLocal { get; set; }
	[Sync] Transform LeftHandLocal { get; set; }
	[Sync] Transform RightHandLocal { get; set; }
	[Sync] Transform AimLocal { get; set; }
	[Sync] bool LeftHanded { get; set; }

	/// <summary>
	/// The headset, in world space.
	/// </summary>
	public Transform Head => WorldTransform.ToWorld( HeadLocal );

	public Transform LeftHand => WorldTransform.ToWorld( LeftHandLocal );
	public Transform RightHand => WorldTransform.ToWorld( RightHandLocal );

	/// <summary>
	/// The grip of the hand holding weapons and tools.
	/// </summary>
	public Transform DominantHand => LeftHanded ? LeftHand : RightHand;

	/// <summary>
	/// The grip of the other hand.
	/// </summary>
	public Transform OffHand => LeftHanded ? RightHand : LeftHand;

	/// <summary>
	/// Where the dominant hand points - what weapons and tools aim along.
	/// </summary>
	public Transform Aim => WorldTransform.ToWorld( AimLocal );

	/// <summary>
	/// True when this is our own player and we're in VR.
	/// </summary>
	public bool IsLocalVR => !IsProxy && VRMode.IsActive;

	/// <summary>
	/// The VR player for this player, or null for desktop players.
	/// </summary>
	public static VRPlayer Get( Player player )
	{
		if ( !player.IsValid() ) return null;

		var vr = player.GetComponent<VRPlayer>();
		return vr.IsValid() && vr.IsVR ? vr : null;
	}

	/// <summary>
	/// Our own player's VR component, while we're in VR.
	/// </summary>
	public static VRPlayer Local => VRMode.IsActive ? Get( Player.FindLocalPlayer() ) : null;

	float _rigYaw;
	Vector3 _rigPosition;
	bool _rigInitialized;
	Vector3 _lastBodyPosition;
	float _defaultEyeDistance;

	protected override void OnStart()
	{
		if ( !IsLocalVR ) return;

		IsVR = true;
		_defaultEyeDistance = Controller.EyeDistanceFromTop;

		// We aim, look and move from the headset and controllers ourselves
		Controller.UseLookControls = false;
		Controller.UseCameraControls = false;
		Controller.ThirdPerson = false;
		Controller.HideBodyInFirstPerson = false;

		if ( Scene.Camera.IsValid() )
			Scene.Camera.TargetEye = StereoTargetEye.Both;

		_ = VRHud.Enable( Scene );
	}

	protected override void OnDestroy()
	{
		if ( !IsLocalVR ) return;

		if ( Controller.IsValid() )
			Controller.EyeDistanceFromTop = _defaultEyeDistance;
	}

	/// <summary>
	/// Runs first in <see cref="Player"/>'s input pass, before anything reads the eyes or actions.
	/// </summary>
	internal void OnControl()
	{
		if ( !IsLocalVR ) return;

		UpdateRig();
		UpdatePose();
		UpdateFingerPose();
		UpdateBodyScale();
		UpdateEyes();
		MapInput();
	}

	/// <summary>
	/// Keeps the play area centred under the player: the headset walking around the room moves the player's body
	/// with it, and walls stop the body and push the play area back instead, so you can't lean through them.
	/// </summary>
	void UpdateRig()
	{
		var vr = Input.VR;

		// The headset in the play area's space. Input.VR.Head is already in world space, through the current anchor
		var headLocal = vr.Anchor.ToLocal( vr.Head );

		if ( !_rigInitialized )
		{
			_rigInitialized = true;

			// Face the way the player spawned, with the headset over the player's feet
			_rigYaw = Controller.EyeAngles.yaw - headLocal.Rotation.Yaw();
			_rigPosition = WorldPosition - (Rotation.FromYaw( _rigYaw ) * headLocal.Position).WithZ( 0 );
		}
		else
		{
			// The body moved on its own (stick, teleport, physics, getting pushed) - the play area comes with it
			_rigPosition += WorldPosition - _lastBodyPosition;
		}

		var seated = Player.IsSeated;
		var rigRotation = Rotation.FromYaw( _rigYaw );

		if ( seated )
		{
			// Keep the play area on the seat, which drives us around
			_rigPosition = WorldPosition - (rigRotation * headLocal.Position).WithZ( 0 );
		}
		else
		{
			var headWorld = _rigPosition + rigRotation * headLocal.Position;
			var delta = (headWorld - WorldPosition).WithZ( 0 );

			if ( delta.Length > 0.1f )
			{
				var start = WorldPosition;
				var end = start + delta;

				if ( !Player.IsNoclipping )
				{
					var tr = Controller.TraceBody( start, end, 1f, 0.9f );
					if ( !tr.StartedSolid )
						end = tr.EndPosition;
					else
						end = start;
				}

				WorldPosition = end;

				// Whatever we couldn't walk into pushes the room back
				_rigPosition -= delta - (end - start);
			}
		}

		_rigPosition.z = WorldPosition.z;
		vr.Anchor = new Transform( _rigPosition, rigRotation );

		_lastBodyPosition = WorldPosition;
	}

	/// <summary>
	/// Turns the play area around the headset, so the player turns on the spot.
	/// </summary>
	public void Turn( float degrees )
	{
		var head = Input.VR.Head.Position;
		var turned = new Transform( _rigPosition, Rotation.FromYaw( _rigYaw ) ).RotateAround( head, Rotation.FromYaw( degrees ) );

		_rigYaw += degrees;
		_rigPosition = turned.Position.WithZ( _rigPosition.z );
		Input.VR.Anchor = new Transform( _rigPosition, Rotation.FromYaw( _rigYaw ) );
	}

	/// <summary>
	/// Moves the player - teleporting, dashing. The play area follows on the next rig update.
	/// </summary>
	public void MoveTo( Vector3 position )
	{
		WorldPosition = position;
		Transform.ClearInterpolation();
	}

	void UpdatePose()
	{
		var vr = Input.VR;
		var root = WorldTransform;

		HeadLocal = root.ToLocal( vr.Head );
		LeftHandLocal = root.ToLocal( vr.LeftHand.Transform );
		RightHandLocal = root.ToLocal( vr.RightHand.Transform );
		AimLocal = root.ToLocal( VRMode.DominantHand.AimTransform );
		LeftHanded = VRMode.LeftHanded;
	}

	/// <summary>
	/// The engine's eyes follow the headset, so everything built on <see cref="PlayerController.EyeTransform"/> -
	/// animation, using things, what other players see us look at - follows where we really look.
	/// </summary>
	void UpdateEyes()
	{
		var head = Head;

		var angles = head.Rotation.Angles();
		Controller.EyeAngles = angles.WithRoll( 0 );

		// Put the engine's eye height on the real head
		var headHeight = head.Position.z - WorldPosition.z;
		Controller.EyeDistanceFromTop = (Controller.CurrentHeight - headHeight).Clamp( -32f, Controller.CurrentHeight - 8f );
	}

	int ICameraModifier.CameraOrder => 10000;

	// Last in the chain: whatever the game, weapons or vehicles did to the view, the headset wins.
	void ICameraModifier.ModifyCamera( CameraComponent camera, ref CameraView view )
	{
		if ( !IsLocalVR || Scene.Camera != camera ) return;

		var head = Input.VR.Head;
		view.Position = head.Position;
		view.Rotation = head.Rotation;

		camera.TargetEye = StereoTargetEye.Both;
		camera.RenderExcludeTags.Remove( "firstperson" );

		// Shakes and punches make people sick in a headset
		if ( CameraEffectSystem.Get( Scene ) is { } effects )
			effects.Scale = 0;
	}
}
