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

	/// <summary>
	/// Bullets per shot, more than one for a shotgun's pellets. Each deals <see cref="Damage"/>.
	/// </summary>
	[Property, Group( "Stats" )] public int Pellets { get; set; } = 1;

	/// <summary>
	/// How far the bullets stray from the barrel's line, in degrees.
	/// </summary>
	[Property, Group( "Stats" )] public float Spread { get; set; } = 0;

	/// <summary>
	/// Worked by hand after every shot, like a pump or a bolt: the casing stays in the chamber until the
	/// <see cref="SlideReleaseSystem"/>'s part is pulled back, and the next round goes in as it goes forward.
	/// </summary>
	[Property, Group( "Stats" )] public bool ManualAction { get; set; }

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
	/// A magazine built into the gun, like a shotgun's tube, when it has no magazine that comes out.
	/// </summary>
	[Property] public InternalMagazine InternalMagazine { get; set; }

	/// <summary>
	/// A revolver's cylinder, in place of a chamber.
	/// </summary>
	[Property] public RevolverCylinder Cylinder { get; set; }

	/// <summary>
	/// Where the next round's fired from.
	/// </summary>
	public IGunChamber ActiveChamber => Cylinder.IsValid() ? Cylinder : Chamber.IsValid() ? Chamber : null;

	/// <summary>
	/// What's fired instead of bullets, like a rocket. The host makes it at the muzzle.
	/// </summary>
	[Property, Group( "Projectile" )] public GameObject ProjectilePrefab { get; set; }

	[Property, Group( "Projectile" )] public float ProjectileSpeed { get; set; } = 1024f;

	/// <summary>
	/// A magazine going in chambers its round straight away, like a rocket into a launcher: there's nothing to rack.
	/// </summary>
	[Property, Group( "Projectile" )] public bool ChambersOnInsert { get; set; }

	/// <summary>
	/// An emptied magazine is used up and goes, like the rocket that was in the tube.
	/// </summary>
	[Property, Group( "Projectile" )] public bool ConsumesEmptyMagazine { get; set; }

	/// <summary>
	/// The last rocket fired, steered at what the muzzle points at while the trigger stays down.
	/// </summary>
	[Sync( SyncFlags.FromHost )] RpgProjectile Projectile { get; set; }

	/// <summary>
	/// A shot throws its casing out of the ejection port: not when it's worked by hand, not from a revolver's cylinder,
	/// not for a rocket.
	/// </summary>
	bool EjectsOnShot => !ManualAction && !Cylinder.IsValid() && ProjectilePrefab is null;

	/// <summary>
	/// Where the chamber's fed from: the magazine in the gun, or the one built into it.
	/// </summary>
	public RoundStore AmmoSource => Magazine.IsValid() ? Magazine : InternalMagazine.IsValid() ? InternalMagazine : null;

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
			if ( AmmoSource is { BulletPrefab: { } fromMagazine } ) return fromMagazine;
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

		if ( IsProxy ) return;

		VRMode.Pulse( GripHand?.Controller, 0.3f, 0.3f );

		if ( ChambersOnInsert )
			TryFeedFromMagazine();
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
		if ( ActiveChamber is not { } chamber || AmmoSource is not { } source ) return false;
		return chamber.Feed( source ) > 0;
	}

	/// <summary>
	/// Throws out what's in the chamber, for the slide being pulled back: the casing of the round just fired, or the round
	/// in it, unfired. Owner only.
	/// </summary>
	internal void TryEjectFromChamber()
	{
		if ( ActiveChamber is not { } chamber ) return;

		if ( chamber.EjectSpentCasing() )
		{
			BroadcastCasing();
			return;
		}

		if ( chamber.Eject() is null || !EjectionPort.IsValid() ) return;

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

		// A bolt that isn't home doesn't fire, and doesn't click either
		if ( ManualAction && SlideReleaseSystem.IsValid() && !SlideReleaseSystem.IsClosed )
			return false;

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

		if ( triggerDown && ProjectilePrefab is not null )
			GuideProjectile();

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

		if ( ActiveChamber?.Eject() is null )
		{
			TryDryShoot();
			return;
		}

		CurrentRecoilAmount += CalcRecoil();
		VRMode.Pulse( GripHand?.Controller, 0.8f, 0.6f );
		if ( SecondaryGrabPoint?.Hand?.Controller is { } support )
			VRMode.Pulse( support, 0.5f, 0.5f );

		var ray = WeaponRay;
		Shoot( ray.Position, ray.Forward, Game.Random.Int( 0, int.MaxValue - 1 ), EjectsOnShot );

		// The rocket's gone from the tube
		if ( ConsumesEmptyMagazine && Magazine.IsValid() && !Magazine.HasAmmo )
		{
			var spent = Magazine;
			spent.Attachable.Detach();
			DestroyOnHost( spent.GameObject );
		}

		// Worked by hand: the casing waits in the chamber for the pump or the bolt
		if ( ManualAction )
		{
			ActiveChamber.LeaveSpentCasing();
			return;
		}

		// The next round comes up from the magazine, or the slide locks back on an empty one
		if ( !TryFeedFromMagazine() )
			SlideReleaseSystem?.PullManually();
	}

	/// <summary>
	/// The directions of a shot's bullets, from its seed: the same on every machine, so effects and damage agree.
	/// </summary>
	IEnumerable<Vector3> BulletDirections( Vector3 forward, int seed )
	{
		var count = Math.Max( Pellets, 1 );
		if ( Spread <= 0f )
		{
			for ( var i = 0; i < count; i++ ) yield return forward;
			yield break;
		}

		var random = new Random( seed );
		var look = Rotation.LookAt( forward );
		for ( var i = 0; i < count; i++ )
		{
			// Evenly over the cone's disc, not bunched in its middle
			var angle = MathF.Sqrt( (float)random.NextDouble() ) * Spread;
			var around = (float)random.NextDouble() * 360f;
			yield return look * (Rotation.FromRoll( around ) * Rotation.FromYaw( angle )).Forward;
		}
	}

	/// <summary>
	/// Clients can't destroy networked objects: the host does it.
	/// </summary>
	[Rpc.Host]
	static void DestroyOnHost( GameObject gameObject )
	{
		if ( gameObject.IsValid() && gameObject.GetComponent<GunMagazine>().IsValid() )
			gameObject.Destroy();
	}

	/// <summary>
	/// The host makes the rocket at the muzzle, fired by whoever pulled the trigger.
	/// </summary>
	void LaunchProjectile( Ray ray, Player shooter )
	{
		if ( ProjectilePrefab is null ) return;

		var go = ProjectilePrefab.Clone( new Transform( ray.Position + ray.Forward * 8f, Rotation.LookAt( ray.Forward ) ) );
		if ( go.GetComponent<RpgProjectile>() is not { } projectile )
		{
			go.Destroy();
			return;
		}

		projectile.Instigator = shooter;
		go.NetworkSpawn();
		projectile.UpdateDirection( ray.Forward, ProjectileSpeed );
		Projectile = projectile;
	}

	/// <summary>
	/// While the trigger stays down after a rocket's fired, it flies at what the muzzle points at. Owner only.
	/// </summary>
	void GuideProjectile()
	{
		if ( !Projectile.IsValid() || !MuzzleGameObject.IsValid() ) return;

		var ray = WeaponRay;
		var tr = Scene.Trace.Ray( ray, 16384f )
			.IgnoreGameObjectHierarchy( GameObject.Root )
			.WithoutTags( "trigger", "projectile" )
			.Run();

		Projectile.UpdateWithTarget( tr.Hit ? tr.HitPosition : ray.Position + ray.Forward * 16384f, ProjectileSpeed );
	}

	/// <summary>
	/// A casing thrown out of the chamber by hand, on every machine.
	/// </summary>
	[Rpc.Broadcast]
	void BroadcastCasing() => EjectCasing();

	/// <summary>
	/// A shot, on every machine: its effects everywhere, and its damage on the host.
	/// </summary>
	[Rpc.Broadcast]
	void Shoot( Vector3 origin, Vector3 direction, int seed, bool ejectCasing )
	{
		var shooter = Player.FindForConnection( Rpc.Caller );

		// Only a VR player's hands fire these
		var dealsDamage = (Networking.IsHost || !Networking.IsActive) && (Rpc.Caller.IsHost || VRPlayer.Get( shooter ) is not null);
		var attacker = shooter.IsValid() ? shooter.GameObject : GameObject;

		if ( ProjectilePrefab is not null )
		{
			if ( dealsDamage )
				LaunchProjectile( new Ray( origin, direction.Normal ), shooter );

			DoShootEffects();
			return;
		}

		foreach ( var bullet in BulletDirections( direction.Normal, seed ) )
		{
			var ray = new Ray( origin, bullet );
			var tr = BulletTrace( ray, shooter );

			// Our own trace, past the gun: the engine's only leaves out the shooter, and the bullet, as wide as it is,
			// started out in the gun's own barrel and hit that
			if ( dealsDamage )
				BaseCombatWeapon.ShootBullet( tr, Damage, Force, attacker, GameObject, null, true );

			if ( tr.Hit )
				CreateImpactEffects( tr.GameObject, tr.Surface, tr.EndPosition, tr.Normal );

			DoTracer( ray.Position, tr.EndPosition );
		}

		DoShootEffects();
		if ( ejectCasing ) EjectCasing();
	}

	/// <summary>
	/// A bullet's path, past this gun and whoever fired it.
	/// </summary>
	internal static SceneTraceResult BulletTrace( Scene scene, Ray ray, float range, float size, GameObject self, Player shooter )
	{
		var trace = scene.Trace.Ray( ray, range )
			.Size( size )
			.IgnoreGameObjectHierarchy( self.Root )
			.WithoutTags( "trigger" )
			.UseHitboxes();

		if ( shooter.IsValid() )
			trace = trace.IgnoreGameObjectHierarchy( shooter.GameObject );

		return trace.Run();
	}

	SceneTraceResult BulletTrace( Ray ray, Player shooter ) => BulletTrace( Scene, ray, MaxRange, BulletSize, GameObject, shooter );

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
