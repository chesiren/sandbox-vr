using Sandbox.VR;

/// <summary>
/// Single place deciding whether this client plays in VR. Everything VR-only is gated on <see cref="IsActive"/>,
/// so keyboard and gamepad players keep the exact desktop behaviour.
/// </summary>
public static class VRMode
{
	/// <summary>
	/// True when this client is running in VR.
	/// </summary>
	public static bool IsActive => Game.IsRunningInVR && Input.VR is not null;

	/// <summary>
	/// True when the player chose to hold weapons in their left hand.
	/// </summary>
	public static bool LeftHanded => VRSettings.Current.LeftHanded;

	/// <summary>
	/// The hand holding weapons and tools.
	/// </summary>
	public static VRController DominantHand => LeftHanded ? Input.VR.LeftHand : Input.VR.RightHand;

	/// <summary>
	/// The other hand - locomotion, menus and the wrist HUD.
	/// </summary>
	public static VRController OffHand => LeftHanded ? Input.VR.RightHand : Input.VR.LeftHand;

	/// <summary>
	/// Short pulse on a hand, for confirming grabs, shots and UI clicks.
	/// </summary>
	public static void Pulse( VRController hand, float amplitude = 0.4f, float length = 0.5f )
	{
		if ( !IsActive || hand is null ) return;
		hand.TriggerHaptics( HapticEffect.SoftImpact, length, 1f, amplitude.Clamp( 0f, 1f ) );
	}
}
