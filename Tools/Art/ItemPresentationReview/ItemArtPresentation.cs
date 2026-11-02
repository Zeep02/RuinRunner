using UnityEngine;

namespace RuinRunner
{
    public static class ItemArtPresentation
    {
        private static Material material;
        public static Material OutlineMaterial
        {
            get
            {
                if(material==null) material=new Material(Resources.Load<Shader>("Art/Pixel/ItemOutline"))
                    { name="Native item black contour", hideFlags=HideFlags.HideAndDontSave };
                return material;
            }
        }
        public static void Draw(Rect rect, Texture2D texture)
        {
            if(texture==null)return;
            var color=GUI.color;
            var pixel=Mathf.Max(1,Mathf.Round(Mathf.Min(rect.width/texture.width,rect.height/texture.height)));
            GUI.color=new Color(.067f,.043f,.09f,color.a);
            for(var y=-1;y<=1;y++)for(var x=-1;x<=1;x++)
                if(x!=0||y!=0)GUI.DrawTexture(new Rect(rect.x+x*pixel,rect.y+y*pixel,rect.width,rect.height),texture,ScaleMode.ScaleToFit,true);
            GUI.color=color;
            GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);
        }
    }
}
