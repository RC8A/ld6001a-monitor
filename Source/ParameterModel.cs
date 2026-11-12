namespace RadarTest {
using System;
using System.Drawing;
using System.Globalization;
using System.Collections.Generic;


sealed class ParameterDefinition {
    public string Command,Key,Name,Unit,Description;
    public int Min,Max,Initial,Scale=1;
    public bool Extended;
    public string Display(int value) {return ((decimal)value/Scale).ToString(Scale==10?"0.0":"0",CultureInfo.CurrentCulture);}
    public bool TryValue(string text,out int value) {
        value=0;decimal n;
        if(!decimal.TryParse(text.Trim().Replace(',','.'),NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out n))return false;
        n*=Scale;if(n<Min||n>Max||n!=decimal.Truncate(n))return false;
        value=(int)n;return true;
    }
}
static class ParameterCatalog {
    public static readonly ParameterDefinition[] All={
        new ParameterDefinition {Command="AT+RANGE",Key="Range",Name="Радиус обнаружения",Unit="см",Min=100,Max=500,Initial=450,Description="Ограничивает область обнаружения кругом."},
        new ParameterDefinition {Command="AT+HEIGHTD",Key="detectionHeight",Name="Высота установки",Unit="см",Min=50,Max=500,Initial=300,Description="Высота размещения датчика над полом."},
        new ParameterDefinition {Command="AT+DPKTH",Key="Sen",Name="Порог чувствительности",Unit="",Min=1,Max=9,Initial=4,Description="Чем больше число, тем ниже чувствительность вдали."},
        new ParameterDefinition {Command="AT+HEATIME",Key="Heart_Time",Name="Heartbeat",Unit="с",Min=10,Max=999,Initial=60,Extended=true,Description="Периодическая выдача результата. Влияние на частый поток DEBUG 3 отдельно не проверено."},
        new ParameterDefinition {Command="AT+Moving",Key="Moving target",Name="Исчезновение движущейся цели",Unit="с",Min=5,Max=1000,Initial=110,Scale=10,Extended=true,Description="Время удержания потерянной движущейся цели; не задержка обнаружения."},
        new ParameterDefinition {Command="AT+Static",Key="Static target",Name="Исчезновение неподвижной цели",Unit="с",Min=5,Max=1000,Initial=100,Scale=10,Extended=true,Description="Время удержания потерянной неподвижной цели."},
        new ParameterDefinition {Command="AT+Exit",Key="Target exit",Name="Выход за границу",Unit="с",Min=2,Max=1000,Initial=5,Scale=10,Extended=true,Description="Время выхода цели за границу области."},
        new ParameterDefinition {Command="AT+XNega",Key="XboundaryN",Name="Граница X−",Unit="см",Min=-500,Max=-20,Initial=-450,Description="Отрицательная граница единой аппаратной зоны."},
        new ParameterDefinition {Command="AT+XPosi",Key="XboundaryP",Name="Граница X+",Unit="см",Min=20,Max=500,Initial=450,Description="Положительная граница единой аппаратной зоны."},
        new ParameterDefinition {Command="AT+YNega",Key="YboundaryN",Name="Граница Y−",Unit="см",Min=-500,Max=-20,Initial=-450,Description="Отрицательная граница единой аппаратной зоны."},
        new ParameterDefinition {Command="AT+YPosi",Key="YboundaryP",Name="Граница Y+",Unit="см",Min=20,Max=500,Initial=450,Description="Положительная граница единой аппаратной зоны."}
    };
    public static ParameterDefinition ByCommand(string command) {
        foreach(var p in All)if(string.Equals(p.Command,command,StringComparison.OrdinalIgnoreCase))return p;
        return null;
    }
}

}