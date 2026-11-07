/// <summary>
/// Hits what it's swung into, like a crowbar held in a VR hand: the harder the swing, the more damage, up to
/// <see cref="MaxDamage"/>. The holder's machine feels the hit and the host deals it, as the player who swung.
/// </summary>
public sealed class MeleeImpact : Component, Component.ICollisionListener
{
	[Property] public GunplayInteractable Interactable { get; set; }

	[Property] public float MaxDamage { get; set; } = 12f;

	/// <summary>
	/// Slower than this, in inches a second, a knock does nothing.
	/// </summary>
	[Property] public float MinSpeed { get; set; } = 120f;

	/// <summary>
	/// This fast or more deals all of <see cref="MaxDamage"/>.
	/// </summary>
	[Property] public float FullSpeed { get; set; } = 450f;

	[Property] public float Force { get; set; } = 1000f;

	/// <summary>
	/// Between hits on the same thing.
	/// </summary>
	[Property] public float Cooldown { get; set; } = 0.4f;

	[Property] public SoundEvent HitSound { get; set; }

	readonly Dictionary<GameObject, TimeSince> _lastHit = new();

	void ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( IsProxy || !Interactable.IsValid() || !Interactable.IsHeld ) return;

		var other = collision.Other.GameObject?.Root;
		if ( !other.IsValid() || other == GameObject.Root ) return;

		// Not the one swinging it
		var hand = Interactable.PrimaryGrabPoint?.Hand;
		if ( hand?.Owner?.Player is { } holder && other == holder.GameObject.Root ) return;

		var speed = MathF.Abs( collision.Contact.NormalSpeed );
		if ( speed < MinSpeed ) return;

		if ( _lastHit.TryGetValue( other, out var since ) && since < Cooldown ) return;
		_lastHit[other] = 0;

		var damage = MaxDamage * speed.Remap( MinSpeed, FullSpeed, 0.25f, 1f, true );
		// Along the swing at the point that hit
		var body = Interactable.Rigidbody;
		var direction = body.IsValid() ? body.GetVelocityAtPoint( collision.Contact.Point ).Normal : -collision.Contact.Normal;
		if ( direction.IsNearZeroLength ) direction = -collision.Contact.Normal;
		VRMode.Pulse( hand?.Controller, speed.Remap( MinSpeed, FullSpeed, 0.3f, 1f, true ), 0.15f );

		Hit( collision.Contact.Point, direction, damage );
	}

	/// <summary>
	/// The hit, on every machine: its sound everywhere, its damage on the host.
	/// </summary>
	[Rpc.Broadcast]
	void Hit( Vector3 point, Vector3 direction, float damage )
	{
		if ( HitSound is not null )
			Sound.Play( HitSound, point );

		if ( !Networking.IsHost && Networking.IsActive ) return;

		var shooter = Player.FindForConnection( Rpc.Caller );
		if ( !Rpc.Caller.IsHost && VRPlayer.Get( shooter ) is null ) return;

		var attacker = shooter.IsValid() ? shooter.GameObject : GameObject;
		var ray = new Ray( point - direction * 6f, direction );
		BaseCombatWeapon.ShootBullet( Scene, ray, 12f, 2f, damage, Force, attacker, GameObject, attacker );
	}
}
