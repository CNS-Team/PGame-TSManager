using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace PGameTSManager
{
    internal static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            if (args.Any(a => string.Equals(a, "--tool", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = AdminCli.Run(args);
                return;
            }
            if (args.Any(a => string.Equals(a, "--selfcheck", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = SelfCheck.Run();
                return;
            }
            if (args.Any(a => string.Equals(a, "--syncplugins", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = SelfCheck.SyncPlugins();
                return;
            }
            if (args.Any(a => string.Equals(a, "--send", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = Cli.Send(args);
                return;
            }
            if (args.Any(a => string.Equals(a, "--dumpconfig", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = Cli.DumpConfig();
                return;
            }
            var restoreIndex = Array.FindIndex(args, a =>
                string.Equals(a, "--apply-restore", StringComparison.OrdinalIgnoreCase));
            if (restoreIndex >= 0)
            {
                Environment.ExitCode = ManagerSelfUpdate.Run(args[restoreIndex..]);
                return;
            }
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .LogToTrace();
    }

    internal static class UiTheme
    {
        public static readonly FontFamily BodyFont = new("Microsoft YaHei UI,Segoe UI");
        public static readonly FontFamily MonoFont = new("Cascadia Mono,Consolas,Microsoft YaHei UI");

        public static readonly IBrush Bg = Brush.Parse("#0B1016");
        public static readonly IBrush Surface = Brush.Parse("#111923");
        public static readonly IBrush SurfaceRaised = Brush.Parse("#172231");
        public static readonly IBrush SurfaceHover = Brush.Parse("#1E2B3C");
        public static readonly IBrush Border = Brush.Parse("#273446");
        public static readonly IBrush BorderStrong = Brush.Parse("#3B4B60");
        public static readonly IBrush Text = Brush.Parse("#E6EDF3");
        public static readonly IBrush TextDim = Brush.Parse("#8B9AAF");
        public static readonly IBrush TextFaint = Brush.Parse("#637184");
        public static readonly IBrush Accent = Brush.Parse("#24B6A5");
        public static readonly IBrush AccentHover = Brush.Parse("#3AD5C1");
        public static readonly IBrush AccentBlue = Brush.Parse("#4E8DFF");
        public static readonly IBrush Success = Brush.Parse("#46C878");
        public static readonly IBrush Warning = Brush.Parse("#F0B44C");
        public static readonly IBrush Error = Brush.Parse("#EF6464");
        public static readonly IBrush ConsoleBg = Brush.Parse("#080C11");
        public static readonly IBrush LogNormal = Brush.Parse("#D7E0EA");
        public static readonly IBrush LogInfo = Brush.Parse("#65C7E8");
        public static readonly IBrush LogPlugin = Brush.Parse("#C49BFF");
        public static readonly IBrush LogWarn = Brush.Parse("#F0B44C");
        public static readonly IBrush LogError = Brush.Parse("#FF7070");
        public static readonly IBrush LogOk = Brush.Parse("#55D88B");
        public static readonly IBrush LogCmd = Brush.Parse("#70A7FF");

        public static void Apply(Avalonia.Application app)
        {
            app.RequestedThemeVariant = ThemeVariant.Dark;
            app.Styles.Add(new FluentTheme());

            app.Styles.Add(new Style(x => x.OfType<Window>())
            {
                Setters =
                {
                    new Setter(Window.BackgroundProperty, Bg),
                    new Setter(Window.FontFamilyProperty, BodyFont),
                    new Setter(Window.ForegroundProperty, Text)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<TextBlock>())
            {
                Setters =
                {
                    new Setter(TextBlock.ForegroundProperty, Text),
                    new Setter(TextBlock.FontFamilyProperty, BodyFont),
                    new Setter(TextBlock.FontSizeProperty, 12.5)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>())
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfaceRaised),
                    new Setter(Button.ForegroundProperty, Text),
                    new Setter(Button.BorderBrushProperty, Border),
                    new Setter(Button.BorderThicknessProperty, new Thickness(1)),
                    new Setter(Button.CornerRadiusProperty, new CornerRadius(6)),
                    new Setter(Button.PaddingProperty, new Thickness(12, 6)),
                    new Setter(Button.MinHeightProperty, 30.0),
                    new Setter(Button.FontSizeProperty, 12.0),
                    new Setter(Button.FontWeightProperty, FontWeight.SemiBold),
                    new Setter(Button.CursorProperty, new Cursor(Avalonia.Input.StandardCursorType.Hand))
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("primary"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Accent),
                    new Setter(Button.ForegroundProperty, Brush.Parse("#061412")),
                    new Setter(Button.BorderBrushProperty, AccentHover)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("danger"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Brush.Parse("#3A1B22")),
                    new Setter(Button.ForegroundProperty, Brush.Parse("#FFB6B6")),
                    new Setter(Button.BorderBrushProperty, Error)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("ghost"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.BorderBrushProperty, Brushes.Transparent)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("nav"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.ForegroundProperty, TextDim),
                    new Setter(Button.BorderBrushProperty, Brushes.Transparent)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("nav-active"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Brush.Parse("#143C3B")),
                    new Setter(Button.ForegroundProperty, AccentHover),
                    new Setter(Button.BorderBrushProperty, Accent)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("chrome"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Brushes.Transparent),
                    new Setter(Button.ForegroundProperty, TextDim),
                    new Setter(Button.BorderBrushProperty, Brushes.Transparent),
                    new Setter(Button.BorderThicknessProperty, new Thickness(0)),
                    new Setter(Button.CornerRadiusProperty, new CornerRadius(0)),
                    new Setter(Button.PaddingProperty, new Thickness(0)),
                    new Setter(Button.MinWidthProperty, 44.0),
                    new Setter(Button.WidthProperty, 46.0),
                    new Setter(Button.HeightProperty, 36.0),
                    new Setter(Button.MinHeightProperty, 36.0),
                    new Setter(Button.FontSizeProperty, 12.0),
                    new Setter(Button.FontWeightProperty, FontWeight.Normal)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("chrome").Class("close"))
            {
                Setters =
                {
                    new Setter(Button.ForegroundProperty, Text),
                    new Setter(Button.BackgroundProperty, Brushes.Transparent)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("chrome").Class("close").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, Brush.Parse("#C42B1C")),
                    new Setter(Button.ForegroundProperty, Brushes.White)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<Button>().Class("chrome").Class(":pointerover"))
            {
                Setters =
                {
                    new Setter(Button.BackgroundProperty, SurfaceHover),
                    new Setter(Button.ForegroundProperty, Text)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<TextBox>())
            {
                Setters =
                {
                    new Setter(TextBox.BackgroundProperty, Surface),
                    new Setter(TextBox.ForegroundProperty, Text),
                    new Setter(TextBox.BorderBrushProperty, Border),
                    new Setter(TextBox.BorderThicknessProperty, new Thickness(1)),
                    new Setter(TextBox.CornerRadiusProperty, new CornerRadius(6)),
                    new Setter(TextBox.PaddingProperty, new Thickness(9, 6)),
                    new Setter(TextBox.MinHeightProperty, 30.0),
                    new Setter(TextBox.FontSizeProperty, 12.0),
                    new Setter(TextBox.SelectionBrushProperty, Accent),
                    new Setter(TextBox.CaretBrushProperty, AccentHover)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<ComboBox>())
            {
                Setters =
                {
                    new Setter(ComboBox.BackgroundProperty, Surface),
                    new Setter(ComboBox.ForegroundProperty, Text),
                    new Setter(ComboBox.BorderBrushProperty, Border),
                    new Setter(ComboBox.BorderThicknessProperty, new Thickness(1)),
                    new Setter(ComboBox.CornerRadiusProperty, new CornerRadius(6)),
                    new Setter(ComboBox.PaddingProperty, new Thickness(9, 4)),
                    new Setter(ComboBox.MinHeightProperty, 30.0),
                    new Setter(ComboBox.FontSizeProperty, 12.0)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<ListBox>())
            {
                Setters =
                {
                    new Setter(ListBox.BackgroundProperty, Surface),
                    new Setter(ListBox.BorderBrushProperty, Border),
                    new Setter(ListBox.BorderThicknessProperty, new Thickness(1)),
                    new Setter(ListBox.CornerRadiusProperty, new CornerRadius(8))
                }
            });
            app.Styles.Add(new Style(x => x.OfType<ListBoxItem>())
            {
                Setters =
                {
                    new Setter(ListBoxItem.PaddingProperty, new Thickness(8, 5)),
                    new Setter(ListBoxItem.MarginProperty, new Thickness(0, 1)),
                    new Setter(ListBoxItem.CornerRadiusProperty, new CornerRadius(5))
                }
            });
            app.Styles.Add(new Style(x => x.OfType<ListBoxItem>().Class(":selected"))
            {
                Setters =
                {
                    new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent),
                    new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0))
                }
            });
            app.Styles.Add(new Style(x => x.OfType<TabItem>())
            {
                Setters =
                {
                    new Setter(TabItem.ForegroundProperty, TextDim),
                    new Setter(TabItem.FontSizeProperty, 12.0),
                    new Setter(TabItem.PaddingProperty, new Thickness(10, 5))
                }
            });
            app.Styles.Add(new Style(x => x.OfType<CheckBox>())
            {
                Setters =
                {
                    new Setter(CheckBox.ForegroundProperty, Text),
                    new Setter(CheckBox.FontSizeProperty, 12.0)
                }
            });
            app.Styles.Add(new Style(x => x.OfType<ToolTip>())
            {
                Setters =
                {
                    new Setter(ToolTip.BackgroundProperty, SurfaceRaised),
                    new Setter(ToolTip.ForegroundProperty, Text),
                    new Setter(ToolTip.BorderBrushProperty, BorderStrong),
                    new Setter(ToolTip.BorderThicknessProperty, new Thickness(1))
                }
            });
        }
    }

    internal static class Ui
    {
        public static TextBlock T(
            string text,
            double size = 12.5,
            IBrush? color = null,
            FontWeight weight = FontWeight.Normal,
            TextWrapping wrap = TextWrapping.NoWrap,
            TextTrimming? trimming = null)
            => new()
            {
                Text = text,
                FontSize = size,
                Foreground = color ?? UiTheme.Text,
                FontWeight = weight,
                TextWrapping = wrap,
                TextTrimming = trimming ?? TextTrimming.None,
                VerticalAlignment = VerticalAlignment.Center
            };

        public static Button Btn(
            string text,
            Action? click = null,
            string? kind = null,
            double minWidth = 0,
            double height = 30)
        {
            var button = new Button
            {
                Content = text,
                MinWidth = minWidth,
                Height = height,
                HorizontalContentAlignment = HorizontalAlignment.Center
            };
            if (!string.IsNullOrWhiteSpace(kind))
                foreach (var item in kind.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    button.Classes.Add(item);
            if (click != null) button.Click += (_, _) => click();
            return button;
        }

        public static Button IconBtn(string glyph, string tip, Action click)
        {
            var button = Btn(glyph, click, "ghost", 32, 30);
            ToolTip.SetTip(button, tip);
            button.FontSize = 15;
            return button;
        }

        public static TextBox Input(string? text = null, string? watermark = null)
            => new()
            {
                Text = text ?? "",
                Watermark = watermark,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

        public static ComboBox Combo(IEnumerable<object>? items = null)
            => new()
            {
                ItemsSource = items,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

        public static Border Card(Control child, double padding = 12, IBrush? background = null)
            => new()
            {
                Child = child,
                Padding = new Thickness(padding),
                Background = background ?? UiTheme.Surface,
                BorderBrush = UiTheme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };

        public static StackPanel V(params Control[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Vertical };
            foreach (var child in children) panel.Children.Add(child);
            return panel;
        }

        public static StackPanel H(params Control[] children)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            foreach (var child in children) panel.Children.Add(child);
            return panel;
        }

        public static Grid Grid(string rows, string columns)
            => new()
            {
                RowDefinitions = new RowDefinitions(rows),
                ColumnDefinitions = new ColumnDefinitions(columns)
            };

        public static void Place(Control control, int row = 0, int column = 0, int rowSpan = 1, int columnSpan = 1)
        {
            Avalonia.Controls.Grid.SetRow(control, row);
            Avalonia.Controls.Grid.SetColumn(control, column);
            Avalonia.Controls.Grid.SetRowSpan(control, rowSpan);
            Avalonia.Controls.Grid.SetColumnSpan(control, columnSpan);
        }

        public static Window Dialog(
            string title,
            double width,
            double height,
            Control content,
            double minWidth = 0,
            double minHeight = 0)
            => new()
            {
                Title = title,
                Width = width,
                Height = height,
                MinWidth = minWidth > 0 ? minWidth : width,
                MinHeight = minHeight > 0 ? minHeight : height,
                Background = UiTheme.Bg,
                Foreground = UiTheme.Text,
                FontFamily = UiTheme.BodyFont,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ShowInTaskbar = false,
                CanResize = true,
                Icon = App.AppIcon,
                Content = new Grid
                {
                    Margin = new Thickness(12),
                    Children = { Card(content, 16, UiTheme.Surface) }
                }
            };

        public static async Task AlertAsync(
            Window owner,
            string title,
            string message,
            string primary = "确定")
        {
            var dialog = new ChoiceDialog(title, message, primary, "", "");
            await dialog.ShowDialog<ChoiceDialogResult>(owner);
        }

        public static async Task<bool> ConfirmAsync(
            Window owner,
            string title,
            string message,
            string primary = "确定",
            bool danger = false)
        {
            var dialog = new ChoiceDialog(title, message, primary, "", "取消", danger);
            return await dialog.ShowDialog<ChoiceDialogResult>(owner) == ChoiceDialogResult.Primary;
        }

        public static FuncDataTemplate<T> Template<T>(Func<T, Control> build)
            => new((item, _) => item == null ? new Panel() : build(item), true);

        public static async Task CaptureAsync(Window window, string outputPath)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            var width = Math.Max(1, (int)Math.Ceiling(window.ClientSize.Width));
            var height = Math.Max(1, (int)Math.Ceiling(window.ClientSize.Height));
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(window);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? AppContext.BaseDirectory);
            bitmap.Save(outputPath);
        }

        public static void BindText(TextBlock target, string path, BindingMode mode = BindingMode.OneWay)
            => target.Bind(TextBlock.TextProperty, new Binding(path) { Mode = mode });

        public static void BindForeground(Control target, string path)
            => target.Bind(TextBlock.ForegroundProperty, new Binding(path));
    }

    public sealed class App : Avalonia.Application
    {
        private const string SingleInstanceMutexName = @"Local\Pigeon.PGameTSManager.SingleInstance";
        private const string SingleInstanceShowEventName = @"Local\Pigeon.PGameTSManager.ShowWindow";
        private static Mutex? _singleInstanceMutex;
        private static EventWaitHandle? _showWindowEvent;
        private static RegisteredWaitHandle? _showWindowRegistration;
        private static bool _showWindowRequested;
        private static ControlApiServer? _controlApi;
        private static MainWindow? _window;
        private static TrayIcon? _tray;
        private static bool _exiting;

        public static WindowIcon? AppIcon { get; private set; }
        public static bool IsExiting => _exiting;

        public override void Initialize()
        {
            UiTheme.Apply(this);
            try
            {
                AppIcon = new WindowIcon(AssetLoader.Open(
                    new Uri("avares://PGame-TSManager/PGame-TSManager.ico")));
            }
            catch { }

            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                LogCrash("UnhandledException", e.ExceptionObject as Exception);
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                LogCrash("UnobservedTaskException", e.Exception);
                e.SetObserved();
            };
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            {
                base.OnFrameworkInitializationCompleted();
                return;
            }

            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var args = desktop.Args ?? Array.Empty<string>();
            if (args.Any(a => string.Equals(a, "--tool", StringComparison.OrdinalIgnoreCase)))
            {
                desktop.Shutdown(AdminCli.Run(args));
                return;
            }
            if (args.Any(a => string.Equals(a, "--selfcheck", StringComparison.OrdinalIgnoreCase)))
            {
                desktop.Shutdown(SelfCheck.Run());
                return;
            }
            if (args.Any(a => string.Equals(a, "--syncplugins", StringComparison.OrdinalIgnoreCase)))
            {
                desktop.Shutdown(SelfCheck.SyncPlugins());
                return;
            }
            if (args.Any(a => string.Equals(a, "--send", StringComparison.OrdinalIgnoreCase)))
            {
                desktop.Shutdown(Cli.Send(args));
                return;
            }
            if (args.Any(a => string.Equals(a, "--dumpconfig", StringComparison.OrdinalIgnoreCase)))
            {
                desktop.Shutdown(Cli.DumpConfig());
                return;
            }
            if (!EnsureSingleInstance())
            {
                desktop.Shutdown(0);
                return;
            }

            if (args.Any(a => string.Equals(a, "--nowindow", StringComparison.OrdinalIgnoreCase)))
                ManagerConfig.ShowWindowOverride = false;

            MainWindow.StartAllOnLoad =
                args.Any(a => string.Equals(a, "--startall", StringComparison.OrdinalIgnoreCase));

            try
            {
                _window = new MainWindow();
            }
            catch (Exception ex)
            {
                LogCrash("MainWindowCtor", ex);
                desktop.Shutdown(1);
                return;
            }

            desktop.MainWindow = _window;
            SetupTrayIcon(_window);
            desktop.Exit += (_, _) =>
            {
                try { _controlApi?.Stop(); } catch { }
                _controlApi = null;
                DisposeSingleInstance();
            };
            _window.Show();
            var captureArg = args.FirstOrDefault(a =>
                a.StartsWith("--capture=", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(captureArg))
            {
                var capturePath = captureArg["--capture=".Length..].Trim('"');
                DispatcherTimer.RunOnce(() =>
                {
                    _ = CaptureAndExitAsync(capturePath);
                }, TimeSpan.FromSeconds(1.5));
            }
            _controlApi = new ControlApiServer(ManagerConfig.Instance, () => _window);
            _controlApi.Start();
            if (_showWindowRequested)
            {
                _showWindowRequested = false;
                RestoreFromTray();
            }
            base.OnFrameworkInitializationCompleted();
        }

        private static async Task CaptureAndExitAsync(string outputPath)
        {
            try
            {
                if (_window != null) await Ui.CaptureAsync(_window, outputPath);
            }
            catch (Exception ex)
            {
                LogCrash("Capture", ex);
            }
            finally
            {
                ShutdownApplication();
            }
        }

        public static bool IsLightTheme()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return Convert.ToInt32(key?.GetValue("AppsUseLightTheme") ?? 0) == 1;
            }
            catch { return false; }
        }

        private static void LogCrash(string kind, Exception? ex)
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(AppContext.BaseDirectory, "crash.log"),
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {kind}\r\n{ex?.ToString() ?? "(null)"}\r\n\r\n",
                    new UTF8Encoding(false));
            }
            catch { }
        }

        private static void SetupTrayIcon(Window window)
        {
            try
            {
                _tray = new TrayIcon
                {
                    Icon = AppIcon,
                    ToolTipText = "PGame-TSManager",
                    IsVisible = true,
                    Menu = BuildStaticTrayMenu()
                };
                _tray.Clicked += (_, _) => RestoreFromTray();
            }
            catch (Exception ex) { LogCrash("SetupTrayIcon", ex); }
        }

        private static NativeMenu BuildStaticTrayMenu()
        {
            var menu = new NativeMenu();
            var show = new NativeMenuItem("显示管理器");
            show.Click += (_, _) => RestoreFromTray();
            var exit = new NativeMenuItem("退出");
            exit.Click += (_, _) => ExitFromTray();
            menu.Items.Add(show);
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exit);
            return menu;
        }

        public static void RestoreFromTray()
        {
            if (_window == null) return;
            _window.Show();
            if (_window.WindowState == WindowState.Minimized)
                _window.WindowState = WindowState.Normal;
            _window.Activate();
        }

        public static void ShowFromTray() => RestoreFromTray();

        public static void UpdateTrayTip(string text)
        {
            if (_tray == null) return;
            var tip = (text ?? "").Replace("\r", "").Replace("\n", " | ").Trim();
            _tray.ToolTipText = tip.Length > 120 ? tip[..117] + "..." : tip;
        }

        public static void SetTrayMenu(NativeMenu menu)
        {
            if (_tray == null) return;
            menu.Items.Insert(0, new NativeMenuItemSeparator());
            var show = new NativeMenuItem("显示管理器");
            show.Click += (_, _) => RestoreFromTray();
            menu.Items.Insert(0, show);
            var exit = new NativeMenuItem("退出");
            exit.Click += (_, _) => ExitFromTray();
            menu.Items.Add(new NativeMenuItemSeparator());
            menu.Items.Add(exit);
            _tray.Menu = menu;
        }

        public static void ExitFromTray() => ShutdownApplication();

        private static void StopManagedServers()
        {
            if (_window == null) return;
            foreach (var container in _window.Containers.ToList())
            {
                try { container.StopForExit(TimeSpan.FromSeconds(8)); }
                catch { }
            }
        }

        private static void ShutdownApplication()
        {
            if (_exiting) return;
            _exiting = true;
            try
            {
                _window?.PrepareForExit();
                StopManagedServers();
                if (_tray != null)
                {
                    _tray.IsVisible = false;
                    _tray.Dispose();
                    _tray = null;
                }
            }
            catch { }
            DisposeSingleInstance();
            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                desktop.Shutdown(0);
            else
                Environment.Exit(0);
        }

        private static bool EnsureSingleInstance()
        {
            try
            {
                _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
                if (createdNew)
                {
                    _showWindowEvent = new EventWaitHandle(
                        false, EventResetMode.AutoReset, SingleInstanceShowEventName);
                    _showWindowRegistration = ThreadPool.RegisterWaitForSingleObject(
                        _showWindowEvent,
                        (_, timedOut) => { if (!timedOut) RequestShowExistingWindow(); },
                        null,
                        Timeout.Infinite,
                        executeOnlyOnce: false);
                    return true;
                }

                _singleInstanceMutex.Dispose();
                _singleInstanceMutex = null;
                for (var i = 0; i < 20; i++)
                {
                    try
                    {
                        using var showEvent = EventWaitHandle.OpenExisting(SingleInstanceShowEventName);
                        showEvent.Set();
                        return false;
                    }
                    catch (WaitHandleCannotBeOpenedException)
                    {
                        Thread.Sleep(50);
                    }
                    catch { break; }
                }
                return false;
            }
            catch (Exception ex)
            {
                LogCrash("EnsureSingleInstance", ex);
                return true;
            }
        }

        private static void RequestShowExistingWindow()
        {
            Dispatcher.UIThread.Post(() =>
            {
                _showWindowRequested = true;
                if (_window == null) return;
                _showWindowRequested = false;
                RestoreFromTray();
            });
        }

        private static void DisposeSingleInstance()
        {
            try { _showWindowRegistration?.Unregister(null); } catch { }
            _showWindowRegistration = null;
            _showWindowEvent?.Dispose();
            _showWindowEvent = null;
            try { _singleInstanceMutex?.ReleaseMutex(); } catch { }
            _singleInstanceMutex?.Dispose();
            _singleInstanceMutex = null;
        }

    }

    internal enum WorkspacePage
    {
        Console,
        Operations,
        Plugins
    }

    public sealed class MainWindow : Window
    {
        public static bool StartAllOnLoad { get; set; }

        private readonly ManagerConfig _cfg;
        private readonly ListBox _serverList;
        private readonly ListBox _consoleList;
        private readonly TextBox _commandBox;
        private readonly TextBox _serverSearch;
        private readonly ObservableCollection<ServerContainer> _visibleContainers = new();
        private readonly TextBlock _statusText;
        private readonly TextBlock _alertText;
        private readonly TextBlock _serverCountText;
        private readonly TextBlock _summaryName;
        private readonly TextBlock _summarySubtitle;
        private readonly TextBlock _summaryPid;
        private readonly TextBlock _summaryMemory;
        private readonly TextBlock _summaryUptime;
        private readonly TextBlock _summaryPorts;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly Button _restartButton;
        private readonly Button _startAllButton;
        private readonly Button _stopAllButton;
        private readonly Button _settingsButton;
        private readonly Button _folderButton;
        private readonly Button _consoleNav;
        private readonly Button _operationsNav;
        private readonly Button _pluginsNav;
        private readonly Button _manageNav;
        private readonly OperationsCenterView _operationsView;
        private readonly PluginLibraryView _pluginLibraryView;
        private readonly Control _consolePage;
        private readonly Border _consolePanel;
        private readonly Grid _commandBar;
        private readonly ShapePath _maximizeIcon;
        private readonly ShapePath _fullscreenIcon;
        private readonly ConsoleBuffer _systemConsole = new();
        private WindowState _stateBeforeFullscreen = WindowState.Normal;
        private bool _consoleScrollScheduled;
        private DispatcherTimer? _statusTimer;
        private DispatcherTimer? _scheduleTimer;
        private DispatcherTimer? _capacityTimer;
        private DispatcherTimer? _periodicSaveTimer;
        private DispatcherTimer? _housekeepingTimer;
        private readonly Dictionary<string, int> _lastPlayerCount = new();
        private readonly HashSet<int> _reportedConflictPids = new();
        private DateTime _lastConflictScanUtc = DateTime.MinValue;
        private string _lastAlertResult = "";
        private bool _startingAll;
        private bool _allowClose;

        public ObservableCollection<ServerContainer> Containers { get; } = new();
        public ServerContainer? Current =>
            _serverList.SelectedItem as ServerContainer;

        public MainWindow()
        {
            Title = "PGame-TSManager";
            Width = 1260;
            Height = 820;
            MinWidth = 1020;
            MinHeight = 660;
            Background = UiTheme.Bg;
            Foreground = UiTheme.Text;
            FontFamily = UiTheme.BodyFont;
            Icon = App.AppIcon;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaChromeHints = ExtendClientAreaChromeHints.NoChrome;
            ExtendClientAreaTitleBarHeightHint = 0;
            _cfg = ManagerConfig.Instance;
            _cfg.MakeDirectories();

            _serverList = new ListBox
            {
                Name = "ServerList",
                ItemsSource = _visibleContainers,
                ItemTemplate = Ui.Template<ServerContainer>(ServerSidebarItem),
                ItemContainerTheme = BuildSidebarItemTheme(),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(4)
            };
            _serverList.SelectionChanged += ServerListSelectionChanged;

            _consoleList = new ListBox
            {
                ItemsSource = _systemConsole,
                ItemTemplate = Ui.Template<ConsoleEntry>(line =>
                {
                    var text = new TextBlock
                    {
                        FontSize = 12.0,
                        Foreground = line.Brush,
                        FontWeight = FontWeight.Normal,
                        TextWrapping = TextWrapping.NoWrap
                    };
                    text.FontFamily = UiTheme.MonoFont;
                    foreach (var run in line.Runs)
                        text.Inlines?.Add(new Run(run.Text) { Foreground = run.Brush });
                    if (text.Inlines == null || text.Inlines.Count == 0)
                        text.Text = line.Text;
                    return text;
                }),
                Background = UiTheme.ConsoleBg,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(8)
            };

            _commandBox = Ui.Input(watermark: "输入指令，例如 /help、/save、/hr help");
            _commandBox.KeyDown += CommandBoxKeyDown;
            _commandBox.TextChanged += (_, _) =>
                _commandBox.Watermark = string.IsNullOrEmpty(_commandBox.Text)
                    ? "输入指令，例如 /help、/save、/hr help"
                    : null;

            _statusText = Ui.T("准备就绪", 11, UiTheme.TextDim);
            _alertText = Ui.T("", 11, UiTheme.TextFaint);
            _alertText.HorizontalAlignment = HorizontalAlignment.Right;
            _serverCountText = Ui.T("0", 10.5, UiTheme.TextFaint);
            _serverSearch = Ui.Input(watermark: "搜索服务器");
            _serverSearch.TextChanged += (_, _) => ApplyServerFilter(Current?.StableId);
            _summaryName = Ui.T("服务器工作区", 20, UiTheme.Text, FontWeight.SemiBold);
            _summarySubtitle = Ui.T("尚未选择服务器", 11, UiTheme.TextDim);
            _summaryPid = Ui.T("PID  —", 11, UiTheme.TextDim);
            _summaryMemory = Ui.T("内存  —", 11, UiTheme.LogInfo);
            _summaryUptime = Ui.T("运行  —", 11, UiTheme.TextDim);
            _summaryPorts = Ui.T("端口  —", 11, UiTheme.TextDim);

            _startButton = Ui.Btn("启动", StartCurrent, "primary", 64, 30);
            _stopButton = Ui.Btn("停止", StopCurrent, "danger", 64, 30);
            _restartButton = Ui.Btn("重启", () => _ = RestartCurrentAsync(), null, 64, 30);
            _startAllButton = Ui.Btn("全部启动", () => _ = StartAllSequentialAsync(), null, 82, 30);
            _stopAllButton = Ui.Btn("全部停止", () => _ = StopAllAsync(), "danger", 82, 30);
            _settingsButton = Ui.Btn("设置", OpenServerSettings, null, 58, 30);
            _folderButton = Ui.Btn("目录", OpenCurrentFolder, null, 58, 30);
            _consoleNav = Ui.Btn("控制台", () => ShowWorkspace(false), "nav nav-active", 72, 30);
            _operationsNav = Ui.Btn("运维中心", () => ShowWorkspace(true), "nav", 88, 30);
            _pluginsNav = Ui.Btn("插件库", () => ShowPage(WorkspacePage.Plugins), "nav", 72, 30);
            _manageNav = Ui.Btn("管理", null, "nav", 64, 30);
            _manageNav.Flyout = BuildManagementFlyout();
            _maximizeIcon = new ShapePath
            {
                Width = 12,
                Height = 12,
                Data = StreamGeometry.Parse("M 1,1 L 11,1 L 11,11 L 1,11 Z"),
                Stroke = UiTheme.TextDim,
                StrokeThickness = 1.1,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            _fullscreenIcon = new ShapePath
            {
                Width = 13,
                Height = 13,
                Data = FullscreenIconData(false),
                Stroke = UiTheme.TextDim,
                StrokeThickness = 1.1,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            _operationsView = new OperationsCenterView { IsVisible = false };
            _pluginLibraryView = new PluginLibraryView(
                _cfg,
                container => OpenPluginsFor(container),
                text => AppendLine("[插件库] " + text))
            {
                IsVisible = false
            };
            _consolePanel = new Border
            {
                Child = _consoleList,
                Background = UiTheme.ConsoleBg,
                BorderBrush = UiTheme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8)
            };
            _commandBar = BuildCommandBar();
            _consolePage = BuildConsolePage();

            Content = BuildLayout();
            UpdateChromeIcons();
            Opened += OnOpened;
            Closing += OnClosing;
            KeyDown += (_, e) =>
            {
                if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
                {
                    ToggleFullscreen();
                    e.Handled = true;
                }
            };
            PropertyChanged += (_, e) =>
            {
                if (e.Property == WindowStateProperty) UpdateChromeIcons();
            };
            Containers.CollectionChanged += ContainersChanged;
        }

        public ServerContainer? FindContainer(string id)
            => Containers.FirstOrDefault(c =>
                string.Equals(c.StableId, id, StringComparison.OrdinalIgnoreCase));

        public void PrepareForExit()
        {
            _allowClose = true;
            _statusTimer?.Stop();
            _scheduleTimer?.Stop();
            _capacityTimer?.Stop();
            _periodicSaveTimer?.Stop();
            _housekeepingTimer?.Stop();
        }

        private Control BuildLayout()
        {
            var root = Ui.Grid("36,64,*,36", "*");
            root.Background = UiTheme.Bg;

            var titleBar = BuildWindowTitleBar();
            Ui.Place(titleBar, 0);
            root.Children.Add(titleBar);

            var header = BuildHeader();
            Ui.Place(header, 1);
            root.Children.Add(header);

            var body = Ui.Grid("*", "260,*");
            body.Margin = new Thickness(12, 0, 12, 8);
            var sidebar = BuildSidebar();
            Ui.Place(sidebar, 0, 0);
            body.Children.Add(sidebar);

            var workspace = Ui.Grid("*,Auto", "*");
            workspace.Margin = new Thickness(12, 0, 0, 0);
            var pages = new Grid { Children = { _consolePage, _operationsView, _pluginLibraryView } };
            Ui.Place(pages, 0);
            workspace.Children.Add(pages);
            Ui.Place(_commandBar, 1);
            workspace.Children.Add(_commandBar);
            Ui.Place(workspace, 0, 1);
            body.Children.Add(workspace);

            Ui.Place(body, 2);
            root.Children.Add(body);

            var status = Ui.Grid("*", "*,*,Auto");
            status.Margin = new Thickness(14, 4, 14, 0);
            Ui.Place(_statusText, 0, 0);
            Ui.Place(_alertText, 0, 1);
            var brand = Ui.T("团队：流光工作室  ·  作者：cc004、Pigeon-X  ·  1.2", 10, UiTheme.TextFaint);
            brand.HorizontalAlignment = HorizontalAlignment.Right;
            Ui.Place(brand, 0, 2);
            status.Children.Add(_statusText);
            status.Children.Add(_alertText);
            status.Children.Add(brand);
            Ui.Place(status, 3);
            root.Children.Add(status);
            return root;
        }

        private Control BuildConsolePage()
        {
            var left = Ui.V(_summaryName, _summarySubtitle);
            left.Spacing = 4;
            left.VerticalAlignment = VerticalAlignment.Center;

            var metrics = Ui.H(_summaryPid, _summaryMemory, _summaryUptime, _summaryPorts);
            metrics.Spacing = 18;
            metrics.VerticalAlignment = VerticalAlignment.Center;
            metrics.HorizontalAlignment = HorizontalAlignment.Right;

            var headerGrid = Ui.Grid("*", "*,Auto");
            Ui.Place(left, 0, 0);
            Ui.Place(metrics, 0, 1);
            headerGrid.Children.Add(left);
            headerGrid.Children.Add(metrics);
            var header = new Border
            {
                Child = headerGrid,
                Padding = new Thickness(2, 0, 2, 12),
                BorderBrush = UiTheme.Border,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };

            var page = Ui.Grid("Auto,*", "*");
            Ui.Place(header, 0);
            Ui.Place(_consolePanel, 1);
            _consolePanel.Margin = new Thickness(0, 10, 0, 0);
            page.Children.Add(header);
            page.Children.Add(_consolePanel);
            return page;
        }

        private Control BuildWindowTitleBar()
        {
            var bar = Ui.Grid("*", "Auto,*,Auto");
            bar.Height = 36;
            bar.Background = UiTheme.Surface;
            bar.PointerPressed += WindowTitleBarPointerPressed;

            var traffic = Ui.H(
                TrafficButton("#FF5F57", "关闭", Close),
                TrafficButton("#FEBC2E", "最小化", () => WindowState = WindowState.Minimized),
                TrafficButton("#28C840", "最大化", ToggleMaximize));
            traffic.Spacing = 8;
            traffic.Margin = new Thickness(13, 0, 0, 0);
            traffic.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(traffic, 0, 0);
            bar.Children.Add(traffic);

            var title = Ui.T("PGame-TSManager", 10.5, UiTheme.TextFaint);
            title.FontWeight = FontWeight.SemiBold;
            var brand = Ui.H(
                new Image
                {
                    Source = LoadBitmap("avares://PGame-TSManager/PGame-TSManager-logo.png"),
                    Width = 20,
                    Height = 20,
                    Stretch = Stretch.UniformToFill
                },
                title);
            brand.Spacing = 8;
            brand.HorizontalAlignment = HorizontalAlignment.Center;
            brand.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(brand, 0, 1);
            bar.Children.Add(brand);

            var fullscreen = ChromeButton(_fullscreenIcon, "全屏 / 退出全屏", ToggleFullscreen);
            var min = ChromeButton(LineIcon(11, 1), "最小化", () => WindowState = WindowState.Minimized);
            var max = ChromeButton(_maximizeIcon, "最大化/还原", ToggleMaximize);
            var close = ChromeButton(CrossIcon(), "关闭", Close, "close");
            fullscreen.Name = "TitleBarFullscreenButton";
            min.Name = "TitleBarMinimizeButton";
            max.Name = "TitleBarMaximizeButton";
            close.Name = "TitleBarCloseButton";
            var actions = Ui.H(fullscreen, min, max, close);
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            Ui.Place(actions, 0, 2);
            bar.Children.Add(actions);

            return new Border
            {
                Child = bar,
                Background = UiTheme.Surface,
                BorderBrush = UiTheme.Border,
                BorderThickness = new Thickness(0, 0, 0, 1)
            };
        }

        private static Button TrafficButton(string color, string tip, Action click)
        {
            var button = new Button
            {
                Width = 12,
                Height = 12,
                MinWidth = 12,
                MinHeight = 12,
                Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6),
                BorderThickness = new Thickness(0),
                Background = Brush.Parse(color),
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Click += (_, _) => click();
            ToolTip.SetTip(button, tip);
            return button;
        }

        private static Button ChromeButton(Control icon, string tip, Action click, string? extraClass = null)
        {
            var button = new Button
            {
                Content = icon,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            button.Classes.Add("chrome");
            if (!string.IsNullOrWhiteSpace(extraClass)) button.Classes.Add(extraClass);
            button.Click += (_, _) => click();
            ToolTip.SetTip(button, tip);
            return button;
        }

        private static Border LineIcon(double width, double height)
            => new()
            {
                Width = width,
                Height = height,
                Background = UiTheme.TextDim,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        private static ShapePath CrossIcon()
            => new()
            {
                Width = 12,
                Height = 12,
                Data = StreamGeometry.Parse("M 2,2 L 10,10 M 10,2 L 2,10"),
                Stroke = UiTheme.TextDim,
                StrokeThickness = 1.1,
                Stretch = Stretch.None,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

        private void WindowTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || IsInsideButton(e.Source)) return;
            if (e.ClickCount == 2)
            {
                ToggleMaximize();
                e.Handled = true;
                return;
            }
            try { BeginMoveDrag(e); } catch { }
        }

        private static bool IsInsideButton(object? source)
        {
            var visual = source as Visual;
            while (visual != null)
            {
                if (visual is Button) return true;
                visual = visual.GetVisualParent();
            }
            return false;
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
            UpdateChromeIcons();
        }

        private void ToggleFullscreen()
        {
            if (WindowState == WindowState.FullScreen)
            {
                WindowState = _stateBeforeFullscreen;
            }
            else
            {
                _stateBeforeFullscreen = WindowState;
                WindowState = WindowState.FullScreen;
            }
            UpdateChromeIcons();
        }

        private static StreamGeometry FullscreenIconData(bool restore)
            => StreamGeometry.Parse(restore
                ? "M 5,1 L 5,5 L 1,5 M 8,1 L 8,5 L 12,5 M 12,8 L 8,8 L 8,12 M 1,8 L 5,8 L 5,12"
                : "M 1,5 L 1,1 L 5,1 M 8,1 L 12,1 L 12,5 M 12,8 L 12,12 L 8,12 M 5,12 L 1,12 L 1,8");

        private void UpdateChromeIcons()
        {
            _maximizeIcon.Data = StreamGeometry.Parse(WindowState == WindowState.Maximized
                ? "M 3,1 L 11,1 L 11,9 M 1,3 L 9,3 L 9,11 L 1,11 Z"
                : "M 1,1 L 11,1 L 11,11 L 1,11 Z");
            _fullscreenIcon.Data = FullscreenIconData(WindowState == WindowState.FullScreen);
        }

        private Control BuildHeader()
        {
            var header = Ui.Grid("*", "Auto,Auto,*,Auto");
            header.Margin = new Thickness(10, 0);
            header.Background = UiTheme.Surface;

            var brand = Ui.H(
                new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(8),
                    ClipToBounds = true,
                    Background = UiTheme.SurfaceRaised,
                    Child = new Image
                    {
                        Source = LoadBitmap("avares://PGame-TSManager/PGame-TSManager-logo.png"),
                        Stretch = Stretch.UniformToFill
                    }
                },
                Ui.V(
                    Ui.T("PGame-TSManager", 15, UiTheme.Text, FontWeight.SemiBold),
                    Ui.T("TShock 多服控制台", 10.5, UiTheme.TextDim)
                )
            );
            brand.Spacing = 10;
            brand.Margin = new Thickness(0, 0, 10, 0);
            brand.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(brand, 0, 0);
            header.Children.Add(brand);

            var nav = Ui.H(_consoleNav, _operationsNav, _pluginsNav, _manageNav);
            nav.Spacing = 4;
            nav.Margin = new Thickness(18, 0, 0, 0);
            nav.HorizontalAlignment = HorizontalAlignment.Left;
            nav.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(nav, 0, 1);
            header.Children.Add(nav);

            var actions = Ui.H(
                _startButton, _stopButton, _restartButton,
                _startAllButton, _stopAllButton, _settingsButton, _folderButton);
            actions.Spacing = 6;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            actions.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(actions, 0, 3);
            header.Children.Add(actions);
            return header;
        }

        private MenuFlyout BuildManagementFlyout()
        {
            var flyout = new MenuFlyout();
            flyout.Items.Add(MenuAction("保存世界", () => SendQuickCommand("/save")));
            flyout.Items.Add(new Separator());
            flyout.Items.Add(MenuAction("服务器设置", OpenServerSettings));
            flyout.Items.Add(MenuAction("插件开关", OpenPlugins));
            flyout.Items.Add(MenuAction("玩家管理", OpenPlayers));
            flyout.Items.Add(MenuAction("插件检查", OpenPluginCheck));
            flyout.Items.Add(MenuAction("计划任务", OpenSchedules));
            flyout.Items.Add(MenuAction("回滚中心", OpenBackups));
            flyout.Items.Add(new Separator());
            flyout.Items.Add(MenuAction("新建服务器", OpenNewServer));
            flyout.Items.Add(MenuAction("刷新服务器列表", ReloadContainers));
            return flyout;
        }

        private static MenuItem MenuAction(string text, Action action)
        {
            var item = new MenuItem { Header = text };
            item.Click += (_, _) => action();
            return item;
        }

        private Control BuildSidebar()
        {
            var heading = Ui.Grid("*", "*,Auto");
            var title = Ui.T("本机服务器", 12, UiTheme.Text, FontWeight.SemiBold);
            Ui.Place(title, 0, 0);
            _serverCountText.HorizontalAlignment = HorizontalAlignment.Right;
            Ui.Place(_serverCountText, 0, 1);
            heading.Children.Add(title);
            heading.Children.Add(_serverCountText);

            var footer = Ui.Grid("*", "*,*");
            var add = Ui.Btn("新建服务器", OpenNewServer);
            var refresh = Ui.Btn("刷新列表", ReloadContainers);
            Ui.Place(add, 0, 0);
            Ui.Place(refresh, 0, 1);
            refresh.Margin = new Thickness(6, 0, 0, 0);
            footer.Children.Add(add);
            footer.Children.Add(refresh);

            var panel = Ui.Grid("Auto,Auto,*,Auto", "*");
            Ui.Place(heading, 0);
            Ui.Place(_serverSearch, 1);
            _serverSearch.Margin = new Thickness(0, 8, 0, 8);
            Ui.Place(_serverList, 2);
            Ui.Place(footer, 3);
            footer.Margin = new Thickness(0, 8, 0, 0);
            panel.Children.Add(heading);
            panel.Children.Add(_serverSearch);
            panel.Children.Add(_serverList);
            panel.Children.Add(footer);
            var card = Ui.Card(panel, 10, UiTheme.Surface);
            card.Margin = new Thickness(0, 0, 0, 0);
            return card;
        }

        private static ControlTheme BuildSidebarItemTheme()
        {
            var theme = new ControlTheme(typeof(ListBoxItem));
            theme.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(0)));
            theme.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0, 0, 0, 8)));
            theme.Setters.Add(new Setter(ListBoxItem.CornerRadiusProperty, new CornerRadius(6)));
            theme.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, Brushes.Transparent));
            theme.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(0)));
            theme.Setters.Add(new Setter(ListBoxItem.TemplateProperty, new FuncControlTemplate((_, _) =>
            {
                var root = new ContentPresenter();
                root.Bind(ContentPresenter.BackgroundProperty, new Binding
                {
                    Path = "Background",
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });
                root.Bind(ContentPresenter.BorderBrushProperty, new Binding
                {
                    Path = "BorderBrush",
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });
                root.Bind(ContentPresenter.BorderThicknessProperty, new Binding
                {
                    Path = "BorderThickness",
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });
                root.Bind(ContentPresenter.CornerRadiusProperty, new Binding
                {
                    Path = "CornerRadius",
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });
                root.Bind(ContentPresenter.ContentProperty, new Binding
                {
                    Path = "Content",
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });
                root.Bind(ContentPresenter.ContentTemplateProperty, new Binding
                {
                    Path = "ContentTemplate",
                    RelativeSource = new RelativeSource(RelativeSourceMode.TemplatedParent)
                });
                return root;
            })));
            return theme;
        }

        private Grid BuildCommandBar()
        {
            var bar = Ui.Grid("*", "*,Auto,Auto");
            bar.Margin = new Thickness(0, 8, 0, 0);
            bar.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            Ui.Place(_commandBox, 0, 0);
            var send = Ui.Btn("发送", SendCurrent, "primary", 72);
            send.Margin = new Thickness(8, 0, 0, 0);
            Ui.Place(send, 0, 1);
            var sendAll = Ui.Btn("全部发送", SendAll, null, 86);
            sendAll.Margin = new Thickness(6, 0, 0, 0);
            Ui.Place(sendAll, 0, 2);
            bar.Children.Add(_commandBox);
            bar.Children.Add(send);
            bar.Children.Add(sendAll);
            return bar;
        }

        private static Bitmap? LoadBitmap(string uri)
        {
            try { return new Bitmap(AssetLoader.Open(new Uri(uri))); }
            catch { return null; }
        }

        private static Control ServerSidebarItem(ServerContainer c)
        {
            var dot = new Border
            {
                Width = 9,
                Height = 9,
                CornerRadius = new CornerRadius(5)
            };
            dot.Bind(Border.BackgroundProperty, new Binding(nameof(ServerContainer.StatusBrush)));
            var name = Ui.T("", 12.5, UiTheme.Text, FontWeight.SemiBold);
            name.Bind(TextBlock.TextProperty, new Binding(nameof(ServerContainer.ListLabel)));
            var status = Ui.T("", 10.5, UiTheme.TextDim);
            status.Bind(TextBlock.TextProperty, new Binding(nameof(ServerContainer.StatusText)));
            var ports = Ui.T($"端口 {c.GamePort} / REST {c.RestPort}", 10.5, UiTheme.TextFaint);
            var detail = Ui.H(ports, status);
            detail.Spacing = 8;
            var left = Ui.V(name, detail);
            left.Spacing = 2;
            var row = Ui.Grid("Auto,*", "Auto,*");
            dot.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(dot, 0, 0);
            Ui.Place(left, 0, 1);
            row.ColumnDefinitions[0].Width = new GridLength(20);
            left.Margin = new Thickness(6, 0, 0, 0);
            row.Children.Add(dot);
            row.Children.Add(left);
            var card = Ui.Card(row, 8, UiTheme.SurfaceRaised);
            var selection = new Border
            {
                BorderBrush = UiTheme.Accent,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(9),
                IsVisible = false,
                IsHitTestVisible = false
            };
            selection.Bind(Border.IsVisibleProperty, new Binding
            {
                Path = "IsSelected",
                RelativeSource = new RelativeSource(RelativeSourceMode.FindAncestor)
                {
                    AncestorType = typeof(ListBoxItem),
                    AncestorLevel = 1
                }
            });
            var host = new Grid { Children = { card, selection } };
            return host;
        }

        private async void OnOpened(object? sender, EventArgs e)
        {
            Alerter.Configure(_cfg);
            ReloadContainers();
            _operationsView.Initialize(Current, Containers);
            AppendLine(Alerter.Ready
                ? "[告警] 已启用，异常、掉线与自动重启会发送到测试群"
                : "[告警] 未启用或未配置有效 Webhook（config.json 的 告警开关 / 告警Webhook）");

            if (Containers.Count == 0)
            {
                AppendLine("未配置任何服务器。请在 Servers\\Profiles 下新建配置，或使用「管理 → 新建服务器」。");
                return;
            }

            StartStatusTimer();
            StartScheduleTimer();
            StartCapacityTimer();
            StartPeriodicSaveTimer();
            BuildTrayMenu();
            RunHousekeeping("启动");
            StartHousekeepingTimer();
            UpdateActionStates();
            if (StartAllOnLoad) await StartAllSequentialAsync();
        }

        private async void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_allowClose || App.IsExiting)
            {
                e.Cancel = false;
                return;
            }

            e.Cancel = true;
            var choice = await new ChoiceDialog(
                "PGame-TSManager",
                "要缩小到托盘，还是关闭程序？\n缩小到托盘后，服务器会继续在后台运行。",
                "缩小到托盘",
                "关闭程序",
                "取消").ShowDialog<ChoiceDialogResult>(this);

            if (choice == ChoiceDialogResult.Primary)
            {
                Hide();
                return;
            }
            if (choice != ChoiceDialogResult.Secondary) return;

            if (!await Ui.ConfirmAsync(
                    this,
                    "确认关闭",
                    "确定要关闭 PGame-TSManager 吗？\n正在运行的服务器会一起被关闭。",
                    "关闭",
                    true))
                return;

            _allowClose = true;
            Close();
            App.ExitFromTray();
        }

        private void ContainersChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateActionStates();
            UpdateServerCount();
        }

        private void UpdateServerCount()
        {
            _statusText.Text = Containers.Count == 0
                ? "未配置服务器"
                : $"服务器 {Containers.Count} 台 · 运行 {Containers.Count(c => c.IsRunning)} 台 · " +
                  $"在线 {Containers.Where(c => c.IsRunning).Sum(c => Math.Max(0, c.PlayerCount))} 人";
            _serverCountText.Text = _visibleContainers.Count == Containers.Count
                ? Containers.Count.ToString()
                : $"{_visibleContainers.Count}/{Containers.Count}";
            UpdateServerSummary();
        }

        private void ReloadContainers()
        {
            var selectedId = Current?.StableId;
            var existing = Containers.ToDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
            Containers.Clear();
            foreach (var profile in _cfg.LoadProfiles())
            {
                if (existing.TryGetValue(profile.name, out var kept))
                {
                    Containers.Add(kept);
                    continue;
                }
                var container = new ServerContainer(_cfg, profile);
                container.OnTextChanged += c =>
                {
                    if (c == Current) ScrollConsoleToEnd();
                };
                container.OnAlert += (c, title, detail) =>
                {
                    c.Log($"[告警] {title} —— {detail}\n");
                    Alerter.Send(title, detail);
                };
                Containers.Add(container);
            }

            if (Containers.Count > 0)
            {
                var selected = Containers.FirstOrDefault(c =>
                    string.Equals(c.StableId, selectedId, StringComparison.OrdinalIgnoreCase)) ?? Containers[0];
                ApplyServerFilter(selected.StableId);
            }
            else
            {
                _visibleContainers.Clear();
                _serverList.SelectedItem = null;
            }
            _operationsView.SetContext(Current, Containers);
            _pluginLibraryView.SetContext(Current, Containers);
            BuildTrayMenu();
            UpdateActionStates();
            UpdateServerCount();
        }

        private void ApplyServerFilter(string? selectedId)
        {
            var query = (_serverSearch.Text ?? "").Trim();
            var matches = Containers.Where(c =>
                query.Length == 0 ||
                c.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.ListLabel.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.GamePort.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                c.RestPort.ToString().Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            _visibleContainers.Clear();
            foreach (var container in matches) _visibleContainers.Add(container);

            var selected = matches.FirstOrDefault(c =>
                string.Equals(c.StableId, selectedId, StringComparison.OrdinalIgnoreCase)) ??
                matches.FirstOrDefault();
            _serverList.SelectedItem = selected;
            UpdateServerCount();
        }

        private void ServerListSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            SetCurrent(_serverList.SelectedItem as ServerContainer);
        }

        private void SetCurrent(ServerContainer? current)
        {
            _consoleList.ItemsSource = current?.ConsoleLines ?? _systemConsole;
            _operationsView.SetContext(current, Containers);
            _pluginLibraryView.SetContext(current, Containers);
            UpdateActionStates();
            UpdateServerCount();
            UpdateServerSummary();
            ScrollConsoleToEnd();
        }

        private void UpdateServerSummary()
        {
            var current = Current;
            if (current == null)
            {
                _summaryName.Text = "服务器工作区";
                _summarySubtitle.Text = "尚未选择服务器";
                _summaryPid.Text = "PID  —";
                _summaryMemory.Text = "内存  —";
                _summaryUptime.Text = "运行  —";
                _summaryPorts.Text = "端口  —";
                return;
            }

            _summaryName.Text = current.Name;
            _summarySubtitle.Text = $"{current.StatusText}  ·  端口 {current.GamePort} / REST {current.RestPort}" +
                                    $"  ·  玩家 {current.PlayerText}  ·  插件 {current.PluginCount}" +
                                    $"  ·  世界 {FormatSize(current.WorldSizeBytes)}";
            _summaryPid.Text = "PID  " + (current.IsRunning ? current.ProcessId.ToString() : "—");
            _summaryMemory.Text = "内存  " + (current.IsRunning ? FormatSize(current.MemoryBytes) : "—");
            _summaryUptime.Text = "运行  " + (current.IsRunning && !string.IsNullOrWhiteSpace(current.Uptime)
                ? current.Uptime
                : "—");
            _summaryPorts.Text = $"端口  {current.GamePort} / {current.RestPort}";
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / 1024d / 1024d / 1024d).ToString("0.0") + " GB";
            if (bytes >= 1024L * 1024L) return (bytes / 1024d / 1024d).ToString("0.0") + " MB";
            if (bytes >= 1024L) return (bytes / 1024d).ToString("0.0") + " KB";
            return bytes + " B";
        }

        private void ScrollConsoleToEnd()
        {
            if (_consoleScrollScheduled) return;
            _consoleScrollScheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                _consoleScrollScheduled = false;
                try
                {
                    // 虚拟列表在批量增删后直接 ScrollIntoView 可能拿到无效 arrange 矩形。
                    // 优先滚到 ScrollViewer 末端；找不到时才回退到最后一项。
                    var viewer = _consoleList.GetVisualDescendants()
                        .OfType<ScrollViewer>()
                        .FirstOrDefault();
                    if (viewer != null)
                    {
                        viewer.ScrollToEnd();
                        return;
                    }

                    var items = _consoleList.ItemsSource as System.Collections.IEnumerable;
                    var last = items?.Cast<object>().LastOrDefault();
                    if (last != null) _consoleList.ScrollIntoView(last);
                }
                catch
                {
                    // 布局过渡期的滚动失败不应影响控制台继续输出。
                }
            }, DispatcherPriority.Background);
        }

        private void ShowWorkspace(bool operations)
        {
            ShowPage(operations ? WorkspacePage.Operations : WorkspacePage.Console);
        }

        private void ShowPage(WorkspacePage page)
        {
            _consolePage.IsVisible = page == WorkspacePage.Console;
            _commandBar.IsVisible = page == WorkspacePage.Console;
            _operationsView.IsVisible = page == WorkspacePage.Operations;
            _pluginLibraryView.IsVisible = page == WorkspacePage.Plugins;
            SetNavState(_consoleNav, page == WorkspacePage.Console);
            SetNavState(_operationsNav, page == WorkspacePage.Operations);
            SetNavState(_pluginsNav, page == WorkspacePage.Plugins);
            if (page == WorkspacePage.Operations) _operationsView.RefreshAll();
            if (page == WorkspacePage.Plugins) _pluginLibraryView.RefreshAll();
            if (page == WorkspacePage.Console) ScrollConsoleToEnd();
        }

        private static void SetNavState(Button button, bool active)
        {
            if (active)
            {
                if (!button.Classes.Contains("nav-active")) button.Classes.Add("nav-active");
            }
            else
            {
                button.Classes.Remove("nav-active");
            }
        }

        private void StartStatusTimer()
        {
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _statusTimer.Tick += async (_, _) =>
            {
                foreach (var c in Containers)
                {
                    try
                    {
                        await c.RefreshStatusAsync();
                        c.TickWatchdog();
                        c.TickIdleMemory();
                        TickLastPlayerLeaveSave(c);
                    }
                    catch { }
                }
                UpdateTrayTip();
                UpdateActionStates();
                UpdateServerCount();
                TickUnmanagedScan();
                var result = Alerter.LastResult;
                if (!string.IsNullOrEmpty(result) && result != _lastAlertResult)
                {
                    _lastAlertResult = result;
                    AppendLine("[告警] 上报状态：" + result);
                }
            };
            _statusTimer.Start();
        }

        private void StartScheduleTimer()
        {
            _scheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
            _scheduleTimer.Tick += async (_, _) =>
            {
                try
                {
                    await ScheduleService.TickAsync(Containers, AppendLine,
                        c => RestartOneAsync(c, "计划任务"));
                }
                catch (Exception ex) { AppendLine("[计划任务] 异常：" + ex.Message); }
            };
            _scheduleTimer.Start();
        }

        private void StartCapacityTimer()
        {
            if (!_cfg.capacitySampleEnabled) return;
            _capacityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(Math.Max(10, _cfg.capacitySampleSeconds))
            };
            _capacityTimer.Tick += (_, _) => TickCapacitySample();
            _capacityTimer.Start();
        }

        private void StartPeriodicSaveTimer()
        {
            if (!_cfg.periodicSaveEnabled) return;
            _periodicSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(Math.Max(5, _cfg.periodicSaveMinutes))
            };
            _periodicSaveTimer.Tick += (_, _) => TickPeriodicSave();
            _periodicSaveTimer.Start();
        }

        private void StartHousekeepingTimer()
        {
            _housekeepingTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(24) };
            _housekeepingTimer.Tick += (_, _) => RunHousekeeping("每日");
            _housekeepingTimer.Start();
        }

        private void TickUnmanagedScan()
        {
            if ((DateTime.UtcNow - _lastConflictScanUtc).TotalSeconds < 30) return;
            _lastConflictScanUtc = DateTime.UtcNow;
            try
            {
                var managed = new HashSet<int>(
                    Containers.Where(c => c.IsRunning).Select(c => c.ProcessId).Where(p => p > 0));
                var seen = new HashSet<int>();
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name='TShock.Server.exe'");
                foreach (ManagementBaseObject mo in searcher.Get())
                {
                    int pid;
                    try { pid = Convert.ToInt32(mo["ProcessId"]); } catch { continue; }
                    seen.Add(pid);
                    if (managed.Contains(pid)) continue;
                    var cmd = mo["CommandLine"] as string ?? "";
                    var port = ParsePortArgument(cmd);
                    var serverId = port > 0
                        ? Containers.FirstOrDefault(c => c.GamePort == port)?.StableId
                        : null;
                    if (!_reportedConflictPids.Add(pid)) continue;
                    ControlEventHub.Publish("server.process.conflict", serverId, new
                    {
                        serverId,
                        pid,
                        name = "TShock.Server",
                        port,
                        managed = false
                    });
                    AppendLine($"[冲突] 发现非 TSM 托管的 TShock.Server 进程 pid={pid}" +
                               (port > 0 ? $" port={port}" : "") + "（只告警，不自动清理）。");
                }
                foreach (var stale in _reportedConflictPids.Where(p => !seen.Contains(p)).ToList())
                    _reportedConflictPids.Remove(stale);
            }
            catch { }
        }

        private static int ParsePortArgument(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine)) return 0;
            var parts = commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < parts.Length - 1; i++)
                if (parts[i].Equals("-port", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(parts[i + 1].Trim('"'), out var port))
                    return port;
            return 0;
        }

        private void TickCapacitySample()
        {
            try
            {
                var dir = ManagerConfig.Resolve(_cfg.dataDir);
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "capacity-samples.csv");
                if (!File.Exists(path))
                    File.AppendAllText(path,
                        "时间,serverId,label,人数,cpu百分比,工作集MB,私有MB" + Environment.NewLine,
                        new UTF8Encoding(false));
                var now = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var rows = new List<string>();
                foreach (var c in Containers.Where(c => c.IsRunning))
                {
                    rows.Add(string.Join(",",
                        now,
                        c.StableId,
                        c.ListLabel.Replace(',', ' '),
                        Math.Max(0, c.PlayerCount).ToString(),
                        c.SampleCpuPercent().ToString("0.0", CultureInfo.InvariantCulture),
                        (c.MemoryBytes / 1024L / 1024L).ToString(),
                        (c.PrivateBytes / 1024L / 1024L).ToString()));
                }
                if (rows.Count > 0) File.AppendAllLines(path, rows, new UTF8Encoding(false));
            }
            catch { }
        }

        private void TickPeriodicSave()
        {
            foreach (var c in Containers.Where(c => c.IsRunning))
            {
                if (_cfg.periodicSaveOnlyWhenEmpty && c.PlayerCount > 0) continue;
                SaveWorld(c, "周期保存", "0 人补存");
            }
        }

        private void TickLastPlayerLeaveSave(ServerContainer c)
        {
            if (!_cfg.saveOnLastPlayerLeave) return;
            var now = c.IsRunning ? Math.Max(0, c.PlayerCount) : 0;
            _lastPlayerCount.TryGetValue(c.Name, out var previous);
            _lastPlayerCount[c.Name] = now;
            if (previous > 0 && now == 0) SaveWorld(c, "离场保存", "最后一名玩家离线");
        }

        private void SaveWorld(ServerContainer c, string tag, string reason)
        {
            try
            {
                var error = c.SendCommandViaRest("/save", out _);
                if (error == null)
                    AppendLine($"[{tag}] {c.ListLabel} {reason} → world={Path.GetFileName(c.WorldPath)} " +
                               $"size={c.WorldSizeBytes}");
                else
                    AppendLine($"[{tag}] {c.ListLabel} 保存失败：{error}");
            }
            catch (Exception ex) { AppendLine($"[{tag}] {c.ListLabel} 保存异常：{ex.Message}"); }
        }

        private void RunHousekeeping(string reason)
        {
            try { AppendLine(ManagerHousekeeping.RunOnce() + "（" + reason + "）"); }
            catch (Exception ex) { AppendLine("[清理] 异常：" + ex.Message); }
        }

        private void UpdateTrayTip()
        {
            var text = new StringBuilder("PGame-TSManager");
            foreach (var c in Containers)
                text.Append('\n').Append(c.ListLabel).Append(' ')
                    .Append(c.IsRunning ? c.StatusText : "已停止");
            App.UpdateTrayTip(text.ToString());
        }

        private void BuildTrayMenu()
        {
            var menu = new NativeMenu();
            foreach (var c in Containers)
            {
                var target = c;
                var item = new NativeMenuItem(target.ListLabel + (target.IsRunning ? "（运行中）" : "（已停止）"));
                var submenu = new NativeMenu();
                var start = new NativeMenuItem("启动");
                start.Click += (_, _) => target.IsRunning = true;
                var stop = new NativeMenuItem("停止");
                stop.Click += async (_, _) =>
                {
                    if (await ConfirmStopAsync(target.Name)) target.IsRunning = false;
                };
                var ready = new NativeMenuItem("启动并等待就绪");
                ready.Click += async (_, _) =>
                {
                    if (!target.IsRunning) target.IsRunning = true;
                    await target.WaitUntilReadyAsync(
                        TimeSpan.FromSeconds(Math.Max(30, _cfg.startReadyTimeoutSeconds)));
                    UpdateTrayTip();
                };
                submenu.Items.Add(start);
                submenu.Items.Add(stop);
                submenu.Items.Add(new NativeMenuItemSeparator());
                submenu.Items.Add(ready);
                item.Menu = submenu;
                menu.Items.Add(item);
            }
            menu.Items.Add(new NativeMenuItemSeparator());
            var startAll = new NativeMenuItem("全部启动");
            startAll.Click += (_, _) => _ = StartAllSequentialAsync();
            menu.Items.Add(startAll);
            var stopAll = new NativeMenuItem("全部停止");
            stopAll.Click += async (_, _) => await StopAllAsync();
            menu.Items.Add(stopAll);
            App.SetTrayMenu(menu);
            UpdateTrayTip();
        }

        private void UpdateActionStates()
        {
            var current = Current;
            _startButton.IsEnabled = current != null && !current.IsRunning;
            _stopButton.IsEnabled = current?.IsRunning == true;
            _restartButton.IsEnabled = current?.IsRunning == true;
            _startAllButton.IsEnabled = Containers.Any(c => !c.IsRunning);
            _stopAllButton.IsEnabled = Containers.Any(c => c.IsRunning);
            _settingsButton.IsEnabled = current != null;
            _folderButton.IsEnabled = current != null;
            UpdateServerSummary();
        }

        private void AppendLine(string text)
        {
            var current = Current;
            if (current != null)
            {
                current.Log(text + "\n");
                ScrollConsoleToEnd();
                return;
            }
            _systemConsole.Add(new ConsoleEntry(text, UiTheme.LogNormal));
            while (_systemConsole.Count > 4096) _systemConsole.RemoveAt(0);
            ScrollConsoleToEnd();
        }

        private void CommandBoxKeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key != Avalonia.Input.Key.Enter) return;
            SendCurrent();
            e.Handled = true;
        }

        private void SendCurrent()
        {
            var current = Current;
            if (current == null || !current.IsRunning) return;
            if (string.IsNullOrWhiteSpace(_commandBox.Text)) return;
            current.SendText(_commandBox.Text);
            _commandBox.Text = "";
        }

        private void SendAll()
        {
            var command = _commandBox.Text ?? "";
            if (string.IsNullOrWhiteSpace(command)) return;
            foreach (var c in Containers.Where(c => c.IsRunning)) c.SendText(command);
            _commandBox.Text = "";
        }

        private void SendQuickCommand(string command)
        {
            var current = Current;
            if (current == null || !current.IsRunning)
            {
                AppendLine("[快捷指令] 当前服务器未运行");
                return;
            }
            try { current.SendText(command); }
            catch (Exception ex) { AppendLine("[快捷指令] 发送失败：" + ex.Message); }
        }

        private void StartCurrent()
        {
            if (Current == null) return;
            Current.IsRunning = true;
            UpdateActionStates();
        }

        private async void StopCurrent()
        {
            if (Current == null || !Current.IsRunning) return;
            if (!await ConfirmStopAsync(Current.Name)) return;
            Current.IsRunning = false;
            UpdateActionStates();
        }

        private async Task RestartCurrentAsync()
        {
            var current = Current;
            if (current == null || !current.IsRunning) return;
            if (!await Ui.ConfirmAsync(
                    this,
                    "重启服务器",
                    $"{current.Name}\n\n将先保存并停止当前实例，停止成功后再通过 TSM 正门启动。",
                    "重启",
                    true))
                return;
            current.IsRunning = false;
            await RestartOneAsync(current, "手动重启");
        }

        private async Task<bool> ConfirmStopAsync(string serverName)
            => await Ui.ConfirmAsync(
                this,
                "确认停止服务器",
                $"确定要停止 {serverName} 吗？\n正在运行的玩家连接会中断。",
                "停止",
                true);

        private async Task StartAllSequentialAsync()
        {
            if (_startingAll) return;
            _startingAll = true;
            try
            {
                if (!_cfg.startAllSequential)
                {
                    AppendLine("[全部启动] 配置为同时启动");
                    foreach (var c in Containers.Where(c => !c.IsRunning))
                        try { c.IsRunning = true; } catch (Exception ex) { AppendLine($"[启动失败] {c.Name}：{ex.Message}"); }
                    return;
                }

                var list = Containers.ToList();
                var timeout = TimeSpan.FromSeconds(Math.Max(30, _cfg.startReadyTimeoutSeconds));
                AppendLine($"[顺序启动] 共 {list.Count} 台，逐台启动（每台最多等 {timeout.TotalSeconds:0} 秒）");
                for (var i = 0; i < list.Count; i++)
                {
                    var c = list[i];
                    var tag = $"[顺序启动] {i + 1}/{list.Count} {c.Name}";
                    if (c.IsRunning)
                    {
                        AppendLine($"{tag}：已在运行，跳过");
                        continue;
                    }
                    var startedAt = DateTime.UtcNow;
                    try { c.IsRunning = true; }
                    catch (Exception ex)
                    {
                        AppendLine($"{tag}：启动失败（{ex.Message}）");
                        Alerter.Send($"服务器启动失败：{c.Name}", ex.Message);
                        continue;
                    }
                    var ok = await c.WaitUntilReadyAsync(timeout);
                    var seconds = (DateTime.UtcNow - startedAt).TotalSeconds;
                    if (ok)
                    {
                        AppendLine($"{tag}：已就绪（{seconds:0} 秒）");
                    }
                    else
                    {
                        AppendLine($"{tag}：等待就绪超时（{seconds:0} 秒），继续下一台");
                        Alerter.Send($"服务器启动后未就绪：{c.Name}",
                            $"等待 {seconds:0} 秒仍未进入监听状态（端口 {c.GamePort}）。");
                    }
                }
                AppendLine("[顺序启动] 全部完成");
            }
            catch (Exception ex) { AppendLine("[顺序启动] 异常：" + ex.Message); }
            finally
            {
                _startingAll = false;
                UpdateActionStates();
            }
        }

        private async Task StopAllAsync()
        {
            if (!Containers.Any(c => c.IsRunning)) return;
            if (!await ConfirmStopAsync("全部服务器")) return;
            foreach (var c in Containers.Where(c => c.IsRunning))
                try { c.IsRunning = false; } catch (Exception ex) { AppendLine($"[停止失败] {c.Name}：{ex.Message}"); }
            UpdateActionStates();
        }

        private async Task RestartOneAsync(ServerContainer c, string source = "插件开关")
        {
            try
            {
                await c.WaitForStoppedAsync(TimeSpan.FromSeconds(30));
                c.IsRunning = true;
                var ok = await c.WaitUntilReadyAsync(
                    TimeSpan.FromSeconds(Math.Max(30, _cfg.startReadyTimeoutSeconds)));
                AppendLine(ok
                    ? $"[{source}] {c.Name} 已重启并就绪"
                    : $"[{source}] {c.Name} 重启后等待就绪超时");
            }
            catch (Exception ex) { AppendLine($"[{source}] {c.Name} 重启失败：{ex.Message}"); }
        }

        private async void OpenPlugins()
        {
            var current = Current;
            if (current == null) return;
            await OpenPluginsForAsync(current);
        }

        private void OpenPluginsFor(ServerContainer current)
        {
            _ = OpenPluginsForAsync(current);
        }

        private async Task OpenPluginsForAsync(ServerContainer current)
        {
            var pool = ManagerConfig.Resolve(_cfg.pluginDir);
            var dialog = new PluginWindow(current.Name, current.ProfileDirectory, pool,
                command => current.SendCommandViaRest(command, out _));
            if (!await dialog.ShowDialog<bool>(this)) return;
            AppendLine($"[插件开关] {current.Name} 的插件清单已保存");
            if (dialog.RestartRequested && current.IsRunning)
            {
                current.IsRunning = false;
                _ = RestartOneAsync(current, "插件开关");
            }
        }

        private void OpenCurrentFolder()
        {
            var current = Current;
            if (current == null) return;
            OperationsCenterView.OpenFolder(current.ProfileDirectory);
        }

        private async void OpenServerSettings()
        {
            var current = Current;
            if (current == null)
            {
                AppendLine("[服务器设置] 当前没有选中的服务器");
                return;
            }
            var dialog = new ServerSettingsWindow(current.Name, current.ProfileDirectory,
                command => current.SendCommandViaRest(command, out _));
            if (!await dialog.ShowDialog<bool>(this)) return;
            AppendLine($"[服务器设置] {current.Name} 的配置已保存");
            if (dialog.RestartRequested && current.IsRunning)
            {
                current.IsRunning = false;
                _ = RestartOneAsync(current, "服务器设置");
            }
        }

        private async void OpenPlayers()
        {
            var current = Current;
            if (current == null) return;
            await new PlayerManagerWindow(current).ShowDialog(this);
        }

        private async void OpenBackups()
        {
            var current = Current;
            if (current == null) return;
            await new BackupCenterWindow(
                current.Name,
                Path.Combine(current.ProfileDirectory, "tshock", "config.json"),
                ManagerConfig.Resolve(_cfg.backupDir)).ShowDialog(this);
        }

        private async void OpenPluginCheck()
        {
            var pluginDir = ManagerConfig.Resolve(_cfg.pluginDir);
            var binDir = Path.Combine(ManagerConfig.Resolve(_cfg.sharedRuntimeDir), "bin");
            await new PluginCheckWindow(pluginDir, binDir).ShowDialog(this);
        }

        private async void OpenSchedules()
        {
            await new ScheduleWindow(Containers.Select(c => c.Name)).ShowDialog(this);
        }

        private async void OpenNewServer()
        {
            var dialog = new NewServerWindow(_cfg);
            if (!await dialog.ShowDialog<bool>(this)) return;
            ReloadContainers();
            AppendLine($"[新建服务器] 已创建 {dialog.CreatedDirName}，列表已刷新");
        }
    }

    public sealed class OperationsCenterView : UserControl
    {
        public sealed class HealthRow
        {
            public string Name { get; init; } = "";
            public string State { get; init; } = "";
            public IBrush StateBrush { get; init; } = UiTheme.TextDim;
            public string Pid { get; init; } = "";
            public string Memory { get; init; } = "";
            public string Ports { get; init; } = "";
            public string Detail { get; init; } = "";
        }

        private readonly ListBox _healthList;
        private readonly ComboBox _logServer;
        private readonly TextBox _logFilter;
        private readonly TextBox _logText;
        private readonly TextBlock _logStatus;
        private readonly DispatcherTimer _timer;
        private List<ServerContainer> _containers = new();
        private ServerContainer? _current;
        private bool _errorsOnly;

        public OperationsCenterView()
        {
            _healthList = new ListBox
            {
                ItemTemplate = Ui.Template<HealthRow>(HealthItem),
                Background = UiTheme.Surface,
                Margin = new Thickness(0, 0, 0, 8)
            };
            _logServer = Ui.Combo();
            _logServer.Width = 180;
            _logServer.SelectionChanged += (_, _) => RefreshLogs();
            _logFilter = Ui.Input(watermark: "筛选 Exception、插件名、Warning");
            _logFilter.TextChanged += (_, _) => RefreshLogs();
            _logText = new TextBox
            {
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = UiTheme.MonoFont,
                Background = UiTheme.ConsoleBg,
                MinHeight = 260
            };
            _logStatus = Ui.T("", 11, UiTheme.TextDim);

            var healthButtons = Ui.H(
                Ui.Btn("刷新健康", RefreshHealth),
                Ui.Btn("刷新日志", RefreshLogs));
            healthButtons.Spacing = 6;
            healthButtons.HorizontalAlignment = HorizontalAlignment.Right;

            var healthPage = Ui.Grid("*,Auto", "*");
            Ui.Place(_healthList, 0);
            Ui.Place(healthButtons, 1);
            healthButtons.Margin = new Thickness(0, 8, 0, 0);
            healthPage.Children.Add(_healthList);
            healthPage.Children.Add(healthButtons);

            var logToolbar = Ui.Grid("*", "Auto,*");
            Ui.Place(_logServer, 0, 0);
            Ui.Place(_logFilter, 0, 1);
            _logFilter.Margin = new Thickness(8, 0, 0, 0);
            logToolbar.Children.Add(_logServer);
            logToolbar.Children.Add(_logFilter);

            var logButtons = Ui.H(
                _logStatus,
                Ui.Btn("只看异常", ToggleErrors),
                Ui.Btn("打开日志目录", OpenLogs));
            logButtons.Spacing = 6;
            logButtons.HorizontalAlignment = HorizontalAlignment.Right;
            logButtons.Margin = new Thickness(0, 8, 0, 0);

            var logPage = Ui.Grid("Auto,*,Auto", "*");
            Ui.Place(logToolbar, 0);
            Ui.Place(_logText, 1);
            _logText.Margin = new Thickness(0, 8, 0, 0);
            Ui.Place(logButtons, 2);
            logPage.Children.Add(logToolbar);
            logPage.Children.Add(_logText);
            logPage.Children.Add(logButtons);

            var tabs = new TabControl
            {
                Background = Brushes.Transparent,
                Items =
                {
                    new TabItem { Header = "健康状态", Content = healthPage },
                    new TabItem { Header = "日志中心", Content = logPage }
                }
            };

            var root = Ui.V(
                Ui.T("运维中心", 15, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("健康状态与日志检索集中在这里", 11, UiTheme.TextDim),
                tabs);
            root.Spacing = 8;
            root.Margin = new Thickness(0);
            Content = Ui.Card(root, 14, UiTheme.Surface);

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _timer.Tick += (_, _) => RefreshHealth();
        }

        private static Control HealthItem(HealthRow row)
        {
            var grid = Ui.Grid("Auto", "150,100,80,110,110,*");
            var values = new Control[]
            {
                Ui.T(row.Name, 12.5, UiTheme.Text, FontWeight.SemiBold),
                Ui.T(row.State, 11.5, row.StateBrush),
                Ui.T(row.Pid, 11, UiTheme.TextDim),
                Ui.T(row.Memory, 11, UiTheme.LogInfo),
                Ui.T(row.Ports, 11, UiTheme.TextDim),
                Ui.T(row.Detail, 11, UiTheme.TextDim)
            };
            for (var i = 0; i < values.Length; i++)
            {
                Ui.Place(values[i], 0, i);
                grid.Children.Add(values[i]);
            }
            return Ui.Card(grid, 8, UiTheme.SurfaceRaised);
        }

        public void Initialize(ServerContainer? current, IEnumerable<ServerContainer> containers)
        {
            SetContext(current, containers);
            if (!_timer.IsEnabled) _timer.Start();
        }

        public void SetContext(ServerContainer? current, IEnumerable<ServerContainer> containers)
        {
            _current = current;
            _containers = containers.ToList();
            if (_current != null && !_containers.Contains(_current)) _containers.Insert(0, _current);
            _logServer.ItemsSource = _containers;
            _logServer.ItemTemplate = Ui.Template<ServerContainer>(c => Ui.T(c.ListLabel, 11.5));
            _logServer.SelectedItem = _current;
            RefreshHealth();
            RefreshLogs();
        }

        public void RefreshAll()
        {
            RefreshHealth();
            RefreshLogs();
        }

        private void RefreshHealth()
        {
            _healthList.ItemsSource = _containers.Select(c => new HealthRow
            {
                Name = c.Name,
                State = c.IsRunning ? "运行中" : "已停止",
                StateBrush = c.IsRunning ? UiTheme.Success : UiTheme.TextDim,
                Pid = c.IsRunning ? c.ProcessId.ToString() : "-",
                Memory = c.IsRunning ? FormatSize(c.MemoryBytes) : "-",
                Ports = $"{c.GamePort}/{c.RestPort}",
                Detail = $"玩家 {c.PlayerText} · 插件 {c.PluginCount} · 世界 {FormatSize(c.WorldSizeBytes)}"
            }).ToList();
        }

        private void RefreshLogs()
        {
            var server = _logServer.SelectedItem as ServerContainer ?? _current;
            if (server == null)
            {
                _logText.Text = "";
                return;
            }
            var path = server.TodayLogPath;
            if (!File.Exists(path))
            {
                _logText.Text = "今日暂无日志：" + path;
                _logStatus.Text = "";
                return;
            }
            try
            {
                var lines = File.ReadAllLines(path);
                var filter = (_logFilter.Text ?? "").Trim();
                var selected = lines.Where(line =>
                        (!_errorsOnly || IsErrorLine(line)) &&
                        (filter.Length == 0 || line.Contains(filter, StringComparison.OrdinalIgnoreCase)))
                    .TakeLast(1000).ToList();
                _logText.Text = string.Join(Environment.NewLine, selected);
                _logStatus.Text = $"显示 {selected.Count} 行 / 共 {lines.Length} 行";
                _logText.CaretIndex = _logText.Text?.Length ?? 0;
            }
            catch (Exception ex) { _logText.Text = "读取失败：" + ex.Message; }
        }

        private static bool IsErrorLine(string line)
            => line.Contains("Exception", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("错误", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("失败", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Warning", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("警告", StringComparison.OrdinalIgnoreCase);

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L * 1024L) return (bytes / 1024d / 1024d / 1024d).ToString("0.0") + " GB";
            if (bytes >= 1024L * 1024L) return (bytes / 1024d / 1024d).ToString("0.0") + " MB";
            if (bytes >= 1024L) return (bytes / 1024d).ToString("0.0") + " KB";
            return bytes + " B";
        }

        private void ToggleErrors()
        {
            _errorsOnly = !_errorsOnly;
            RefreshLogs();
        }

        private void OpenLogs()
        {
            var server = _logServer.SelectedItem as ServerContainer ?? _current;
            if (server == null) return;
            var dir = Path.GetDirectoryName(server.TodayLogPath);
            if (dir != null) OpenFolder(dir);
        }

        internal static void OpenFolder(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"")
                    {
                        UseShellExecute = true
                    });
            }
            catch { }
        }
    }

    public sealed class PluginArtifactRow
    {
        public string Name { get; init; } = "";
        public string FileName { get; init; } = "";
        public string Version { get; init; } = "";
        public string Size { get; init; } = "";
        public string Sha256 { get; init; } = "";
        public string Path { get; init; } = "";
    }

    public sealed class PluginLibraryView : UserControl
    {
        private readonly ManagerConfig _config;
        private readonly Action<ServerContainer> _editServerPlugins;
        private readonly Action<string> _log;
        private readonly TextBox _search;
        private readonly ListBox _list;
        private readonly ComboBox _server;
        private readonly TextBlock _status;
        private readonly TextBlock _count;
        private readonly Button _editButton;
        private List<PluginArtifactRow> _rows = new();
        private List<ServerContainer> _containers = new();
        private ServerContainer? _selected;
        private bool _loading;

        public PluginLibraryView(
            ManagerConfig config,
            Action<ServerContainer> editServerPlugins,
            Action<string> log)
        {
            _config = config;
            _editServerPlugins = editServerPlugins;
            _log = log;
            _search = Ui.Input(watermark: "搜索名称、文件名、版本或 SHA256");
            _search.TextChanged += (_, _) => ApplyFilter();
            _list = new ListBox
            {
                ItemTemplate = Ui.Template<PluginArtifactRow>(PluginRow),
                Background = UiTheme.Surface
            };
            _server = Ui.Combo();
            _server.ItemTemplate = Ui.Template<ServerContainer>(c => Ui.T(c.ListLabel, 11.5));
            _editButton = Ui.Btn("编辑此服插件", EditSelectedPlugins, "primary");
            _editButton.IsEnabled = false;
            _server.SelectionChanged += (_, _) =>
            {
                _selected = _server.SelectedItem as ServerContainer;
                _editButton.IsEnabled = _selected != null;
            };
            _status = Ui.T("打开此页后载入总插件库", 11, UiTheme.TextDim);
            _count = Ui.T("0 个插件", 11, UiTheme.TextFaint);

            var heading = Ui.Grid("*", "*,Auto");
            var title = Ui.V(
                Ui.T("总插件库", 20, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("集中查看 DLL 版本与哈希，并为目标服选择加载项", 11, UiTheme.TextDim));
            title.Spacing = 4;
            Ui.Place(title, 0, 0);
            var headingActions = Ui.H(
                Ui.Btn("打开目录", OpenPluginFolder),
                Ui.Btn("导入 DLL", () => _ = ImportAsync()),
                Ui.Btn("刷新", () => _ = RefreshAllAsync()));
            headingActions.Spacing = 6;
            headingActions.HorizontalAlignment = HorizontalAlignment.Right;
            headingActions.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(headingActions, 0, 1);
            heading.Children.Add(title);
            heading.Children.Add(headingActions);

            var toolbar = Ui.Grid("*", "*,Auto,Auto,Auto");
            Ui.Place(_search, 0, 0);
            Ui.Place(_count, 0, 1);
            _count.Margin = new Thickness(12, 0, 12, 0);
            _count.VerticalAlignment = VerticalAlignment.Center;
            Ui.Place(_server, 0, 2);
            _server.Width = 180;
            Ui.Place(_editButton, 0, 3);
            _editButton.Margin = new Thickness(8, 0, 0, 0);
            toolbar.Children.Add(_search);
            toolbar.Children.Add(_count);
            toolbar.Children.Add(_server);
            toolbar.Children.Add(_editButton);

            var listHeader = Ui.Grid("*", "220,120,100,*");
            string[] labels = { "插件 / 文件名", "程序集版本", "大小", "SHA256" };
            for (var i = 0; i < labels.Length; i++)
            {
                var label = Ui.T(labels[i], 10.5, UiTheme.TextDim);
                Ui.Place(label, 0, i);
                listHeader.Children.Add(label);
            }
            listHeader.Margin = new Thickness(12, 0, 12, 6);

            var listGrid = Ui.Grid("Auto,*", "*");
            Ui.Place(listHeader, 0);
            Ui.Place(_list, 1);
            listGrid.Children.Add(listHeader);
            listGrid.Children.Add(_list);
            var listCard = new Border
            {
                Background = UiTheme.Surface,
                BorderBrush = UiTheme.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Child = listGrid
            };

            var root = Ui.Grid("Auto,Auto,*,Auto", "*");
            Ui.Place(heading, 0);
            Ui.Place(toolbar, 1);
            toolbar.Margin = new Thickness(0, 16, 0, 10);
            Ui.Place(listCard, 2);
            Ui.Place(_status, 3);
            _status.Margin = new Thickness(2, 8, 0, 0);
            root.Children.Add(heading);
            root.Children.Add(toolbar);
            root.Children.Add(listCard);
            root.Children.Add(_status);
            Content = root;
        }

        public void SetContext(ServerContainer? current, IEnumerable<ServerContainer> containers)
        {
            _containers = containers.ToList();
            _server.ItemsSource = _containers;
            var target = current != null && _containers.Contains(current) ? current : _containers.FirstOrDefault();
            _server.SelectedItem = target;
            _selected = target;
            _editButton.IsEnabled = target != null;
        }

        public void RefreshAll()
        {
            _ = RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            if (_loading) return;
            _loading = true;
            _status.Text = "正在读取插件元数据与 SHA256…";
            try
            {
                var dir = ManagerConfig.Resolve(_config.pluginDir);
                if (!Directory.Exists(dir))
                {
                    _rows = new List<PluginArtifactRow>();
                    ApplyFilter();
                    _status.Text = "插件目录不存在：" + dir;
                    return;
                }

                var files = Directory.GetFiles(dir, "*.dll").OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
                _rows = await Task.Run(() => files.Select(ReadRow).Where(x => x != null).Select(x => x!).ToList());
                ApplyFilter();
                _status.Text = $"插件总库：{dir}  ·  {_rows.Count} 个 DLL";
            }
            catch (Exception ex)
            {
                _status.Text = "读取失败：" + ex.Message;
            }
            finally
            {
                _loading = false;
            }
        }

        private static PluginArtifactRow? ReadRow(string path)
        {
            try
            {
                var info = new FileInfo(path);
                var assembly = System.Reflection.AssemblyName.GetAssemblyName(path);
                using var stream = File.OpenRead(path);
                var sha = Convert.ToHexString(SHA256.HashData(stream));
                return new PluginArtifactRow
                {
                    Name = assembly.Name ?? Path.GetFileNameWithoutExtension(path),
                    FileName = info.Name,
                    Version = assembly.Version?.ToString() ?? "-",
                    Size = FormatSize(info.Length),
                    Sha256 = sha,
                    Path = path
                };
            }
            catch
            {
                return null;
            }
        }

        private void ApplyFilter()
        {
            var query = (_search.Text ?? "").Trim();
            var rows = query.Length == 0
                ? _rows
                : _rows.Where(row =>
                    row.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    row.FileName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    row.Version.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    row.Sha256.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
            _list.ItemsSource = rows;
            _count.Text = query.Length == 0
                ? $"{_rows.Count} 个插件"
                : $"{rows.Count}/{_rows.Count} 个插件";
        }

        private static Control PluginRow(PluginArtifactRow row)
        {
            var name = Ui.V(
                Ui.T(row.Name, 12, UiTheme.Text, FontWeight.SemiBold),
                Ui.T(row.FileName, 10.5, UiTheme.TextDim));
            name.Spacing = 2;
            var version = Ui.T(row.Version, 11, UiTheme.AccentHover);
            var size = Ui.T(row.Size, 11, UiTheme.TextDim);
            var sha = Ui.T(row.Sha256, 10, UiTheme.TextFaint, FontWeight.Normal,
                TextWrapping.NoWrap, TextTrimming.CharacterEllipsis);
            var grid = Ui.Grid("*", "220,120,100,*");
            Ui.Place(name, 0, 0);
            Ui.Place(version, 0, 1);
            Ui.Place(size, 0, 2);
            Ui.Place(sha, 0, 3);
            grid.Children.Add(name);
            grid.Children.Add(version);
            grid.Children.Add(size);
            grid.Children.Add(sha);
            return Ui.Card(grid, 8, UiTheme.SurfaceRaised);
        }

        private async Task ImportAsync()
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return;
            var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "导入插件 DLL",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("插件程序集") { Patterns = new[] { "*.dll" } }
                }
            });
            if (files.Count == 0) return;
            var target = ManagerConfig.Resolve(_config.pluginDir);
            Directory.CreateDirectory(target);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var copied = 0;
            foreach (var file in files)
            {
                var source = file.TryGetLocalPath();
                if (string.IsNullOrWhiteSpace(source) || !File.Exists(source)) continue;
                var name = Path.GetFileName(source);
                var destination = Path.Combine(target, name);
                if (File.Exists(destination))
                {
                    var backup = destination + ".bak-import-" + stamp;
                    if (!File.Exists(backup)) File.Copy(destination, backup);
                }
                File.Copy(source, destination, true);
                copied++;
            }
            _log($"导入 {copied} 个插件到总库");
            await RefreshAllAsync();
        }

        private void EditSelectedPlugins()
        {
            if (_selected != null) _editServerPlugins(_selected);
        }

        private void OpenPluginFolder()
            => OperationsCenterView.OpenFolder(ManagerConfig.Resolve(_config.pluginDir));

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L) return (bytes / 1024d / 1024d).ToString("0.0") + " MB";
            if (bytes >= 1024L) return (bytes / 1024d).ToString("0.0") + " KB";
            return bytes + " B";
        }
    }

    public enum ChoiceDialogResult
    {
        None,
        Primary,
        Secondary,
        Cancel
    }

    public sealed class ChoiceDialog : Window
    {
        public ChoiceDialog(
            string title,
            string message,
            string primaryText,
            string secondaryText = "",
            string cancelText = "取消",
            bool primaryDanger = false)
        {
            Title = title;
            Width = 520;
            SizeToContent = SizeToContent.Height;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;

            var primary = Ui.Btn(primaryText, () => Close(ChoiceDialogResult.Primary),
                primaryDanger ? "danger" : "primary", 96);
            var secondary = Ui.Btn(secondaryText, () => Close(ChoiceDialogResult.Secondary),
                secondaryText.Contains("关闭", StringComparison.Ordinal) ? "danger" : null, 96);
            secondary.IsVisible = !string.IsNullOrWhiteSpace(secondaryText);
            var cancel = Ui.Btn(cancelText, () => Close(ChoiceDialogResult.Cancel), null, 82);
            cancel.IsVisible = !string.IsNullOrWhiteSpace(cancelText);
            var buttons = Ui.H(cancel, secondary, primary);
            buttons.Spacing = 6;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;

            var content = Ui.V(
                Ui.T(title, 14, UiTheme.Text, FontWeight.SemiBold),
                Ui.T(message, 12, UiTheme.TextDim, FontWeight.Normal, TextWrapping.Wrap),
                new Border { Height = 1, Background = UiTheme.Border, Margin = new Thickness(0, 14) },
                buttons);
            content.Spacing = 7;
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
        }
    }

    public sealed class BackupEntry
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string Detail { get; init; } = "";
        public string SizeText { get; init; } = "";
    }

    public sealed class BackupCenterWindow : Window
    {
        private readonly string _serverName;
        private readonly string _configPath;
        private readonly string _backupRoot;
        private readonly string _serverBackupRoot;
        private readonly ListBox _serverList;
        private readonly ListBox _managerList;
        private readonly TabControl _tabs;
        private readonly Button _restoreServer;
        private readonly Button _restoreManager;
        private readonly TextBlock _status;

        public BackupCenterWindow(string serverName, string configPath, string backupRoot)
        {
            Title = "回滚中心";
            Width = 820;
            Height = 620;
            MinWidth = 680;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _serverName = serverName;
            _configPath = configPath;
            _backupRoot = backupRoot;
            _serverBackupRoot = Path.Combine(backupRoot, Sanitize(serverName));

            _serverList = new ListBox { ItemTemplate = Ui.Template<BackupEntry>(BackupItem) };
            _managerList = new ListBox { ItemTemplate = Ui.Template<BackupEntry>(BackupItem) };
            _tabs = new TabControl
            {
                Items =
                {
                    new TabItem { Header = "服务器配置备份", Content = _serverList },
                    new TabItem { Header = "管理器更新备份", Content = _managerList }
                }
            };
            _tabs.SelectionChanged += (_, _) => UpdateButtons();
            _status = Ui.T("", 11, UiTheme.TextDim);
            _restoreServer = Ui.Btn("恢复选中", () => _ = RestoreServerAsync(), "danger", 92);
            _restoreManager = Ui.Btn("恢复管理器", () => _ = RestoreManagerAsync(), "danger", 96);
            var actions = Ui.H(
                Ui.Btn("刷新", Refresh),
                Ui.Btn("打开目录", OpenFolder),
                _restoreManager,
                _restoreServer,
                Ui.Btn("关闭", () => Close(false)));
            actions.Spacing = 6;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            var footer = Ui.Grid("*", "*,Auto");
            Ui.Place(_status, 0, 0);
            Ui.Place(actions, 0, 1);
            footer.Children.Add(_status);
            footer.Children.Add(actions);

            var content = Ui.Grid("Auto,*,Auto", "*");
            var header = Ui.V(
                Ui.T("回滚中心", 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("服务器配置恢复前会再次确认；恢复后由主窗口请求重启当前服。",
                    11.5, UiTheme.TextDim, FontWeight.Normal, TextWrapping.Wrap));
            header.Spacing = 4;
            Ui.Place(header, 0);
            Ui.Place(_tabs, 1);
            _tabs.Margin = new Thickness(0, 12, 0, 12);
            Ui.Place(footer, 2);
            content.Children.Add(header);
            content.Children.Add(_tabs);
            content.Children.Add(footer);
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            Refresh();
        }

        private static Control BackupItem(BackupEntry entry)
        {
            var title = Ui.T(entry.Name, 12.5, UiTheme.Text, FontWeight.SemiBold);
            var detail = Ui.T(entry.Detail, 10.5, UiTheme.TextDim);
            var path = Ui.T(entry.Path, 10, UiTheme.TextFaint, FontWeight.Normal,
                TextWrapping.NoWrap, TextTrimming.CharacterEllipsis);
            var left = Ui.V(title, detail, path);
            left.Spacing = 2;
            var size = Ui.T(entry.SizeText, 11, UiTheme.AccentHover);
            var grid = Ui.Grid("Auto", "*,Auto");
            Ui.Place(left, 0, 0);
            Ui.Place(size, 0, 1);
            size.Margin = new Thickness(12, 0, 0, 0);
            grid.Children.Add(left);
            grid.Children.Add(size);
            return Ui.Card(grid, 8, UiTheme.SurfaceRaised);
        }

        private void Refresh()
        {
            _serverList.ItemsSource = LoadServerBackups();
            _managerList.ItemsSource = LoadManagerBackups();
            UpdateButtons();
        }

        private List<BackupEntry> LoadServerBackups()
        {
            var result = new List<BackupEntry>();
            if (!Directory.Exists(_serverBackupRoot)) return result;
            foreach (var dir in Directory.GetDirectories(_serverBackupRoot).OrderByDescending(x => x))
            {
                var files = Directory.GetFiles(dir);
                if (!files.Any(f =>
                        Path.GetFileName(f).Equals("config.json", StringComparison.OrdinalIgnoreCase) ||
                        Path.GetFileName(f).Equals("sscconfig.json", StringComparison.OrdinalIgnoreCase)))
                    continue;
                result.Add(CreateEntry(dir, "配置文件备份"));
            }
            return result;
        }

        private List<BackupEntry> LoadManagerBackups()
        {
            var result = new List<BackupEntry>();
            if (!Directory.Exists(_backupRoot)) return result;
            foreach (var dir in Directory.GetDirectories(_backupRoot).OrderByDescending(x => x))
            {
                var name = Path.GetFileName(dir);
                if (!name.StartsWith("manager-update-", StringComparison.OrdinalIgnoreCase) &&
                    !name.StartsWith("tshock-update-", StringComparison.OrdinalIgnoreCase))
                    continue;
                result.Add(CreateEntry(dir, "管理器/核心更新备份"));
            }
            return result;
        }

        private static BackupEntry CreateEntry(string dir, string type)
        {
            var files = Directory.GetFiles(dir);
            long size = 0;
            foreach (var file in files)
                try { size += new FileInfo(file).Length; } catch { }
            return new BackupEntry
            {
                Name = Path.GetFileName(dir),
                Path = dir,
                Detail = type + " · " + files.Length + " 个文件",
                SizeText = FormatSize(size)
            };
        }

        private static string FormatSize(long size)
        {
            if (size >= 1024 * 1024) return (size / 1024d / 1024d).ToString("0.0") + " MB";
            if (size >= 1024) return (size / 1024d).ToString("0.0") + " KB";
            return size + " B";
        }

        private BackupEntry? SelectedServer => _serverList.SelectedItem as BackupEntry;
        private BackupEntry? SelectedManager => _managerList.SelectedItem as BackupEntry;
        private BackupEntry? Selected => _tabs.SelectedIndex == 0 ? SelectedServer : SelectedManager;

        private void UpdateButtons()
        {
            _restoreServer.IsEnabled = _tabs.SelectedIndex == 0 && SelectedServer != null;
            _restoreManager.IsEnabled = _tabs.SelectedIndex == 1 && SelectedManager != null &&
                SelectedManager.Name.StartsWith("manager-update-", StringComparison.OrdinalIgnoreCase);
            _status.Text = Selected == null ? "未选择备份" : Selected.Path;
        }

        private void OpenFolder()
        {
            if (!string.IsNullOrWhiteSpace(Selected?.Path)) OperationsCenterView.OpenFolder(Selected.Path);
        }

        private async Task RestoreServerAsync()
        {
            var entry = SelectedServer;
            if (entry == null) return;
            if (!await Ui.ConfirmAsync(
                    this,
                    "确认恢复配置",
                    $"将把备份 {entry.Name} 中的 config.json / sscconfig.json 恢复给 {_serverName}。\n" +
                    "恢复后会请求重启当前服。",
                    "恢复并重启",
                    true))
                return;

            try
            {
                var serverDir = Path.GetDirectoryName(Path.GetDirectoryName(_configPath)) ?? "";
                var tshockDir = Path.Combine(serverDir, "tshock");
                Directory.CreateDirectory(tshockDir);
                var safety = Path.Combine(_serverBackupRoot,
                    "rollback-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Directory.CreateDirectory(safety);
                foreach (var name in new[] { "config.json", "sscconfig.json" })
                {
                    var current = Path.Combine(tshockDir, name);
                    if (File.Exists(current)) File.Copy(current, Path.Combine(safety, name), true);
                    var source = Path.Combine(entry.Path, name);
                    if (File.Exists(source)) File.Copy(source, current, true);
                }
                Close(true);
            }
            catch (Exception ex)
            {
                await Ui.AlertAsync(this, "恢复失败", ex.Message);
            }
        }

        private async Task RestoreManagerAsync()
        {
            var entry = SelectedManager;
            if (entry == null ||
                !entry.Name.StartsWith("manager-update-", StringComparison.OrdinalIgnoreCase))
                return;
            if (!await Ui.ConfirmAsync(
                    this,
                    "确认恢复管理器",
                    $"将用备份 {entry.Name} 覆盖管理器文件，然后关闭并自动重启。",
                    "恢复并重启",
                    true))
                return;
            try
            {
                var root = ManagerConfig.BaseDir.TrimEnd('\\');
                var backup = entry.Path.TrimEnd('\\');
                var updater = Path.Combine(Path.GetTempPath(),
                    "PGame-TSManager-updater-" + Guid.NewGuid().ToString("N") + ".exe");
                File.Copy(Path.Combine(AppContext.BaseDirectory, "PGame-TSManager.exe"), updater, true);
                Process.Start(new ProcessStartInfo(updater,
                    "--apply-restore \"" + backup + "\" \"" + root + "\" " + Environment.ProcessId)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetTempPath()
                });
                Close(true);
                App.ExitFromTray();
            }
            catch (Exception ex)
            {
                await Ui.AlertAsync(this, "恢复管理器失败", ex.Message);
            }
        }

        private static string Sanitize(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }
    }

    public sealed class ColorPickerDialog : Window
    {
        private static readonly string[] Colors =
        {
            "#FFFFFF", "#E8E8E8", "#BEBEBE", "#808080", "#3F3F3F", "#000000", "#FFD166", "#F4A261",
            "#E76F51", "#FF4D6D", "#FF7AA2", "#C77DFF", "#9D4EDD", "#7B2CBF", "#5A189A", "#3C096C",
            "#BDE0FE", "#8ECAE6", "#219EBC", "#0077B6", "#00B4D8", "#48CAE4", "#90E0EF", "#ADE8F4",
            "#D8F3DC", "#95D5B2", "#52B788", "#2D6A4F", "#F8F9FA", "#DDB892", "#B08968", "#7F5539",
            "#FEE440", "#F9C74F", "#F9844A", "#F8961E", "#90BE6D", "#43AA8B", "#577590", "#277DA1"
        };

        private readonly TextBox _r;
        private readonly TextBox _g;
        private readonly TextBox _b;
        private readonly Border _preview;
        private readonly TextBlock _previewText;
        private bool _updating;

        public string ColorHex { get; private set; } = "#FFFFFF";

        public ColorPickerDialog(string initialHex)
        {
            Title = "调色板";
            Width = 560;
            Height = 470;
            CanResize = false;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;

            var palette = new WrapPanel { ItemWidth = 46, ItemHeight = 34 };
            foreach (var hex in Colors)
            {
                var button = new Button
                {
                    Width = 38,
                    Height = 26,
                    Margin = new Thickness(3),
                    Background = Brush.Parse(hex),
                    BorderBrush = UiTheme.BorderStrong,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(0)
                };
                ToolTip.SetTip(button, hex);
                button.Click += (_, _) => ApplyHex(hex);
                palette.Children.Add(button);
            }

            _r = Ui.Input();
            _g = Ui.Input();
            _b = Ui.Input();
            _r.TextChanged += (_, _) => RgbChanged();
            _g.TextChanged += (_, _) => RgbChanged();
            _b.TextChanged += (_, _) => RgbChanged();
            var rgb = Ui.Grid("Auto", "Auto,*,Auto,*,Auto,*");
            var labels = new[] { "R", "G", "B" };
            var boxes = new[] { _r, _g, _b };
            for (var i = 0; i < 3; i++)
            {
                var label = Ui.T(labels[i], 11, UiTheme.TextDim);
                label.Margin = new Thickness(i == 0 ? 0 : 10, 0, 6, 0);
                Ui.Place(label, 0, i * 2);
                Ui.Place(boxes[i], 0, i * 2 + 1);
                rgb.Children.Add(label);
                rgb.Children.Add(boxes[i]);
            }

            _preview = new Border
            {
                Height = 50,
                CornerRadius = new CornerRadius(6),
                BorderBrush = UiTheme.BorderStrong,
                BorderThickness = new Thickness(1),
                Background = Brushes.White
            };
            _previewText = Ui.T("#FFFFFF", 12, UiTheme.Text, FontWeight.SemiBold);
            _previewText.HorizontalAlignment = HorizontalAlignment.Right;
            _previewText.Margin = new Thickness(0, 0, 12, 0);
            var previewGrid = new Grid { Children = { _preview, _previewText } };

            var buttons = Ui.H(
                Ui.Btn("取消", () => Close(false)),
                Ui.Btn("确定", () => Close(true), "primary", 86));
            buttons.Spacing = 6;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var content = Ui.V(
                Ui.T("聊天颜色调色板", 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("点击预设颜色，或直接修改 R / G / B 数值。", 11.5, UiTheme.TextDim),
                palette,
                rgb,
                previewGrid,
                buttons);
            content.Spacing = 12;
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            ApplyHex(string.IsNullOrWhiteSpace(initialHex) ? "#FFFFFF" : initialHex);
        }

        private void ApplyHex(string hex)
        {
            try
            {
                var color = Color.Parse(hex);
                _updating = true;
                _r.Text = color.R.ToString();
                _g.Text = color.G.ToString();
                _b.Text = color.B.ToString();
                _updating = false;
                RefreshPreview(color);
            }
            catch
            {
                _updating = true;
                _r.Text = _g.Text = _b.Text = "255";
                _updating = false;
                RefreshPreview(Avalonia.Media.Colors.White);
            }
        }

        private void RgbChanged()
        {
            if (_updating) return;
            byte Parse(string? value) => byte.TryParse(value, out var number) ? number : (byte)0;
            RefreshPreview(Color.FromRgb(Parse(_r.Text), Parse(_g.Text), Parse(_b.Text)));
        }

        private void RefreshPreview(Color color)
        {
            _preview.Background = new SolidColorBrush(color);
            ColorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            _previewText.Text = ColorHex;
        }
    }

    public sealed class ItemPickerDialog : Window
    {
        private readonly ObservableCollection<ItemNameEntry> _items = new();
        private readonly ListBox _list;
        private readonly TextBox _search;

        public ItemNameEntry? SelectedItem { get; private set; }

        public ItemPickerDialog()
        {
            Title = "选择物品";
            Width = 660;
            Height = 620;
            MinWidth = 560;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;

            _search = Ui.Input(watermark: "中文名、英文名、内部名或 netID");
            _search.TextChanged += (_, _) => Refresh(_search.Text ?? "");
            _list = new ListBox
            {
                ItemsSource = _items,
                ItemTemplate = Ui.Template<ItemNameEntry>(item =>
                {
                    var grid = Ui.Grid("Auto", "240,*,70");
                    var chinese = Ui.T(item.Chinese, 12.5);
                    var detail = Ui.T(item.Detail, 10.5, UiTheme.TextDim, FontWeight.Normal,
                        TextWrapping.NoWrap, TextTrimming.CharacterEllipsis);
                    var id = Ui.T(item.Id.ToString(), 11, UiTheme.AccentHover);
                    id.HorizontalAlignment = HorizontalAlignment.Right;
                    Ui.Place(chinese, 0, 0);
                    Ui.Place(detail, 0, 1);
                    Ui.Place(id, 0, 2);
                    grid.Children.Add(chinese);
                    grid.Children.Add(detail);
                    grid.Children.Add(id);
                    return grid;
                })
            };
            _list.DoubleTapped += (_, _) => Choose();

            var buttons = Ui.H(
                Ui.Btn("取消", () => Close(false)),
                Ui.Btn("选择", Choose, "primary", 86));
            buttons.Spacing = 6;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var content = Ui.V(
                Ui.T("选择初始物品", 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("选择后只保存 netID。", 11.5, UiTheme.TextDim),
                _search,
                _list,
                buttons);
            content.Spacing = 10;
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            Refresh("");
        }

        private void Refresh(string query)
        {
            _items.Clear();
            foreach (var item in ItemCatalog.Search(query)) _items.Add(item);
            if (_items.Count > 0) _list.SelectedIndex = 0;
        }

        private void Choose()
        {
            if (_list.SelectedItem is not ItemNameEntry item) return;
            SelectedItem = item;
            Close(true);
        }
    }

    public sealed class NewServerWindow : Window
    {
        private readonly ManagerConfig _cfg;
        private readonly string _serversDir;
        private readonly List<string> _templates = new();
        private readonly TextBox _name;
        private readonly TextBox _port;
        private readonly TextBox _rest;
        private readonly TextBox _world;
        private readonly ComboBox _template;

        public string CreatedDirName { get; private set; } = "";

        public NewServerWindow(ManagerConfig cfg)
        {
            Title = "新建服务器";
            Width = 560;
            Height = 470;
            MinWidth = 500;
            MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _cfg = cfg;
            _serversDir = ManagerConfig.Resolve(cfg.serverDir);
            var dirs = Directory.Exists(_serversDir)
                ? Directory.GetDirectories(_serversDir).Select(Path.GetFileName)
                    .Where(x => x != null).Select(x => x!).OrderBy(x => x).ToList()
                : new List<string>();
            _templates.AddRange(dirs);

            _name = Ui.Input(watermark: "例如：生存服（不要带序号）");
            _port = Ui.Input();
            _rest = Ui.Input();
            _world = Ui.Input(watermark: "留空 = 名字.wld");
            _template = Ui.Combo(_templates.Cast<object>());
            if (_templates.Count > 0) _template.SelectedIndex = 0;
            ReadUsedPorts(dirs, out var games, out var rests);
            _port.Text = (games.Count > 0 ? games.Max() + 2 : 2025).ToString();
            _rest.Text = (rests.Count > 0 ? rests.Max() + 2 : 7881).ToString();

            var form = Ui.Grid("Auto,Auto,Auto,Auto,Auto", "110,*");
            AddFormRow(form, 0, "服务器名", _name);
            AddFormRow(form, 1, "游戏端口", _port);
            AddFormRow(form, 2, "REST 端口", _rest);
            AddFormRow(form, 3, "世界文件", _world);
            AddFormRow(form, 4, "模板服", _template);
            form.RowSpacing = 8;

            var buttons = Ui.H(
                Ui.Btn("取消", () => Close(false)),
                Ui.Btn("创建", () => _ = CreateAsync(), "primary", 86));
            buttons.Spacing = 6;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var content = Ui.V(
                Ui.T("新建服务器", 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("目录自动取下一个序号，tshock 配置从模板服复制；端口与数据库创建后再核对。",
                    11.5, UiTheme.TextDim, FontWeight.Normal, TextWrapping.Wrap),
                form,
                buttons);
            content.Spacing = 12;
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
        }

        private static void AddFormRow(Grid grid, int row, string label, Control editor)
        {
            var text = Ui.T(label, 11.5, UiTheme.TextDim);
            Ui.Place(text, row, 0);
            Ui.Place(editor, row, 1);
            grid.Children.Add(text);
            grid.Children.Add(editor);
        }

        private void ReadUsedPorts(List<string> dirs, out List<int> games, out List<int> rests)
        {
            games = new List<int>();
            rests = new List<int>();
            foreach (var dir in dirs)
            {
                try
                {
                    var file = Path.Combine(_serversDir, dir, ServerProfile.ManifestFileName);
                    if (!File.Exists(file)) continue;
                    var root = JObject.Parse(File.ReadAllText(file));
                    if (root["端口"] != null) games.Add(Convert.ToInt32(root["端口"]!.ToString()));
                    if (root["REST端口"] != null) rests.Add(Convert.ToInt32(root["REST端口"]!.ToString()));
                }
                catch { }
            }
        }

        private static int NextOrdinal(string serversDir)
        {
            var max = 0;
            try
            {
                foreach (var dir in Directory.GetDirectories(serversDir))
                    max = Math.Max(max, ManagerConfig.OrdinalOf(Path.GetFileName(dir)));
            }
            catch { }
            return max + 1;
        }

        private async Task CreateAsync()
        {
            try
            {
                var name = ManagerConfig.StripOrdinal(_name.Text ?? "").Trim();
                if (name.Length == 0) { await WarnAsync("请填服务器名。"); return; }
                if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    await WarnAsync("服务器名里有不能用于文件名的字符。");
                    return;
                }
                if (!int.TryParse(_port.Text?.Trim(), out var port) || port is < 1 or > 65535)
                {
                    await WarnAsync("游戏端口要填 1~65535。");
                    return;
                }
                if (!int.TryParse(_rest.Text?.Trim(), out var rest) || rest is < 1 or > 65535)
                {
                    await WarnAsync("REST 端口要填 1~65535。");
                    return;
                }
                if (port == rest)
                {
                    await WarnAsync("游戏端口和 REST 端口不能相同。");
                    return;
                }

                ReadUsedPorts(_templates, out var games, out var rests);
                var used = games.Concat(rests).ToHashSet();
                if (used.Contains(port) || used.Contains(rest))
                {
                    await WarnAsync("端口已被别的服占用。");
                    return;
                }
                var template = _template.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(template))
                {
                    await WarnAsync("请选一个模板服。");
                    return;
                }

                var ordinal = NextOrdinal(_serversDir);
                var dirName = ordinal + "." + name;
                var target = Path.Combine(_serversDir, dirName);
                if (Directory.Exists(target))
                {
                    await WarnAsync("目录已存在：" + target);
                    return;
                }
                Directory.CreateDirectory(target);
                var templateTshock = Path.Combine(_serversDir, template, "tshock");
                if (Directory.Exists(templateTshock))
                    CopyDirectory(templateTshock, Path.Combine(target, "tshock"));

                var plugins = new List<string>();
                try
                {
                    var manifest = Path.Combine(_serversDir, template, ServerProfile.ManifestFileName);
                    if (File.Exists(manifest))
                    {
                        var root = JObject.Parse(File.ReadAllText(manifest));
                        if (root["插件"] is JArray array)
                            plugins = array.Select(x => x?.ToString() ?? "")
                                .Where(x => x.Length > 0).ToList();
                    }
                }
                catch { }

                var world = (_world.Text ?? "").Trim();
                if (world.Length == 0) world = name + ".wld";
                var profile = new JObject
                {
                    ["服务器名称"] = name,
                    ["启用"] = true,
                    ["世界"] = world,
                    ["语言"] = 7,
                    ["端口"] = port,
                    ["REST端口"] = rest,
                    ["最大玩家"] = 252,
                    ["IP"] = "0.0.0.0",
                    ["密码"] = "",
                    ["启动参数"] = "",
                    ["插件"] = new JArray(plugins),
                    ["备注"] = $"端口 {port} / REST {rest}"
                };
                File.WriteAllText(
                    Path.Combine(target, ServerProfile.ManifestFileName),
                    profile.ToString(Formatting.Indented),
                    new UTF8Encoding(false));
                CreatedDirName = dirName;
                await Ui.AlertAsync(
                    this,
                    "新建服务器",
                    $"已创建：{dirName}\n目录：{target}\n端口：{port} / REST {rest}\n" +
                    $"插件：{plugins.Count} 个\n\n请核对 tshock\\config.json 的 REST 端口和数据库连接，" +
                    $"并把世界文件放入 Worlds\\。");
                Close(true);
            }
            catch (Exception ex)
            {
                await Ui.AlertAsync(this, "创建失败", ex.Message);
            }
        }

        private Task WarnAsync(string message)
            => Ui.AlertAsync(this, "新建服务器", message);

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }
    }

    public sealed class PlayerRow
    {
        public string Name { get; init; } = "";
        public string Group { get; init; } = "";
        public string Ip { get; init; } = "";
        public string State { get; init; } = "";
    }

    public sealed class PlayerManagerWindow : Window
    {
        private readonly ServerContainer _server;
        private readonly ListBox _list;
        private readonly TextBlock _status;

        public PlayerManagerWindow(ServerContainer server)
        {
            Title = "玩家与权限管理";
            Width = 860;
            Height = 600;
            MinWidth = 700;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _server = server;
            _list = new ListBox
            {
                ItemTemplate = Ui.Template<PlayerRow>(row =>
                {
                    var grid = Ui.Grid("Auto", "180,120,160,*");
                    var values = new[]
                    {
                        Ui.T(row.Name, 12.5),
                        Ui.T(row.Group, 11.5, UiTheme.AccentHover),
                        Ui.T(row.Ip, 11, UiTheme.TextDim),
                        Ui.T(row.State, 11, UiTheme.TextDim)
                    };
                    for (var i = 0; i < values.Length; i++)
                    {
                        Ui.Place(values[i], 0, i);
                        grid.Children.Add(values[i]);
                    }
                    return grid;
                })
            };
            _status = Ui.T("", 11, UiTheme.TextDim);
            var actions = Ui.H(
                Ui.Btn("刷新", Refresh),
                Ui.Btn("静默", () => _ = RunAsync("/mute {player}", "静默")),
                Ui.Btn("解除静默", () => _ = RunAsync("/unmute {player}", "解除静默")),
                Ui.Btn("击杀", () => _ = RunAsync("/kill {player}", "击杀")),
                Ui.Btn("踢出", () => _ = RunAsync("/kick {player}", "踢出")),
                Ui.Btn("封禁", () => _ = RunAsync("/ban add {player} 由 PGame-TSManager 封禁", "封禁"), "danger"));
            actions.Spacing = 6;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            var footer = Ui.Grid("*", "*,Auto");
            Ui.Place(_status, 0, 0);
            Ui.Place(actions, 0, 1);
            footer.Children.Add(_status);
            footer.Children.Add(actions);
            var content = Ui.Grid("Auto,*,Auto", "*");
            var header = Ui.V(
                Ui.T("玩家与权限管理 — " + server.Name, 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("在线玩家来自 TShock REST；操作通过该服管理员令牌执行。", 11.5, UiTheme.TextDim));
            header.Spacing = 4;
            Ui.Place(header, 0);
            Ui.Place(_list, 1);
            _list.Margin = new Thickness(0, 12, 0, 12);
            Ui.Place(footer, 2);
            content.Children.Add(header);
            content.Children.Add(_list);
            content.Children.Add(footer);
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            Refresh();
        }

        private PlayerRow? Selected => _list.SelectedItem as PlayerRow;

        private void Refresh()
        {
            var error = _server.GetRestJson("v2/server/status?players=true", out var body);
            if (error != null)
            {
                _status.Text = "读取失败：" + error;
                _list.ItemsSource = new List<PlayerRow>();
                return;
            }
            try
            {
                var rows = new List<PlayerRow>();
                var root = JObject.Parse(body);
                if (root["players"] is JArray players)
                {
                    foreach (var token in players.OfType<JObject>())
                        rows.Add(new PlayerRow
                        {
                            Name = token.Value<string>("nickname") ?? token.Value<string>("name") ?? "",
                            Group = token.Value<string>("group") ?? "",
                            Ip = token.Value<string>("ip") ?? "",
                            State = (token.Value<bool?>("muted") ?? false) ? "已静默" : "正常"
                        });
                }
                _list.ItemsSource = rows;
                _status.Text = "在线玩家：" + rows.Count;
            }
            catch (Exception ex)
            {
                _status.Text = "解析失败：" + ex.Message;
                _list.ItemsSource = new List<PlayerRow>();
            }
        }

        private async Task RunAsync(string command, string title)
        {
            var player = Selected;
            if (player == null)
            {
                _status.Text = "请先选择玩家";
                return;
            }
            if (title is not ("静默" or "解除静默" or "击杀"))
            {
                if (!await Ui.ConfirmAsync(
                        this,
                        "确认" + title,
                        $"确定要对 {player.Name} 执行「{title}」吗？",
                        title,
                        title is "封禁" or "踢出"))
                    return;
            }
            var error = _server.SendCommandViaRest(
                command.Replace("{player}", player.Name), out _);
            _status.Text = error == null ? title + "成功：" + player.Name : title + "失败：" + error;
            Refresh();
        }
    }

    public sealed class PluginCheckRow
    {
        public string Name { get; init; } = "";
        public string Version { get; init; } = "";
        public string Path { get; init; } = "";
        public string DependencyText { get; init; } = "依赖正常";
    }

    public sealed class PluginCheckWindow : Window
    {
        private static readonly HashSet<string> IgnoredRefs = new(StringComparer.OrdinalIgnoreCase)
        {
            "System", "System.Core", "System.Runtime", "System.Private.CoreLib", "mscorlib",
            "netstandard", "TShockAPI", "Terraria", "OTAPI", "OTAPI.Runtime", "TerrariaServerAPI",
            "MonoMod", "MonoMod.RuntimeDetour", "Newtonsoft.Json", "MySql.Data", "Microsoft.Data.Sqlite"
        };
        private readonly string _pluginDir;
        private readonly string _binDir;
        private readonly ListBox _list;
        private readonly TextBlock _status;

        public PluginCheckWindow(string pluginDir, string binDir)
        {
            Title = "插件依赖检查";
            Width = 900;
            Height = 640;
            MinWidth = 760;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _pluginDir = pluginDir;
            _binDir = binDir;
            _list = new ListBox
            {
                ItemTemplate = Ui.Template<PluginCheckRow>(row =>
                {
                    var grid = Ui.Grid("Auto", "240,100,*");
                    var left = Ui.V(
                        Ui.T(row.Name, 12.5, UiTheme.Text, FontWeight.SemiBold),
                        Ui.T(row.Path, 10, UiTheme.TextDim, FontWeight.Normal,
                            TextWrapping.NoWrap, TextTrimming.CharacterEllipsis));
                    left.Spacing = 2;
                    var version = Ui.T(row.Version, 11.5, UiTheme.AccentHover);
                    var dependency = Ui.T(row.DependencyText, 11,
                        row.DependencyText == "依赖正常" ? UiTheme.Success : UiTheme.Warning,
                        FontWeight.Normal, TextWrapping.Wrap);
                    Ui.Place(left, 0, 0);
                    Ui.Place(version, 0, 1);
                    Ui.Place(dependency, 0, 2);
                    grid.Children.Add(left);
                    grid.Children.Add(version);
                    grid.Children.Add(dependency);
                    return Ui.Card(grid, 8, UiTheme.SurfaceRaised);
                })
            };
            _status = Ui.T("", 11, UiTheme.TextDim);
            var actions = Ui.H(
                Ui.Btn("刷新", Refresh),
                Ui.Btn("打开插件库", () => OperationsCenterView.OpenFolder(_pluginDir)),
                Ui.Btn("打开依赖库", () => OperationsCenterView.OpenFolder(_binDir)),
                Ui.Btn("关闭", Close));
            actions.Spacing = 6;
            actions.HorizontalAlignment = HorizontalAlignment.Right;
            var footer = Ui.Grid("*", "*,Auto");
            Ui.Place(_status, 0, 0);
            Ui.Place(actions, 0, 1);
            footer.Children.Add(_status);
            footer.Children.Add(actions);
            var content = Ui.Grid("Auto,*,Auto", "*");
            var header = Ui.V(
                Ui.T("插件依赖与版本检查", 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("只读取 DLL 元数据，不加载插件。", 11.5, UiTheme.TextDim));
            header.Spacing = 4;
            Ui.Place(header, 0);
            Ui.Place(_list, 1);
            _list.Margin = new Thickness(0, 12, 0, 12);
            Ui.Place(footer, 2);
            content.Children.Add(header);
            content.Children.Add(_list);
            content.Children.Add(footer);
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            Refresh();
        }

        private void Refresh()
        {
            var rows = new List<PluginCheckRow>();
            try
            {
                var available = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var file in Directory.Exists(_pluginDir)
                             ? Directory.GetFiles(_pluginDir, "*.dll")
                             : Array.Empty<string>())
                    available.Add(Path.GetFileNameWithoutExtension(file));
                foreach (var file in Directory.Exists(_binDir)
                             ? Directory.GetFiles(_binDir, "*.dll")
                             : Array.Empty<string>())
                    available.Add(Path.GetFileNameWithoutExtension(file));

                var files = Directory.Exists(_pluginDir)
                    ? Directory.GetFiles(_pluginDir, "*.dll")
                    : Array.Empty<string>();
                foreach (var file in files.OrderBy(x => x))
                {
                    var assemblyName = System.Reflection.AssemblyName.GetAssemblyName(file);
                    var references = ReadReferences(file)
                        .Where(x => !x.StartsWith("System.", StringComparison.OrdinalIgnoreCase) &&
                                    !x.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase) &&
                                    !IgnoredRefs.Contains(x))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var missing = references.Where(x => !available.Contains(x)).ToList();
                    rows.Add(new PluginCheckRow
                    {
                        Name = assemblyName.Name ?? Path.GetFileNameWithoutExtension(file),
                        Version = assemblyName.Version?.ToString() ?? "-",
                        Path = file,
                        DependencyText = missing.Count == 0
                            ? "依赖正常"
                            : "可能缺失：" + string.Join(", ", missing)
                    });
                }
                _list.ItemsSource = rows;
                _status.Text = $"共 {rows.Count} 个插件，发现 {rows.Count(x => x.DependencyText != "依赖正常")} 个需检查";
            }
            catch (Exception ex)
            {
                _status.Text = "检查失败：" + ex.Message;
            }
        }

        private static IEnumerable<string> ReadReferences(string file)
        {
            using var stream = File.OpenRead(file);
            using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
            if (!pe.HasMetadata) yield break;
            var metadata = pe.GetMetadataReader();
            foreach (var handle in metadata.AssemblyReferences)
                yield return metadata.GetString(metadata.GetAssemblyReference(handle).Name);
        }
    }

    public sealed class ScheduleWindow : Window
    {
        private readonly List<string> _serverNames;
        private List<ScheduledTask> _tasks = new();
        private readonly ListBox _taskList;
        private readonly ComboBox _server;
        private readonly ComboBox _action;
        private readonly TextBox _time;
        private readonly TextBox _message;
        private readonly CheckBox _enabled;
        private readonly TextBlock _status;

        public ScheduleWindow(IEnumerable<string> serverNames)
        {
            Title = "计划任务";
            Width = 800;
            Height = 620;
            MinWidth = 680;
            MinHeight = 480;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _serverNames = serverNames.ToList();
            _taskList = new ListBox
            {
                ItemTemplate = Ui.Template<ScheduledTask>(task =>
                {
                    var grid = Ui.Grid("Auto", "80,*,80");
                    var time = Ui.T(task.Time, 12, UiTheme.AccentHover);
                    var summary = Ui.T(task.Summary, 12);
                    var state = Ui.T(task.EnabledText, 10.5, UiTheme.TextDim);
                    Ui.Place(time, 0, 0);
                    Ui.Place(summary, 0, 1);
                    Ui.Place(state, 0, 2);
                    grid.Children.Add(time);
                    grid.Children.Add(summary);
                    grid.Children.Add(state);
                    return grid;
                })
            };
            _taskList.SelectionChanged += (_, _) => LoadSelected();
            _server = Ui.Combo(_serverNames.Cast<object>());
            if (_serverNames.Count > 0) _server.SelectedIndex = 0;
            _action = Ui.Combo(new object[]
            {
                new ComboBoxItem { Content = "保存世界", Tag = "save" },
                new ComboBoxItem { Content = "广播", Tag = "broadcast" },
                new ComboBoxItem { Content = "重启当前服", Tag = "restart" }
            });
            _action.SelectedIndex = 0;
            _time = Ui.Input("04:00");
            _message = Ui.Input();
            _enabled = new CheckBox { Content = "启用", IsChecked = true };
            _status = Ui.T("", 11, UiTheme.TextDim);

            var editor = Ui.V(
                Ui.T("服务器", 11, UiTheme.TextDim), _server,
                Ui.T("动作", 11, UiTheme.TextDim), _action,
                Ui.T("时间 HH:mm", 11, UiTheme.TextDim), _time,
                Ui.T("广播内容", 11, UiTheme.TextDim), _message,
                _enabled,
                Ui.H(
                    Ui.Btn("新增", AddTask),
                    Ui.Btn("更新", UpdateTask),
                    Ui.Btn("删除", DeleteTask, "danger")));
            editor.Spacing = 6;
            ((StackPanel)editor).Children[^1].Margin = new Thickness(0, 8, 0, 0);

            var body = Ui.Grid("*", "*,280");
            Ui.Place(_taskList, 0, 0);
            Ui.Place(editor, 0, 1);
            editor.Margin = new Thickness(12, 0, 0, 0);
            body.Children.Add(_taskList);
            body.Children.Add(editor);

            var footer = Ui.Grid("*", "*,Auto");
            Ui.Place(_status, 0, 0);
            var close = Ui.Btn("关闭", Close);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            Ui.Place(close, 0, 1);
            footer.Children.Add(_status);
            footer.Children.Add(close);
            var content = Ui.Grid("Auto,*,Auto", "*");
            var header = Ui.V(
                Ui.T("计划任务", 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("每天按 HH:mm 执行；主窗口每 30 秒检查一次。", 11.5, UiTheme.TextDim));
            header.Spacing = 4;
            Ui.Place(header, 0);
            Ui.Place(body, 1);
            body.Margin = new Thickness(0, 12, 0, 12);
            Ui.Place(footer, 2);
            content.Children.Add(header);
            content.Children.Add(body);
            content.Children.Add(footer);
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            Reload();
        }

        private ScheduledTask? Selected => _taskList.SelectedItem as ScheduledTask;

        private void Reload()
        {
            _tasks = ScheduledTaskStore.Load();
            _taskList.ItemsSource = null;
            _taskList.ItemsSource = _tasks;
            _status.Text = "任务数：" + _tasks.Count;
        }

        private void LoadSelected()
        {
            var task = Selected;
            if (task == null) return;
            _server.SelectedItem = task.ServerName;
            _action.SelectedIndex = task.Action switch
            {
                "broadcast" => 1,
                "restart" => 2,
                _ => 0
            };
            _time.Text = task.Time;
            _message.Text = task.Message;
            _enabled.IsChecked = task.Enabled;
        }

        private bool TryRead(out ScheduledTask task)
        {
            task = new ScheduledTask();
            var server = _server.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(server))
            {
                _status.Text = "请选择服务器";
                return false;
            }
            if (!DateTime.TryParseExact((_time.Text ?? "").Trim(), "HH:mm",
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                _status.Text = "时间格式应为 HH:mm";
                return false;
            }
            task.ServerName = server;
            task.Action = (_action.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "save";
            task.Time = time.ToString("HH:mm");
            task.Message = (_message.Text ?? "").Trim();
            task.Enabled = _enabled.IsChecked == true;
            return true;
        }

        private void AddTask()
        {
            if (!TryRead(out var task)) return;
            task.Id = Guid.NewGuid().ToString("N");
            _tasks.Add(task);
            ScheduledTaskStore.Save(_tasks);
            Reload();
        }

        private void UpdateTask()
        {
            var current = Selected;
            if (current == null || !TryRead(out var task)) return;
            current.ServerName = task.ServerName;
            current.Action = task.Action;
            current.Time = task.Time;
            current.Message = task.Message;
            current.Enabled = task.Enabled;
            ScheduledTaskStore.Save(_tasks);
            Reload();
        }

        private void DeleteTask()
        {
            var current = Selected;
            if (current == null) return;
            _tasks.Remove(current);
            ScheduledTaskStore.Save(_tasks);
            Reload();
        }
    }

    public sealed class PluginToggle
    {
        public string Name { get; init; } = "";
        public string Description { get; init; } = "";
        public string Status { get; init; } = "";
        public bool Enabled { get; set; }
    }

    public sealed class PluginWindow : Window
    {
        private static readonly HashSet<string> ProtectedPlugins = new(StringComparer.OrdinalIgnoreCase)
        {
            "TShockAPI.dll",
            "HotReload.dll"
        };

        private readonly string _manifestPath;
        private readonly string _poolDir;
        private readonly string _serverName;
        private readonly Func<string, string?>? _hotReloadCommand;
        private readonly HashSet<string> _originalEnabled = new(StringComparer.OrdinalIgnoreCase);
        private readonly ObservableCollection<PluginToggle> _items = new();
        private readonly ItemsControl _list;

        public bool RestartRequested { get; private set; }

        public PluginWindow(
            string serverName,
            string serverDirectory,
            string poolDir,
            Func<string, string?>? hotReloadCommand = null)
        {
            Title = "插件开关";
            Width = 620;
            Height = 620;
            MinWidth = 520;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _serverName = serverName;
            _manifestPath = Path.Combine(serverDirectory, ServerProfile.ManifestFileName);
            _poolDir = poolDir;
            _hotReloadCommand = hotReloadCommand;

            _list = new ItemsControl
            {
                ItemsSource = _items,
                ItemTemplate = Ui.Template<PluginToggle>(toggle =>
                {
                    var check = new CheckBox
                    {
                        Content = toggle.Name,
                        IsChecked = toggle.Enabled,
                        FontWeight = FontWeight.SemiBold
                    };
                    check.IsCheckedChanged += (_, _) => toggle.Enabled = check.IsChecked == true;
                    ToolTip.SetTip(check, toggle.Description);
                    var status = Ui.T(toggle.Status, 10.5, UiTheme.Warning);
                    var description = Ui.T(toggle.Description, 11, UiTheme.TextDim,
                        FontWeight.Normal, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis);
                    var left = Ui.H(check, status);
                    left.Spacing = 6;
                    var grid = Ui.Grid("Auto", "240,*");
                    Ui.Place(left, 0, 0);
                    Ui.Place(description, 0, 1);
                    description.Margin = new Thickness(8, 0, 0, 0);
                    grid.Children.Add(left);
                    grid.Children.Add(description);
                    return Ui.Card(grid, 7, UiTheme.SurfaceRaised);
                })
            };

            var buttons = Ui.H(
                Ui.Btn("取消", () => Close(false)),
                Ui.Btn("热重载应用", () => _ = HotReloadAsync()),
                Ui.Btn("保存", Save, "primary"),
                Ui.Btn("保存并重启本服", SaveRestart, "primary"));
            buttons.Spacing = 6;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var content = Ui.Grid("Auto,*,Auto", "*");
            var header = Ui.V(
                Ui.T("插件开关 — " + serverName, 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("勾选 = 这台服加载的插件。TShockAPI.dll、HotReload.dll 为必需项。",
                    11.5, UiTheme.TextDim, FontWeight.Normal, TextWrapping.Wrap));
            header.Spacing = 4;
            Ui.Place(header, 0);
            var scroll = new ScrollViewer { Content = _list };
            Ui.Place(scroll, 1);
            scroll.Margin = new Thickness(0, 12, 0, 12);
            Ui.Place(buttons, 2);
            content.Children.Add(header);
            content.Children.Add(scroll);
            content.Children.Add(buttons);
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            LoadItems();
        }

        private void LoadItems()
        {
            var enabled = new List<string>();
            try
            {
                if (File.Exists(_manifestPath))
                {
                    var root = JObject.Parse(File.ReadAllText(_manifestPath));
                    if (root["插件"] is JArray array)
                        enabled = array.Select(x => x?.ToString() ?? "")
                            .Where(x => x.Length > 0).ToList();
                }
            }
            catch { }
            _originalEnabled.Clear();
            foreach (var name in enabled) _originalEnabled.Add(name);

            var pool = new List<string>();
            try
            {
                if (Directory.Exists(_poolDir))
                    pool = Directory.GetFiles(_poolDir, "*.dll")
                        .Select(Path.GetFileName).Where(x => x != null).Select(x => x!).ToList();
            }
            catch { }

            var all = pool.Union(enabled, StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => string.Equals(name, "TShockAPI.dll", StringComparison.OrdinalIgnoreCase) ? 0 :
                                 string.Equals(name, "HotReload.dll", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(name => name, StringComparer.OrdinalIgnoreCase);
            foreach (var name in all)
            {
                var inPool = pool.Any(p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
                _items.Add(new PluginToggle
                {
                    Name = name,
                    Description = Describe(name),
                    Status = ProtectedPlugins.Contains(name) ? "必需" : inPool ? "" : "总库缺失",
                    Enabled = ProtectedPlugins.Contains(name) ||
                              enabled.Any(e => string.Equals(e, name, StringComparison.OrdinalIgnoreCase))
                });
            }
            if (_items.Count == 0)
                _items.Add(new PluginToggle
                {
                    Name = "（插件总库是空的）",
                    Description = "请先把插件 DLL 放进总库",
                    Enabled = false
                });
        }

        private static Dictionary<string, string> LoadPluginDescriptions()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var dir = ManagerConfig.Resolve(ManagerConfig.Instance.dataDir);
                foreach (var file in new[] { "plugin-descriptions.json", "plugin-descriptions.local.json" })
                {
                    var path = Path.Combine(dir, file);
                    if (!File.Exists(path)) continue;
                    var root = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                    var table = root["plugins"] as JObject ?? root;
                    foreach (var property in table.Properties())
                    {
                        var text = (property.Value as JValue)?.Value<string>() ?? "";
                        if (text.Length > 0) map[property.Name] = text.Trim();
                    }
                }
            }
            catch { }
            return map;
        }

        private static readonly Dictionary<string, string> PluginDescriptions = LoadPluginDescriptions();
        private static string Describe(string name)
            => PluginDescriptions.TryGetValue(name, out var text) ? text : "自定义或专用插件";

        private bool SaveManifest()
        {
            try
            {
                var chosen = _items.Where(i => i.Enabled).Select(i => i.Name)
                    .Union(ProtectedPlugins, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(name => string.Equals(name, "TShockAPI.dll", StringComparison.OrdinalIgnoreCase) ? 0 :
                                     string.Equals(name, "HotReload.dll", StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                    .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                JObject root;
                if (File.Exists(_manifestPath))
                {
                    var text = File.ReadAllText(_manifestPath);
                    try
                    {
                        File.WriteAllText(_manifestPath + ".bak", text, new UTF8Encoding(false));
                    }
                    catch { }
                    root = JObject.Parse(text);
                }
                else
                {
                    root = new JObject();
                }
                root["插件"] = new JArray(chosen);
                File.WriteAllText(_manifestPath, root.ToString(Formatting.Indented), new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                _ = Ui.AlertAsync(this, "插件开关", "保存失败：" + ex.Message);
                return false;
            }
        }

        private void Save()
        {
            if (!SaveManifest()) return;
            Close(true);
        }

        private void SaveRestart()
        {
            if (!SaveManifest()) return;
            RestartRequested = true;
            Close(true);
        }

        private async Task HotReloadAsync()
        {
            if (!SaveManifest()) return;
            if (_hotReloadCommand == null)
            {
                await Ui.AlertAsync(this, "插件开关", "当前服务器未运行，无法执行热重载。");
                return;
            }
            var hotReloadEnabled = _items.Any(x =>
                string.Equals(x.Name, "HotReload.dll", StringComparison.OrdinalIgnoreCase) && x.Enabled);
            if (!hotReloadEnabled)
            {
                await Ui.AlertAsync(this, "插件开关", "当前插件清单没有启用 HotReload.dll。");
                return;
            }
            var toLoad = _items
                .Where(x => x.Enabled && !_originalEnabled.Contains(x.Name) && !ProtectedPlugins.Contains(x.Name))
                .Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList();
            var toUnload = _items
                .Where(x => !x.Enabled && _originalEnabled.Contains(x.Name) && !ProtectedPlugins.Contains(x.Name))
                .Select(x => Path.GetFileNameWithoutExtension(x.Name)).ToList();
            if (toLoad.Count == 0 && toUnload.Count == 0)
            {
                await Ui.AlertAsync(this, "热重载", "插件状态没有变化。");
                return;
            }
            var errors = new List<string>();
            foreach (var name in toUnload)
            {
                var error = _hotReloadCommand("/hr unload " + name);
                if (!string.IsNullOrWhiteSpace(error)) errors.Add("卸载 " + name + "：" + error);
            }
            foreach (var name in toLoad)
            {
                var error = _hotReloadCommand("/hr load " + name);
                if (!string.IsNullOrWhiteSpace(error)) errors.Add("加载 " + name + "：" + error);
            }
            if (errors.Count > 0)
            {
                await Ui.AlertAsync(this, "热重载", "未完全成功：\n" + string.Join("\n", errors));
                return;
            }
            Close(true);
        }
    }

    public sealed class DiffLine
    {
        public string Kind { get; init; } = "Same";
        public string Marker => Kind == "Add" ? "+" : Kind == "Remove" ? "-" : " ";
        public string Text { get; init; } = "";
        public IBrush Brush => Kind == "Add"
            ? UiTheme.Success
            : Kind == "Remove" ? UiTheme.Error : UiTheme.TextDim;
    }

    public sealed class DiffCategory
    {
        public string Name { get; init; } = "";
        public List<DiffLine> Lines { get; init; } = new();
    }

    public sealed class ConfigDiffWindow : Window
    {
        private readonly List<(TabItem Tab, ListBox List, DiffCategory Category)> _tabs = new();
        private readonly TabControl _tabControl;
        private readonly TextBox _search;

        public ConfigDiffWindow(IReadOnlyList<DiffCategory> categories, bool saveFlow = true)
        {
            Title = "配置变更预览";
            Width = 860;
            Height = 640;
            MinWidth = 700;
            MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _search = Ui.Input(watermark: "筛选配置键或值");
            _search.TextChanged += (_, _) => ApplyFilter();
            _tabControl = new TabControl();
            foreach (var category in categories)
            {
                var list = new ListBox
                {
                    ItemsSource = category.Lines,
                    ItemTemplate = Ui.Template<DiffLine>(line =>
                    {
                        var grid = Ui.Grid("Auto", "24,*");
                        var marker = Ui.T(line.Marker, 11.5, line.Brush, FontWeight.Bold);
                        marker.FontFamily = UiTheme.MonoFont;
                        var text = Ui.T(line.Text, 11.5, line.Brush, FontWeight.Normal, TextWrapping.Wrap);
                        text.FontFamily = UiTheme.MonoFont;
                        Ui.Place(marker, 0, 0);
                        Ui.Place(text, 0, 1);
                        grid.Children.Add(marker);
                        grid.Children.Add(text);
                        return grid;
                    })
                };
                var tab = new TabItem { Header = category.Name + " (" + category.Lines.Count + ")", Content = list };
                _tabControl.Items.Add(tab);
                _tabs.Add((tab, list, category));
            }
            if (_tabControl.Items.Count > 0) _tabControl.SelectedIndex = 0;

            var buttons = Ui.H(
                Ui.Btn("返回修改", () => Close(false)),
                Ui.Btn("确认保存", () => Close(true), "primary"));
            buttons.Spacing = 6;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var content = Ui.Grid("Auto,*,Auto", "*");
            var header = Ui.V(
                Ui.T("配置变更预览", 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("确认后才写入文件。", 11.5, UiTheme.TextDim),
                _search,
                Ui.Btn("导出差异", () => _ = ExportAsync()));
            header.Spacing = 8;
            Ui.Place(header, 0);
            Ui.Place(_tabControl, 1);
            _tabControl.Margin = new Thickness(0, 12, 0, 12);
            Ui.Place(buttons, 2);
            content.Children.Add(header);
            content.Children.Add(_tabControl);
            content.Children.Add(buttons);
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
        }

        private void ApplyFilter()
        {
            var query = (_search.Text ?? "").Trim();
            foreach (var item in _tabs)
            {
                var lines = string.IsNullOrWhiteSpace(query)
                    ? item.Category.Lines
                    : item.Category.Lines.Where(x => x.Text.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
                item.List.ItemsSource = lines;
                item.Tab.Header = item.Category.Name + " (" + lines.Count + ")";
            }
        }

        private async Task ExportAsync()
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = "配置差异-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("文本文件") { Patterns = new[] { "*.txt" } }
                }
            });
            if (file == null) return;
            var text = new StringBuilder();
            foreach (var item in _tabs)
            {
                text.AppendLine("[" + item.Category.Name + "]");
                if (item.List.ItemsSource != null)
                    foreach (var line in item.List.ItemsSource.Cast<DiffLine>())
                        text.AppendLine(line.Marker + " " + line.Text);
                text.AppendLine();
            }
            try
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
                await writer.WriteAsync(text.ToString());
            }
            catch (Exception ex)
            {
                await Ui.AlertAsync(this, "配置差异", "导出失败：" + ex.Message);
            }
        }
    }

    public sealed class ConfigSetting : INotifyPropertyChanged
    {
        public string DisplayName { get; set; } = "";
        public string Path { get; set; } = "";
        public string PathText { get; set; } = "";
        public List<string> Segments { get; set; } = new();
        public string Kind { get; set; } = "Text";
        public string TypeName { get; set; } = "文本";
        public string ValueText { get; set; } = "";
        public string RawJson { get; set; } = "";

        private string _colorHex = "#FFFFFF";
        public string ColorHex
        {
            get => _colorHex;
            set
            {
                if (_colorHex == value) return;
                _colorHex = value;
                Raise(nameof(ColorHex));
                Raise(nameof(ColorBrush));
            }
        }

        public IBrush ColorBrush
        {
            get
            {
                try { return new SolidColorBrush(Color.Parse(ColorHex)); }
                catch { return Brushes.White; }
            }
        }

        private bool _boolValue;
        public bool BoolValue
        {
            get => _boolValue;
            set
            {
                if (_boolValue == value) return;
                _boolValue = value;
                Raise(nameof(BoolValue));
                Raise(nameof(BoolDisplay));
            }
        }

        public string BoolDisplay => BoolValue ? "true" : "false";
        public event PropertyChangedEventHandler? PropertyChanged;
        private void Raise(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class SscInventoryItem : INotifyPropertyChanged
    {
        private int _netId;
        private string _stackText = "1";
        private int _stackValue = 1;
        private bool _favorited;

        public int NetId
        {
            get => _netId;
            set
            {
                if (_netId == value) return;
                _netId = value;
                Raise(nameof(NetId));
                Raise(nameof(DisplayName));
                Raise(nameof(Detail));
            }
        }

        public string StackText
        {
            get => _stackText;
            set
            {
                if (_stackText == value) return;
                _stackText = value;
                Raise(nameof(StackText));
                if (int.TryParse(value, out var parsed) && parsed >= 1)
                {
                    _stackValue = parsed;
                    Raise(nameof(StackValue));
                }
            }
        }

        public int StackValue
        {
            get => _stackValue;
            set
            {
                if (_stackValue == value) return;
                _stackValue = value;
                _stackText = value.ToString();
                Raise(nameof(StackValue));
                Raise(nameof(StackText));
            }
        }

        public int PrefixId { get; set; }
        public bool Favorited
        {
            get => _favorited;
            set
            {
                if (_favorited == value) return;
                _favorited = value;
                Raise(nameof(Favorited));
            }
        }

        public string DisplayName => ItemCatalog.NameOf(NetId);
        public string Detail => "netID " + NetId;
        public IReadOnlyList<PrefixOption> PrefixOptions => ItemCatalog.CommonPrefixes;
        public event PropertyChangedEventHandler? PropertyChanged;

        public void SetItem(ItemNameEntry item)
        {
            NetId = item.Id;
            Raise(nameof(DisplayName));
            Raise(nameof(Detail));
        }

        private void Raise(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public sealed class ServerSettingsWindow : Window
    {
        private sealed class ConfigDocument
        {
            public string FilePath { get; set; } = "";
            public ObservableCollection<ConfigSetting> Items { get; } = new();
        }

        private static readonly Dictionary<string, string> SscNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Enabled"] = "启用 SSC（服务端角色）",
            ["ServerSideCharacterSave"] = "服务端角色保存间隔",
            ["LogonDiscardThreshold"] = "登录丢弃阈值",
            ["StartingHealth"] = "初始生命值",
            ["StartingMana"] = "初始法力值",
            ["WarnPlayersAboutBypassPermission"] = "绕过权限时警告玩家",
            ["KeepPlayerAppearance"] = "保留玩家外观",
            ["StartingInventory"] = "初始物品"
        };

        private readonly ConfigDocument _config = new();
        private readonly ConfigDocument _ssc = new();
        private readonly ObservableCollection<SscInventoryItem> _inventoryItems = new();
        private readonly string _serverName;
        private readonly Func<string, string?>? _reloadCommand;
        private readonly TabControl _tabs;
        private readonly TabItem _tshockTab;
        private readonly TabItem _sscTab;
        private readonly TabItem _inventoryTab;
        private readonly TextBox _configSearch;
        private readonly TextBox _sscSearch;
        private readonly TextBlock _configCount;
        private readonly TextBlock _sscCount;
        private readonly StackPanel _configPanel;
        private readonly StackPanel _sscPanel;
        private readonly StackPanel _inventoryPanel;
        private readonly TextBlock _status;
        private readonly Button _reloadTshock;
        private readonly Button _reloadSsc;

        public bool RestartRequested { get; private set; }

        public ServerSettingsWindow(
            string serverName,
            string serverDirectory,
            Func<string, string?>? reloadCommand = null)
        {
            Title = "服务器设置";
            Width = 1040;
            Height = 760;
            MinWidth = 820;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;
            Background = UiTheme.Bg;
            Icon = App.AppIcon;
            FontFamily = UiTheme.BodyFont;
            _serverName = serverName;
            _reloadCommand = reloadCommand;

            var tshockDir = Path.Combine(serverDirectory, "tshock");
            LoadDocument(_config, Path.Combine(tshockDir, "config.json"), "config.json");
            var sscPath = Path.Combine(tshockDir, "sscconfig.json");
            LoadDocument(_ssc, sscPath, "sscconfig.json", true);
            LoadInventory(sscPath);

            _configSearch = Ui.Input(watermark: "按配置名、路径或当前值筛选");
            _sscSearch = Ui.Input(watermark: "按配置名、路径或当前值筛选");
            _configCount = Ui.T("", 10.5, UiTheme.TextDim);
            _sscCount = Ui.T("", 10.5, UiTheme.TextDim);
            _configPanel = new StackPanel { Spacing = 4 };
            _sscPanel = new StackPanel { Spacing = 4 };
            _inventoryPanel = new StackPanel { Spacing = 4 };
            _configSearch.TextChanged += (_, _) => RebuildSettings(_config, _configPanel, _configSearch, _configCount);
            _sscSearch.TextChanged += (_, _) => RebuildSettings(_ssc, _sscPanel, _sscSearch, _sscCount);
            _status = Ui.T("", 11, UiTheme.TextDim);
            _reloadTshock = Ui.Btn("保存并重载 TShock", () => _ = SaveAndReloadAsync("/reload", "TShock"));
            _reloadSsc = Ui.Btn("保存并重载 SSC", () => _ = SaveAndReloadAsync("/reload ssc", "SSC"));

            _tshockTab = new TabItem
            {
                Header = "TShock 配置",
                Content = BuildSettingsPage(_configSearch, _configCount, _configPanel)
            };
            _sscTab = new TabItem
            {
                Header = "SSC 配置",
                Content = BuildSettingsPage(_sscSearch, _sscCount, _sscPanel)
            };
            _inventoryTab = new TabItem
            {
                Header = "初始物品",
                Content = BuildInventoryPage()
            };
            _tabs = new TabControl
            {
                Items =
                {
                    _tshockTab,
                    _sscTab,
                    _inventoryTab,
                    new TabItem
                    {
                        Header = "更多设置",
                        Content = Ui.T("世界、备份、数据库、REST 与插件数据页可在后续版本继续扩展。",
                            12, UiTheme.TextDim, FontWeight.Normal, TextWrapping.Wrap)
                    }
                }
            };
            _tabs.SelectionChanged += (_, _) => UpdateTabActions();

            var buttons = Ui.H(
                Ui.Btn("取消", () => Close(false)),
                Ui.Btn("保存", () => _ = SaveAsync(), "primary"),
                _reloadTshock,
                _reloadSsc,
                Ui.Btn("保存并重启本服", () => _ = SaveRestartAsync(), "primary"),
                Ui.Btn("查看变更", () => _ = ShowDiffAsync()),
                Ui.Btn("回滚中心", () => _ = OpenRollbackAsync()));
            buttons.Spacing = 6;
            buttons.HorizontalAlignment = HorizontalAlignment.Right;
            var footer = Ui.Grid("*", "*,Auto");
            Ui.Place(_status, 0, 0);
            Ui.Place(buttons, 0, 1);
            footer.Children.Add(_status);
            footer.Children.Add(buttons);
            var content = Ui.Grid("Auto,*,Auto", "*");
            var header = Ui.V(
                Ui.T("服务器设置 — " + serverName, 16, UiTheme.Text, FontWeight.SemiBold),
                Ui.T("修改会先备份原文件；保存后按需重载或重启。", 11.5, UiTheme.TextDim));
            header.Spacing = 4;
            Ui.Place(header, 0);
            Ui.Place(_tabs, 1);
            _tabs.Margin = new Thickness(0, 12, 0, 12);
            Ui.Place(footer, 2);
            content.Children.Add(header);
            content.Children.Add(_tabs);
            content.Children.Add(footer);
            Content = new Grid
            {
                Margin = new Thickness(14),
                Children = { Ui.Card(content, 16, UiTheme.Surface) }
            };
            RebuildSettings(_config, _configPanel, _configSearch, _configCount);
            RebuildSettings(_ssc, _sscPanel, _sscSearch, _sscCount);
            RebuildInventory();
            UpdateTabActions();
        }

        private static Control BuildSettingsPage(TextBox search, TextBlock count, StackPanel panel)
        {
            var toolbar = Ui.Grid("*", "*,Auto");
            Ui.Place(search, 0, 0);
            Ui.Place(count, 0, 1);
            count.Margin = new Thickness(10, 0, 0, 0);
            toolbar.Children.Add(search);
            toolbar.Children.Add(count);
            var page = Ui.Grid("Auto,*", "*");
            Ui.Place(toolbar, 0);
            var scroll = new ScrollViewer { Content = panel };
            Ui.Place(scroll, 1);
            scroll.Margin = new Thickness(0, 8, 0, 0);
            page.Children.Add(toolbar);
            page.Children.Add(scroll);
            return page;
        }

        private Control BuildInventoryPage()
        {
            var add = Ui.Btn("添加物品", () => _ = AddInventoryAsync(), "primary", 86);
            var toolbar = Ui.Grid("*", "*,Auto");
            var hint = Ui.T("每行可设置中文物品、数量、前缀与收藏。", 11, UiTheme.TextDim);
            Ui.Place(hint, 0, 0);
            Ui.Place(add, 0, 1);
            toolbar.Children.Add(hint);
            toolbar.Children.Add(add);
            var page = Ui.Grid("Auto,*", "*");
            Ui.Place(toolbar, 0);
            var scroll = new ScrollViewer { Content = _inventoryPanel };
            Ui.Place(scroll, 1);
            scroll.Margin = new Thickness(0, 8, 0, 0);
            page.Children.Add(toolbar);
            page.Children.Add(scroll);
            return page;
        }

        private void UpdateTabActions()
        {
            var selected = _tabs.SelectedItem;
            _reloadTshock.IsVisible = selected == _tshockTab;
            _reloadSsc.IsVisible = selected == _sscTab || selected == _inventoryTab;
        }

        private static void LoadDocument(
            ConfigDocument document,
            string filePath,
            string fileName,
            bool skipStartingInventory = false)
        {
            document.FilePath = filePath;
            if (!File.Exists(filePath))
            {
                document.Items.Add(new ConfigSetting
                {
                    DisplayName = fileName,
                    Path = filePath,
                    Kind = "Info",
                    ValueText = "文件不存在"
                });
                return;
            }
            try
            {
                var root = JObject.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                Flatten(root, new List<string>(), document.Items, skipStartingInventory);
            }
            catch (Exception ex)
            {
                document.Items.Add(new ConfigSetting
                {
                    DisplayName = fileName,
                    Path = filePath,
                    Kind = "Info",
                    ValueText = "JSON 读取失败：" + ex.Message
                });
            }
        }

        private static void Flatten(
            JToken token,
            IList<string> path,
            ObservableCollection<ConfigSetting> items,
            bool skipStartingInventory = false)
        {
            if (token is JObject obj)
            {
                foreach (var property in obj.Properties())
                {
                    if (skipStartingInventory && path.Count == 1 && path[0] == "Settings" &&
                        string.Equals(property.Name, "StartingInventory", StringComparison.OrdinalIgnoreCase))
                        continue;
                    path.Add(property.Name);
                    Flatten(property.Value, path, items, skipStartingInventory);
                    path.RemoveAt(path.Count - 1);
                }
                return;
            }

            var setting = new ConfigSetting
            {
                PathText = string.Join(".", path),
                Path = string.Join(".", path),
                Segments = path.ToList(),
                DisplayName = path.Count > 0 ? path[^1] : "(root)"
            };
            if (skipStartingInventory && path.Count == 2 && path[0] == "Settings" &&
                SscNames.TryGetValue(path[1], out var localizedName))
            {
                setting.DisplayName = localizedName;
                setting.PathText = "SSC · " + localizedName;
            }

            if (token is JArray array && path.Count == 2 && path[0] == "Settings" &&
                path[1].Contains("颜色(RGB)", StringComparison.OrdinalIgnoreCase) && array.Count >= 3)
            {
                var red = Math.Clamp(array[0].Value<int>(), 0, 255);
                var green = Math.Clamp(array[1].Value<int>(), 0, 255);
                var blue = Math.Clamp(array[2].Value<int>(), 0, 255);
                setting.Kind = "Color";
                setting.TypeName = "颜色";
                setting.ColorHex = $"#{red:X2}{green:X2}{blue:X2}";
                setting.ValueText = setting.ColorHex;
            }
            else if (token is JArray jsonArray)
            {
                setting.Kind = "Complex";
                setting.TypeName = "JSON 数组";
                setting.RawJson = jsonArray.ToString(Formatting.Indented);
                setting.ValueText = setting.RawJson;
            }
            else if (token is JObject nested)
            {
                setting.Kind = "Complex";
                setting.TypeName = "JSON 对象";
                setting.RawJson = nested.ToString(Formatting.Indented);
                setting.ValueText = setting.RawJson;
            }
            else if (token is JValue value)
            {
                switch (value.Type)
                {
                    case JTokenType.Boolean:
                        setting.Kind = "Bool";
                        setting.TypeName = "布尔";
                        setting.BoolValue = value.Value<bool>();
                        break;
                    case JTokenType.Integer:
                        setting.Kind = "Text";
                        setting.TypeName = "整数";
                        setting.ValueText = value.ToString();
                        break;
                    case JTokenType.Float:
                        setting.Kind = "Text";
                        setting.TypeName = "小数";
                        setting.ValueText = value.ToString();
                        break;
                    case JTokenType.Null:
                        setting.Kind = "Text";
                        setting.TypeName = "空值";
                        setting.ValueText = "";
                        break;
                    default:
                        setting.Kind = "Text";
                        setting.TypeName = "文本";
                        setting.ValueText = value.ToString();
                        break;
                }
            }
            items.Add(setting);
        }

        private void RebuildSettings(
            ConfigDocument document,
            StackPanel panel,
            TextBox search,
            TextBlock count)
        {
            var query = (search.Text ?? "").Trim();
            var filtered = string.IsNullOrWhiteSpace(query)
                ? document.Items.ToList()
                : document.Items.Where(item =>
                        item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        item.PathText.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        item.ValueText.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            panel.Children.Clear();
            foreach (var setting in filtered) panel.Children.Add(BuildSettingRow(setting));
            count.Text = filtered.Count + " / " + document.Items.Count;
        }

        private Control BuildSettingRow(ConfigSetting setting)
        {
            Control editor;
            switch (setting.Kind)
            {
                case "Bool":
                {
                    var check = new CheckBox
                    {
                        IsChecked = setting.BoolValue,
                        Content = setting.BoolDisplay
                    };
                    check.IsCheckedChanged += (_, _) =>
                    {
                        setting.BoolValue = check.IsChecked == true;
                        check.Content = setting.BoolDisplay;
                    };
                    editor = check;
                    break;
                }
                case "Complex":
                {
                    var box = new TextBox
                    {
                        Text = setting.RawJson,
                        AcceptsReturn = true,
                        MinHeight = 64,
                        MaxHeight = 130,
                        FontFamily = UiTheme.MonoFont,
                        TextWrapping = TextWrapping.NoWrap
                    };
                    box.TextChanged += (_, _) => setting.RawJson = box.Text ?? "";
                    editor = box;
                    break;
                }
                case "Info":
                    editor = Ui.T(setting.ValueText, 11.5, UiTheme.TextDim,
                        FontWeight.Normal, TextWrapping.Wrap);
                    break;
                case "Color":
                {
                    var swatch = new Border
                    {
                        Width = 34,
                        Height = 26,
                        CornerRadius = new CornerRadius(4),
                        Background = setting.ColorBrush,
                        BorderBrush = UiTheme.BorderStrong,
                        BorderThickness = new Thickness(1)
                    };
                    var value = Ui.T(setting.ColorHex, 11.5);
                    var picker = Ui.Btn("调色板", () => _ = PickColorAsync(setting, swatch, value));
                    var row = Ui.H(swatch, value, picker);
                    row.Spacing = 8;
                    editor = row;
                    break;
                }
                default:
                {
                    var box = Ui.Input(setting.ValueText);
                    box.TextChanged += (_, _) => setting.ValueText = box.Text ?? "";
                    editor = box;
                    break;
                }
            }

            var labels = Ui.V(
                Ui.T(setting.DisplayName, 12, UiTheme.Text, FontWeight.SemiBold,
                    TextWrapping.NoWrap, TextTrimming.CharacterEllipsis),
                Ui.T(setting.PathText, 10.5, UiTheme.TextDim,
                    FontWeight.Normal, TextWrapping.NoWrap, TextTrimming.CharacterEllipsis));
            labels.Spacing = 2;
            ToolTip.SetTip(labels, setting.Path);
            var grid = Ui.Grid("Auto", "300,*");
            Ui.Place(labels, 0, 0);
            Ui.Place(editor, 0, 1);
            grid.Children.Add(labels);
            grid.Children.Add(editor);
            return Ui.Card(grid, 8, UiTheme.SurfaceRaised);
        }

        private void LoadInventory(string filePath)
        {
            _inventoryItems.Clear();
            if (!File.Exists(filePath)) return;
            try
            {
                var root = JObject.Parse(File.ReadAllText(filePath, Encoding.UTF8));
                if (root["Settings"]?["StartingInventory"] is not JArray array) return;
                foreach (var token in array.OfType<JObject>())
                {
                    var netId = token.Value<int?>("netID") ?? 0;
                    if (netId <= 0) continue;
                    _inventoryItems.Add(new SscInventoryItem
                    {
                        NetId = netId,
                        StackValue = token.Value<int?>("stack") ?? 1,
                        PrefixId = token.Value<int?>("prefix") ?? 0,
                        Favorited = token.Value<bool?>("favorited") ?? false
                    });
                }
            }
            catch { }
        }

        private void RebuildInventory()
        {
            _inventoryPanel.Children.Clear();
            foreach (var item in _inventoryItems) _inventoryPanel.Children.Add(BuildInventoryRow(item));
            if (_inventoryItems.Count == 0)
                _inventoryPanel.Children.Add(Ui.T("初始物品列表为空。", 11.5, UiTheme.TextDim));
        }

        private Control BuildInventoryRow(SscInventoryItem item)
        {
            var itemButton = Ui.Btn(item.DisplayName, () => _ = PickInventoryAsync(item), null, 190);
            var stack = Ui.Input(item.StackText);
            stack.Width = 64;
            stack.TextChanged += (_, _) => item.StackText = stack.Text ?? "1";
            var slider = new Slider
            {
                Minimum = 1,
                Maximum = 9999,
                Value = item.StackValue,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Width = 130
            };
            slider.ValueChanged += (_, e) =>
            {
                item.StackValue = (int)Math.Round(e.NewValue);
                if (stack.Text != item.StackText) stack.Text = item.StackText;
            };
            var prefix = Ui.Combo(item.PrefixOptions.Cast<object>());
            prefix.Width = 150;
            prefix.SelectedItem = item.PrefixOptions.FirstOrDefault(x => x.Id == item.PrefixId);
            prefix.SelectionChanged += (_, _) =>
            {
                if (prefix.SelectedItem is PrefixOption option) item.PrefixId = option.Id;
            };
            var favorite = new CheckBox { Content = "收藏", IsChecked = item.Favorited };
            favorite.IsCheckedChanged += (_, _) => item.Favorited = favorite.IsChecked == true;
            var delete = Ui.Btn("删除", () =>
            {
                _inventoryItems.Remove(item);
                RebuildInventory();
            }, "danger", 52);
            var row = Ui.H(itemButton, stack, slider, prefix, favorite, delete);
            row.Spacing = 6;
            return Ui.Card(row, 7, UiTheme.SurfaceRaised);
        }

        private async Task AddInventoryAsync()
        {
            var picker = new ItemPickerDialog();
            if (!await picker.ShowDialog<bool>(this) || picker.SelectedItem == null) return;
            _inventoryItems.Add(new SscInventoryItem
            {
                NetId = picker.SelectedItem.Id,
                StackText = "1",
                PrefixId = 0
            });
            RebuildInventory();
        }

        private async Task PickInventoryAsync(SscInventoryItem item)
        {
            var picker = new ItemPickerDialog();
            if (!await picker.ShowDialog<bool>(this) || picker.SelectedItem == null) return;
            item.SetItem(picker.SelectedItem);
            RebuildInventory();
        }

        private async Task PickColorAsync(ConfigSetting setting, Border swatch, TextBlock value)
        {
            var picker = new ColorPickerDialog(setting.ColorHex);
            if (!await picker.ShowDialog<bool>(this)) return;
            setting.ColorHex = picker.ColorHex;
            swatch.Background = setting.ColorBrush;
            value.Text = setting.ColorHex;
        }

        private static bool SetByPath(JObject root, IReadOnlyList<string> parts, JToken value)
        {
            JToken current = root;
            for (var i = 0; i < parts.Count; i++)
            {
                if (current is not JObject obj) return false;
                var property = obj.Property(parts[i], StringComparison.OrdinalIgnoreCase);
                if (property == null) return false;
                if (i == parts.Count - 1)
                {
                    property.Value = value;
                    return true;
                }
                current = property.Value;
            }
            return false;
        }

        private static bool TryBuildDocument(
            ConfigDocument document,
            JArray? inventory,
            out JObject root,
            out string message)
        {
            message = "";
            root = new JObject();
            if (!File.Exists(document.FilePath)) return true;
            try
            {
                root = JObject.Parse(File.ReadAllText(document.FilePath, Encoding.UTF8));
                foreach (var setting in document.Items)
                {
                    if (setting.Kind == "Info") continue;
                    JToken value;
                    if (setting.Kind == "Bool")
                    {
                        value = new JValue(setting.BoolValue);
                    }
                    else if (setting.Kind == "Color")
                    {
                        var color = Color.Parse(setting.ColorHex);
                        value = new JArray(color.R, color.G, color.B);
                    }
                    else if (setting.Kind == "Complex")
                    {
                        try { value = JToken.Parse(setting.RawJson); }
                        catch (Exception ex)
                        {
                            throw new FormatException(
                                $"{setting.DisplayName} 的 JSON 格式不正确：{ex.Message}");
                        }
                    }
                    else if (setting.TypeName == "整数")
                    {
                        if (!long.TryParse(setting.ValueText, out var number))
                            throw new FormatException($"{setting.DisplayName} 需要填整数。");
                        value = new JValue(number);
                    }
                    else if (setting.TypeName == "小数")
                    {
                        if (!double.TryParse(setting.ValueText, out var number))
                            throw new FormatException($"{setting.DisplayName} 需要填数字。");
                        value = new JValue(number);
                    }
                    else if (setting.TypeName == "空值" && string.IsNullOrWhiteSpace(setting.ValueText))
                    {
                        value = JValue.CreateNull();
                    }
                    else
                    {
                        value = new JValue(setting.ValueText ?? "");
                    }

                    if (!SetByPath(root, setting.Segments, value))
                        throw new InvalidOperationException("找不到配置项：" + setting.Path);
                }
                if (inventory != null &&
                    !SetByPath(root, new[] { "Settings", "StartingInventory" }, inventory))
                    throw new InvalidOperationException("找不到配置项：Settings.StartingInventory");
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private static bool SaveDocument(ConfigDocument document, JArray? inventory, out string message)
        {
            if (!TryBuildDocument(document, inventory, out var root, out message)) return false;
            if (!File.Exists(document.FilePath)) return true;
            try
            {
                try
                {
                    File.WriteAllText(document.FilePath + ".bak",
                        File.ReadAllText(document.FilePath, Encoding.UTF8),
                        new UTF8Encoding(false));
                }
                catch { }
                File.WriteAllText(document.FilePath, root.ToString(Formatting.Indented),
                    new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        private bool TryBuildDiffCategories(out List<DiffCategory> categories, out string error)
        {
            categories = new List<DiffCategory>();
            error = "";
            if (!TryBuildInventory(out var inventory, out error)) return false;
            if (!TryBuildDocument(_config, null, out var configNew, out error)) return false;
            if (!TryBuildDocument(_ssc, inventory, out var sscNew, out error)) return false;
            categories.Add(BuildLineDiff("TShock 配置", ReadText(_config.FilePath),
                configNew.ToString(Formatting.Indented)));
            categories.Add(BuildLineDiff("SSC 配置", ReadText(_ssc.FilePath),
                sscNew.ToString(Formatting.Indented)));
            var oldSsc = TryParseObject(ReadText(_ssc.FilePath));
            var oldInventory = oldSsc?["Settings"]?["StartingInventory"]?.ToString(Formatting.Indented) ?? "[]";
            var newInventory = sscNew["Settings"]?["StartingInventory"]?.ToString(Formatting.Indented) ?? "[]";
            categories.Add(BuildLineDiff("初始物品", oldInventory, newInventory));
            return true;
        }

        private async Task<bool> ConfirmDiffOrCancelAsync()
        {
            if (!TryBuildDiffCategories(out var categories, out var error))
            {
                await Ui.AlertAsync(this, "服务器设置", "无法生成变更预览：\n" + error);
                return false;
            }
            var changed = categories.Where(x => x.Lines.Count > 0).ToList();
            if (changed.Count == 0) return true;
            return await new ConfigDiffWindow(changed).ShowDialog<bool>(this);
        }

        private static string ReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : ""; }
            catch { return ""; }
        }

        private static JObject? TryParseObject(string text)
        {
            try { return JObject.Parse(text); }
            catch { return null; }
        }

        private static DiffCategory BuildLineDiff(string name, string oldText, string newText)
        {
            var oldLines = oldText.Replace("\r\n", "\n").Split('\n');
            var newLines = newText.Replace("\r\n", "\n").Split('\n');
            var lcs = new int[oldLines.Length + 1, newLines.Length + 1];
            for (var i = oldLines.Length - 1; i >= 0; i--)
                for (var j = newLines.Length - 1; j >= 0; j--)
                    lcs[i, j] = oldLines[i] == newLines[j]
                        ? lcs[i + 1, j + 1] + 1
                        : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
            var result = new DiffCategory { Name = name };
            var x = 0;
            var y = 0;
            while (x < oldLines.Length || y < newLines.Length)
            {
                if (x < oldLines.Length && y < newLines.Length && oldLines[x] == newLines[y])
                {
                    x++;
                    y++;
                }
                else if (y < newLines.Length &&
                         (x >= oldLines.Length || lcs[x, y + 1] >= lcs[x + 1, y]))
                {
                    result.Lines.Add(new DiffLine { Kind = "Add", Text = newLines[y] });
                    y++;
                }
                else
                {
                    result.Lines.Add(new DiffLine { Kind = "Remove", Text = oldLines[x] });
                    x++;
                }
            }
            return result;
        }

        private async Task<bool> SaveAllAsync()
        {
            if (!TryBuildInventory(out var inventory, out var inventoryError))
            {
                await Ui.AlertAsync(this, "服务器设置", "初始物品保存失败：\n" + inventoryError);
                return false;
            }
            if (!SaveDocument(_config, null, out var configError))
            {
                await Ui.AlertAsync(this, "服务器设置", "TShock 配置保存失败：\n" + configError);
                return false;
            }
            if (!SaveDocument(_ssc, inventory, out var sscError))
            {
                await Ui.AlertAsync(this, "服务器设置", "SSC 配置保存失败：\n" + sscError);
                return false;
            }
            return true;
        }

        private bool TryBuildInventory(out JArray inventory, out string error)
        {
            inventory = new JArray();
            error = "";
            try
            {
                foreach (var item in _inventoryItems)
                {
                    if (item.NetId <= 0) throw new FormatException("物品 netID 必须大于 0。");
                    if (!int.TryParse(item.StackText.Trim(), out var stack) || stack < 1)
                        throw new FormatException(ItemCatalog.NameOf(item.NetId) + " 的数量必须大于 0。");
                    inventory.Add(new JObject
                    {
                        ["netID"] = item.NetId,
                        ["prefix"] = item.PrefixId,
                        ["stack"] = stack,
                        ["favorited"] = item.Favorited
                    });
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                inventory = new JArray();
                return false;
            }
        }

        private async Task SaveAsync()
        {
            if (!await ConfirmDiffOrCancelAsync()) return;
            if (!await SaveAllAsync()) return;
            _status.Text = "已保存，重启服务器后生效";
            Close(true);
        }

        private async Task SaveRestartAsync()
        {
            if (!await ConfirmDiffOrCancelAsync()) return;
            if (!await Ui.ConfirmAsync(
                    this,
                    "确认保存并重启",
                    $"将保存 {_serverName} 当前所有配置页，并重启该服务器。",
                    "保存并重启",
                    true))
                return;
            if (!await SaveAllAsync()) return;
            RestartRequested = true;
            Close(true);
        }

        private async Task SaveAndReloadAsync(string command, string label)
        {
            if (!await ConfirmDiffOrCancelAsync()) return;
            if (!await SaveAllAsync()) return;
            if (_reloadCommand == null)
            {
                await Ui.AlertAsync(this, "服务器设置", $"当前服务器未运行，无法执行重载 {label}。");
                return;
            }
            var error = _reloadCommand(command);
            if (!string.IsNullOrWhiteSpace(error))
            {
                await Ui.AlertAsync(this, "服务器设置", $"保存成功，但重载 {label} 失败：\n" + error);
                _status.Text = $"已保存，重载 {label} 失败";
                return;
            }
            _status.Text = $"已保存并重载 {label}";
            Close(true);
        }

        private async Task ShowDiffAsync()
        {
            if (!TryBuildDiffCategories(out var categories, out var error))
            {
                await Ui.AlertAsync(this, "配置变更预览", "无法生成变更预览：\n" + error);
                return;
            }
            var changed = categories.Where(x => x.Lines.Count > 0).ToList();
            if (changed.Count == 0)
            {
                await Ui.AlertAsync(this, "配置变更预览", "当前配置没有未保存的变更。");
                return;
            }
            await new ConfigDiffWindow(changed, false).ShowDialog<bool>(this);
        }

        private async Task OpenRollbackAsync()
        {
            var backupRoot = ManagerConfig.Resolve(ManagerConfig.Instance.backupDir);
            var dialog = new BackupCenterWindow(_serverName, _config.FilePath, backupRoot);
            if (!await dialog.ShowDialog<bool>(this)) return;
            RestartRequested = true;
            Close(true);
        }
    }
}
