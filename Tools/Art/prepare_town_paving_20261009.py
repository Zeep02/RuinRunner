"""Prepare the render-only town correction outside Assets; never apply it here."""
from pathlib import Path
import hashlib
import json

root = Path(__file__).resolve().parents[2]
source = root / 'Assets/RuinRunner/Runtime/World/TownWorld.cs'
text = source.read_text(encoding='utf-8-sig')
start = text.index('        void Court(Transform parent,Rect bounds,string label)')
end = text.index('        SpriteRenderer Decorate(', start)
replacement = '''        void Court(Transform parent,Rect bounds,string label)
        {
            // Use the same native half-unit stones and staggered pixel grid as
            // the approach paths. Worn corners and missing stones reveal turf.
            var rules=new EnvironmentPlacementRules(Terrain,null);
            var occupied=new System.Collections.Generic.HashSet<Vector2Int>();
            foreach(var existing in parent.GetComponentsInChildren<SpriteRenderer>())
                if(existing.sortingLayerName=="WorldFloor"&&(existing.name=="Worn paving"||existing.name.StartsWith("Refuge paving ",System.StringComparison.Ordinal)))
                    occupied.Add(Vector2Int.RoundToInt((Vector2)existing.transform.position*32));
            var radius=bounds.size*.5f;
            for(int row=Mathf.FloorToInt(bounds.yMin*2);row<Mathf.CeilToInt(bounds.yMax*2);row++)
            for(int column=Mathf.FloorToInt(bounds.xMin*2)-1;column<Mathf.CeilToInt(bounds.xMax*2);column++)
            {
                var point=new Vector2(column*.5f+.25f+(row&1)*.25f,row*.5f+.25f);
                var delta=point-bounds.center;
                float edge=(1-Mathf.Pow(Mathf.Pow(Mathf.Abs(delta.x)/radius.x,4)+Mathf.Pow(Mathf.Abs(delta.y)/radius.y,4),.25f))*radius.y;
                uint hash;unchecked{hash=(uint)(column*73856093^row*19349663)*2654435761u;hash^=hash>>16;hash*=0x7feb352du;hash^=hash>>15;hash*=0x846ca68bu;hash^=hash>>16;}
                if(edge<.04f+(hash%7)*.025f||(edge<.35f&&hash%3==0)||hash%13==0)continue;
                if(occupied.Contains(Vector2Int.RoundToInt(point*32))||!rules.HasFloor(new Rect(point-Vector2.one*.24f,Vector2.one*.48f)))continue;
                var go=new GameObject("Refuge paving "+label);go.transform.SetParent(parent,false);go.transform.position=point;
                var view=go.AddComponent<SpriteRenderer>();view.sprite=CainosRuinsArt.Sprite("Paving/"+(hash%32).ToString("00"));view.sortingLayerName="WorldFloor";view.sortingOrder=1;
            }
        }
'''
candidate = text[:start] + replacement + text[end:]
candidate = candidate.replace('// Furnished work bays use the same native stone as the connecting ruins.', '// Furnished work bays continue the native worn paving of the approach paths.')
out = root / 'Tools/Art/Staging'
out.mkdir(parents=True, exist_ok=True)
target = out / 'TownWorld.Paving.cs'
target.write_text(candidate, encoding='utf-8')
receipt = {
    'status': 'PREPARED OUTSIDE ASSETS; not applied or Play verified',
    'source': str(source.relative_to(root)).replace('\\', '/'),
    'sourceSha256': hashlib.sha256(source.read_bytes()).hexdigest(),
    'candidate': str(target.relative_to(root)).replace('\\', '/'),
    'candidateSha256': hashlib.sha256(target.read_bytes()).hexdigest(),
    'scope': 'Court rendering only; service positions, props, floor, collider, generator and seed contracts unchanged',
}
(out / 'town-paving-prepared.json').write_text(json.dumps(receipt, ensure_ascii=False, indent=2), encoding='utf-8')
print(json.dumps(receipt, ensure_ascii=False))
