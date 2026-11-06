using Sandbox.VR;

public sealed partial class VRPlayer
{
	GunplayHand _leftGunplayHand;
	GunplayHand _rightGunplayHand;

	/// <summary>
	/// The hand holding weapons and tools, for taking hold of gunplay objects. Local VR player only.
	/// </summary>
	public GunplayHand DominantGunplayHand => VRMode.LeftHanded ? _leftGunplayHand : _rightGunplayHand;

	/// <summary>
	/// The other hand, for taking hold of gunplay objects. Local VR player only.
	/// </summary>
	public GunplayHand OffGunplayHand => VRMode.LeftHanded ? _rightGunplayHand : _leftGunplayHand;

	/// <summary>
	/// The dominant hand holds a gunplay object: the inventory weapon's put away for everyone, see
	/// <see cref="PlaceHeldWeapon"/>.
	/// </summary>
	[Sync] public bool DominantHandBusy { get; private set; }

	/// <summary>
	/// The hands that pick up the physical guns, their magazines and rounds. Only for our own VR player, and not
	/// networked: what they hold is.
	/// </summary>
	void CreateGunplayHands()
	{
		_leftGunplayHand = CreateGunplayHand( true );
		_rightGunplayHand = CreateGunplayHand( false );
	}

	GunplayHand CreateGunplayHand( bool left )
	{
		var go = Scene.CreateObject();
		go.Name = left ? "Gunplay Hand (left)" : "Gunplay Hand (right)";
		go.NetworkMode = NetworkMode.Never;
		go.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
		go.Tags.Add( "hands" );

		var body = go.AddComponent<Rigidbody>();
		body.Gravity = false;
		body.MotionEnabled = false;

		var trigger = go.AddComponent<SphereCollider>();
		trigger.Center = new Vector3( 2.4f, 0, 0 );
		trigger.Radius = 3f;
		trigger.IsTrigger = true;

		var hand = go.AddComponent<GunplayHand>();
		hand.IsLeft = left;
		hand.Owner = this;

		return hand;
	}

	void DestroyGunplayHands()
	{
		// Already gone with the scene when play stops: a destroyed hand has no GameObject left
		if ( _leftGunplayHand.IsValid() ) _leftGunplayHand.GameObject.Destroy();
		if ( _rightGunplayHand.IsValid() ) _rightGunplayHand.GameObject.Destroy();
		_leftGunplayHand = null;
		_rightGunplayHand = null;
	}

	/// <summary>
	/// The hand holds a gunplay object.
	/// </summary>
	static bool HoldsGunplay( GunplayHand hand ) => hand.IsValid() && hand.IsHolding();

	/// <summary>
	/// The hand holds a physical gun by its grip, so its buttons are the gun's.
	/// </summary>
	static bool HoldsGun( GunplayHand hand ) => HoldsGunplay( hand ) && hand.CurrentGrabbable.Interactable is PhysicalGun;

	/// <summary>
	/// The hand's grip goes to a gunplay object: it holds one, or has one to take hold of. Using and carrying don't get it.
	/// </summary>
	static bool GripTakenByGunplay( GunplayHand hand ) => HoldsGunplay( hand ) || TargetTakenWith( hand, GrabInputType.Grip );

	/// <summary>
	/// The hand's trigger goes to a gunplay object: it holds one, or is at something taken with the trigger, like a
	/// gun's slide. The weapon and undo don't get it.
	/// </summary>
	static bool TriggerTakenByGunplay( GunplayHand hand ) => HoldsGunplay( hand ) || TargetTakenWith( hand, GrabInputType.Trigger );

	static bool TargetTakenWith( GunplayHand hand, GrabInputType input ) => hand.IsValid() && hand.Target is { } target && target.IsValid() && target.GrabInput == input;

	/// <summary>
	/// The grab point each hand holds, synced so every peer puts the body's hand on it, see <see cref="GripIkTarget"/>.
	/// </summary>
	[Sync] GameObject LeftGrip { get; set; }
	[Sync] GameObject RightGrip { get; set; }

	void UpdateGunplayHands()
	{
		DominantHandBusy = HoldsGunplay( DominantGunplayHand );
		LeftGrip = HoldsGunplay( _leftGunplayHand ) ? _leftGunplayHand.CurrentGrabbable.GameObject : null;
		RightGrip = HoldsGunplay( _rightGunplayHand ) ? _rightGunplayHand.CurrentGrabbable.GameObject : null;
	}

	// gunbuilder's hand model on a grab point: its hand bone, relative to the grab point it's set on. The body's hand bones
	// lie the same way (fingers along x, thumb along y), so the body's hand goes where gunbuilder's did, around the grip.
	static readonly Transform LeftGripBone = new( new Vector3( -2.541992f, 0.9680022f, -0.3415412f ), new Rotation( 0.726409f, -0.004804412f, 0.1322637f, 0.6743983f ) );
	static readonly Transform RightGripBone = new( new Vector3( -2.542029f, -0.9679199f, -0.3414217f ), new Rotation( 0.6743067f, -0.1321493f, 0.004909032f, 0.7265142f ) );

	/// <summary>
	/// Where the body's hand bone goes while that hand holds a gun, a magazine or a round: on the grab point, the way
	/// gunbuilder showed it, rather than on the controller - it's the gun that's lined up with the real hand.
	/// </summary>
	Transform? GripIkTarget( bool left )
	{
		var grip = left ? LeftGrip : RightGrip;
		if ( !grip.IsValid() ) return null;

		// On the grab point, turned like the real hand: gunbuilder's hand pose from the controller. On a gun's grip that's
		// how the grab point's turned anyway, the gun's held that way. On the SG-5's front grip it isn't - the gun points
		// along both hands, and the grab point's turn (set up for a right hand, palm down on top) put the left hand on
		// upside down.
		var controller = left ? LeftHand : RightHand;
		var bone = left ? LeftGripBone : RightGripBone;
		var real = new Transform( grip.WorldPosition, controller.Rotation * GripHoldOffset ).ToWorld( bone );

		// A supporting hand, like on the SG-5's front grip, holds the gun as gunbuilder showed it - along the barrel - but
		// turned round the barrel so its palm faces where the real palm does. Taken as it is, the grab point's turn puts the
		// palm up under the barrel, and the body's forearm twisted round to get there; turned like the controller instead,
		// the hand lay across the barrel.
		if ( grip.GetComponent<GrabPoint>() is { IsSecondaryGrabPoint: true } support )
		{
			var held = grip.WorldTransform.WithScale( 1f ).ToWorld( bone );
			var axis = (support.Interactable.IsValid() ? support.Interactable.WorldRotation : grip.WorldRotation).Forward;

			// The palm's the side the fingers close to: down the right hand bone's z, up the left one's
			var palm = left ? Vector3.Up : Vector3.Down;
			var from = Flatten( held.Rotation * palm, axis );
			var to = Flatten( real.Rotation * palm, axis );
			var degrees = MathF.Atan2( Vector3.Dot( axis, Vector3.Cross( from, to ) ), Vector3.Dot( from, to ) ) * 180f / MathF.PI;

			var turn = Rotation.FromAxis( axis, degrees );
			var pivot = grip.WorldPosition;
			return new Transform( pivot + turn * (held.Position - pivot), turn * held.Rotation );
		}

		return real;
	}

	/// <summary>
	/// <paramref name="v"/> with its part along <paramref name="axis"/> taken out.
	/// </summary>
	static Vector3 Flatten( Vector3 v, Vector3 axis ) => (v - axis * Vector3.Dot( v, axis )).Normal;

	/// <summary>
	/// gunbuilder's hand model sat pitched 45 degrees down from the controller's grip, see <see cref="GunplayHand"/>.
	/// </summary>
	static readonly Rotation GripHoldOffset = new( 0f, 0.3826835f, 0f, 0.9238795f );

	/// <summary>
	/// A controller's finger curls, packed for syncing, with the fingers that hold a grab point set as it says.
	/// </summary>
	static int PackCurls( VRController hand, HandPreset preset )
	{
		var packed = 0;
		for ( var i = 0; i < 5; i++ )
		{
			var curl = hand.GetFingerCurl( i );
			if ( preset is not null ) curl = preset.Curl( i, curl );

			packed |= (int)(curl.Clamp( 0f, 1f ) * 63f) << (i * 6);
		}

		return packed;
	}
}
