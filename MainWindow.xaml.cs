using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Painel7X;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _runTimer = new();
    private bool _wDown;
    private int _ms = 320;
    private bool _runEnabled = true;
    private IntPtr _hook;
    private HookProc? _hookProc;
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Painel7X", "settings.json");
    private const int WH_KEYBOARD_LL = 13, WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101, WM_SYSKEYDOWN = 0x0104, WM_SYSKEYUP = 0x0105, VK_W = 0x57;

    [StructLayout(LayoutKind.Sequential)] private struct INPUT { public uint type; public MOUSEINPUT mi; }
    [StructLayout(LayoutKind.Sequential)] private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    private delegate IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr GetModuleHandle(string? lpModuleName);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    public MainWindow()
    {
        InitializeComponent();
        _runTimer.Tick += (_, _) => { if (_wDown && _runEnabled) SendWheelUp(); };
        LoadSettings();
        ShowHome();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _hookProc = HookCallback;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, GetModuleHandle(null), 0);
    }
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vk = Marshal.ReadInt32(lParam);
            int msg = wParam.ToInt32();
            if (vk == VK_W)
            {
                if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                {
                    _wDown = true;
                    Dispatcher.InvokeAsync(() => { if (_runEnabled && !_runTimer.IsEnabled) _runTimer.Start(); });
                }
                if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                {
                    _wDown = false;
                    Dispatcher.InvokeAsync(() => _runTimer.Stop());
                }
            }
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }
    private void SendWheelUp()
    {
        var input = new INPUT[1]; input[0].type = 0; input[0].mi.dwFlags = 0x0800; input[0].mi.mouseData = 120;
        SendInput(1, input, Marshal.SizeOf<INPUT>());
    }
    private void SetMs(int value)
    {
        _ms = Math.Clamp(value, 10, 2000);
        _runTimer.Interval = TimeSpan.FromMilliseconds(_ms);
        SaveSettings();
    }
    private void ShowHome()
    {
        PageTitle.Text = "Visão geral"; PageSubtitle.Text = "Gerencie os recursos do Painel 7X.";
        var panel = Card(); var stack = new StackPanel();
        stack.Children.Add(Header("BEM-VINDO AO 7X", "Painel inspirado na organização visual e na estrutura WPF do projeto de referência."));
        var row = new UniformGrid { Columns = 2, Margin = new Thickness(0, 18, 0, 0) };
        row.Children.Add(StatCard("MACRO DISPONÍVEL", "Correr", "Ativado quando você mantém W pressionado"));
        row.Children.Add(StatCard("INTERVALO ATUAL", $"{_ms} MS", "Ajustável na aba Macro de correr"));
        stack.Children.Add(row);
        var note = new TextBlock { Text = "Os módulos Deagle, Lite Foot e Recoil aparecem na navegação, mas não foram ativados nesta primeira versão.", Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(2, 20, 2, 0) };
        stack.Children.Add(note); panel.Child = stack; PageHost.Content = panel;
    }
    private void ShowRun()
    {
        PageTitle.Text = "Macro de correr"; PageSubtitle.Text = "Repete o scroll para cima enquanto W estiver pressionado.";
        var panel = Card(); var s = new StackPanel(); s.Children.Add(Header("CORRER", "Mantenha W pressionado no jogo. O painel envia scroll para cima no intervalo escolhido."));
        var status = new TextBlock { Text = _runEnabled ? "●  MACRO ATIVADO" : "●  MACRO PAUSADO", Foreground = _runEnabled ? Brushes.LightGreen : Brushes.Orange, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 22, 0, 12) }; s.Children.Add(status);
        var msLabel = new TextBlock { Text = $"Intervalo: {_ms} ms", Foreground = (Brush)FindResource("TextBrush"), FontSize = 16, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 8) }; s.Children.Add(msLabel);
        var slider = new Slider { Minimum = 10, Maximum = 1000, Value = _ms, TickFrequency = 10, IsSnapToTickEnabled = false, Margin = new Thickness(0, 0, 0, 8) }; slider.ValueChanged += (_, _) => { SetMs((int)slider.Value); msLabel.Text = $"Intervalo: {_ms} ms"; }; s.Children.Add(slider);
        var presets = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 16) };
        foreach (var p in new[] { 320, 250, 180, 100 }) { var b = new Button { Content = $"{p} MS", Margin = new Thickness(0, 0, 8, 0), Padding = new Thickness(12, 8, 12, 8) }; b.Click += (_, _) => { slider.Value = p; }; presets.Children.Add(b); }
        s.Children.Add(presets);
        var toggle = new Button { Content = _runEnabled ? "PAUSAR MACRO" : "ATIVAR MACRO", HorizontalAlignment = HorizontalAlignment.Left, MinWidth = 170 }; toggle.Click += (_, _) => { _runEnabled = !_runEnabled; _runTimer.IsEnabled = _runEnabled && _wDown; status.Text = _runEnabled ? "●  MACRO ATIVADO" : "●  MACRO PAUSADO"; status.Foreground = _runEnabled ? Brushes.LightGreen : Brushes.Orange; toggle.Content = _runEnabled ? "PAUSAR MACRO" : "ATIVAR MACRO"; if (!_runEnabled) _runTimer.Stop(); else if (_wDown) _runTimer.Start(); SaveSettings(); }; s.Children.Add(toggle);
        s.Children.Add(new TextBlock { Text = "Como funciona: pressione e segure W para iniciar os envios de scroll para cima; solte W para interromper. O valor em MS controla o intervalo entre os envios.", Foreground = (Brush)FindResource("MutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 0) });
        panel.Child = s; PageHost.Content = panel;
    }
    private void ShowSettings()
    {
        PageTitle.Text = "Configurações"; PageSubtitle.Text = "Preferências locais do aplicativo.";
        var panel = Card(); var s = new StackPanel(); s.Children.Add(Header("CONFIGURAÇÕES", "As preferências são salvas no seu perfil do Windows."));
        var b = new Button { Content = "SALVAR CONFIGURAÇÕES", HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 20, 0, 0) }; b.Click += (_, _) => { SaveSettings(); MessageBox.Show("Configurações salvas.", "Painel 7X", MessageBoxButton.OK, MessageBoxImage.Information); }; s.Children.Add(b);
        s.Children.Add(new TextBlock { Text = "Pasta de configurações: %LOCALAPPDATA%\\Painel7X", Foreground = (Brush)FindResource("MutedBrush"), Margin = new Thickness(0, 14, 0, 0) }); panel.Child = s; PageHost.Content = panel;
    }
    private Border Card() => new() { Background = (Brush)FindResource("PanelBrush"), BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(13), Padding = new Thickness(22) };
    private UIElement Header(string title, string subtitle) { var s = new StackPanel(); s.Children.Add(new TextBlock { Text = title, Foreground = (Brush)FindResource("GoldBrush"), FontSize = 11, FontWeight = FontWeights.Bold }); s.Children.Add(new TextBlock { Text = subtitle, Foreground = (Brush)FindResource("TextBrush"), FontSize = 18, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) }); return s; }
    private UIElement StatCard(string title, string value, string desc) { var b = new Border { Background = (Brush)FindResource("Panel2Brush"), BorderBrush = (Brush)FindResource("LineBrush"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(15), Margin = new Thickness(0, 0, 10, 0) }; var s = new StackPanel(); s.Children.Add(new TextBlock { Text = title, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 10, FontWeight = FontWeights.Bold }); s.Children.Add(new TextBlock { Text = value, Foreground = (Brush)FindResource("GoldBrush"), FontSize = 22, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 3) }); s.Children.Add(new TextBlock { Text = desc, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 11, TextWrapping = TextWrapping.Wrap }); b.Child = s; return b; }
    private void LoadSettings() { try { if (File.Exists(_settingsPath)) { var x = JsonSerializer.Deserialize<Settings>(File.ReadAllText(_settingsPath)); if (x != null) { _ms = Math.Clamp(x.Ms, 10, 2000); _runEnabled = x.RunEnabled; } } } catch { } _runTimer.Interval = TimeSpan.FromMilliseconds(_ms); }
    private void SaveSettings() { try { Directory.CreateDirectory(Path.GetDirectoryName(_settingsPath)!); File.WriteAllText(_settingsPath, JsonSerializer.Serialize(new Settings { Ms = _ms, RunEnabled = _runEnabled }, new JsonSerializerOptions { WriteIndented = true })); } catch { } }
    private sealed class Settings { public int Ms { get; set; } = 320; public bool RunEnabled { get; set; } = true; }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowHome();
    private void Run_Click(object sender, RoutedEventArgs e) => ShowRun();
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowSettings();
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
    private void TitleBar_Down(object sender, MouseButtonEventArgs e) { if (e.LeftButton == MouseButtonState.Pressed) DragMove(); }
    private void Window_Closing(object? sender, CancelEventArgs e) { _runTimer.Stop(); if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook); SaveSettings(); }
}
