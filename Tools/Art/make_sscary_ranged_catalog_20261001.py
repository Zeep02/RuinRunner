"""Remaining ten native weapon IDs, real normal/special inputs in both side views."""
from pathlib import Path
root=Path(__file__).resolve().parents[2]
t=(root/'Tools/Validation/SscaryRangedPresentation20261001.cs').read_text(encoding='utf-8')
a=t.index('        foreach(var id in new[]{"hunter_bow"')
b=t.index('        foreach(var id in new[]{"hunter_bow"',a+10)
t=t[:b]+'''        game.ReturnToMainMenu();yield return null;Require(UnityEngine.Object.FindObjectsByType<PlayerController>().Length==0,"catalog clears player");
    }
}
'''
t=t.replace('SscaryRangedPresentation20261001','SscaryRangedCatalog20261001').replace('presentation-" + DateTime','catalog-" + DateTime')
t=t.replace('"hunter_bow","hand_crossbow","scrap_pistol","storm_rifle","scrap_shotgun","dragon_cannon"','"composite_bow","repeater","flintlock","scattergun","great_bow","siege_crossbow","twin_pistols","celestial_bow","void_revolver","starbreaker"')
t=t.replace('new[]{0,1,2,4,5,7}','new[]{2,5}')
t=t.replace('special&&p.EquippedWeapon.Family==WeaponFamily.Pistol?3:1','special&&p.EquippedWeapon.Family==WeaponFamily.Pistol&&p.EquippedWeapon.SpecialType!=WeaponSpecialType.Nova?3:1')
(root/'Tools/Validation/SscaryRangedCatalog20261001.cs').write_text(t,encoding='utf-8')
print('Generated ten additional actual weapon-ID cases.')
