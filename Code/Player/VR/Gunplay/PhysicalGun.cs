/// <summary>
/// A gun held and worked by VR hands: a magazine goes in its <see cref="AttachmentPoint"/>, the slide feeds the
/// <see cref="GunChamber"/>, and the trigger of the hand on its grip fires. Ported from gunbuilder's Weapon.
/// <para>The holder's machine owns the gun and decides every shot. The shot is sent to everyone for its effects, and
/// the host deals the damage, as the player who fired, like any other weapon.</para>
/// </summary>
public sealed class PhysicalGun : GunplayInteractable
{
	/// <summary>
	/// Where the bullets come out.
	/// </summary>
	[Property, Group( "Setup" )] public GameObject MuzzleGameObject { get; set; }

	/// <summary>
	/// Where casings and unfired rounds fly out.
	/// </summary>
	[Property, Group( "Setup" )] public GameObject EjectionPort { get; set; }

	/// <summary>
	/// A round of this gun as an object, see <see cref="LooseRound"/>: what an unfired round thrown out by the slide is.
	/// </summary>
	[Property, Group( "Setup" )] public GameObject RoundPrefab { get; set; }

	/// <summary>
	/// Rounds per minute.
	/// </summary>
	[Property, Group( "Stats" )] public float RPM { get; set; } = 600;

	/// <summary>
	/// Keeps firing while the trigger's held. Off, like a pistol, each shot takes a fresh pull: let go of the trigger
	/// and squeeze it again.
	/// </summary>
	[Property, Group( "Stats" )] public bool Automatic { get; set; } = true;

	[Property, Group( "Stats" )] public float Damage { get; set; } = 12;

	/// <summary>
	/// How hard a bullet pushes what it hits.
	/// </summary>
	[Property, Group( "Stats" )] public float Force { get; set; } = 300;

	[Property, Group( "Stats" )] public float MaxRange { get; set; } = 4096;

	/// <summary>
	/// The bullet's radius.
	/// </summary>
	[Property, Group( "Stats" )] public float BulletSize { get; set; } = 1;

	[Property, Group( "Sounds" )] public SoundEvent ShootSound { get; set; }

	[Property, Group( "Sounds" )] public SoundEvent DryFireSound { get; set; }

	[Property, Group( "Recoil" )] public RangedFloat VerticalRecoil { get; set; } = new( 300, 600 );
	[Property, Group( "Recoil" )] public RangedFloat HorizontalRecoil { get; set; } = new( -200, 200 );
	[Property, Group( "Recoil" )] public float BaseRecoilDecay { get; set; } = 3f;

	[Property, Group( "Effects" )] public Color TracerColor { get; set; } = new Color( 1f, 0.85f, 0.6f, 0.5f );
	[Property, Group( "Effects" )] public float TracerWidth { get; set; } = 0.15f;
	[Property, Group( "Effects" )] public float TracerLifetime { get; set; } = 0.05f;

	[Property] public GunChamber Chamber { get; set; }

	[Property] public SlideReleaseSystem SlideReleaseSystem { get; set; }

	/// <summary>
	/// The magazine in the gun, if any.
	/// </summary>
	public GunMagazine Magazine { get; private set; }

	/// <summary>
	/// What this gun's rounds are.
	/// </summary>
	public Round RoundTemplate
	{
		get
		{
			if ( Magazine.IsValid() && Magazine.BulletPrefab is { } fromMagazine ) return fromMagazine;
			if ( RoundPrefab?.GetComponent<LooseRound>() is { } loose ) return loose.Bullet with { Prefab = RoundPrefab };
			return null;
		}
	}

	TimeSince TimeSinceShoot { get; set; }
	TimeUntil TimeUntilNextDryFire { get; set; }

	Vector2 CurrentRecoilAmount { get; set; }

	/// <summary>
	/// The grip hand's trigger was down last frame, for a fresh pull on a gun that isn't <see cref="Automatic"/>.
	/// </summary>
	bool _triggerWasDown;

	/// <summary>
	/// The hand on the grip, on the holder's machine.
	/// </summary>
	public GunplayHand GripHand => PrimaryGrabPoint?.Hand;

	public override bool CanAccept( Attachable attachable )
	{
		// Only a magazine of our caliber goes in the magazine well
		if ( attachable.GetComponent<GunMagazine>() is not { } magazine ) return false;
		return RoundTemplate is not { } round || magazine.Caliber == round.Caliber;
	}

	protected override void OnAttachableAdded( Attachable attachable, AttachmentPoint attachmentPoint )
	{
		if ( attachable.GetComponent<GunMagazine>() is { } magazine )
			Magazine = magazine;

		if ( !IsProxy ) VRMode.Pulse( GripHand?.Controller, 0.3f, 0.3f );
	}

	protected override void OnAttachableRemoved( Attachable attachable, AttachmentPoint attachmentPoint )
	{
		if ( attachable.GetComponent<GunMagazine>() is { } magazine && magazine == Magazine )
			Magazine = null;
	}

	protected override Rotation GetHoldRotation()
	{
		return base.GetHoldRotation() * Rotation.From( -CurrentRecoilAmount.y * Time.Delta, CurrentRecoilAmount.x * Time.Delta, 0 );
	}

	void TryDetachMagazine()
	{
		Magazine?.Attachable?.Detach();
	}

	/// <summary>
	/// Chambers a round from the magazine. Owner only.
	/// </summary>
	public bool TryFeedFromMagazine()
	{
		if ( !Chamber.IsValid() ) return false;
		return Chamber.Feed( Magazine ) > 0;
	}

	/// <summary>
	/// Throws out the round in the chamber, unfired, for the slide being pulled back. Owner only.
	/// </summary>
	internal void TryEjectFromChamber()
	{
		if ( Chamber?.Eject() is null || !EjectionPort.IsValid() ) return;

		var velocity = (EjectionPort.WorldRotation.Right * 150f + EjectionPort.WorldRotation.Up * 100f) + (Rigidbody?.Velocity ?? 0f);
		SpawnLooseRound( EjectionPort.WorldPosition, EjectionPort.WorldRotation, velocity );
	}

	/// <summary>
	/// Clients can't create networked objects: the host makes the round that flew out.
	/// </summary>
	[Rpc.Host]
	void SpawnLooseRound( Vector3 position, Rotation rotation, Vector3 velocity )
	{
		if ( RoundPrefab is null ) return;
		if ( !Rpc.Caller.IsHost && VRPlayer.Get( Player.FindForConnection( Rpc.Caller ) ) is null ) return;

		var go = RoundPrefab.Clone( new Transform( position, rotation ) );
		go.Tags.Add( "removable" );
		go.NetworkSpawn( true, null );

		if ( go.GetComponent<Rigidbody>() is { } body )
			body.Velocity = velocity;
	}

	float RPMToSeconds() => 60f / RPM;

	Ray WeaponRay => new( MuzzleGameObject.WorldPosition, MuzzleGameObject.WorldRotation.Forward );

	bool CanShoot()
	{
		if ( SlideReleaseSystem?.IsPulled ?? false )
		{
			TryDryShoot();
			return false;
		}

		return TimeSinceShoot >= RPMToSeconds();
	}

	protected override void OnUpdate()
	{
		CurrentRecoilAmount = CurrentRecoilAmount.LerpTo( 0, Time.Delta * BaseRecoilDecay );

		if ( IsProxy || GripHand is not { } hand || hand.Controller is not { } controller )
		{
			_triggerWasDown = false;
			return;
		}

		var triggerDown = hand.IsTriggerDown();
		var pulled = triggerDown && !_triggerWasDown;
		_triggerWasDown = triggerDown;

		// Clicking a menu with the laser doesn't fire
		if ( hand.Owner?.PointingAtUI ?? false ) return;

		if ( Automatic ? triggerDown : pulled )
			TryShoot();

		if ( controller.ButtonB.IsPressed )
			TryDetachMagazine();
	}

	void TryDryShoot()
	{
		if ( !TimeUntilNextDryFire ) return;

		GunplaySounds.Play( DryFireSound, WorldPosition );
		TimeUntilNextDryFire = 0.5f;
	}

	Vector2 CalcRecoil() => new()
	{
		y = Game.Random.Float( VerticalRecoil.Min, VerticalRecoil.Max ),
		x = Game.Random.Float( HorizontalRecoil.Min, HorizontalRecoil.Max )
	};

	/// <summary>
	/// Fires the chambered round, if there is one. Owner only.
	/// </summary>
	public void TryShoot()
	{
		if ( IsProxy || !MuzzleGameObject.IsValid() || !CanShoot() ) return;

		TimeSinceShoot = 0;

		if ( Chamber?.Eject() is null )
		{
			TryDryShoot();
			return;
		}

		CurrentRecoilAmount += CalcRecoil();
		VRMode.Pulse( GripHand?.Controller, 0.8f, 0.6f );
		if ( SecondaryGrabPoint?.Hand?.Controller is { } support )
			VRMode.Pulse( support, 0.5f, 0.5f );

		var ray = WeaponRay;
		Shoot( ray.Position, ray.Forward );

		// The next round comes up from the magazine, or the slide locks back on an empty one
		if ( !TryFeedFromMagazine() )
			SlideReleaseSystem?.PullManually();
	}

	/// <summary>
	/// A shot, on every machine: its effects everywhere, and its damage on the host.
	/// </summary>
	[Rpc.Broadcast]
	void Shoot( Vector3 origin, Vector3 direction )
	{
		var ray = new Ray( origin, direction.Normal );
		var shooter = Player.FindForConnection( Rpc.Caller );

		if ( Networking.IsHost || !Networking.IsActive )
		{
			// Only a VR player's hands fire these
			if ( Rpc.Caller.IsHost || VRPlayer.Get( shooter ) is not null )
			{
				var attacker = shooter.IsValid() ? shooter.GameObject : GameObject;
				BaseCombatWeapon.ShootBullet( Scene, ray, MaxRange, BulletSize, Damage, Force, attacker, GameObject, attacker );
			}
		}

		var trace = Scene.Trace.Ray( ray, MaxRange )
			.Size( BulletSize )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( "trigger" )
			.UseHitboxes();

		if ( shooter.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( shooter.GameObject );

		var tr = trace.Run();

		if ( tr.Hit )
			CreateImpactEffects( tr.GameObject, tr.Surface, tr.EndPosition, tr.Normal );

		DoShootEffects();
		DoTracer( ray.Position, tr.EndPosition );
		EjectCasing();
	}

	void EjectCasing()
	{
		if ( !EjectionPort.IsValid() || RoundTemplate is not { } round ) return;

		var casing = (round with { IsSpent = true }).CreateCasing( EjectionPort.WorldPosition, EjectionPort.WorldRotation );
		if ( casing?.GetComponent<Rigidbody>() is not { } body ) return;

		var port = EjectionPort.WorldRotation;
		body.Velocity = (Rigidbody?.Velocity ?? 0f) + port.Right * 150f + port.Up * Game.Random.Float( 60f, 120f );
		body.AngularVelocity = casing.WorldRotation.Forward * 10f;
	}

	static void DestroyAfter( GameObject gameObject, float seconds )
	{
		if ( !gameObject.IsValid() ) return;

		var effect = gameObject.Components.GetOrCreate<TemporaryEffect>();
		effect.DestroyAfterSeconds = seconds;
	}

	/// <summary>
	/// The surface's bullet impact effect, decal and sound.
	/// </summary>
	void CreateImpactEffects( GameObject hitObject, Surface surface, Vector3 pos, Vector3 normal )
	{
		if ( surface is null ) return;

		if ( surface.PrefabCollection.BulletImpact is GameObject impactPrefab )
		{
			var impact = impactPrefab.Clone( new Transform( pos + normal, Rotation.LookAt( normal ) ) );
			DestroyAfter( impact, 5f );
		}

		if ( surface.PrefabCollection.BulletImpactDecal is GameObject decalPrefab )
		{
			var decal = decalPrefab.Clone( new Transform( pos + normal, Rotation.LookAt( -normal ) ) );

			// Stays on what it hit as that moves
			if ( hitObject.IsValid() )
				decal.SetParent( hitObject, true );

			DestroyAfter( decal, 30f );
		}

		if ( surface.SoundCollection.Bullet is SoundEvent impactSound )
			Sound.Play( impactSound, pos );
	}

	void DoShootEffects()
	{
		if ( ShootSound is null ) return;

		// At the muzzle, for the shooter too. Not ListenLocal: that puts the listener at 0,0,0, so the muzzle's world
		// position read as that far from the shooter's head, and the shot faded out with the distance.
		Sound.Play( ShootSound, MuzzleGameObject.WorldPosition );
	}

	void DoTracer( Vector3 startPosition, Vector3 endPosition )
	{
		var origin = MuzzleGameObject.IsValid() ? MuzzleGameObject.WorldPosition : startPosition;

		var gameObject = Scene.CreateObject();
		gameObject.Name = "Tracer";
		gameObject.NetworkMode = NetworkMode.Never;
		gameObject.Flags |= GameObjectFlags.NotSaved;
		gameObject.WorldPosition = origin;

		var line = gameObject.AddComponent<LineRenderer>();
		line.UseVectorPoints = true;
		line.VectorPoints = new() { origin, endPosition };
		line.Color = TracerColor;
		line.Width = TracerWidth;
		line.Additive = true;
		line.Opaque = false;
		line.Lighting = false;
		line.CastShadows = false;

		DestroyAfter( gameObject, TracerLifetime );
	}
}
