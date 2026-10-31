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
	/// <item>Dominant hand: trigger fires (attack1), B is attack2, A reloads, grip uses/carries (held to carry,
	/// letting go throws with the swing; B turns what's carried with the wrist), stick click cycles weapons,
	/// stick turns, up jumps, down crouches.</item>
	/// <item>Off hand: stick moves (or aims teleport/shift), stick click runs, A holds the spawn menu,
	/// B holds the inspect menu, trigger undoes. Both stick clicks together toggle noclip.</item>
	/// <item>Seated: dominant trigger accelerates, off hand trigger brakes and reverses, off hand stick steers, see
	/// <see cref="DriveInput"/>. Stick up is the handbrake, B is attack1 (the horn, the seat's weapons), A is attack2.</item>
	/// </list>
	/// </summary>
	internal void MapInput()
	{
		if ( !IsLocalVR ) return;

		var main = VRMode.DominantHand;
		var off = VRMode.OffHand;

		// The view never turns from look input in VR, so look input is free to carry the hand's twist: the physgun
		// and tools rotate what they hold with it, exactly as they do with the mouse.
		Input.AnalogLook = HandLookDelta( main );
		Input.AnalogMove = default;
		Input.MouseWheel = default;

		_triggerDown = Hysteresis( _triggerDown, main.Trigger.Value );

		// Holding the trigger, the stick pushes and pulls (the scroll wheel) instead of jumping and crouching
		if ( _triggerDown && !Player.IsSeated )
		{
			if ( MathF.Abs( main.Joystick.Value.y ) > 0.3f )
				Input.MouseWheel = new Vector2( 0, main.Joystick.Value.y * Time.Delta * 10f );

			_stickUpLatched = _stickDownLatched = true;
		}
		_offTriggerDown = Hysteresis( _offTriggerDown, off.Trigger.Value );

		// Driving, the trigger's the accelerator (see DriveInput) - left on attack1 it honks the horn and fires the
		// seat's weapons with every push, so those move to the buttons. The menus still click with it.
		var driving = Player.IsSeated && !SpawnMenuHost.IsOpen;

		// The HUD's laser clicks with these too, Player keeps them from the weapon while it's on the UI
		Press( "attack1", driving ? main.ButtonB.IsPressed : _triggerDown );
		Press( "attack2", driving ? main.ButtonA.IsPressed : main.ButtonB.IsPressed );

		Press( "reload", !driving && main.ButtonA.IsPressed );
		Press( "use", main.Grip.Value > 0.6f || off.Grip.Value > 0.6f );
		Press( "slotnext", main.JoystickPress.IsPressed && !off.JoystickPress.IsPressed );

		Press( "run", off.JoystickPress.IsPressed && !main.JoystickPress.IsPressed );
		Press( "spawnmenu", off.ButtonA.IsPressed );
		Press( "inspectmenu", off.ButtonB.IsPressed );
		Press( "undo", _offTriggerDown && !Player.IsSeated );
		Press( "noclip", main.JoystickPress.IsPressed && off.JoystickPress.IsPressed );

		// SteamVR also presses the engine's gamepad bindings from the controllers - pushing the stick to turn hit
		// the d-pad weapon cycling. Hold those up.
		Press( "slotprev", false );
		Press( "view", false );
		Press( "duck", false );

		UpdateTurning( main.Joystick.Value );
		UpdateJumpAndCrouch( main.Joystick.Value );

		if ( Input.Pressed( "jump" ) && Controller.IsOnGround && !Player.IsNoclipping && !Player.IsSeated )
		{
			Controller.Jump( Vector3.Up * Controller.JumpSpeed );
			Controller.OnJumped();
		}
		UpdateLocomotionInput( off );
	}

	/// <summary>
	/// The engine only looks for what's in front of the eyes, and presses it with use, while it's driving the view
	/// itself - which we turned off. Without this nothing can be used in VR: seats, buttons, doors. Runs after
	/// <see cref="Player"/>'s input pass, where the engine would, so carrying something still keeps use from it.
	/// </summary>
	internal void UpdatePressing()
	{
		if ( !IsLocalVR ) return;

		Controller.UpdateLookAt();
	}

	static bool Hysteresis( bool down, float value ) => down ? value > 0.4f : value > 0.6f;

	/// <summary>
	/// How far the controllers push whatever we're sitting in, the same as a gamepad's triggers and left stick. The
	/// triggers click a menu while one's open, rather than driving off.
	/// </summary>
	internal (float Forward, float Backward, float Steer) DriveInput
	{
		get
		{
			var steer = VRMode.OffHand.Joystick.Value.x;
			if ( SpawnMenuHost.IsOpen ) return (0f, 0f, steer);

			return (VRMode.DominantHand.Trigger.Value, VRMode.OffHand.Trigger.Value, steer);
		}
	}

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

	readonly HashSet<string> _actionsDown = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// Sets an action down or up for this frame, along with what it was last frame. SetAction alone reports the
	/// action as freshly pressed every frame it's set, so holding a button cycled weapons, refired semi-auto
	/// guns and toggled carrying every frame.
	/// </summary>
	void Press( string action, bool down )
	{
		Input.SetLastAction( action, _actionsDown.Contains( action ) );
		Input.SetAction( action, down );

		if ( down ) _actionsDown.Add( action );
		else _actionsDown.Remove( action );
	}

	readonly HashSet<string> _fixedActionsDown = new( StringComparer.OrdinalIgnoreCase );

	/// <summary>
	/// Presses the same actions for the fixed update, with its own edges. What's set in the update doesn't reach it,
	/// and that's where the host reads a driver's actions: the handbrake, boost, seated weapons.
	/// </summary>
	internal void MapFixedInput()
	{
		if ( !IsLocalVR ) return;

		foreach ( var action in _actionsDown )
		{
			Input.SetLastAction( action, _fixedActionsDown.Contains( action ) );
			Input.SetAction( action, true );
		}

		foreach ( var action in _fixedActionsDown.Except( _actionsDown, StringComparer.OrdinalIgnoreCase ) )
		{
			Input.SetLastAction( action, true );
			Input.SetAction( action, false );
		}

		_fixedActionsDown.Clear();
		_fixedActionsDown.UnionWith( _actionsDown );
	}

	bool _stickUpLatched;
	bool _stickDownLatched;
	bool _crouchToggled;

	void UpdateJumpAndCrouch( Vector2 stick )
	{
		// Stick y is up-positive; jump is held while the stick is up, so it only jumps once per push
		if ( stick.y > 0.7f && MathF.Abs( stick.x ) < 0.5f )
		{
			Press( "jump", !_stickUpLatched || _actionsDown.Contains( "jump" ) );
			_stickUpLatched = true;
		}
		else
		{
			Press( "jump", false );
			if ( stick.y < 0.3f ) _stickUpLatched = false;
		}

		// A firm push down, not the wobble of clicking the stick to change weapon
		if ( stick.y < -0.85f && MathF.Abs( stick.x ) < 0.4f && !VRMode.DominantHand.JoystickPress.IsPressed )
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
