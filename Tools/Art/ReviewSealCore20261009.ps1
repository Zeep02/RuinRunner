param([string]$ProjectRoot='F:/codex/LoglikeCap',[string]$ExportVersion='v4')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
public static class SealCoreReview20261009
{
    public static string[] Review(string root,string version)
    {
        string[] clips={"idle","move","cast","sweep","charge","hurt","death","entrance"};
        var rows=new string[61];int record=0;rows[record++]="clip,frame,x,y,width,height,opaquePixels,colors,partialAlpha,nonzeroTransparentRGB";
        using(var contact=new Bitmap(8*256,8*280,PixelFormat.Format32bppArgb))
        using(var graphics=Graphics.FromImage(contact))
        using(var font=new Font("Segoe UI",12))
        {
            graphics.Clear(Color.FromArgb(40,40,45));
            graphics.InterpolationMode=InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode=PixelOffsetMode.Half;
            for(int r=0;r<clips.Length;r++)
            {
                string[] files=Directory.GetFiles(Path.Combine(root,"frames-"+version,clips[r]),"*.png");Array.Sort(files);
                for(int f=0;f<files.Length;f++)using(var bmp=new Bitmap(files[f]))
                {
                    int left=80,top=80,right=-1,bottom=-1,opaque=0,partial=0,invisibleRGB=0;
                    var palette=new int[6400];int paletteCount=0;
                    for(int y=0;y<bmp.Height;y++)for(int x=0;x<bmp.Width;x++)
                    {
                        var c=bmp.GetPixel(x,y);
                        if(c.A==0){if(c.R!=0||c.G!=0||c.B!=0)invisibleRGB++;continue;}
                        if(c.A!=255)partial++;
                        opaque++;if(Array.IndexOf(palette,c.ToArgb(),0,paletteCount)<0)palette[paletteCount++]=c.ToArgb();left=Math.Min(left,x);top=Math.Min(top,y);right=Math.Max(right,x);bottom=Math.Max(bottom,y);
                    }
                    rows[record++]=clips[r]+","+f+","+left+","+top+","+(right-left+1)+","+(bottom-top+1)+","+opaque+","+paletteCount+","+partial+","+invisibleRGB;
                    if(opaque==0||partial!=0||invisibleRGB!=0||paletteCount>14)throw new Exception("Invalid exported pixel data "+files[f]);
                    // QA montage only: every adopted frame remains its own untouched PNG.
                    graphics.DrawImage(bmp,new Rectangle(f*256+8,r*280+30,240,240),0,0,80,80,GraphicsUnit.Pixel);
                    graphics.DrawString(clips[r]+" "+f.ToString("D2"),font,Brushes.White,f*256+12,r*280+6);
                    graphics.DrawLine(Pens.DimGray,f*256+8,r*280+30+216,f*256+248,r*280+30+216);
                }
            }
            contact.Save(Path.Combine(root,"contact-"+version+".png"),ImageFormat.Png);
        }
        return rows;
    }
}
'@ -ReferencedAssemblies @([System.Drawing.Bitmap].Assembly.Location,[System.Drawing.Color].Assembly.Location,(Join-Path $PSHOME 'System.Private.Windows.GdiPlus.dll'),(Join-Path $PSHOME 'System.Private.Windows.Core.dll'))
$root=Join-Path $ProjectRoot 'ArtDirection/Generated/SealCore-20261009'
$rows=[SealCoreReview20261009]::Review($root,$ExportVersion)
[IO.File]::WriteAllLines((Join-Path $root ('pixel-audit-'+$ExportVersion+'.csv')),$rows,[Text.UTF8Encoding]::new($false))
[PSCustomObject]@{frames=$rows.Count-1;review=(Join-Path $root ('contact-'+$ExportVersion+'.png'));audit=(Join-Path $root ('pixel-audit-'+$ExportVersion+'.csv'))}
