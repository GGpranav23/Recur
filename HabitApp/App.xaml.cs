using System;
using System.Threading;
using System.Windows;

namespace HabitApp;

public partial class App : Application
{
    // Unique name for this app. Keep it the same even after renaming the app.
    const string InstanceName = "HabitApp_SingleInstance_7F3A";
    static Mutex? _instanceMutex;
    static EventWaitHandle? _showSignal;

    protected override void OnStartup(StartupEventArgs e)
    {
        // Only allow one copy of the app to run.
        _instanceMutex = new Mutex(true, InstanceName, out bool isFirstCopy);
        if (!isFirstCopy)
        {
            // Another copy is already running: ask it to show its window, then quit.
            try { EventWaitHandle.OpenExisting(InstanceName + "_Show").Set(); } catch { }
            Environment.Exit(0);
            return;
        }

        // Listen for later copies asking this one to show itself.
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, InstanceName + "_Show");
        var listener = new Thread(() =>
        {
            while (_showSignal.WaitOne())
                Dispatcher.BeginInvoke(new Action(ShowMainWindow));
        });
        listener.IsBackground = true;
        listener.Start();

        base.OnStartup(e);

        Database.Initialize();
        Preferences.Load();

        // Give every window a title bar that matches the theme.
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) => Preferences.ApplyTitleBar((Window)sender)));

        // Create the main window. When started with "--tray" (at Windows startup),
        // keep it hidden so the app waits quietly in the tray.
        var main = new MainWindow();
        MainWindow = main;

        bool startInTray = Array.Exists(e.Args,
            arg => string.Equals(arg, "--tray", StringComparison.OrdinalIgnoreCase));
        if (!startInTray)
        {
            main.Show();
        }
    }

    // Brings the main window back from the tray and puts it in front.
    void ShowMainWindow()
    {
        var window = MainWindow;
        if (window == null) return;

        window.Show();
        if (window.WindowState == WindowState.Minimized)
            window.WindowState = WindowState.Normal;
        window.Activate();
        window.Topmost = true;
        window.Topmost = false;
    }
}