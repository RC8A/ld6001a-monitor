namespace RadarTest {
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

static class Language {
    public static string Code="ru";
    static readonly ConditionalWeakTable<DependencyObject,Dictionary<string,string>> originals=new ConditionalWeakTable<DependencyObject,Dictionary<string,string>>();
    static readonly Dictionary<string,string> words=new Dictionary<string,string> {
        {"Настройки","Settings"},{"Справка","Help"},{"Язык / Language","Language / Язык"},{"Параметры DEBUG 0 / DEBUG 3…","DEBUG 0 / DEBUG 3 parameters…"},{"Считать параметры из датчика","Read sensor parameters"},{"О программе…","About…"},
        {"Обновить","Refresh"},{"Подключить","Connect"},{"Отключить","Disconnect"},{"Демо","Demo"},{"Радиус, см:","Radius, cm:"},{"Высота, см:","Height, cm:"},{"Порог:","Threshold:"},{"Отправить + LF","Send + LF"},
        {"Люминофор","Phosphor trail"},{"Затухание, с:","Fade, s:"},{"Зеркало X","Mirror X"},{"Зеркало Y","Mirror Y"},{"Скрыть поток данных","Hide data stream"},
        {"При подключении параметры считываются из датчика.","Sensor parameters are read on connection."},{"Отключено · UART 8N1","Disconnected · UART 8N1"},{"шаг сетки: 1","grid step: 1"},
        {"Зона обнаружения · см","Detection zone · cm"},{"Зона","Zone"},{"Датчик","Sensor"},{"Проект","Draft"},{"Мышью","Draw"},{"Считать","Read"},{"Авто: через 1 с","Auto: after 1 s"},
        {"Границы не считаны. Проект сохранён локально.","Boundaries not read. Draft saved locally."},{"Зелёная зона считана из датчика. Ограничена радиусом.","Green zone read from the sensor. Limited by radius."},{"Оранжевая — проект. Автоприменение через 1 с.","Orange is a draft. Applied automatically after 1 s."},
        {"Допустимо −500…−20 см","Allowed: −500…−20 cm"},{"Допустимо 20…500 см","Allowed: 20…500 cm"},{"Сначала подключите датчик.","Connect the sensor first."},{"Введите одну AT-команду без перевода строки.","Enter one AT command without a line break."},
        {"Дождитесь ответа на предыдущую команду.","Wait for the previous command to complete."},{"Изменения будут отправлены через 1 с после последней правки.","Changes will be sent 1 s after the last edit."},{"Сохранено локально. Датчик не подключён.","Saved locally. The sensor is disconnected."},
        {"Ожидание готовности датчика: чтение параметров, до 30 секунд.","Waiting for the sensor: reading parameters, up to 30 seconds."},{"Датчик не ответил за 30 с. Проверьте питание и порт; повторите AT+READ.","No response within 30 s. Check power and port; retry AT+READ."},
        {"Подтверждённое состояние датчика","Confirmed sensor state"},{"Изменить состояние датчика","Change sensor state"},{"Состояние неизвестно. START подтверждается пакетами или ответом; STOP — ответом датчика.","State unknown. START is confirmed by packets or ACK; STOP by ACK."},
        {"Параметры датчика · DEBUG 0 / DEBUG 3","Sensor parameters · DEBUG 0 / DEBUG 3"},{"Параметры общие для DEBUG 0 и 3. Редактируйте «Значение» и нажмите Enter: отправка через 1 секунду, затем проверка чтением.","Parameters are shared by DEBUG 0 and 3. Edit Value and press Enter: sent after 1 second, then verified by readback."},
        {"Считать из датчика","Read from sensor"},{"Параметр","Parameter"},{"Значение","Value"},{"В датчике","Sensor value"},{"Ед.","Unit"},{"Диапазон","Range"},{"Назначение","Description"},{"Да","Yes"},{"Допустимо ","Allowed: "},
        {"Радиус обнаружения","Detection radius"},{"Высота установки","Mounting height"},{"Порог чувствительности","Sensitivity threshold"},{"Исчезновение движущейся цели","Moving target timeout"},{"Исчезновение неподвижной цели","Static target timeout"},{"Выход за границу","Boundary exit timeout"},
        {"Граница X−","X− boundary"},{"Граница X+","X+ boundary"},{"Граница Y−","Y− boundary"},{"Граница Y+","Y+ boundary"},{"см","cm"},{"с","s"},
        {"Ограничивает область обнаружения кругом.","Limits the detection area to a circle."},{"Высота размещения датчика над полом.","Sensor mounting height above the floor."},{"Чем больше число, тем ниже чувствительность вдали.","Higher values reduce long-range sensitivity."},
        {"Периодическая выдача результата. Влияние на частый поток DEBUG 3 отдельно не проверено.","Periodic result output. Its effect on the frequent DEBUG 3 stream has not been verified separately."},
        {"Время удержания потерянной движущейся цели; не задержка обнаружения.","How long a lost moving target is retained; not an initial detection delay."},{"Время удержания потерянной неподвижной цели.","How long a lost stationary target is retained."},{"Время выхода цели за границу области.","Target exit time at the detection boundary."},
        {"Отрицательная граница единой аппаратной зоны.","Negative boundary of the single hardware zone."},{"Положительная граница единой аппаратной зоны.","Positive boundary of the single hardware zone."},
        {"О программе — ","About — "},{"Версия ","Version "},{" · сборка "," · build "},{"Автор: ","Author: "},{"Лицензия: ","License: "},{"Разработано при участии OpenAI Codex","Developed with assistance from OpenAI Codex"},{"Закрыть","Close"},{" — ошибка"," — error"},
        {"Тестирование HLK-LD6001A-60G через USB–UART.\nНезависимый проект, не программа производителя.","HLK-LD6001A-60G testing over USB–UART.\nIndependent project, not the manufacturer's software."},
        {"Не подтверждено. ","Not confirmed. "},{"Нет ответа: ","No response: "},{"Ожидание датчика: ","Waiting for sensor: "},{"Ошибка датчика: ","Sensor error: "},{"Значение в ACK не совпало: ","ACK value mismatch: "},{"Датчик принял: ","Sensor accepted: "},
        {"Неполный ответ AT+READ","Incomplete AT+READ response"},{"Ответ содержит значения вне поддерживаемого диапазона","Readback contains values outside the supported range"},{"Не совпало ","Mismatch for "},{": ожидалось ",": expected "},{", прочитано ",", read "},{"нет в ответе","missing in response"},
        {"Настройки прочитаны из датчика","Parameters read from sensor"},{"Подтверждено чтением настроек из датчика","Confirmed by reading parameters from sensor"},
        {"Демо: синтетические цели","Demo: synthetic targets"},{"Демо выключено","Demo off"},{"Ошибка подключения: ","Connection error: "},{"Ошибка обмена: ","Communication error: "},{"Ошибка сохранения: ","Save error: "},
        {"ожидание первого счётчика","waiting for the first count"},{"последнее число людей: ","last people count: "},{" · счётчик не обновляется"," · count not updated"},{"Ожидание пакетов","Waiting for packets"},{"Нет свежих данных: ","No fresh data: "},{"Людей: ","People: "},{" · координаты"," · coordinates"},
        {"Инициализация датчика · повторяем чтение параметров","Sensor initializing · retrying parameter read"},{"STOP · датчик остановлен","STOP · sensor stopped"},{"START/STOP неизвестно · ","START/STOP unknown · "},{"ДЕМО","DEMO"},{" байт · пакеты "," bytes · packets "},{" · ошибки "," · errors "},
        {"Только счётчик · heartbeat ","Count only · heartbeat "},{" с · координаты не передаются"," s · coordinates are not transmitted"},{" с"," s"}
    };
    static readonly List<KeyValuePair<string,string>> ordered;
    static Language() {ordered=new List<KeyValuePair<string,string>>(words);ordered.Sort((a,b)=>b.Key.Length.CompareTo(a.Key.Length));}
    public static void Set(string code) {Code=code=="en"?"en":"ru";var culture=CultureInfo.GetCultureInfo(Code=="en"?"en-US":"ru-RU");Thread.CurrentThread.CurrentCulture=culture;Thread.CurrentThread.CurrentUICulture=culture;}
    public static string T(string text) {
        if(Code!="en"||string.IsNullOrEmpty(text))return text;string result;if(words.TryGetValue(text,out result))return result;
        result=text;foreach(var pair in ordered)if(pair.Key.Length>1)result=result.Replace(pair.Key,pair.Value);return result;
    }
    static string Original(DependencyObject owner,string property,string text) {var saved=originals.GetOrCreateValue(owner);string source;if(!saved.TryGetValue(property,out source)){source=text;saved[property]=text;}return source;}
    public static void Apply(DependencyObject root) {Apply(root,new HashSet<DependencyObject>());}
    static void Apply(DependencyObject item,HashSet<DependencyObject> seen) {
        if(item==null||!seen.Add(item))return;
        var block=item as TextBlock;if(block!=null&&!System.Windows.Data.BindingOperations.IsDataBound(block,TextBlock.TextProperty))block.Text=T(Original(item,"Text",block.Text));
        var button=item as ButtonBase;if(button!=null&&button.Content is string)button.Content=T(Original(item,"Content",(string)button.Content));
        var menu=item as MenuItem;if(menu!=null&&menu.Header is string)menu.Header=T(Original(item,"Header",(string)menu.Header));
        var window=item as Window;if(window!=null){window.Title=T(Original(item,"Title",window.Title));window.Language=System.Windows.Markup.XmlLanguage.GetLanguage(Code=="en"?"en-US":"ru-RU");}
        var grid=item as DataGrid;if(grid!=null)foreach(var column in grid.Columns)if(column.Header is string)column.Header=T(Original(column,"Header",(string)column.Header));
        foreach(object child in LogicalTreeHelper.GetChildren(item)) {var node=child as DependencyObject;if(node!=null)Apply(node,seen);}
        if(menu!=null)foreach(object child in menu.Items)Apply(child as DependencyObject,seen);
    }
    public static void Verify() {Set("en");if(T("Настройки")!="Settings"||T("Датчик принял: AT+START")!="Sensor accepted: AT+START")throw new Exception("English translation failed");Set("ru");}
}
}
