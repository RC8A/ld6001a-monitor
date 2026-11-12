$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$assetDir=Join-Path $PSScriptRoot 'Assets'
New-Item -ItemType Directory -Path $assetDir -Force | Out-Null
$symbol=@'
<rect x="8" y="8" width="240" height="240" rx="52" fill="#142333"/>
<circle cx="128" cy="128" r="84" fill="none" stroke="#427285" stroke-width="5"/>
<circle cx="128" cy="128" r="52" fill="none" stroke="#31596C" stroke-width="4"/>
<path d="M44 128H212M128 44V212" fill="none" stroke="#31596C" stroke-width="3"/>
<path d="M128 128V44A84 84 0 0 1 187.4 68.6Z" fill="#39D0C0" fill-opacity=".15"/>
<path d="M128 128L187.4 68.6" fill="none" stroke="#45D8C4" stroke-width="6" stroke-linecap="round"/>
<circle cx="170" cy="93" r="11" fill="#B1F36B"/>
<circle cx="89" cy="159" r="8" fill="#45D8C4"/>
<circle cx="128" cy="128" r="5" fill="#EAF6FA"/>
'@
$svg='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">'+$symbol+'</svg>'
[IO.File]::WriteAllText((Join-Path $assetDir 'ld-monitor-icon.svg'),$svg)
$logo='<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 820 120"><g transform="translate(0,4) scale(.4375)">'+$symbol+'</g><text x="132" y="70" fill="#142333" font-family="Segoe UI, sans-serif" font-weight="600" font-size="49">LD6001A Monitor</text><text x="135" y="96" fill="#517080" font-family="Segoe UI, sans-serif" font-size="12" letter-spacing="3">RADAR · PRESENCE · TRACKING</text></svg>'
[IO.File]::WriteAllText((Join-Path $assetDir 'ld-monitor-logo.svg'),$logo)
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
public static class BrandRenderer {
 static Color C(string hex) {return ColorTranslator.FromHtml(hex);}
 static void Symbol(Graphics g) {
  g.SmoothingMode=SmoothingMode.AntiAlias;
  using(var p=new GraphicsPath()) {
   p.AddArc(8,8,104,104,180,90);p.AddArc(144,8,104,104,270,90);p.AddArc(144,144,104,104,0,90);p.AddArc(8,144,104,104,90,90);p.CloseFigure();
   using(var b=new SolidBrush(C("#142333")))g.FillPath(b,p);
  }
  using(var pen=new Pen(C("#427285"),5))g.DrawEllipse(pen,44,44,168,168);
  using(var pen=new Pen(C("#31596C"),4))g.DrawEllipse(pen,76,76,104,104);
  using(var pen=new Pen(C("#31596C"),3)){g.DrawLine(pen,44,128,212,128);g.DrawLine(pen,128,44,128,212);}
  using(var b=new SolidBrush(Color.FromArgb(38,C("#39D0C0"))))g.FillPie(b,44,44,168,168,270,45);
  using(var pen=new Pen(C("#45D8C4"),6)){pen.StartCap=LineCap.Round;pen.EndCap=LineCap.Round;g.DrawLine(pen,128,128,187.4f,68.6f);}
  using(var b=new SolidBrush(C("#B1F36B")))g.FillEllipse(b,159,82,22,22);
  using(var b=new SolidBrush(C("#45D8C4")))g.FillEllipse(b,81,151,16,16);
  using(var b=new SolidBrush(C("#EAF6FA")))g.FillEllipse(b,123,123,10,10);
 }
 static Bitmap Icon(int size) {
  var bitmap=new Bitmap(size,size,PixelFormat.Format32bppArgb);
  using(var g=Graphics.FromImage(bitmap)){g.Clear(Color.Transparent);g.ScaleTransform(size/256f,size/256f);Symbol(g);}return bitmap;
 }
 public static void Write(string folder) {
  int[] sizes={16,20,24,32,40,48,64,128,256};var chunks=new List<byte[]>();
  foreach(int size in sizes)using(var bitmap=Icon(size))using(var ms=new MemoryStream()){bitmap.Save(ms,ImageFormat.Png);chunks.Add(ms.ToArray());if(size==256)bitmap.Save(Path.Combine(folder,"ld-monitor-icon.png"),ImageFormat.Png);}
  using(var file=File.Create(Path.Combine(folder,"ld-monitor.ico")))using(var w=new BinaryWriter(file)) {
   w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+sizes.Length*16;
   for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(chunks[i].Length);w.Write(offset);offset+=chunks[i].Length;}
   foreach(byte[] bytes in chunks)w.Write(bytes);
  }
  using(var bitmap=new Bitmap(1640,240))using(var g=Graphics.FromImage(bitmap)) {
   g.Clear(Color.White);g.ScaleTransform(2,2);g.TranslateTransform(0,4);g.ScaleTransform(.4375f,.4375f);Symbol(g);g.ResetTransform();g.ScaleTransform(2,2);
   g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
   using(var font=new Font("Segoe UI",49,FontStyle.Bold,GraphicsUnit.Pixel))using(var brush=new SolidBrush(C("#142333")))g.DrawString("LD6001A Monitor",font,brush,128,18);
   using(var font=new Font("Segoe UI",12,FontStyle.Regular,GraphicsUnit.Pixel))using(var brush=new SolidBrush(C("#517080")))g.DrawString("RADAR  ·  PRESENCE  ·  TRACKING",font,brush,134,81);
   bitmap.Save(Path.Combine(folder,"ld-monitor-logo.png"),ImageFormat.Png);
  }
  using(var bitmap=new Bitmap(800,260))using(var g=Graphics.FromImage(bitmap)) {
   g.Clear(C("#F3F5F8"));int[] preview={16,24,32,48,64,128};int x=28;
   foreach(int size in preview){using(var icon=Icon(size))g.DrawImageUnscaled(icon,x,58);using(var font=new Font("Segoe UI",10))using(var brush=new SolidBrush(C("#517080")))g.DrawString(size+" px",font,brush,x,208);x+=size+40;}
   bitmap.Save(Path.Combine(folder,"icon-sizes.png"),ImageFormat.Png);
  }
 }
}
'@ -ReferencedAssemblies System.Drawing
[BrandRenderer]::Write($assetDir)
Write-Output "Brand assets: $assetDir"
