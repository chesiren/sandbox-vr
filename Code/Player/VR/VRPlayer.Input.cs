using Sandbox.VR;

public sealed partial class VRPlayer
{
	/// <summary>
	/// While true, the dominant trigger clicks UI instead of firing (a world panel is under the laser).
	/// </summary>
	public bool PointingAtUI { get; set; }

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

		// Nothing else reads the mouse or the analog sticks in VR
		Input.AnalogLook = default;
		Input.AnalogMove = default;
		Input.MouseWheel = default;

		_triggerDown = Hysteresis( _triggerDown, main.Trigger.Value );
		_offTriggerDown = Hysteresis( _offTriggerDown, off.Trigger.Value );

		if ( !PointingAtUI )
		{
			Press( "attack1", _triggerDown );
			Press( "attack2", main.ButtonB.IsPressed );
		}

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
			var standingEye = Controller.BodyHeight - _defaultEyeDistance;
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
