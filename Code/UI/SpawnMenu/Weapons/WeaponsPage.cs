[Title( "#spawnmenu.tab.weapons" ), Order( 2010 ), Icon( "🔫" )]
public class WeaponsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		AddOption( "📦", "#spawnmenu.entity.all", () => new EntityListCloud { LocalCategory = "Weapon", Query = "+weapon" } );
		AddOption( "🗡️", "#spawnmenu.weapon.melee", () => new EntityListCloud { LocalCategory = "Weapon/Melee", Query = "+weapon +melee" } );
		AddOption( "🛠️", "#spawnmenu.weapon.utility", () => new EntityListCloud { LocalCategory = "Weapon/Utility", Query = "+weapon +utility" } );
		AddOption( "🔫", "#spawnmenu.weapon.weapons", () => new EntityListCloud { LocalCategory = "Weapon", IncludeLocalSubcategories = false, Query = "+weapon +ranged" } );

		// The guns worked by hand: everyone sees them, only VR players can spawn them, see VROnlySpawnGate
		AddOption( "🥽", "#vr.spawnmenu.weapons", () => new EntityListCloud { LocalCategory = "Weapon/VR", Query = "+weapon +vr" } );
	}
}
