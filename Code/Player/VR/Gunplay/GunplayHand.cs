using Sandbox.VR;

/// <summary>
/// One of the local VR player's hands, for taking hold of gunplay objects: the physical guns, their magazines and
/// rounds. It follows its controller and grabs the <see cref="IGrabbable"/> it's touching, or pointing at close by,
/// when the grab point's input is down. Ported from gunbuilder's Hand.
/// <para>Only on the VR player's own machine, made by <see cref="VRPlayer"/>, and not networked: what it holds is,
/// through the held object's ownership.</para>
/// </summary>
public sealed class GunplayHand : Component, Component.ITriggerListener
{
	/// <summary>
	/// How far in front of the hand a grab point can be pointed at and taken.
	/// </summary>
	const float PointReach = 96f;

	/// <summary>
	/// How far down an input has to be to count.
	/// </summary>
	const float Deadzone = 0.25f;

	/// <summary>
	/// The hand pose from the controller's grip: gunbuilder's hand model sat back a little and pitched 45 degrees down
	/// from it, and its guns are set up to be held that way.
	/// </summary>
	static readonly Rotation HoldOffset = new( 0f, 0.3826835f, 0f, 0.9238795f );

	public bool IsLeft { get; set; }

	public VRPlayer Owner { get; set; }

	public VRController Controller => IsLeft ? Input.VR?.LeftHand : Input.VR?.RightHand;

	/// <summary>
	/// What this hand holds right now.
	/// </summary>
	public IGrabbable CurrentGrabbable { get; set; }

	/// <summary>
	/// What this hand would take hold of now, as of its last update. While there's one, the grip goes to it rather than
	/// to using and carrying.
	/// </summary>
	public IGrabbable Target { get; private set; }

	/// <summary>
	/// How far the hand moved last frame.
	/// </summary>
	public Vector3 Velocity { get; private set; }

	/// <summary>
	/// How fast something let go of by this hand flies.
	/// </summary>
	public Vector3 ThrowVelocity => Owner.IsValid() ? Owner.HandThrowVelocity( IsLeft ) : Velocity / MathF.Max( Time.Delta, 0.001f );

	/// <summary>
	/// Where what it holds goes.
	/// </summary>
	public Vector3 HoldPosition => WorldPosition;

	/// <summary>
	/// How what it holds is turned.
	/// </summary>
	public Rotation HoldRotation => WorldRotation * HoldOffset;

	/// <summary>
	/// How the fingers sit around what it holds, if that says.
	/// </summary>
	public HandPreset ActivePreset => (CurrentGrabbable as GrabPoint)?.Preset;

	readonly HashSet<IGrabbable> _hovered = new();

	public bool IsHolding() => CurrentGrabbable.IsValid();

	public bool IsGripDown() => Controller is { } c && c.Grip.Value > Deadzone;

	public bool IsTriggerDown() => Controller is { } c && c.Trigger.Value > Deadzone;

	public bool IsDown( GrabInputType inputType ) => inputType switch
	{
		GrabInputType.Hover => true,
		GrabInputType.Grip => IsGripDown(),
		GrabInputType.Trigger => IsTriggerDown(),
		_ => false
	};

	void StartGrabbing( IGrabbable grabbable )
	{
		if ( CurrentGrabbable == grabbable ) return;
		if ( !IsDown( grabbable.GrabInput ) ) return;

		if ( grabbable.StartGrabbing( this ) )
		{
			CurrentGrabbable = grabbable;
			VRMode.Pulse( Controller, 0.3f, 0.3f );
		}
	}

	/// <summary>
	/// Lets go of what it holds. <paramref name="force"/> doesn't wait for the delay after grabbing.
	/// </summary>
	public void StopGrabbing( bool force = false )
	{
		var current = CurrentGrabbable;
		if ( current is null ) return;

		var released = current is GrabPoint grabPoint && grabPoint.IsValid() ? grabPoint.StopGrabbing( this, force ) : current.IsValid() && current.StopGrabbing( this );

		if ( released || !current.IsValid() )
		{
			_hovered.Remove( current );
			CurrentGrabbable = null;
		}
	}

	void UpdateTrackedLocation()
	{
		if ( Controller is not { } controller ) return;

		var grip = controller.Transform;
		var previous = WorldPosition;

		WorldTransform = new Transform( grip.Position + grip.Rotation.Forward * -2f, grip.Rotation );
		Velocity = WorldPosition - previous;
	}

	/// <summary>
	/// A grabbable the hand points at, close by.
	/// </summary>
	IGrabbable LookForHandAligned( float size )
	{
		var forward = HoldRotation.Forward;

		var tr = Scene.Trace.Ray( WorldPosition, WorldPosition + forward * PointReach )
			.IgnoreGameObjectHierarchy( GameObject )
			.IgnoreGameObjectHierarchy( Owner?.GameObject )
			.Size( size )
			.Run();

		if ( !tr.Hit || !tr.GameObject.IsValid() ) return null;

		// Any of its grab points, the gun's grips as well as its slide's: the nearest one the input that's down takes
		var candidates = tr.GameObject.Root.GetComponentsInChildren<GrabPoint>().Cast<IGrabbable>();
		return Best( candidates, tr.HitPosition );
	}

	bool CanTake( IGrabbable grabbable ) => grabbable.IsValid() && !grabbable.IsHeld && grabbable.GameObject.Active && !IsUnheldSupport( grabbable );

	/// <summary>
	/// A support grip, like the SG-5's front one, on a gun nobody holds by its grip yet. It can't carry the gun on its own,
	/// so it isn't taken: it was nearer the hand than the grip and got in the way of picking the gun up.
	/// </summary>
	static bool IsUnheldSupport( IGrabbable grabbable ) =>
		grabbable is GrabPoint { IsSecondaryGrabPoint: true } support && support.PrimaryGrabPoint is not IGrabbable { IsHeld: true };

	/// <summary>
	/// Of grab points close together, the one taken with an input that's down comes first, then the nearest: the SG-5's
	/// front grip (grip) sits right by its cocking handle (trigger), and squeezing the grip should take the grip.
	/// </summary>
	IGrabbable Best( IEnumerable<IGrabbable> candidates, Vector3 near ) => candidates
		.Where( CanTake )
		.OrderBy( x => x.GrabInput == GrabInputType.Hover || IsDown( x.GrabInput ) ? 0 : 1 )
		.ThenBy( x => x.GameObject.WorldPosition.Distance( near ) )
		.FirstOrDefault();

	IGrabbable TryFindGrabbable()
	{
		if ( CurrentGrabbable.IsValid() ) return CurrentGrabbable;

		_hovered.RemoveWhere( x => !x.IsValid() );

		var touching = Best( _hovered, WorldPosition );
		if ( touching.IsValid() ) return touching;

		return LookForHandAligned( 1f ) ?? LookForHandAligned( 5f );
	}

	/// <summary>
	/// The hand can't hold anything while the player's seated, carrying something, or in a menu.
	/// </summary>
	bool CanUseHand()
	{
		if ( !Owner.IsValid() || !Owner.IsLocalVR ) return false;

		var player = Owner.Player;
		if ( !player.IsValid() || player.IsSeated ) return false;
		if ( player.GetComponent<PlayerCarry>() is { IsCarrying: true } ) return false;

		return true;
	}

	protected override void OnUpdate()
	{
		UpdateTrackedLocation();

		if ( !CanUseHand() )
		{
			Target = null;
			StopGrabbing( true );
			return;
		}

		// A hover grab lets go once the hand moves away
		if ( CurrentGrabbable.IsValid() && CurrentGrabbable.GrabInput == GrabInputType.Hover
			&& CurrentGrabbable.GameObject.WorldPosition.Distance( WorldPosition ) > 3f )
		{
			StopGrabbing();
			return;
		}

		// Something else took it from us (another player, or it went into a gun)
		if ( CurrentGrabbable is not null && (!CurrentGrabbable.IsValid() || CurrentGrabbable.Hand != this) )
			CurrentGrabbable = null;

		var grabbable = TryFindGrabbable();
		Target = CurrentGrabbable.IsValid() ? null : grabbable;

		if ( grabbable.IsValid() && IsDown( grabbable.GrabInput ) )
			StartGrabbing( grabbable );
		else
			StopGrabbing();
	}

	protected override void OnDestroy()
	{
		StopGrabbing( true );
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( other.GetComponent<IGrabbable>() is { } grabbable )
			_hovered.Add( grabbable );
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		if ( other.GetComponent<IGrabbable>() is { } grabbable )
			_hovered.Remove( grabbable );
	}
}
