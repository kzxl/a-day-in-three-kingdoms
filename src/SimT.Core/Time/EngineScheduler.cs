using SimT.Contracts.Time;

namespace SimT.Core.Time;

/// <summary>
/// Sovereign implementation of <see cref="IEngineScheduler"/>.
/// Dispatches one-shot and recurring callbacks deterministically during engine ticks.
/// </summary>
public sealed class EngineScheduler : IEngineScheduler
{
    private sealed class TaskNode : IScheduledTask
    {
        public readonly Action Callback;
        public readonly double Interval;
        public readonly bool IsPeriodic;
        public double RemainingTime;
        public bool IsCompleted { get; private set; }
        public bool IsCancelled { get; private set; }

        public TaskNode(double delay, double interval, bool isPeriodic, Action callback)
        {
            RemainingTime = delay;
            Interval = interval;
            IsPeriodic = isPeriodic;
            Callback = callback;
        }

        public void Complete()
        {
            IsCompleted = true;
        }

        public void Cancel()
        {
            IsCancelled = true;
        }

        public void Dispose() => Cancel();
    }

    private readonly List<TaskNode> _tasks = new();
    private readonly List<TaskNode> _pendingAdd = new();
    private bool _isUpdating;

    public IScheduledTask ScheduleAfter(double delaySeconds, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var task = new TaskNode(Math.Max(0, delaySeconds), 0, false, callback);
        AddTask(task);
        return task;
    }

    public IScheduledTask SchedulePeriodic(double intervalSeconds, Action callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        var interval = Math.Max(0.001, intervalSeconds);
        var task = new TaskNode(interval, interval, true, callback);
        AddTask(task);
        return task;
    }

    private void AddTask(TaskNode task)
    {
        if (_isUpdating)
        {
            _pendingAdd.Add(task);
        }
        else
        {
            _tasks.Add(task);
        }
    }

    public void Update(double deltaTimeSeconds)
    {
        if (deltaTimeSeconds <= 0 || (_tasks.Count == 0 && _pendingAdd.Count == 0)) return;

        _isUpdating = true;
        try
        {
            for (var i = 0; i < _tasks.Count; i++)
            {
                var task = _tasks[i];
                if (task.IsCancelled || task.IsCompleted) continue;

                task.RemainingTime -= deltaTimeSeconds;
                if (task.RemainingTime <= 1e-7)
                {
                    task.Callback();

                    if (task.IsPeriodic && !task.IsCancelled)
                    {
                        task.RemainingTime += task.Interval;
                    }
                    else
                    {
                        task.Complete();
                    }
                }
            }
        }
        finally
        {
            _isUpdating = false;

            // Remove finished/cancelled tasks
            _tasks.RemoveAll(t => t.IsCancelled || t.IsCompleted);

            if (_pendingAdd.Count > 0)
            {
                _tasks.AddRange(_pendingAdd);
                _pendingAdd.Clear();
            }
        }
    }

    public void CancelAll()
    {
        foreach (var task in _tasks)
        {
            task.Cancel();
        }
        _tasks.Clear();

        foreach (var task in _pendingAdd)
        {
            task.Cancel();
        }
        _pendingAdd.Clear();
    }
}
