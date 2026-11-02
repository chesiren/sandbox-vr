/// <summary>
/// Dead players become these. They try to observe their last corpse. 
/// </summary>
internal sealed class PlayerObserver : Component
{
	Angles EyeAngles;
	TimeSince timeSinceStarted;
	DeathCameraTarget _cachedCorpse;
	float currentDistance;

	protected override void OnEnabled()
	{
		base.OnEnabled();

		EyeAngles = Scene.Camera.WorldRotation;
		timeSinceStarted = 0;
		currentDistance = 32;

		_cachedCorpse = Scene.GetAllComponents<DeathCameraTarget>()
					.Where( x => x.Connection == Network.Owner )
					.OrderByDescending( x => x.Created )
					.FirstOrDefault();

		// A trigger held down while dying has to be let go first
		_triggerWasDown = true;

		if ( !IsProxy && VRMode.IsActive )
			StepOutOfBody();
	}

	/// <summary>
	/// In a headset the view can't be swung round the corpse without making people sick, and nothing applies the
	/// headset to the camera once the player's gone. So we're put a step back and up from where the head was, still
	/// facing the same way, and from there the head looks around freely while we drift after the corpse.
	/// </summary>
	void StepOutOfBody()
	{
		var head = Input.VR.Head;
		var back = -head.Rotation.Forward.WithZ( 0 ).Normal;
		var target = head.Position + back * 70f + Vector3.Up * 30f;

		var tr = Scene.Trace.Ray( head.Position, target ).Radius( 8f ).WithoutTags( "ragdoll", "effect", "player" ).Run();

		_viewPoint = head.Position;
		MoveView( tr.EndPosition );
	}

	Vector3 _viewPoint;
	Vector3? _corpseOffset;

	/// <summary>
	/// Keeps to where we stepped out to from the corpse, wherever it's thrown. Only the play area moves, never how
	/// it's turned, and it eases along rather than jolting.
	/// </summary>
	void FollowCorpse()
	{
		if ( !_cachedCorpse.IsValid() ) return;

		var center = CorpseCenter( _cachedCorpse );
		_corpseOffset ??= _viewPoint - center;

		var tr = Scene.Trace.Ray( center, center + _corpseOffset.Value ).Radius( 8f ).WithoutTags( "ragdoll", "effect", "player" ).Run();

		MoveView( _viewPoint.LerpTo( tr.EndPosition, Time.Delta * 3f ) );
	}

	void MoveView( Vector3 to )
	{
		var vr = Input.VR;
		vr.Anchor = vr.Anchor.WithPosition( vr.Anchor.Position + (to - _viewPoint) );
		_viewPoint = to;
	}

	bool _triggerWasDown;

	/// <summary>
	/// The game's attack and jump come from the player's VR input, which went with the player.
	/// </summary>
	bool VRRespawnPressed()
	{
		if ( !VRMode.IsActive ) return false;

		var down = Input.VR.RightHand.Trigger.Value > 0.7f || Input.VR.LeftHand.Trigger.Value > 0.7f;
		var pressed = down && !_triggerWasDown;
		_triggerWasDown = down;
		return pressed;
	}

	protected override void OnUpdate()
	{
		// Don't allow immediate respawn
		if ( timeSinceStarted < 1 )
			return;

		// If pressed a button, or has been too long, ask the host to respawn us. The host destroys
		// this observer once the spawn goes through; if a listener vetoed it we stay here and try
		// again after the same delay.
		if ( Input.Pressed( "attack1" ) || Input.Pressed( "jump" ) || VRRespawnPressed() || timeSinceStarted > 4f )
		{
			GameManager.Current?.RequestRespawn();
			timeSinceStarted = 0;
		}
	}

	protected override void OnPreRender()
	{
		if ( IsProxy ) return;

		if ( VRMode.IsActive )
		{
			FollowCorpse();

			var head = Input.VR.Head;
			Scene.Camera.WorldPosition = head.Position;
			Scene.Camera.WorldRotation = head.Rotation;
			Scene.Camera.TargetEye = StereoTargetEye.Both;
			return;
		}

		if ( _cachedCorpse.IsValid() )
		{
			RotateAround( _cachedCorpse );
		}
	}

	/// <summary>
	/// Roughly the corpse's chest.
	/// </summary>
	static Vector3 CorpseCenter( Component target )
	{
		if ( target.Components.Get<SkinnedModelRenderer>().TryGetBoneTransform( "pelvis", out var tx ) )
			return tx.Position + Vector3.Up * 25;

		return target.WorldPosition;
	}

	private void RotateAround( Component target )
	{

		var e = EyeAngles;
		e += Input.AnalogLook;
		e.pitch = e.pitch.Clamp( -90, 90 );
		e.roll = 0.0f;
		EyeAngles = e;

		currentDistance = currentDistance.LerpTo( 150, Time.Delta * 5 );

		var center = CorpseCenter( target );
		var targetPos = center - EyeAngles.Forward * currentDistance;

		var tr = Scene.Trace.FromTo( center, targetPos ).Radius( 1.0f ).WithoutTags( "ragdoll", "effect" ).Run();

		Scene.Camera.WorldPosition = tr.EndPosition;
		Scene.Camera.WorldRotation = EyeAngles;
	}
}
