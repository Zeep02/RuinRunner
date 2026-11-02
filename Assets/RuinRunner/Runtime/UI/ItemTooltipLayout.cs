using UnityEngine;

namespace RuinRunner
{
    public readonly struct ItemTooltipLayout
    {
        public readonly Rect Panel,Title,Category,Icon,Viewport;
        public readonly float ContentWidth,DescriptionHeight,SetTop,ContentHeight;
        public readonly bool NeedsScroll;
        public ItemTooltipLayout(float width,float height,Vector2 mouse,float titleHeight,float categoryHeight,float descriptionHeight,float setHeight)
        {
            float panelWidth=Mathf.Min(440,Mathf.Max(160,width-16));
            float header=Mathf.Max(108,30+titleHeight+8+categoryHeight+16);
            ContentWidth=panelWidth-66;DescriptionHeight=descriptionHeight;
            SetTop=descriptionHeight+18;ContentHeight=SetTop+setHeight;
            float panelHeight=Mathf.Min(Mathf.Max(header+36,height-16),header+ContentHeight+26);
            float x=Mathf.Clamp(mouse.x+22,8,Mathf.Max(8,width-panelWidth-8));
            float y=Mathf.Clamp(mouse.y+18,8,Mathf.Max(8,height-panelHeight-8));
            Panel=new Rect(x,y,panelWidth,panelHeight);
            Icon=new Rect(x+18,y+28,72,72);
            Title=new Rect(x+104,y+26,panelWidth-122,titleHeight);
            Category=new Rect(Title.x,Title.yMax+8,Title.width,categoryHeight);
            Viewport=new Rect(x+24,y+header,panelWidth-48,panelHeight-header-16);
            NeedsScroll=ContentHeight>Viewport.height;
            if(NeedsScroll)Viewport=new Rect(Viewport.x,Viewport.y,Viewport.width,Mathf.Max(16,Viewport.height-24));
        }
    }
}
