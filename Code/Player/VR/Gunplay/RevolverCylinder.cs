/// <summary>
/// A revolver's cylinder, its chambers in place of a magazine and a chamber. Each pull of the trigger turns it a chamber
/// on and fires the one under the hammer, double action. A on the grip hand swings it out, or back in, as does a flick
/// of the wrist; out, B or the muzzle pointed at the sky throws everything in it out, and loose rounds pushed into it
/// go in the next empty chambers.
/// <para>The gun's owner keeps it. What's in each chamber, which one's up and whether it's out are synced, and every
/// machine swings and turns the model's cylinder from them.</para>
/// </summary>
public sealed class RevolverCylinder : Component, IGunChamber, Component.ITriggerListener
{
	enum Load { Empty, Live, Spent }

	[Property] public PhysicalGun Gun { get; set; }

	[Property] public SkinnedModelRenderer Renderer { get; set; }

	/// <summary>
	/// How many chambers it has.
	/// </summary>
	[Property] public int Chambers { get; set; } = 6;

	/// <summary>
	/// The model's bone the cylinder swings out on.
	/// </summary>
	[Property, Group( "Setup" )] public string CraneBone { get; set; } = "cylinder_crane";

	/// <summary>
	/// The model's cylinder bone, turned a chamber at each shot.
	/// </summary>
	[Property, Group( "Setup" )] public string CylinderBone { get; set; } = "cylinder";

	/// <summary>
	/// The model's bones of the rounds in the chambers, numbered from 0, the one under the hammer.
	/// </summary>
	[Property, Group( "Setup" )] public string RoundBonePrefix { get; set; } = "bullet_";

	/// <summary>
	/// How far it swings out, in degrees.
	/// </summary>
	[Property, Group( "Setup" )] public float SwingAngle { get; set; } = 80f;

	[Property, Group( "Sounds" )] public SoundEvent OpenSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent CloseSound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent EmptySound { get; set; }
	[Property, Group( "Sounds" )] public SoundEvent InsertSound { get; set; }

	/// <summary>
	/// What's in each chamber, two bits each.
	/// </summary>
	[Sync] int Contents { get; set; }

	/// <summary>
	/// The chamber under the hammer.
	/// </summary>
	[Sync] public int Current { get; private set; }

	/// <summary>
	/// Swung out.
	/// </summary>
	[Sync] public bool IsOpen { get; private set; }

	/// <summary>
	/// How many rounds it comes with.
	/// </summary>
	[Property] public int DefaultRounds { get; set; } = 6;

	Load Get( int chamber ) => (Load)((Contents >> (Wrap( chamber ) * 2)) & 3);

	void Set( int chamber, Load load )
	{
		var shift = Wrap( chamber ) * 2;
		Contents = (Contents & ~(3 << shift)) | ((int)load << shift);
	}

	int Wrap( int chamber ) => ((chamber % Chambers) + Chambers) % Chambers;

	public bool HasRound => !IsOpen && Get( Current + 1 ) == Load.Live;

	public bool HasSpentCasing => false;

	public int LiveRounds => Enumerable.Range( 0, Chambers ).Count( i => Get( i ) == Load.Live );

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		for ( var i = 0; i < Chambers; i++ )
			Set( i, i < DefaultRounds ? Load.Live : Load.Empty );

		// The first pull turns the first round under the hammer
		Current = Chambers - 1;
	}

	/// <summary>
	/// A pull of the trigger: turns the next chamber under the hammer, and takes its round if it's live. Owner only.
	/// </summary>
	public Round Eject()
	{
		if ( IsProxy || IsOpen ) return null;

		Current = Wrap( Current + 1 );
		if ( Get( Current ) != Load.Live ) return null;

		Set( Current, Load.Spent );
		return Gun?.RoundTemplate is { } round ? round with { IsSpent = false } : null;
	}

	/// <summary>
	/// Fills the empty chambers from <paramref name="src"/>. Owner only.
	/// </summary>
	public int Feed( IAmmoSource src )
	{
		if ( IsProxy || src is null ) return 0;

		var count = 0;
		for ( var i = 0; i < Chambers; i++ )
		{
			var chamber = Wrap( Current + 1 + i );
			if ( Get( chamber ) != Load.Empty ) continue;
			if ( src.Pop().FirstOrDefault() is null ) break;

			Set( chamber, Load.Live );
			count++;
		}

		return count;
	}

	// The casings stay in their chambers until it's swung out and emptied
	public void LeaveSpentCasing() { }
	public bool EjectSpentCasing() => false;

	/// <summary>
	/// Swings it out or back in. Owner only.
	/// </summary>
	public void Toggle()
	{
		if ( IsProxy ) return;

		IsOpen = !IsOpen;
		GunplaySounds.Play( IsOpen ? OpenSound : CloseSound, WorldPosition );
		VRMode.Pulse( Gun?.GripHand?.Controller, 0.3f, 0.2f );
	}

	/// <summary>
	/// Throws everything out of it: the casings fly off, the live rounds drop out as loose rounds. Owner only.
	/// </summary>
	public void EmptyOut()
	{
		if ( IsProxy || !IsOpen || Contents == 0 ) return;

		var live = 0;
		var spent = 0;
		for ( var i = 0; i < Chambers; i++ )
		{
			if ( Get( i ) == Load.Live ) live++;
			if ( Get( i ) == Load.Spent ) spent++;
			Set( i, Load.Empty );
		}

		GunplaySounds.Play( EmptySound, WorldPosition );
		ThrowOut( live, spent );
	}

	/// <summary>
	/// The casings, as effects on every machine; the live rounds, made by the host as objects to pick up.
	/// </summary>
	[Rpc.Broadcast]
	void ThrowOut( int live, int spent )
	{
		var round = Gun?.RoundTemplate;
		var back = -WorldRotation.Forward;

		for ( var i = 0; i < spent; i++ )
		{
			if ( round?.CreateCasing( WorldPosition + Vector3.Random * 0.4f, Rotation.LookAt( back ) )?.GetComponent<Rigidbody>() is { } body )
				body.Velocity = back * 40f + Vector3.Random * 20f;
		}

		if ( !Networking.IsHost && Networking.IsActive ) return;
		if ( !Rpc.Caller.IsHost && VRPlayer.Get( Player.FindForConnection( Rpc.Caller ) ) is null ) return;
		if ( Gun?.RoundPrefab is not { } prefab ) return;

		for ( var i = 0; i < live; i++ )
		{
			var go = prefab.Clone( new Transform( WorldPosition + Vector3.Random * 0.4f, Rotation.LookAt( back ) ) );
			go.Tags.Add( "removable" );
			go.NetworkSpawn( true, null );

			if ( go.GetComponent<Rigidbody>() is { } body )
				body.Velocity = back * 30f + Vector3.Random * 15f;
		}
	}

	TimeSince _sinceToggle = 1;
	Rotation _lastRotation;
	bool _aWasDown;
	bool _bWasDown;

	protected override void OnUpdate()
	{
		if ( IsProxy || !Gun.IsValid() ) return;

		var rotation = Gun.WorldRotation;
		var hand = Gun.GripHand;
		if ( hand?.Controller is not { } controller )
		{
			_lastRotation = rotation;
			_aWasDown = _bWasDown = false;
			return;
		}

		var a = controller.ButtonA.IsPressed;
		var b = controller.ButtonB.IsPressed;

		if ( a && !_aWasDown && _sinceToggle > 0.3f )
		{
			Toggle();
			_sinceToggle = 0;
		}

		// A flick of the wrist, a quick roll of the gun, swings it back in
		if ( IsOpen && _sinceToggle > 0.3f && Time.Delta > 0f )
		{
			// Degrees a second the gun's top turned round
			var turn = Vector3.GetAngle( _lastRotation.Up, rotation.Up ) / Time.Delta;
			if ( turn > 900f )
			{
				Toggle();
				_sinceToggle = 0;
			}
		}

		// Out, the ejector rod: B, or the muzzle up at the sky
		if ( IsOpen && ((b && !_bWasDown) || rotation.Forward.z > 0.8f) )
			EmptyOut();

		_aWasDown = a;
		_bWasDown = b;
		_lastRotation = rotation;
	}

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( IsProxy || !IsOpen ) return;

		if ( other.GameObject.Root.GetComponent<LooseRound>() is not { Consumed: false } round || round.IsProxy ) return;
		if ( Gun?.RoundTemplate is not { } template || round.Bullet.Caliber != template.Caliber ) return;

		round.GetComponent<GunplayInteractable>()?.ClearAllInteractions();
		if ( Feed( round ) <= 0 ) return;

		GunplaySounds.Play( InsertSound, WorldPosition );
		round.Remove();
	}

	// The model's parts, as they were with the cylinder in and the first chamber up, relative to the gun
	GameObject _crane;
	GameObject _cylinder;
	GameObject[] _rounds;
	Transform _craneRest;
	Transform _cylinderRest;
	Transform[] _roundRest;
	Vector3 _axisCenter;
	float _swing;
	float _spin;
	float _spinTarget;
	int _shownCurrent = -1;

	bool FindParts()
	{
		if ( _crane.IsValid() ) return true;
		if ( !Renderer.IsValid() || !Gun.IsValid() ) return false;

		_crane = Renderer.GetBoneObject( CraneBone );
		_cylinder = Renderer.GetBoneObject( CylinderBone );
		if ( !_crane.IsValid() || !_cylinder.IsValid() ) return false;

		_rounds = Enumerable.Range( 0, Chambers ).Select( i => Renderer.GetBoneObject( RoundBonePrefix + i ) ).ToArray();

		var root = Gun.WorldTransform;
		_craneRest = root.ToLocal( _crane.WorldTransform );
		_cylinderRest = root.ToLocal( _cylinder.WorldTransform );
		_roundRest = _rounds.Select( r => r.IsValid() ? root.ToLocal( r.WorldTransform ) : default ).ToArray();

		var valid = _roundRest.Where( ( _, i ) => _rounds[i].IsValid() ).ToArray();
		_axisCenter = valid.Length > 0 ? valid.Aggregate( Vector3.Zero, ( s, t ) => s + t.Position ) / valid.Length : _cylinderRest.Position;

		foreach ( var go in _rounds.Append( _crane ).Append( _cylinder ) )
			if ( go.IsValid() ) go.Flags |= GameObjectFlags.ProceduralBone;

		return true;
	}

	protected override void OnPreRender()
	{
		if ( !FindParts() ) return;

		_swing = _swing.LerpTo( IsOpen ? 1f : 0f, Time.Delta * 12f );
		// Always turning on the same way, a chamber at a time
		if ( _shownCurrent < 0 ) _spin = _spinTarget = Current * (360f / Chambers);
		else _spinTarget += Wrap( Current - _shownCurrent ) * (360f / Chambers);
		_shownCurrent = Current;
		_spin = _spin.LerpTo( _spinTarget, Time.Delta * 20f );

		// Out to the left, round the crane's pivot along the barrel; turning round the cylinder's own axis
		var swing = Rotation.FromAxis( Vector3.Forward, -SwingAngle * _swing );
		var pivot = _craneRest.Position;
		var spin = Rotation.FromAxis( Vector3.Forward, _spin );

		Transform Place( Transform rest, bool spins )
		{
			var position = rest.Position;
			var rotation = rest.Rotation;
			if ( spins )
			{
				position = _axisCenter + spin * (position - _axisCenter);
				rotation = spin * rotation;
			}

			position = pivot + swing * (position - pivot);
			rotation = swing * rotation;
			return Gun.WorldTransform.ToWorld( new Transform( position, rotation ) );
		}

		_crane.WorldTransform = Place( _craneRest, false );
		_cylinder.WorldTransform = Place( _cylinderRest, true );

		for ( var i = 0; i < _rounds.Length; i++ )
		{
			if ( !_rounds[i].IsValid() ) continue;

			// Round i sits in chamber i; an empty chamber shows nothing
			var shown = Get( i ) != Load.Empty;
			var t = Place( _roundRest[i], true );
			_rounds[i].WorldTransform = t.WithScale( shown ? 1f : 0.001f );
		}
	}
}
