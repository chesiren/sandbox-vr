/// <summary>
/// Picks a weapon in VR: click the stick to open it, push the stick towards a weapon and let it spring back. Clicking again
/// closes it. A ring in front of the eyes, painted with the screen HUD's own weapon tiles, with the held weapon hidden so it
/// can't get in the way. It stays where it opened, so looking around doesn't move it.
/// </summary>
public sealed class VRWeaponWheel : VRHudPanel
{
	/// <summary>
	/// How wide the panel is, in world units. The ring is most of it.
	/// </summary>
	[Property] public float Width { get; set; } = 23f;

	/// <summary>
	/// How far in front of the eyes the ring opens, closer than a held weapon usually is.
	/// </summary>
	[Property] public float Distance { get; set; } = 20f;

	/// <summary>
	/// How far below the eyes the ring opens.
	/// </summary>
	[Property] public float Drop { get; set; } = 2f;

	/// <summary>
	/// How far the stick has to be pushed to pick a weapon.
	/// </summary>
	const float StickPushed = 0.5f;

	/// <summary>
	/// How far back the stick has to come for the pick to count.
	/// </summary>
	const float StickReleased = 0.3f;

	/// <summary>
	/// Closes on its own if nothing's picked in this long.
	/// </summary>
	const float Timeout = 4f;

	protected override Vector2 PanelPixels => new( 2000, 2000 );

	readonly List<BaseSandboxWeapon> _items = new();
	VRWeaponWheelSection _section;
	BaseSandboxWeapon _selected;
	Vector3 _anchorLocal;
	bool _open;
	bool _picking;
	RealTimeSince _sinceActive;

	protected override void Place( Transform head )
	{
		if ( !_open ) return;

		var position = Input.VR.Anchor.PointToWorld( _anchorLocal );
		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), Width );
	}

	protected override void OnUpdate()
	{
		var vr = VRPlayer.Local;
		var inventory = vr?.Player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		_section ??= Canvas?.Section<VRWeaponWheelSection>();
		if ( _section is null ) return;

		if ( _open )
		{
			var blocked = vr.Player.IsSeated || SpawnMenuHost.IsOpen;
			if ( vr.WheelClicked || blocked || _sinceActive > Timeout )
				Close( inventory, false );
			else
				UpdateSelection( inventory, vr.WheelStick );
		}
		else if ( vr.WheelClicked && !vr.Player.IsSeated && !SpawnMenuHost.IsOpen && !VRKeyboard.IsOpen )
		{
			Open( inventory );
		}

		vr.WheelOpen = _open;
		_section.Open = _open;
		_section.Items = _items;
		_section.Selected = _selected;
	}

	void Open( PlayerInventory inventory )
	{
		_items.Clear();
		_items.AddRange( inventory.Weapons.Where( x => x == inventory.ActiveWeapon || inventory.CanSwitchTo( x ) ) );

		// Nothing to choose between
		if ( _items.Count < 2 || !Hud.IsVisible( HudElement.Inventory ) ) return;

		_open = true;
		_picking = false;
		_sinceActive = 0;
		_selected = inventory.ActiveWeapon;

		var head = Input.VR.Head;
		var look = head.Rotation.Angles().WithRoll( 0 ).ToRotation();
		_anchorLocal = Input.VR.Anchor.PointToLocal( head.Position + look.Forward * Distance + look.Down * Drop );
	}

	void UpdateSelection( PlayerInventory inventory, Vector2 stick )
	{
		// The stick springing back is what picks, so there's nothing to hold
		if ( _picking && stick.Length < StickReleased )
		{
			Close( inventory, true );
			return;
		}

		if ( stick.Length < StickPushed ) return;

		_picking = true;
		_sinceActive = 0;

		// Clockwise from the top, the way the tiles are laid out
		var step = 360f / _items.Count;
		var angle = MathX.RadianToDegree( MathF.Atan2( stick.x, stick.y ) );
		var index = (MathF.Round( angle / step ).CeilToInt() % _items.Count + _items.Count) % _items.Count;

		var picked = _items[index];
		if ( picked == _selected ) return;

		_selected = picked;
		VRMode.PulseDominant( 0.15f, 0.2f );

		if ( Scene.Get<Inventory>()?.SwitchSound is { } sound )
			Sound.Play( sound );
	}

	void Close( PlayerInventory inventory, bool picked )
	{
		_open = false;

		if ( picked && _selected.IsValid() && _selected != inventory.ActiveWeapon )
		{
			inventory.SwitchWeapon( _selected );
			VRMode.PulseDominant( 0.35f, 0.3f );
		}

		_selected = null;
		_items.Clear();
	}
}

/// <summary>
/// Paints <see cref="VRWeaponWheel"/>'s ring of tiles through <see cref="InventorySection.DrawTile"/>.
/// </summary>
public sealed class VRWeaponWheelSection : HudSection
{
	/// <summary>
	/// After the inventory section, which has to have been given the HUD's colour to paint its tiles with.
	/// </summary>
	public override int Order => 20;

	const float TileWidth = 230;
	const float TileHeight = 120;

	/// <summary>
	/// How far from the centre the tiles are, as a fraction of the canvas's width.
	/// </summary>
	const float Radius = 0.35f;

	public bool Open { get; set; }
	public IReadOnlyList<BaseSandboxWeapon> Items { get; set; }
	public BaseSandboxWeapon Selected { get; set; }

	public override void GetCompositeRegions( List<Rect> regions )
	{
		if ( Open ) regions.Add( CanvasRect );
	}

	public override void Paint( Painter painter )
	{
		if ( !Open || Items is null || Items.Count == 0 ) return;

		var tiles = Canvas.Section<InventorySection>();
		var scale = ScaleToScreen;
		var bounds = painter.Bounds;
		var size = new Vector2( TileWidth, TileHeight ) * scale;

		for ( var i = 0; i < Items.Count; i++ )
		{
			var angle = MathX.DegreeToRadian( 360f * i / Items.Count );
			var center = bounds.Center + new Vector2( MathF.Sin( angle ), -MathF.Cos( angle ) ) * bounds.Width * Radius;
			var rect = new Rect( center - size * 0.5f, size );

			tiles.DrawTile( painter, rect, Items[i], -1, Items[i] == Selected, 1f, scale );
		}
	}
}
