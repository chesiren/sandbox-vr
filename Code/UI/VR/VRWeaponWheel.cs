/// <summary>
/// Picks a weapon in VR, the way Half-Life: Alyx does: hold the stick down and a wheel of weapons opens around the hand, move
/// the hand towards one and let go of the stick to take it. Letting go with the hand still in the middle keeps the weapon
/// you had. Each inventory slot gets a wedge, in the screen HUD's order clockwise, with its weapons in a line going out
/// from the middle - the direction picks the slot and the distance picks the weapon. Painted with the screen HUD's own
/// weapon tiles, with the held weapon hidden so it can't get in the way. It stays where it opened, so looking around
/// doesn't move it.
/// </summary>
public sealed class VRWeaponWheel : VRHudPanel
{
	/// <summary>
	/// How wide a weapon tile is, in world units. The height follows the screen HUD's tiles.
	/// </summary>
	[Property] public float TileSize { get; set; } = 3.4f;

	/// <summary>
	/// How far the hand has to move from where the wheel opened before it picks anything, in world units.
	/// </summary>
	[Property] public float DeadZone { get; set; } = 2.5f;

	/// <summary>
	/// How far from the middle each wedge's first weapon is, in world units.
	/// </summary>
	[Property] public float FirstRing { get; set; } = 5f;

	/// <summary>
	/// The least distance between two weapons of the same wedge, in world units, so the hand can stop on one.
	/// </summary>
	[Property] public float MinStep { get; set; } = 2.6f;

	/// <summary>
	/// How long the stick has to be held before the wheel opens, so clicking both sticks for noclip doesn't flash it.
	/// </summary>
	const float OpenDelay = 0.15f;

	/// <summary>
	/// Space kept between two tiles, as a fraction of <see cref="TileSize"/>.
	/// </summary>
	const float TileGap = 0.1f;

	/// <summary>
	/// How far past a wedge's edge, in degrees, the hand can go before the next wedge takes over.
	/// </summary>
	const float WedgeHysteresis = 6f;

	protected override Vector2 PanelPixels => new( 2000, 2000 );

	readonly List<VRWeaponWheelSection.Tile> _tiles = new();
	readonly List<List<int>> _wedges = new();
	VRWeaponWheelSection _section;
	BaseSandboxWeapon _selected;
	Vector3 _anchorLocal;
	float _width;
	int _wedge = -1;
	bool _open;
	bool _waitForRelease;
	RealTimeSince _sinceHeld;

	Vector3 Center => Input.VR.Anchor.PointToWorld( _anchorLocal );

	Vector2 TileExtent => new( TileSize, TileSize * VRWeaponWheelSection.TileAspect );

	float WedgeAngle => 360f / _wedges.Count;

	/// <summary>
	/// Which way a wedge points, in degrees clockwise from up. Turned half a wedge so the edges, not the wedges, are straight
	/// up and down: the wedges either side are near vertical, where a line of tiles takes the least room.
	/// </summary>
	float WedgeDirection( int wedge ) => (wedge - 0.5f) * WedgeAngle;

	protected override void Place( Transform head )
	{
		if ( !_open ) return;

		var position = Center;
		PlaceAt( position, Rotation.LookAt( head.Position - position, Vector3.Up ), _width );
	}

	protected override void OnUpdate()
	{
		var vr = VRPlayer.Local;
		var inventory = vr?.Player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() ) return;

		_section ??= Canvas?.Section<VRWeaponWheelSection>();
		if ( _section is null ) return;

		// Made here rather than when the wheel first paints with it: the canvas can't take a new section while it's painting them
		Canvas.Section<InventorySection>();

		var blocked = vr.Player.IsSeated || SpawnMenuHost.IsOpen || VRKeyboard.IsOpen;

		if ( !vr.WheelHeld )
		{
			_sinceHeld = 0;
			_waitForRelease = false;
		}

		if ( _open )
		{
			// The other stick clicked too is noclip, not a pick
			if ( vr.WheelCancelled || blocked )
				Close( inventory, false );
			else if ( !vr.WheelHeld )
				Close( inventory, true );
			else
				UpdateSelection( inventory );
		}
		else if ( vr.WheelHeld && !vr.WheelCancelled && !_waitForRelease && !blocked && _sinceHeld > OpenDelay )
		{
			Open( inventory );
		}

		vr.WheelOpen = _open;
		_section.Open = _open;
		_section.Tiles = _tiles;
		_section.TileExtent = TileExtent;
		_section.WorldWidth = _width;
		_section.Selected = _selected;
	}

	void Open( PlayerInventory inventory )
	{
		// Whether it opens or not, it's this hold's only go
		_waitForRelease = true;

		var weapons = inventory.Weapons
			.Where( x => x == inventory.ActiveWeapon || inventory.CanSwitchTo( x ) )
			.Where( x => x.Slot >= 0 && x.Slot < inventory.MaxSlots )
			.ToList();

		// Nothing to choose between
		if ( weapons.Count < 2 || !Hud.IsVisible( HudElement.Inventory ) ) return;

		Layout( weapons, inventory.MaxSlots );

		_open = true;
		_wedge = -1;
		_selected = inventory.ActiveWeapon;
		_anchorLocal = Input.VR.Anchor.PointToLocal( VRMode.DominantHand.AimTransform.Position );
	}

	/// <summary>
	/// Puts each slot's weapons in a line along its wedge. Tiles are wider than tall and always upright, so how far apart two
	/// in a line have to be depends on which way the line goes: just enough that they clear each other across or up.
	/// </summary>
	void Layout( List<BaseSandboxWeapon> weapons, int slots )
	{
		_tiles.Clear();
		_wedges.Clear();
		for ( var i = 0; i < slots; i++ )
			_wedges.Add( new() );

		var extent = TileExtent;
		var gap = TileSize * TileGap;

		// Even the first ring has to keep neighbouring wedges' tiles apart
		var firstRing = MathF.Max( FirstRing, (extent.x + gap) / (2f * MathF.Sin( MathX.DegreeToRadian( WedgeAngle * 0.5f ) )) );

		var reach = 0f;
		for ( var slot = 0; slot < slots; slot++ )
		{
			var angle = MathX.DegreeToRadian( WedgeDirection( slot ) );
			var direction = new Vector2( MathF.Sin( angle ), MathF.Cos( angle ) );

			var across = MathF.Abs( direction.x ) > 0.001f ? (extent.x + gap) / MathF.Abs( direction.x ) : float.MaxValue;
			var up = MathF.Abs( direction.y ) > 0.001f ? (extent.y + gap) / MathF.Abs( direction.y ) : float.MaxValue;
			var step = MathF.Max( MinStep, MathF.Min( across, up ) );

			var radius = firstRing;
			foreach ( var weapon in weapons.Where( x => x.Slot == slot ) )
			{
				var position = direction * radius;
				_wedges[slot].Add( _tiles.Count );
				_tiles.Add( new VRWeaponWheelSection.Tile( weapon, position, radius ) );

				reach = MathF.Max( reach, MathF.Max( MathF.Abs( position.x ) + extent.x * 0.5f, MathF.Abs( position.y ) + extent.y * 0.5f ) );
				radius += step;
			}
		}

		// Room for the selected tile's frame round the furthest one
		_width = (reach + gap * 2f) * 2f;
	}

	void UpdateSelection( PlayerInventory inventory )
	{
		// Where the hand's gone on the wheel, as seen from the eyes
		var center = Center;
		var view = Rotation.LookAt( center - Input.VR.Head.Position, Vector3.Up );
		var offset = VRMode.DominantHand.AimTransform.Position - center;
		var hand = new Vector2( Vector3.Dot( offset, view.Right ), Vector3.Dot( offset, view.Up ) );
		var distance = hand.Length;

		var picked = inventory.ActiveWeapon;
		if ( distance < DeadZone )
		{
			_wedge = -1;
		}
		else
		{
			var angle = MathX.RadianToDegree( MathF.Atan2( hand.x, hand.y ) );

			// The wedge it's in keeps it a little past its edge, so the line between two doesn't flicker
			if ( _wedge < 0 || AngleBetween( angle, WedgeDirection( _wedge ) ) > WedgeAngle * 0.5f + WedgeHysteresis )
				_wedge = ((int)MathF.Round( angle / WedgeAngle + 0.5f ) % _wedges.Count + _wedges.Count) % _wedges.Count;

			// The tile nearest along the line, past the last one is still the last one
			var line = _wedges[_wedge];
			if ( line.Count > 0 )
				picked = _tiles[line.MinBy( x => MathF.Abs( _tiles[x].Distance - distance ) )].Weapon;
		}

		if ( picked == _selected ) return;

		_selected = picked;
		VRMode.PulseDominant( 0.15f, 0.2f );

		if ( Scene.Get<Inventory>()?.SwitchSound is { } sound )
			Sound.Play( sound );
	}

	static float AngleBetween( float a, float b )
	{
		var difference = MathF.Abs( a - b ) % 360f;
		return difference > 180f ? 360f - difference : difference;
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
		_tiles.Clear();
		_wedges.Clear();
	}
}

/// <summary>
/// Paints <see cref="VRWeaponWheel"/>'s tiles through <see cref="InventorySection.DrawTile"/>, where the wheel laid them out.
/// </summary>
public sealed class VRWeaponWheelSection : HudSection
{
	/// <summary>
	/// A weapon on the wheel. Position is in world units from the middle, up positive. Distance is how far out it is.
	/// </summary>
	public readonly record struct Tile( BaseSandboxWeapon Weapon, Vector2 Position, float Distance );

	/// <summary>
	/// After the inventory section, which has to have been given the HUD's colour to paint its tiles with.
	/// </summary>
	public override int Order => 20;

	const float TileWidth = 230;
	const float TileHeight = 120;
	const float SelectedFrame = 6;

	/// <summary>
	/// Height over width of the screen HUD's weapon tiles.
	/// </summary>
	public const float TileAspect = TileHeight / TileWidth;

	public bool Open { get; set; }
	public IReadOnlyList<Tile> Tiles { get; set; }
	public BaseSandboxWeapon Selected { get; set; }

	/// <summary>
	/// How big a tile is, in world units.
	/// </summary>
	public Vector2 TileExtent { get; set; }

	/// <summary>
	/// How wide the panel is in the world, to turn the layout's world units into pixels.
	/// </summary>
	public float WorldWidth { get; set; }

	public override void GetCompositeRegions( List<Rect> regions )
	{
		if ( Open ) regions.Add( CanvasRect );
	}

	public override void Paint( Painter painter )
	{
		if ( !Open || Tiles is null || Tiles.Count == 0 || WorldWidth <= 0 ) return;

		var tiles = Canvas.Section<InventorySection>();
		var bounds = painter.Bounds;
		var pixelsPerUnit = bounds.Width / WorldWidth;
		var size = TileExtent * pixelsPerUnit;

		// The tile's contents are laid out for its screen size
		var scale = size.x / TileWidth;

		foreach ( var tile in Tiles )
		{
			var center = bounds.Center + new Vector2( tile.Position.x, -tile.Position.y ) * pixelsPerUnit;
			var rect = new Rect( center - size * 0.5f, size );
			var selected = tile.Weapon == Selected;

			// In the HUD's colour round the one that would be picked
			if ( selected )
				HudStyle.DrawPanel( painter, rect.Grow( SelectedFrame * scale ), Tinted( Color.White ), scale );

			tiles.DrawTile( painter, rect, tile.Weapon, -1, selected, 1f, scale, solid: true );
		}
	}
}
