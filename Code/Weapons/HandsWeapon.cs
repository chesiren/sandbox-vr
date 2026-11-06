using Sandbox.Rendering;

/// <summary>
/// Nothing in the hands: no model, no attack, no ammo. Everyone starts with it, to put the weapon away - in a headset
/// that leaves both hands free for the physical guns, carrying and climbing. It can't be dropped, see
/// <see cref="PlayerInventory"/>.
/// </summary>
[Title( "#weapon.name.hands" ), Icon( "back_hand" )]
public sealed class HandsWeapon : BaseSandboxWeapon
{
	public HandsWeapon()
	{
		UsesAmmo = false;
		UsesClips = false;
	}

	public override string InventoryIconOverride => "ui/weapons/hands_icon.png";

	protected override bool WantsPrimaryAttack() => false;
	protected override bool WantsSecondaryAttack() => false;
	protected override bool WantsReload() => false;

	/// <summary>
	/// A small dot, for what the use key would reach.
	/// </summary>
	public override void DrawCrosshair( Painter painter, Vector2 center )
	{
		using var scope = painter.Scope();

		painter.BlendMode = BlendMode.Normal;
		painter.Fill = Color.White.WithAlpha( 0.6f );
		painter.Stroke = Stroke.Solid( Color.Black.WithAlpha( 0.6f ), 1f ).WithAlignment( Stroke.StrokeAlignment.Outside );
		painter.Circle( center, 1.5f );
	}
}
