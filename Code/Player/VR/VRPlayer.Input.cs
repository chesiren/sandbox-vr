using Sandbox.VR;

public sealed partial class VRPlayer
{
	/// <summary>
	/// While true, the dominant trigger clicks UI instead of firing: a menu is open or the laser is on the HUD.
	/// </summary>
	public bool PointingAtUI => IsLocalVR && (VRHud.IsPointing || SpawnMenuHost.IsOpen);

	bool _triggerDown;
	bool _offTriggerDown;

	/// <summary>
	/// Presses the game's existing actions from the controllers, so weapons, tools, menus and the engine's
	/// use/press all work in VR without knowing about it.
	/// <list type="bullet">
	/// <item>Dominant hand: trigger fires (attack1), B is attack2, A reloads, grip uses/carries, stick click
	/// cycles weapons, stick turns, up jumps, down crouches.</item>
	/// <item>Off hand: stick moves (or aims teleport/shift), stick click runs, A holds the spawn menu,
	/// B holds the inspect menu, trigger undoes. Both stick clicks together toggle noclip.</item>
	/// </list>
	/// </summary>
	void MapInput()
	{
		var main = VRMode.DominantHand;
		var off = VRMode.OffHand;

		// SteamVR also drives the engine's gamepad bindings from the controllers (turning the stick pressed the
		// d-pad weapon cycling), so start from nothing and press only what's mapped below
		Input.ClearActions();

		// The view never turns from look input in VR, so look input is free to carry the hand's twist: the physgun
		// and tools rotate what they hold with it, exactly as they do with the mouse.
		Input.AnalogLook = HandLookDelta( main );
		Input.AnalogMove = default;
		Input.MouseWheel = default;

		_triggerDown = Hysteresis( _triggerDown, main.Trigger.Value );

		// Holding the trigger, the stick pushes and pulls (the scroll wheel) instead of jumping and crouching
		if ( _triggerDown )
		{
			if ( MathF.Abs( main.Joystick.Value.y ) > 0.3f )
				Input.MouseWheel = new Vector2( 0, main.Joystick.Value.y * Time.Delta * 10f );

			_stickUpLatched = _stickDownLatched = true;
		}
		_offTriggerDown = Hysteresis( _offTriggerDown, off.Trigger.Value );

		// The HUD's laser clicks with these too, Player keeps them from the weapon while it's on the UI
		Press( "attack1", _triggerDown );
		Press( "attack2", main.ButtonB.IsPressed );

		Press( "reload", main.ButtonA.IsPressed );
		Press( "use", main.Grip.Value > 0.6f || off.Grip.Value > 0.6f );
		Press( "slotnext", main.JoystickPress.IsPressed && !off.JoystickPress.IsPressed );

		Press( "run", off.JoystickPress.IsPressed && !main.JoystickPress.IsPressed );
		Press( "spawnmenu", off.ButtonA.IsPressed );
		Press( "inspectmenu", off.ButtonB.IsPressed );
		Press( "undo", _offTriggerDown );
		Press( "noclip", main.JoystickPress.IsPressed && off.JoystickPress.IsPressed );

		UpdateTurning( main.Joystick.Value );
		UpdateJumpAndCrouch( main.Joystick.Value );
		UpdateLocomotionInput( off );
	}

	static bool Hysteresis( bool down, float value ) => down ? value > 0.4f : value > 0.6f;

	Angles? _lastHandAngles;

	/// <summary>
	/// How much the hand turned since last frame, in the play area's space so turning the player doesn't count.
	/// </summary>
	Angles HandLookDelta( VRController hand )
	{
		var angles = Input.VR.Anchor.ToLocal( hand.AimTransform ).Rotation.Angles();
		var last = _lastHandAngles ?? angles;
		_lastHandAngles = angles;

		return new Angles( angles.pitch - last.pitch, Angles.NormalizeAngle( angles.yaw - last.yaw ), 0 );
	}

	static void Press( string action, bool down )
	{
		if ( down ) Input.SetAction( action, true );
	}

	bool _stickUpLatched;
	bool _stickDownLatched;
	bool _crouchToggled;

	void UpdateJumpAndCrouch( Vector2 stick )
	{
		// Stick y is up-positive; latch so holding it doesn't repeat
		if ( stick.y > 0.7f && MathF.Abs( stick.x ) < 0.5f )
		{
			if ( !_stickUpLatched )
				Input.SetAction( "jump", true );

			_stickUpLatched = true;
		}
		else if ( stick.y < 0.3f )
		{
			_stickUpLatched = false;
		}

		if ( stick.y < -0.7f && MathF.Abs( stick.x ) < 0.5f )
		{
			if ( !_stickDownLatched )
				_crouchToggled = !_crouchToggled;

			_stickDownLatched = true;
		}
		else if ( stick.y > -0.3f )
		{
			_stickDownLatched = false;
		}
	}

	/// <summary>
	/// Crouching from the stick toggle, or because the player physically crouched.
	/// </summary>
	bool WantsDuck
	{
		get
		{
			if ( _crouchToggled ) return true;

			var settings = VRSettings.Current;
			var standingEye = _standingHeight > 0f ? _standingHeight : Controller.BodyHeight - _defaultEyeDistance;
			var headHeight = Head.Position.z - WorldPosition.z;
			return headHeight < standingEye * settings.PhysicalCrouchFraction;
		}
	}

	bool _snapLatched;

	void UpdateTurning( Vector2 stick )
	{
		var settings = VRSettings.Current;

		if ( settings.Turn == VRSettings.TurnType.Snap )
		{
			if ( MathF.Abs( stick.x ) > 0.7f )
			{
				if ( !_snapLatched )
					Turn( -MathF.Sign( stick.x ) * settings.SnapAngle );

				_snapLatched = true;
			}
			else if ( MathF.Abs( stick.x ) < 0.3f )
			{
				_snapLatched = false;
			}
		}
		else if ( MathF.Abs( stick.x ) > 0.2f )
		{
			Turn( -stick.x * settings.SmoothTurnSpeed * Time.Delta );
		}
	}
}
