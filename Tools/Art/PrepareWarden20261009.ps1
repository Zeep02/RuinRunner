param([string]$ProjectRoot='F:/codex/LoglikeCap',[string]$ExportVersion='v3')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
public static class WardenExport20261009
{
    static readonly Color[] Palette={
        Color.FromArgb(18,18,28),Color.FromArgb(34,31,41),Color.FromArgb(52,49,58),
        Color.FromArgb(75,71,79),Color.FromArgb(99,94,100),Color.FromArgb(126,117,116),
        Color.FromArgb(96,56,29),Color.FromArgb(145,92,44),Color.FromArgb(188,132,65),Color.FromArgb(226,165,85),
        Color.FromArgb(54,18,36),Color.FromArgb(103,28,48),Color.FromArgb(151,40,56),Color.FromArgb(197,59,66),
        Color.FromArgb(13,68,86),Color.FromArgb(12,144,172),Color.FromArgb(31,220,224),Color.FromArgb(160,253,233)};
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
    static int[] Components(Bitmap sheet,int rows)
    {
        int w=sheet.Width,h=sheet.Height;var labels=new int[w*h];var opaque=new bool[w*h];var queue=new int[w*h];
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)opaque[y*w+x]=sheet.GetPixel(x,y).A>=180;
        int component=0;var cells=new int[256];
        for(int p=0;p<labels.Length;p++)
        {
            if(!opaque[p]||labels[p]!=0)continue;component++;int head=0,tail=0;queue[tail++]=p;labels[p]=component;
            int left=w,right=-1,top=h,bottom=-1;
            while(head<tail)
            {
                int at=queue[head++],x=at%w,y=at/w;left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||nx>=w||ny<0||ny>=h)continue;int next=ny*w+nx;
                    if(opaque[next]&&labels[next]==0){labels[next]=component;queue[tail++]=next;}
                }
            }
            if(component>=cells.Length)throw new Exception("Unexpected many disconnected alpha islands");
            // Source sprites are single connected bodies. Small detached marks are
            // rejected from extraction, never copied from the neighboring pose.
            cells[component]=-1;
            if(tail>1000)
            {
                if(left==0||right==w-1||top==0||bottom==h-1)throw new Exception("Source body is clipped by sheet edge");
                cells[component]=(int)((top+bottom)*.5/h*rows)*4+(int)((left+right)*.5/w*4)+1;
            }
        }
        var counts=new int[rows*4+1];for(int i=1;i<=component;i++)if(cells[i]>0)counts[cells[i]]++;
        for(int i=1;i<counts.Length;i++)if(counts[i]!=1)throw new Exception("Need one complete connected body in cell "+(i-1)+", found "+counts[i]);
        for(int i=0;i<labels.Length;i++)labels[i]=labels[i]==0?0:Math.Max(0,cells[labels[i]]);
        return labels;
    }
    public static void Export(string source,string destination,int first,int count,int rows,int destinationFirst)
    {
        Directory.CreateDirectory(destination);
        using(var sheet=new Bitmap(source))
        {
        var labels=Components(sheet,rows);
        for(int frame=0;frame<count;frame++)
        {
            int cell=first+frame,col=cell%4,row=cell/4;var colors=new Color[112*112];
            for(int y=0;y<112;y++)for(int x=0;x<112;x++)
            {
                int sx=(int)((col+(x-16+.5)/80)*sheet.Width/4);
                int sy=(int)((row+(y-16+.5)/80)*sheet.Height/rows);
                if(sx>=0&&sx<sheet.Width&&sy>=0&&sy<sheet.Height&&labels[sy*sheet.Width+sx]==cell+1)
                    colors[y*112+x]=Quantize(sheet.GetPixel(sx,sy));
            }
            // Only remove isolated one-pixel export crumbs. Preserve source poses.
            for(int y=0;y<112;y++)for(int x=0;x<112;x++)
            {
                if(colors[y*112+x].A==0)continue;bool neighbour=false;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int nx=x+dx,ny=y+dy;if((dx!=0||dy!=0)&&nx>=0&&ny>=0&&nx<112&&ny<112&&colors[ny*112+nx].A>0)neighbour=true;}
                if(!neighbour)colors[y*112+x]=Color.FromArgb(0,0,0,0);
            }
            int bottom=-1;for(int y=0;y<112;y++)for(int x=0;x<112;x++)if(colors[y*112+x].A>0)bottom=Math.Max(bottom,y);
            if(bottom<0)throw new Exception("Empty source cell: "+source+"/"+cell);
            using(var output=new Bitmap(112,112,PixelFormat.Format32bppArgb))
            {
                for(int y=0;y<112;y++)for(int x=0;x<112;x++)
                {int oy=y+103-bottom;if(oy>=0&&oy<112)output.SetPixel(x,oy,colors[y*112+x]);}
                string path=Path.Combine(destination,(destinationFirst+frame).ToString("D2")+".png");
                if(File.Exists(path))throw new Exception("Preserve prior candidate; choose a new export folder: "+path);
                output.Save(path,ImageFormat.Png);
            }
        }
        }
    }
}
'@ -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
$source=Join-Path $ProjectRoot 'ArtDirection/Generated/Warden-20261009'
$target=Join-Path $source ('frames-'+$ExportVersion)
$clips=@(
    @{clip='00';name='idle';sheet='warden-idle-walk-v1.png';first=0;count=8;rows=4},
    @{clip='01';name='walk';sheet='warden-idle-walk-v1.png';first=8;count=6;rows=4},
    @{clip='01';name='walk';sheet='warden-idle-walk-v2.png';first=14;count=1;rows=4;destinationFirst=6},
    @{clip='01';name='walk';sheet='warden-idle-walk-v1.png';first=15;count=1;rows=4;destinationFirst=7},
    @{clip='02';name='radial';sheet='warden-radial-volley-v2.png';first=0;count=8;rows=4},
    @{clip='03';name='volley';sheet='warden-radial-volley-v2.png';first=8;count=8;rows=4},
    @{clip='04';name='charge';sheet='warden-charge-leap-v1.png';first=0;count=8;rows=4},
    @{clip='05';name='leap';sheet='warden-charge-leap-v1.png';first=8;count=8;rows=4},
    @{clip='06';name='hurt';sheet='warden-hurt-death-v1.png';first=0;count=8;rows=4},
    @{clip='07';name='death';sheet='warden-hurt-death-v1.png';first=8;count=8;rows=4},
    @{clip='entrance';name='entrance';sheet='warden-entrance-v1.png';first=0;count=8;rows=2}
)
foreach($clip in $clips){[WardenExport20261009]::Export((Join-Path $source $clip.sheet),(Join-Path $target $clip.clip),$clip.first,$clip.count,$clip.rows,[int]$clip.destinationFirst)}
$records=@(foreach($clip in $clips){for($i=0;$i -lt $clip.count;$i++){
    $frame=[int]$clip.destinationFirst+$i
    $file=Get-Item -LiteralPath (Join-Path (Join-Path $target $clip.clip) ($frame.ToString('D2')+'.png'))
    [PSCustomObject]@{clip=$clip.clip;name=$clip.name;frame=$frame;source=$clip.sheet;sourceCell=$clip.first+$i;path=$file.FullName;sha256=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
}})
$manifest=[PSCustomObject]@{
    method='Built-in image_gen poses isolated by connected alpha body, then nearest sample at80px per source cell inside112px canvas; explicit18-color shared palette; binary alpha180; zero invisible RGB; remove isolated one-pixel crumbs; common ground pivot(64,8). Complete bodies may cross nominal cells, adjacent body fragments are excluded. Original sheets preserved. No repeated or mechanically synthesized intermediate poses.';
    ppu=32;canvas=@(112,112);pivot=@(64,8);files=$records.Count;records=$records
}
[IO.File]::WriteAllText((Join-Path $source ('export-'+$ExportVersion+'.json')),($manifest|ConvertTo-Json -Depth 8),[Text.UTF8Encoding]::new($false))
[PSCustomObject]@{files=$records.Count;folder=$target;uniqueFileHashes=(@($records.sha256|Sort-Object -Unique)).Count}
