using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Msagl.Drawing;

namespace CSharpCodeAnalyst.Areas.GraphArea;

internal enum States
{
    Init,

    /// <summary>
    ///     Detected a left mouse down.
    /// </summary>
    AwaitSingleClick,

    /// <summary>
    ///     First mouse up was received.
    /// </summary>
    DetectedSingleClick,

    /// <summary>
    ///     Second left mouse down was received within the time limit.
    /// </summary>
    AwaitDoubleClick
}

/// <summary>
///     Emulates single and double click events since the graph control does not provide them.
///     I want to detect a double click without executing the single click action first.
///     Also, it is important to let the MSAGL LayoutEditor handles the mouse events first.
///     If the layout editor is not finished with its internal cleanup on mouse up,
///     replacing the graph crashes the application.
/// </summary>
internal class ClickController
{

    private readonly DispatcherTimer _timer = new();
    private readonly Microsoft.Msagl.WpfGraphControl.GraphViewer _viewer;
    private readonly Stopwatch _watch = Stopwatch.StartNew();

    private States _state = States.Init;

    /// <summary>
    ///     The object that was under the mouse cursor when the first click was detected.
    /// </summary>
    private IViewerObject? _targetObject;

    public ClickController(Microsoft.Msagl.WpfGraphControl.GraphViewer viewer, int doubleClickDelay = 300)
    {
        _viewer = viewer;
        _timer.Interval = TimeSpan.FromMilliseconds(doubleClickDelay);
        _timer.Stop();
        _timer.Tick += OnTimerTick;
        _watch.Restart();
        Register(viewer);
    }

    private bool IsShiftPressed()
    {
        return Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
    }

    private bool IsCtrlPressed()
    {
        return Keyboard.IsKeyDown(Key.LeftCtrl);
    }


    private void OnTimerTick(object? sender, EventArgs e)
    {
        _timer.Stop();

        if (_state == States.DetectedSingleClick)
        {
            // No second click within the time limit.
            // Interpret as single click.
            LeftSingleClick?.Invoke(_targetObject);
        }

        _state = States.Init;
    }


    // High-level events
    public event Action<IViewerObject?>? LeftSingleClick;
    public event Action<IViewerObject?>? LeftDoubleClick;
    public event Action<IViewerObject?>? OpenContextMenu;


    private void Register(Microsoft.Msagl.WpfGraphControl.GraphViewer msaglViewer)
    {
        // Important: The LayoutEditor has to handle the events first
        // and finish all cleanup work.

        msaglViewer.MouseDown += OnViewerMouseDown;
        msaglViewer.MouseUp += OnViewerMouseUp;
        msaglViewer.MouseMove += OnViewerMouseMove;
    }

    private static void OnViewerMouseMove(object? sender, MsaglMouseEventArgs e)
    {
        // _timer.Stop();
        // _state = States.Init;
    }

    private void OnViewerMouseUp(object? sender, MsaglMouseEventArgs e)
    {
        if (_state == States.AwaitSingleClick)
        {
            _state = States.DetectedSingleClick;
        }
        else if (_state == States.AwaitDoubleClick)
        {
            // Detected a second left mouse click within the time limit.
            _timer.Stop();
            LeftDoubleClick?.Invoke(_targetObject);
            _state = States.Init;
        }
    }

    private void OnViewerMouseDown(object? sender, MsaglMouseEventArgs e)
    {
        if (e.LeftButtonIsPressed)
        {
            if (_state == States.Init)
            {
                _timer.Stop();
                _targetObject = _viewer.ObjectUnderMouseCursor;
                _state = States.AwaitSingleClick;

                _timer.Start();
            }
            else if (_state == States.DetectedSingleClick)
            {
                // Second left click detected within time limit
                _state = States.AwaitDoubleClick;
            }
            else
            {
                // Unexpected state, reset
                _state = States.Init;
                _timer.Stop();
            }

            return;
        }

        if (e.RightButtonIsPressed)
        {
            OpenContextMenu?.Invoke(_targetObject);
            e.Handled = true;
        }
    }
}