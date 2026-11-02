param([string]$ProjectRoot='F:/codex/LoglikeCap',[string]$ExportVersion='v4')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class SealCoreExport20261009
{
    static readonly Color[] Palette={
        Color.FromArgb(25,17,43),Color.FromArgb(42,27,65),Color.FromArgb(62,43,85),
        Color.FromArgb(89,66,115),Color.FromArgb(118,100,142),Color.FromArgb(151,151,140),
        Color.FromArgb(184,191,159),Color.FromArgb(218,224,193),Color.FromArgb(243,241,209),
        Color.FromArgb(10,70,84),Color.FromArgb(25,108,125),Color.FromArgb(25,179,193),
        Color.FromArgb(56,225,223),Color.FromArgb(113,248,235)};
    static Color Quantize(Color color)
    {
        if(color.A<180)return Color.FromArgb(0,0,0,0);
        int closest=0;double distance=double.MaxValue;
        for(int i=0;i<Palette.Length;i++)
        {
            int r=color.R-Palette[i].R,g=color.G-Palette[i].G,b=color.B-Palette[i].B;
            double next=r*r*.3+g*g*.59+b*b*.11;
            if(next<distance){distance=next;closest=i;}
        }
        return Palette[closest];
    }
    public static void Export(string source,string destination,int first,int count)
    {
        Directory.CreateDirectory(destination);
        using(var sheet=new Bitmap(source))
        for(int frame=0;frame<count;frame++)
        {
            int cell=first+frame, col=cell%4,row=cell/4;
            var colors=new Color[64*64];int bottom=-1;
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                int sx=Math.Min(sheet.Width-1,(int)((col+(x+.5)/64)*sheet.Width/4));
                int sy=Math.Min(sheet.Height-1,(int)((row+(y+.5)/64)*sheet.Height/4));
                var color=Quantize(sheet.GetPixel(sx,sy));colors[y*64+x]=color;
                if(color.A>0)bottom=Math.Max(bottom,y);
            }
            if(bottom<0)throw new Exception("Empty source cell "+source+"/"+cell);
            // Remove only disconnected one-pixel export crumbs. Original generated
            // sheets are preserved; no pose is synthesized from another frame.
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
            {
                if(colors[y*64+x].A==0)continue;bool neighbour=false;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int nx=x+dx,ny=y+dy;if((dx!=0||dy!=0)&&nx>=0&&ny>=0&&nx<64&&ny<64&&colors[ny*64+nx].A>0)neighbour=true;}
                if(!neighbour)colors[y*64+x]=Color.FromArgb(0,0,0,0);
            }
            bottom=-1;for(int y=0;y<64;y++)for(int x=0;x<64;x++)if(colors[y*64+x].A>0)bottom=Math.Max(bottom,y);
            using(var output=new Bitmap(80,80,PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                {int oy=y+71-bottom;if(oy>=0&&oy<80)output.SetPixel(x+8,oy,colors[y*64+x]);}
                string path=Path.Combine(destination,frame.ToString("D2")+".png");
                if(File.Exists(path))throw new Exception("Preserve prior candidate; choose a new export folder: "+path);
                output.Save(path,ImageFormat.Png);
            }
        }
    }
}
'@ -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
$source=Join-Path $ProjectRoot 'ArtDirection/Generated/SealCore-20261009'
$target=Join-Path $source ('frames-'+$ExportVersion)
$clips=@(
    @{clip='idle';sheet='seal-core-style-v2.png';first=0;count=4},
    @{clip='cast';sheet='seal-core-style-v2.png';first=4;count=8},
    @{clip='sweep';sheet='seal-core-attacks-v2.png';first=0;count=8},
    @{clip='charge';sheet='seal-core-attacks-v2.png';first=8;count=8},
    @{clip='hurt';sheet='seal-core-hurt-move-v3.png';first=0;count=8},
    @{clip='move';sheet='seal-core-hurt-move-v3.png';first=8;count=8},
    @{clip='death';sheet='seal-core-death-entrance-v2.png';first=0;count=8},
    @{clip='entrance';sheet='seal-core-death-entrance-v2.png';first=8;count=8}
)
foreach($clip in $clips){[SealCoreExport20261009]::Export((Join-Path $source $clip.sheet),(Join-Path $target $clip.clip),$clip.first,$clip.count)}
$records=@(foreach($clip in $clips){foreach($file in Get-ChildItem -LiteralPath (Join-Path $target $clip.clip) -Filter '*.png'){
    [PSCustomObject]@{clip=$clip.clip;frame=[int]$file.BaseName;source=$clip.sheet;sourceCell=$clip.first+[int]$file.BaseName;path=$file.FullName;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}})
$manifest=[PSCustomObject]@{
    method='Built-in image_gen v2 4x4 sheets exported to a common 64px sampling grid inside 80px canvases; nearest point, explicit 14-color shared palette, binary alpha threshold 180, zero RGB for transparent pixels, removal of isolated one-pixel export crumbs, ground pivot (40,8). Generated originals preserved. No duplicated or mechanically synthesized intermediate poses.';
    ppu=32;canvas=@(80,80);pivot=@(40,8);files=$records.Count;records=$records
}
[IO.File]::WriteAllText((Join-Path $source ('export-'+$ExportVersion+'.json')),($manifest|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
[PSCustomObject]@{files=$records.Count;folder=$target;uniqueFileHashes=(@($records.sha256|Sort-Object -Unique)).Count}
