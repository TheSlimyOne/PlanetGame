using Godot;
using System.Threading.Tasks;
using System.Threading;
using System;

public abstract class Worker
{
    private Task _task;
    protected bool _isRunning = false;

    public bool Paused { get; protected set; } = true;
    public int UpdateIntervalMs = 500;
    protected ManualResetEvent _pauseEvent = new(true);
    private ManualResetEvent _loopCompleteEvent = new(true);

    public virtual void Start()
    {
        if (_task != null && !_task.IsCompleted) return;

        _isRunning = true;

        _pauseEvent.Set();
        _loopCompleteEvent.Set();
        Paused = false;

        _task = Task.Run(ProcessLoop);
    }

    public virtual void Stop()
    {
        if (!_isRunning) return;

        _isRunning = false;
        _pauseEvent.Set();

        _task?.Wait();

        _task = null;

        CleanupGPUResources();
    }

    protected virtual async Task ProcessLoop()
    {
        while (_isRunning)
        {
            try
            {
                _loopCompleteEvent.Reset();

                object invokeResult = Invoke();

                if (invokeResult is Task task)
                    await task;
                    
                _pauseEvent.WaitOne();
            }
            catch (Exception ex)
            {
                GD.PrintErr($"Error during Invoke(): {ex.Message}");
            }
            finally
            {
                await Task.Delay(UpdateIntervalMs);
            }
        }

        _loopCompleteEvent.Set();
    }

    public virtual void Resume()
    {
        if (_task == null || _task.IsCompleted)
            throw new InvalidOperationException("Cannot resume a stopped task");

        _pauseEvent.Set();
        Paused = false;
        GD.Print("Resumed");
    }

    public virtual void Pause()
    {
        if (_task == null || _task.IsCompleted)
            throw new InvalidOperationException("Cannot pause a stopped task");

        _pauseEvent.Reset();
        Paused = true;
        GD.Print("Paused");
    }

    public abstract object Invoke();
    protected abstract void CleanupGPUResources();
}
