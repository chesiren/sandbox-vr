/// <summary>
/// The mouse cursor while the inspect menu is open
/// </summary>
public static class InspectCursor
{
	/// <summary>
	/// How far the mouse has to move with the button held before a click turns into a prop drag
	/// </summary>
	const float DragThreshold = 4f;

	static Vector2 _pressPosition;
	static Vector3 _pressDirection;
	static bool _pressed;
	static bool _dragging;
	static bool _freezeRequested;

	public static bool IsOpen => Game.ActiveScene?.Camera.IsValid() == true && SpawnMenuHost.IsOpen && SpawnMenuHost.GetActiveMode() is ContextMenuHost;
	/// <summary>
	/// Under the mouse, or along the dominant hand in VR
	/// </summary>
	public static Ray Ray => VRPlayer.Local is { } vr ? vr.Aim.ForwardRay : Game.ActiveScene.Camera.ScreenPixelToRay( Mouse.Position );
	public static bool IsDragging => _dragging && IsOpen;
	public static Ray DragRay { get; private set; }

	internal static void Press()
	{
		Release();

		if ( !HasPhysgunOut ) return;

		_pressPosition = Mouse.Position;
		_pressDirection = Ray.Forward;
		_pressed = true;
	}

	static bool HasPhysgunOut => Player.FindLocalPlayer()?.GetComponent<PlayerInventory>()?.ActiveWeapon is Physgun;

	internal static void Release()
	{
		_pressed = false;
		_freezeRequested = false;
		_dragging = false;
	}

	internal static bool RequestFreeze()
	{
		if ( !IsDragging ) return false;

		_freezeRequested = true;
		return true;
	}

	public static bool ConsumeFreeze()
	{
		var requested = _freezeRequested;
		_freezeRequested = false;
		return requested;
	}

	/// <summary>
	/// The cursor moved far enough with the button held to count as a drag. A VR laser has no pixels, so it goes by angle
	/// </summary>
	static bool MovedSincePress => VRMode.IsActive
		? Vector3.GetAngle( _pressDirection, Ray.Forward ) > 1.5f
		: Mouse.Position.Distance( _pressPosition ) > DragThreshold;

	internal static void Update( bool buttonHeld )
	{
		if ( !IsOpen || _pressed && (!buttonHeld || !HasPhysgunOut) )
		{
			Release();
			return;
		}

		if ( _pressed && !_dragging && MovedSincePress )
		{
			_dragging = true;
		}

		if ( _dragging )
		{
			DragRay = Ray;
		}
	}
}
