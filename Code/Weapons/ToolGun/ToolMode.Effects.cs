public abstract partial class ToolMode
{
	[Rpc.Broadcast]
	public virtual void ShootEffects( SelectionPoint target )
	{
		if ( !Toolgun.IsValid() ) return;

		var player = Toolgun.Owner;
		if ( !player.IsValid() ) return;

		if ( !target.IsValid() )
		{
			Log.Warning( "ShootEffects: Unknown object" );
			return;
		}

		if ( player.IsLocalPlayer ) VRMode.PulseDominant( 0.4f, 0.3f );

		Toolgun.PlayToolEffects( target.WorldTransform() );
	}

	public virtual void ShootFailEffects( SelectionPoint target )
	{

	}

}

