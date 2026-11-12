namespace RadarTest {
using System;
using PointF=System.Drawing.PointF;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Point=System.Windows.Point;
using Size=System.Drawing.Size;
using Color=System.Windows.Media.Color;

sealed class RadarCanvas : FrameworkElement {
    public Frame Frame;
    public DetectionZone Draft=new DetectionZone(),Actual;
    public int Radius=450,ActualRadius;
    public bool MirrorX,MirrorY,Phosphor=true,Editing,ShowDraft,Simple;
    public double Fade=10,Now;
    public int Heartbeat=60;
    public readonly PhosphorTrail Trail=new PhosphorTrail();
    public Action<DetectionZone> ZoneChanged;
    bool dragging;int handle;Point anchor;PointF start;DetectionZone before;
    public bool IsDragging {get{return dragging;}}
    static readonly Color[] Colors={ColorsFrom(0,220,240),ColorsFrom(60,225,100),ColorsFrom(255,171,0),ColorsFrom(235,100,230),ColorsFrom(250,225,50),ColorsFrom(50,170,255),ColorsFrom(250,140,120)};
    static Color ColorsFrom(byte r,byte g,byte b) {return Color.FromRgb(r,g,b);}
    MapView View {get {return new MapView(new Size((int)ActualWidth,(int)ActualHeight),MirrorX,MirrorY);}}
    Point Screen(MapView view,PointF p) {var point=view.Screen(p);return new Point(point.X,point.Y);}
    static Brush Brush(Color color,byte alpha) {return new SolidColorBrush(Color.FromArgb(alpha,color.R,color.G,color.B));}
    protected override void OnRender(DrawingContext dc) {
        base.OnRender(dc);if(ActualWidth<1||ActualHeight<1)return;
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(20,28,40)),null,new Rect(0,0,ActualWidth,ActualHeight));
        var v=View;var grid=new Pen(new SolidColorBrush(Color.FromRgb(55,75,95)),0.7);
        int nx=(int)(ActualWidth/v.Scale/2)+1,ny=(int)(ActualHeight/v.Scale/2)+1;
        for(int i=-nx;i<=nx;i++)dc.DrawLine(grid,new Point(v.Cx+i*v.Scale,2),new Point(v.Cx+i*v.Scale,ActualHeight-2));
        for(int i=-ny;i<=ny;i++)dc.DrawLine(grid,new Point(2,v.Cy+i*v.Scale),new Point(ActualWidth-2,v.Cy+i*v.Scale));
        var axes=new Pen(new SolidColorBrush(Color.FromRgb(90,120,145)),1);
        dc.DrawLine(axes,new Point(v.Cx,2),new Point(v.Cx,ActualHeight-2));dc.DrawLine(axes,new Point(2,v.Cy),new Point(ActualWidth-2,v.Cy));
        if(Actual!=null)DrawZone(dc,v,Actual,ActualRadius,Color.FromRgb(50,205,90),false);
        if(Editing||ShowDraft) {
            DrawZone(dc,v,Draft,Radius,Color.FromRgb(255,170,0),true);
            if(Editing)foreach(var h in Draft.Handles()) {var p=Screen(v,h);dc.DrawRectangle(System.Windows.Media.Brushes.Orange,null,new Rect(p.X-4,p.Y-4,8,8));}
        }
        if(Simple) {
            Text(dc,RadarTest.Language.T("DEBUG 0 · Людей: "+(Frame==null?"—":Frame.Count.ToString())),24,new Point(v.Cx,v.Cy-20),true);
            Text(dc,RadarTest.Language.T("Только счётчик · heartbeat "+Heartbeat+" с · координаты не передаются"),12,new Point(v.Cx,v.Cy+15),true);return;
        }
        if(Phosphor)foreach(var t in Trail.Marks) {
            if(Math.Abs(t.X)>5||Math.Abs(t.Y)>5)continue;
            int alpha=PhosphorTrail.Alpha(Now-t.Time,Fade);if(alpha==0)continue;
            var p=Screen(v,new PointF(t.X,t.Y));Color color=Colors[t.Id%(uint)Colors.Length];
            dc.DrawEllipse(Brush(color,(byte)(alpha/5)),null,p,6,6);dc.DrawEllipse(Brush(color,(byte)alpha),null,p,2,2);
        }
        if(Frame==null||!Frame.Detailed)return;
        foreach(var t in Frame.Targets) {
            if(Math.Abs(t.X)>5||Math.Abs(t.Y)>5)continue;
            var p=Screen(v,new PointF(t.X,t.Y));dc.DrawEllipse(new SolidColorBrush(Colors[t.Id%(uint)Colors.Length]),null,p,5,5);
            Text(dc,t.Id.ToString(),12,new Point(p.X+8,p.Y-6),false);
        }
    }
    void Text(DrawingContext dc,string value,double size,Point point,bool center) {
        var text=new FormattedText(value,System.Globalization.CultureInfo.CurrentCulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),size,System.Windows.Media.Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
        if(center)point.X-=text.Width/2;dc.DrawText(text,point);
    }
    void DrawZone(DrawingContext dc,MapView v,DetectionZone z,int radius,Color color,bool draft) {
        var rect=v.Rectangle(z);var box=new RectangleGeometry(new Rect(rect.X,rect.Y,rect.Width,rect.Height));
        var circle=new EllipseGeometry(new Point(v.Cx,v.Cy),radius/100.0*v.Scale,radius/100.0*v.Scale);
        var region=new CombinedGeometry(GeometryCombineMode.Intersect,box,circle);
        dc.DrawGeometry(Brush(color,(byte)(draft?20:28)),null,region);
        var pen=new Pen(Brush(color,190),1.2);if(draft)pen.DashStyle=DashStyles.Dash;
        dc.DrawGeometry(null,pen,box);dc.DrawGeometry(null,pen,circle);
    }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonDown(e);if(!Editing)return;
        anchor=e.GetPosition(this);start=View.World(new PointF((float)anchor.X,(float)anchor.Y));before=Draft.Copy();handle=-1;
        var handles=Draft.Handles();for(int i=0;i<handles.Length;i++) {var p=Screen(View,handles[i]);if(Math.Abs(p.X-anchor.X)<=8&&Math.Abs(p.Y-anchor.Y)<=8){handle=i;break;}}
        dragging=true;CaptureMouse();Focus();e.Handled=true;
    }
    protected override void OnMouseMove(MouseEventArgs e) {
        base.OnMouseMove(e);if(!dragging)return;var pixel=e.GetPosition(this);
        if(Math.Abs(pixel.X-anchor.X)+Math.Abs(pixel.Y-anchor.Y)<3)return;
        var point=View.World(new PointF((float)pixel.X,(float)pixel.Y));
        if(handle>=0) {Draft=before.Copy();Draft.Resize(handle,point);}else Draft=DetectionZone.Between(start,point);
        ShowDraft=true;InvalidateVisual();
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
        base.OnMouseLeftButtonUp(e);if(!dragging)return;OnMouseMove(e);dragging=false;ReleaseMouseCapture();
        if(!Draft.Same(before)&&ZoneChanged!=null)ZoneChanged(Draft.Copy());e.Handled=true;
    }
    protected override void OnLostMouseCapture(MouseEventArgs e) {base.OnLostMouseCapture(e);if(dragging){Draft=before;dragging=false;InvalidateVisual();}}
    protected override void OnKeyDown(KeyEventArgs e) {base.OnKeyDown(e);if(e.Key==Key.Escape&&dragging){Draft=before;dragging=false;ReleaseMouseCapture();InvalidateVisual();e.Handled=true;}}
}
}
