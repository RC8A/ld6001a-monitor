namespace RadarTest {
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

public sealed class NumberField : UserControl {
    readonly TextBox editor;
    readonly Border frame;
    decimal value;
    bool updating;
    public decimal Minimum {get;set;}
    public decimal Maximum {get;set;}
    public decimal Step {get;set;}
    public int DecimalPlaces {get;set;}
    public event EventHandler ValueEdited;
    public event EventHandler InvalidInput;
    public NumberField() {
        Minimum=0;Maximum=100;Step=1;Height=28;Margin=new Thickness(0,0,4,0);
        var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(16)});
        editor=new TextBox{Style=null,Height=26,BorderThickness=new Thickness(0),Margin=new Thickness(0),Padding=new Thickness(5,0,2,0),VerticalContentAlignment=VerticalAlignment.Center,Background=Brushes.Transparent};
        grid.Children.Add(editor);
        var arrows=new Grid();arrows.RowDefinitions.Add(new RowDefinition());arrows.RowDefinitions.Add(new RowDefinition());Grid.SetColumn(arrows,1);
        string template="<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='{x:Type RepeatButton}' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'><Border Background='{TemplateBinding Background}' BorderBrush='#C6CFDA' BorderThickness='0,0,0,0.5'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border></ControlTemplate>";
        for(int i=0;i<2;i++) {
            int direction=i==0?1:-1;
            var button=new RepeatButton{Content=i==0?"▴":"▾",FontSize=9,Foreground=new SolidColorBrush(Color.FromRgb(60,78,100)),Background=new SolidColorBrush(Color.FromRgb(237,241,246)),Template=(ControlTemplate)XamlReader.Parse(template),Focusable=false,IsTabStop=false,Delay=350,Interval=80};
            Grid.SetRow(button,i);button.Click+=delegate {Change(direction);};arrows.Children.Add(button);
        }
        grid.Children.Add(arrows);frame=new Border{Background=Brushes.White,BorderBrush=new SolidColorBrush(Color.FromRgb(173,184,199)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(2),Child=grid};Content=frame;
        editor.TextChanged+=delegate {
            if(updating)return;decimal parsed;
            bool valid=TryParse(editor.Text,out parsed);frame.BorderBrush=valid?new SolidColorBrush(Color.FromRgb(173,184,199)):Brushes.Firebrick;
            if(valid){value=parsed;if(ValueEdited!=null)ValueEdited(this,EventArgs.Empty);}else if(InvalidInput!=null)InvalidInput(this,EventArgs.Empty);
        };
        editor.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Up||e.Key==Key.Down){Change(e.Key==Key.Up?1:-1);e.Handled=true;}};
        editor.LostKeyboardFocus+=delegate {decimal parsed;if(!TryParse(editor.Text,out parsed))SetText();};
    }
    bool TryParse(string text,out decimal parsed) {
        return decimal.TryParse(text.Replace(',','.'),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out parsed)&&parsed>=Minimum&&parsed<=Maximum&&(DecimalPlaces!=0||parsed==decimal.Truncate(parsed));
    }
    public decimal Value {get{return value;}set{this.value=Math.Max(Minimum,Math.Min(Maximum,value));SetText();}}
    public TextBox Editor {get{return editor;}}
    void SetText() {updating=true;try {editor.Text=value.ToString(DecimalPlaces>0?"0."+new string('0',DecimalPlaces):"0",CultureInfo.CurrentCulture);frame.BorderBrush=new SolidColorBrush(Color.FromRgb(173,184,199));}finally {updating=false;}}
    internal void Change(int direction) {value=Math.Max(Minimum,Math.Min(Maximum,value+direction*Step));SetText();if(ValueEdited!=null)ValueEdited(this,EventArgs.Empty);}
    internal static void Verify() {
        var field=new NumberField{Minimum=0.5m,Maximum=60,DecimalPlaces=1,Step=0.5m,Value=15};int edits=0;field.ValueEdited+=delegate{edits++;};field.Change(1);
        if(field.Value!=15.5m||edits!=1)throw new Exception("Numeric spinner step failed");field.Value=60;field.Change(1);if(field.Value!=60)throw new Exception("Numeric spinner bounds failed");
        field.Editor.Text="12,5";if(field.Value!=12.5m)throw new Exception("Numeric keyboard input failed");
    }
}
}
