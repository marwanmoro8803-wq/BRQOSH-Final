using System.Windows.Threading;

namespace Brqosh.Desktop;

internal sealed class SearchDebouncer
{
    private readonly DispatcherTimer _timer;
    private readonly Func<Task> _callback;

    public SearchDebouncer(TimeSpan delay, Func<Task> callback)
    {
        _callback = callback;
        _timer = new DispatcherTimer { Interval = delay };
        _timer.Tick += async (_, _) =>
        {
            _timer.Stop();
            await _callback();
        };
    }

    public void Trigger()
    {
        _timer.Stop();
        _timer.Start();
    }
}
