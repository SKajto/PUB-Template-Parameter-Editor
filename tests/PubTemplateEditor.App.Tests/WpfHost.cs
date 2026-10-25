using System.Threading;
using System.Windows;
using Xunit;
using AppObject = PubTemplateEditor.App.App;

// WPF allows a single Application instance per AppDomain and needs a dedicated STA UI
// thread, so all shell tests share one host and run sequentially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace PubTemplateEditor.UiTests;

/// <summary>
/// Long-lived STA host that mirrors the real start-up path: the App component with its
/// App.xaml resources is created on a UI thread, then work is marshalled onto its dispatcher.
/// </summary>
public sealed class WpfHost : IDisposable
{
    private readonly Thread _uiThread;
    private readonly ManualResetEventSlim _started = new(false);

    public Application App { get; private set; } = null!;
    public System.Windows.Threading.Dispatcher Dispatcher { get; private set; } = null!;

    public WpfHost()
    {
        _uiThread = new Thread(Pump)
        {
            Name = "test-ui-thread",
            IsBackground = true,
        };
        _uiThread.SetApartmentState(ApartmentState.STA);
        _uiThread.Start();
        _started.Wait();
    }

    private void Pump()
    {
        Dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var app = new AppObject();
        app.InitializeComponent();
        App = app;
        _started.Set();
        System.Windows.Threading.Dispatcher.Run();
    }

    /// <summary>Runs work on the UI thread and waits for it, pumping queued continuations.</summary>
    public void Do(Action action) => Dispatcher.Invoke(action);

    public T Do<T>(Func<T> function) => Dispatcher.Invoke(function);

    public void Dispose()
    {
        try
        {
            Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.ContextIdle,
                new Action(() => Dispatcher.InvokeShutdown()));
            _uiThread.Join(TimeSpan.FromSeconds(5));
        }
        catch (Exception)
        {
            // Host teardown must never fail a completed test.
        }

        _started.Dispose();
    }
}

[CollectionDefinition("WPF shell")]
public class WpfShellCollection : ICollectionFixture<WpfHost>
{
}
