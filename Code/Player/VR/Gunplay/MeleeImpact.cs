/// <summary>
/// Hits what it's swung into, like a crowbar held in a VR hand: the harder the swing, the more damage, up to
/// <see cref="MaxDamage"/>. The holder's machine feels the hit and the host deals it, as the player who swung.
/// <para>A held object is keyframed to the hand, and a keyframed body doesn't get contacts with an NPC or a player.
/// So a trigger capsule runs along it, from the first of its <see cref="HitPoints"/> to the last: the physics reports
/// whatever it overlaps, moving or not. How hard the hit is comes from how fast those points have been moving lately,
/// so a hit that lands as the swing slows down still counts.</para>
/// </summary>
public sealed class MeleeImpact : Component, Component.ICollisionListener
{
	[Property] public GunplayInteractable Interactable { get; set; }

	[Property] public float MaxDamage { get; set; } = 12f;

	/// <summary>
	/// Slower than this, in inches a second, a knock does nothing.
	/// </summary>
	[Property] public float MinSpeed { get; set; } = 100f;

	/// <summary>
	/// This fast or more deals all of <see cref="MaxDamage"/>.
	/// </summary>
	[Property] public float FullSpeed { get; set; } = 400f;

	[Property] public float Force { get; set; } = 1000f;

	/// <summary>
	/// Between hits on the same thing.
	/// </summary>
	[Property] public float Cooldown { get; set; } = 0.4f;

	/// <summary>
	/// The points along it that hit, in its own space, like the crowbar's shaft and hook. The hit volume runs from the
	/// first to the last.
	/// </summary>
	[Property] public List<Vector3> HitPoints { get; set; } = new() { new Vector3( 0, 0, 10 ), new Vector3( 0, 0, 19 ), new Vector3( 0, 0, 26 ) };

	/// <summary>
	/// How thick it is round those points.
	/// </summary>
	[Property] public float Radius { get; set; } = 1.75f;

	[Property] public SoundEvent HitSound { get; set; }

	readonly Dictionary<GameObject, TimeSince> _lastHit = new();
	Vector3[] _previous;

	/// <summary>
	/// How fast it's been swung lately, and which way: the fastest of its points, fading over a tenth of a second.
	/// </summary>
	float _swingSpeed;
	Vector3 _swingDirection;

	GunplayHand Hand => Interactable?.PrimaryGrabPoint?.Hand;

	Player Holder => Hand?.Owner?.Player;

	protected override void OnStart()
	{
		if ( HitPoints is not { Count: > 0 } ) return;

		// Only on this machine: it's the holder's that hits
		var go = new GameObject( false, "Melee Hit Volume" );
		go.NetworkMode = NetworkMode.Never;
		go.Flags |= GameObjectFlags.NotNetworked | GameObjectFlags.NotSaved;
		go.Tags.Add( "trigger" );
		go.SetParent( GameObject, false );

		var capsule = go.AddComponent<CapsuleCollider>();
		capsule.Start = HitPoints.First();
		capsule.End = HitPoints.Last();
		capsule.Radius = Radius;
		capsule.IsTrigger = true;

		go.AddComponent<MeleeHitVolume>().Impact = this;
		go.Enabled = true;
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy || !Interactable.IsValid() || !Interactable.IsHeld || Time.Delta <= 0f )
		{
			_previous = null;
			_swingSpeed = 0f;
			return;
		}

		_swingSpeed = MathF.Max( 0f, _swingSpeed - FullSpeed * 10f * Time.Delta );

		var now = HitPoints.Select( p => WorldTransform.PointToWorld( p ) ).ToArray();
		if ( _previous is { } before && before.Length == now.Length )
		{
			for ( var i = 0; i < now.Length; i++ )
			{
				var moved = now[i] - before[i];
				var speed = moved.Length / Time.Delta;
				if ( speed <= _swingSpeed ) continue;

				_swingSpeed = speed;
				_swingDirection = moved.Normal;
			}
		}

		_previous = now;
	}

	/// <summary>
	/// The hit volume overlaps something.
	/// </summary>
	internal void OnTouch( Collider other )
	{
		if ( IsProxy || !Interactable.IsValid() || !Interactable.IsHeld ) return;

		var target = other.GameObject?.Root;
		if ( !target.IsValid() || target == GameObject.Root ) return;
		if ( target.Tags.Has( "hands" ) ) return;

		// Only what takes a hit: things that take damage, things that move
		if ( target.GetComponentInChildren<Component.IDamageable>() is null && other.Rigidbody is not { MotionEnabled: true } )
			return;

		var center = target.GetBounds().Center;
		var from = HitPoints.Select( p => WorldTransform.PointToWorld( p ) ).OrderBy( p => p.DistanceSquared( center ) ).First();
		TryHit( target, from, _swingSpeed );
	}

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy || !Interactable.IsValid() || !Interactable.IsHeld ) return;

		var target = collision.Other.GameObject?.Root;
		if ( !target.IsValid() ) return;

		TryHit( target, collision.Contact.Point, MathF.Max( _swingSpeed, MathF.Abs( collision.Contact.NormalSpeed ) ) );
	}

	void TryHit( GameObject target, Vector3 from, float speed )
	{
		if ( target == GameObject.Root || speed < MinSpeed ) return;
		if ( Holder is { } holder && target == holder.GameObject.Root ) return;

		if ( _lastHit.TryGetValue( target, out var since ) && since < Cooldown ) return;
		_lastHit[target] = 0;

		var damage = MaxDamage * speed.Remap( MinSpeed, FullSpeed, 0.4f, 1f, true );
		VRMode.Pulse( Hand?.Controller, speed.Remap( MinSpeed, FullSpeed, 0.3f, 1f, true ), 0.15f );

		Hit( target, from, _swingDirection, damage );
	}

	/// <summary>
	/// The hit, on every machine: its sound everywhere, its damage on the host.
	/// </summary>
	[Rpc.Broadcast]
	void Hit( GameObject target, Vector3 from, Vector3 swing, float damage )
	{
		if ( HitSound is not null )
			Sound.Play( HitSound, from );

		if ( !Networking.IsHost && Networking.IsActive ) return;
		if ( !target.IsValid() ) return;

		var shooter = Player.FindForConnection( Rpc.Caller );
		if ( !Rpc.Caller.IsHost && VRPlayer.Get( shooter ) is null ) return;

		var attacker = shooter.IsValid() ? shooter.GameObject : GameObject;

		// At the target, from a little behind the bar along the swing: past the bar and whoever swung it, it lands on
		// the target's hitboxes or body
		var center = target.GetBounds().Center;
		var toward = (center - from).Normal;
		if ( toward.IsNearZeroLength ) toward = swing;
		var start = from - toward * 8f;

		var tr = PhysicalGun.BulletTrace( Scene, new Ray( start, toward ), 8f + from.Distance( center ) + 8f, 2f, GameObject, shooter );
		if ( !tr.Hit || tr.GameObject?.Root != target )
			tr = PhysicalGun.BulletTrace( Scene, new Ray( start, (center - start).Normal ), start.Distance( center ) + 16f, 4f, GameObject, shooter );

		BaseCombatWeapon.ShootBullet( tr, damage, Force, attacker, GameObject, null, false );
	}
}

/// <summary>
/// The trigger along a <see cref="MeleeImpact"/>, telling it what it overlaps.
/// </summary>
public sealed class MeleeHitVolume : Component, Component.ITriggerListener
{
	public MeleeImpact Impact { get; set; }

	void ITriggerListener.OnTriggerEnter( Collider other ) => Impact?.OnTouch( other );
}
