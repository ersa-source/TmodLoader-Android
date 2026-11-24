using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Provider;
using Android.Views;
using Android.Widget;
using Tml.Mobile;
using Path = System.IO.Path;
using AColor = Android.Graphics.Color;

namespace Tml.Mobile.Android;

public sealed partial class MainActivity
{
    private ISharedPreferences Preferences => GetSharedPreferences("launcher", FileCreationMode.Private)!;
    private LauncherTheme theme = LauncherTheme.Presets[0];
    private readonly List<View> actions = [];
    private int selectedPage;
    private string pendingReport = "", exportReport = "", notice = "";
    private bool launcherReady;
    private bool compactLayout;
    private string ModDirectory => LocalModImport.DirectoryFor(FilesDir!.AbsolutePath);
    private sealed class PaletteAdapter(Context context, string[] values, AColor foreground, AColor background)
        : ArrayAdapter<string>(context, global::Android.Resource.Layout.SimpleSpinnerDropDownItem, values)
    {
        private View Style(View view)
        {
            view.SetBackgroundColor(background);
            if (view is TextView text) { text.SetTextColor(foreground); text.TextSize = 14; }
            return view;
        }
        public override View GetView(int position, View? convertView, ViewGroup parent) => Style(base.GetView(position, convertView, parent));
        public override View GetDropDownView(int position, View? convertView, ViewGroup? parent) => Style(base.GetDropDownView(position, convertView, parent)!);
    }
    private int Dp(float value) => (int)(value * Resources!.DisplayMetrics!.Density + .5f);
    private AColor Color(string value) => AColor.ParseColor(value);
    private GradientDrawable Background(string value, int radius = 16)
    {
        var background = new GradientDrawable(); background.SetColor(Color(value)); background.SetCornerRadius(Dp(radius)); return background;
    }
    private TextView Text(string value, int size, bool muted = false)
    {
        var view = new TextView(this) { Text = value, TextSize = size };
        view.SetTextColor(Color(muted ? theme.Muted : theme.Text));
        view.SetPadding(0, Dp(3), 0, Dp(7)); return view;
    }
    private Button Action(string label, Action action, bool primary = false, string? id = null)
    {
        var button = new Button(this) { Text = label, TextSize = 14, ContentDescription = id ?? label };
        button.SetAllCaps(false); button.SetTextColor(Color(primary ? theme.Background : theme.Text));
        button.Background = Background(primary ? theme.Accent : theme.Background, 12);
        button.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8));
        button.Click += (_, _) => { if (!busy) action(); }; actions.Add(button); return button;
    }
    private void AddAction(LinearLayout parent, Button button)
    {
        parent.AddView(button, new LinearLayout.LayoutParams(-1, Dp(52)) { TopMargin = Dp(8) });
    }
    private LinearLayout Panel(LinearLayout parent, string title, string? description = null)
    {
        var panel = new LinearLayout(this) { Orientation = Orientation.Vertical };
        panel.SetPadding(Dp(20), Dp(16), Dp(20), Dp(20)); panel.Background = Background(theme.Surface);
        parent.AddView(panel, new LinearLayout.LayoutParams(-1, -2) { BottomMargin = Dp(14) });
        if (title.Length > 0) { var heading = Text(title, 20); heading.SetTypeface(Typeface.Default, TypefaceStyle.Bold); panel.AddView(heading); }
        if (description != null) panel.AddView(Text(description, 13, true)); return panel;
    }
    private void Navigate(int page) { selectedPage = page; notice = ""; BuildLauncher(); }
    private void Pick(int request, bool multiple = false)
    {
        var intent = new Intent(Intent.ActionOpenDocument); intent.AddCategory(Intent.CategoryOpenable); intent.SetType("*/*");
        intent.PutExtra(Intent.ExtraAllowMultiple, multiple); StartActivityForResult(intent, request);
    }
    private LauncherTheme? CustomTheme()
    {
        string path = Path.Combine(FilesDir!.AbsolutePath, "launcher-theme.json");
        if (!File.Exists(path)) return null;
        try { return LauncherTheme.Parse(File.ReadAllText(path)); }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException or InvalidDataException) { return null; }
    }
    protected override void OnResume()
    {
        base.OnResume(); if (launcherReady && !busy) BuildLauncher();
    }
    public override void OnBackPressed()
    {
        if (busy) return;
        if (selectedPage != 0) Navigate(0); else base.OnBackPressed();
    }
    private void BuildLauncher()
    {
        actions.Clear();
        var custom = CustomTheme();
        var themes = custom == null ? LauncherTheme.Presets : LauncherTheme.Presets.Append(custom).ToArray();
        theme = themes.FirstOrDefault(t => t.Name == Preferences.GetString("theme", "tModLoader")) ?? themes[0];
        bool compact = Resources!.Configuration!.ScreenWidthDp < 700;
        compactLayout = compact;
        var shell = new LinearLayout(this) { Orientation = compact ? Orientation.Vertical : Orientation.Horizontal };
        shell.SetBackgroundColor(Color(theme.Background));
        var rail = new LinearLayout(this) { Orientation = compact ? Orientation.Horizontal : Orientation.Vertical };
        rail.SetPadding(Dp(12), Dp(8), Dp(12), Dp(8)); rail.Background = Background(theme.Surface, 0);
        if (compact) shell.AddView(rail, new LinearLayout.LayoutParams(-1, -2));
        else
        {
            var navigationScroll = new ScrollView(this); navigationScroll.SetBackgroundColor(Color(theme.Surface)); navigationScroll.AddView(rail);
            shell.AddView(navigationScroll, new LinearLayout.LayoutParams(Dp(174), -1));
        }
        if (!compact)
        {
            var icon = new ImageView(this); icon.SetImageResource(global::Tml.Game.Android.Resource.Drawable.tml_icon);
            rail.AddView(icon, new LinearLayout.LayoutParams(Dp(40), Dp(40)) { Gravity = GravityFlags.CenterHorizontal });
            var brand = Text("tModLoader", 17); brand.Gravity = GravityFlags.Center; rail.AddView(brand);
            var version = Text("ANDROID · 0.2.18", 10, true); version.Gravity = GravityFlags.Center; rail.AddView(version);
        }
        string[] labels = ["Главная", "Моды", "Файлы", "Настройки", "Диагностика"];
        string[] ids = ["launcher-home", "launcher-mods", "launcher-files", "launcher-settings", "launcher-diagnostics"];
        for (int i = 0; i < labels.Length; i++)
        {
            int page = i; var button = Action(labels[i], () => Navigate(page), selectedPage == i, ids[i]);
            var lp = compact ? new LinearLayout.LayoutParams(0, Dp(52), 1) : new LinearLayout.LayoutParams(-1, Dp(44)) { TopMargin = Dp(4) };
            if (compact) button.TextSize = 11; rail.AddView(button, lp);
        }
        var scroll = new ScrollView(this) { FillViewport = true };
        shell.AddView(scroll, compact ? new LinearLayout.LayoutParams(-1, 0, 1) : new LinearLayout.LayoutParams(0, -1, 1));
        var content = new LinearLayout(this) { Orientation = Orientation.Vertical };
        content.SetPadding(Dp(24), Dp(18), Dp(24), Dp(24)); scroll.AddView(content);
        if (selectedPage != 0)
        {
            var heading = Text(selectedPage == 5 ? "Полный журнал" : labels[selectedPage], 28);
            heading.SetTypeface(Typeface.Default, TypefaceStyle.Bold); content.AddView(heading);
        }
        switch (selectedPage)
        {
            case 0: BuildHome(content); break;
            case 1: BuildMods(content); break;
            case 2: BuildFiles(content); break;
            case 3: BuildSettings(content, themes); break;
            case 4: BuildDiagnostics(content); break;
            case 5:
                AddAction(content, Action("Назад к сводке", () => Navigate(4)));
                var raw = Text(pendingReport, 12); raw.Typeface = Typeface.Monospace; raw.SetTextIsSelectable(true); content.AddView(raw); break;
        }
        status = Text(notice, 13, true); status.ContentDescription = "launcher-status"; content.AddView(status);
        SetContentView(shell); launcherReady = true;
    }
    private void BuildHome(LinearLayout page)
    {
        bool installed = File.Exists(Path.Combine(FilesDir!.AbsolutePath, "Terraria/Terraria.exe"));
        var hero = Panel(page, compactLayout ? "tModLoader · Android" : "Ваше следующее приключение", compactLayout ? null : "Terraria на Android — играйте по-своему.");
        hero.AddView(Text(installed ? "● Файлы готовы" : "○ Импортируйте файлы для начала", 14));
        try { hero.AddView(Text($"{LocalModLibrary.Enabled(ModDirectory).Count} " + "мод(ов) выбрано", 14, true)); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { hero.AddView(Text("Не удалось прочитать список модов: " + e.Message, 13)); }
        play = Action("Запустить tModLoader", () => _ = PlayAsync(), true, "launcher-play"); AddAction(hero, play);
        if (!installed) AddAction(hero, Action("Подготовить файлы", () => Navigate(2)));
        var clean = Panel(page, "Играть без модов", "Отключает выбранные моды, сохраняя их файлы. Выбор можно восстановить в библиотеке модов.");
        AddAction(clean, Action("Запустить без модов", () =>
        {
            try { LocalModLibrary.DisableAll(ModDirectory); _ = PlayAsync(); }
            catch (Exception e) { SetBusy(false, "Не удалось отключить моды: " + e.Message); }
        }, id: "launcher-play-clean"));
    }
    private void BuildMods(LinearLayout page)
    {
        var tools = Panel(page, "Локальная библиотека", "Включайте и отключайте моды до запуска. tModLoader проверяет зависимости и совместимость.");
        AddAction(tools, Action("Добавить моды (.tmod)", () => Pick(11, true), true));
        AddAction(tools, Action("Отключить все", () => ChangeMods(() => LocalModLibrary.DisableAll(ModDirectory)), id: "launcher-disable-all"));
        if (LocalModLibrary.CanRestore(ModDirectory)) AddAction(tools, Action("Восстановить предыдущий выбор", () => ChangeMods(() => LocalModLibrary.Restore(ModDirectory))));
        HashSet<string> enabled;
        try { enabled = LocalModLibrary.Enabled(ModDirectory); }
        catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException) { Panel(page, "Не удалось прочитать выбор", e.Message); return; }
        var groups = LocalModLibrary.Scan(ModDirectory).GroupBy(m => m.Name).ToArray();
        if (groups.Length == 0) Panel(page, "Ваша библиотека пуста", "Импортируйте файл .tmod с телефона.");
        foreach (var group in groups)
        {
            var mod = group.FirstOrDefault(m => m.Target == "9999.0") ?? group.First();
            var card = Panel(page, mod.Name, mod.Error == null ? $"v{mod.Version}  ·  tML {mod.Target}  ·  {mod.Bytes / 1048576d:F2} MB" : "Некорректный пакет: " + mod.Error);
            card.AddView(Text(mod.FileName + (group.Count() > 1 ? $"  (+{group.Count() - 1})" : ""), 12, true));
            if (mod.Error != null) continue;
            var toggle = new Switch(this) { Text = "Включён", Checked = enabled.Contains(mod.Name), ContentDescription = "mod-toggle-" + mod.Name };
            toggle.SetTextColor(Color(theme.Text)); actions.Add(toggle); card.AddView(toggle);
            toggle.CheckedChange += (_, e) => ChangeMods(() => { var names = LocalModLibrary.Enabled(ModDirectory); if (e.IsChecked) names.Add(mod.Name); else names.Remove(mod.Name); LocalModLibrary.Save(ModDirectory, names); });
        }
    }
    private void ChangeMods(Action change)
    {
        try { change(); BuildLauncher(); }
        catch (Exception e) { BuildLauncher(); SetBusy(false, "Не удалось сохранить: " + e.Message); }
    }
    private void BuildFiles(LinearLayout page)
    {
        bool installed = File.Exists(Path.Combine(FilesDir!.AbsolutePath, "Terraria/Terraria.exe"));
        var files = Panel(page, "Terraria PC 1.4.5.8", installed ? "Файлы импортированы. Всё готово к запуску." : "Выберите полный ZIP вашей копии Terraria для ПК. Необходимые файлы будут проверены.");
        import = Action(installed ? "Обновить Terraria.zip" : "Добавить Terraria.zip", () => Pick(10), true); AddAction(files, import);
        Panel(page, "Ваши данные", "Импорт сохраняет моды, персонажей и миры. Обновляйте приложение без удаления, чтобы сохранить данные.");
    }
    private void Choice(LinearLayout panel, string title, string[] values, int selected, Action<int> changed, string id)
    {
        panel.AddView(Text(title, 14)); var picker = new Spinner(this) { ContentDescription = id };
        picker.Adapter = new PaletteAdapter(this, values, Color(theme.Text), Color(theme.Background));
        picker.BackgroundTintList = global::Android.Content.Res.ColorStateList.ValueOf(Color(theme.Accent)); picker.SetSelection(selected); int current = selected;
        picker.ItemSelected += (_, e) => { if (busy || current == e.Position) return; current = e.Position; changed(e.Position); };
        panel.AddView(picker, new LinearLayout.LayoutParams(-1, Dp(52))); actions.Add(picker);
    }
    private void BuildSettings(LinearLayout page, LauncherTheme[] themes)
    {
        var appearance = Panel(page, "Внешний вид", "Темы Calamity и Thorium для лаунчера.");
        Choice(appearance, "Тема", themes.Select(t => t.Name).ToArray(), Array.IndexOf(themes, theme), i => { Preferences.Edit()!.PutString("theme", themes[i].Name)!.Apply(); BuildLauncher(); }, "launcher-theme");
        AddAction(appearance, Action("Импортировать тему (.json)", () => Pick(12)));
        var graphics = Panel(page, "Графика", "OpenGL ES — проверенный вариант по умолчанию. Vulkan экспериментальный и может не работать на этом устройстве.");
        Choice(graphics, "Рендерер", ["OpenGL ES · по умолчанию", "Vulkan · экспериментальный"], Preferences.GetString("renderer", "OpenGL") == "SDLGPU" ? 1 : 0, i => Preferences.Edit()!.PutString("renderer", i == 0 ? "OpenGL" : "SDLGPU")!.Apply(), "Renderizador");
        var game = Panel(page, "Во время игры");
        var awake = new Switch(this) { Text = "Не выключать экран", Checked = Preferences.GetBoolean("keep-screen-on", true) };
        awake.SetTextColor(Color(theme.Text)); awake.CheckedChange += (_, e) => Preferences.Edit()!.PutBoolean("keep-screen-on", e.IsChecked)!.Apply(); game.AddView(awake); actions.Add(awake);
        game.AddView(Text("Настройте положение, размер и действия сенсорных кнопок в настройках игрового мира.", 13, true));
    }
    private void BuildDiagnostics(LinearLayout page)
    {
        pendingReport = HostDiagnostics.ReadReport(this); var report = DiagnosticReport.Parse(pendingReport);
        var summary = Panel(page, "Последний сеанс", !report.HasSession ? "Сеансов пока нет." : report.Errors.Length > 0 ? "В этом сеансе зарегистрированы ошибки." : report.NormalExit ? "Игра завершилась нормально." : "Нет подтверждения нормального завершения. Проверьте историю.");
        foreach (string line in report.Environment) summary.AddView(Text(line, 12, true));
        var errors = Panel(page, "Ошибки сеанса");
        if (report.Errors.Length == 0) errors.AddView(Text("В доступном фрагменте критических ошибок не обнаружено.", 13, true));
        foreach (var issue in report.Errors) errors.AddView(Text((issue.Code == "native" ? "Ошибка нативного кода" : issue.Code == "game" ? "Ошибка игры или мода" : "Ошибка выполнения") + $" (×{issue.Count})\n{issue.Detail}", 13));
        var warnings = Panel(page, "Предупреждения", "Перехваченные исключения могут обрабатываться игрой и сами по себе не означают сбой.");
        if (report.Warnings.Length == 0) warnings.AddView(Text("Предупреждений не обнаружено.", 13, true));
        foreach (var issue in report.Warnings) warnings.AddView(Text($"×{issue.Count}  {issue.Detail}", 12, true));
        var history = Panel(page, "История Android", "Предыдущие записи. Сигнал 9 означает принудительное завершение, но не устанавливает причину.");
        if (report.History.Length == 0) history.AddView(Text("История недоступна.", 12, true));
        foreach (string block in report.History.Split("Android:", StringSplitOptions.RemoveEmptyEntries).Take(4))
        {
            string[] lines = block.Trim().Split('\n');
            string reason = lines.FirstOrDefault(l => l.StartsWith("process=", StringComparison.Ordinal)) ?? "";
            string explanation = reason.Contains("reason=CrashNative(") ? "Сбой нативного кода" :
                reason.Contains("reason=LowMemory(") ? "Android сообщил о нехватке памяти" :
                reason.Contains("reason=PackageUpdated(") ? "Приложение обновлено" :
                reason.Contains("reason=Signaled(") ? "Завершено по сигналу; причина неизвестна" :
                reason.Contains("reason=UserRequested(") ? "Завершение по запросу Android" :
                "Завершение зарегистрировано Android";
            history.AddView(Text(explanation, 14)); history.AddView(Text(lines[0] + "\n" + reason, 11, true));
        }
        var tools = Panel(page, "Журнал и инструменты");
        AddAction(tools, Action("Сохранить диагностику (.txt)", ExportReport, true, "launcher-export-report"));
        AddAction(tools, Action("Посмотреть полный журнал", () => Navigate(5)));
        AddAction(tools, Action("Копировать журнал", () => { ((ClipboardManager)GetSystemService(ClipboardService)!).PrimaryClip = ClipData.NewPlainText("tML Android", pendingReport); status!.Text = "Журнал скопирован."; }));
        AddAction(tools, Action("Проверить графику и управление", () => Launch("probe"), id: "launcher-probe"));
    }
    private void ShowReport() => Navigate(4);
    private void ExportReport()
    {
        exportReport = pendingReport;
        var intent = new Intent(Intent.ActionCreateDocument); intent.AddCategory(Intent.CategoryOpenable); intent.SetType("text/plain");
        intent.PutExtra(Intent.ExtraTitle, "tML-Android-diagnostico-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt"); StartActivityForResult(intent, 13);
    }
    private string DisplayName(global::Android.Net.Uri uri)
    {
        using var cursor = ContentResolver!.Query(uri, new[] { OpenableColumns.DisplayName }, null, null, null);
        return cursor != null && cursor.MoveToFirst() ? cursor.GetString(0) ?? "" : "";
    }
    private async Task ImportModsAsync(Intent data)
    {
        var uris = new List<global::Android.Net.Uri>();
        if (data.ClipData != null) for (int i = 0; i < data.ClipData.ItemCount; i++) { var uri = data.ClipData.GetItemAt(i)?.Uri; if (uri != null) uris.Add(uri); }
        else if (data.Data != null) uris.Add(data.Data);
        if (uris.Count == 0) return;
        SetBusy(true, "Импорт модов…"); int imported = 0; var errors = new List<string>();
        foreach (var uri in uris)
        {
            string name = "Файл";
            try { name = DisplayName(uri); using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Файл недоступен."); await LocalModImport.ImportAsync(input, name, ModDirectory); imported++; }
            catch (Exception e) { errors.Add(name + ": " + e.Message); }
        }
        selectedPage = 1; BuildLauncher(); SetBusy(false, $"{imported} " + "мод(ов) импортировано." + (errors.Count > 0 ? " " + string.Join("; ", errors) : ""));
    }
    private async Task ImportThemeAsync(global::Android.Net.Uri uri)
    {
        SetBusy(true, "Импорт темы…");
        try
        {
            using var input = ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Файл недоступен.");
            using var buffer = new MemoryStream(); byte[] chunk = new byte[4096]; int n;
            while ((n = await input.ReadAsync(chunk)) > 0) { if (buffer.Length + n > 65536) throw new InvalidDataException("Размер темы превышает 64 КиБ."); buffer.Write(chunk, 0, n); }
            string json = System.Text.Encoding.UTF8.GetString(buffer.ToArray()); var parsed = LauncherTheme.Parse(json);
            if (LauncherTheme.Presets.Any(t => t.Name.Equals(parsed.Name, StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Выберите имя, отличное от встроенных тем.");
            string path = Path.Combine(FilesDir!.AbsolutePath, "launcher-theme.json"); File.WriteAllText(path + ".tmp", json); File.Move(path + ".tmp", path, true);
            Preferences.Edit()!.PutString("theme", parsed.Name)!.Apply(); BuildLauncher(); SetBusy(false, "Тема применена: " + parsed.Name);
        }
        catch (Exception e) { SetBusy(false, "Ошибка импорта темы: " + e.Message); }
    }
}
