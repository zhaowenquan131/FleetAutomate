using System.ComponentModel;
using System.Runtime.Serialization;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Model.Actions.System
{
    [DataContract]
    public enum WaitDurationUnit
    {
        [EnumMember]
        Seconds,

        [EnumMember]
        Minutes
    }

    [DataContract]
    public sealed class WaitDurationAction : IPauseAwareAction, INotifyPropertyChanged
    {
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromMilliseconds(100);

        private int _duration = 1;
        private WaitDurationUnit _unit = WaitDurationUnit.Seconds;
        private ActionState _state = ActionState.Ready;
        private TimeSpan _remainingTime;
        private CancellationTokenSource? _executionCancellation;

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Name => "Wait";

        public string Description => State switch
        {
            ActionState.Running or ActionState.Paused => $"Wait for {Duration} {GetUnitText(Duration, Unit)} ({RemainingText})",
            _ => $"Wait for {Duration} {GetUnitText(Duration, Unit)}"
        };

        public ActionPauseBehavior PauseBehavior => ActionPauseBehavior.Cooperative;

        [IgnoreDataMember]
        public ActionState State
        {
            get => _state;
            set
            {
                if (_state != value)
                {
                    _state = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        public bool IsEnabled => true;

        [IgnoreDataMember]
        public TimeSpan RemainingTime
        {
            get => _remainingTime;
            private set
            {
                var normalized = value > TimeSpan.Zero ? value : TimeSpan.Zero;
                if (_remainingTime != normalized)
                {
                    _remainingTime = normalized;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingTime)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RemainingText)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                }
            }
        }

        [IgnoreDataMember]
        public string RemainingText => State switch
        {
            ActionState.Paused => $"paused, {FormatRemainingTime(RemainingTime)} remaining",
            ActionState.Running => $"{FormatRemainingTime(RemainingTime)} remaining",
            _ => string.Empty
        };

        [IgnoreDataMember]
        public double Progress
        {
            get
            {
                var total = GetDelay();
                if (total <= TimeSpan.Zero)
                {
                    return 1;
                }

                return Math.Clamp(1 - RemainingTime.TotalMilliseconds / total.TotalMilliseconds, 0, 1);
            }
        }

        [DataMember]
        public int Duration
        {
            get => _duration;
            set
            {
                var normalized = Math.Max(1, value);
                if (_duration != normalized)
                {
                    _duration = normalized;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Duration)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
                }
            }
        }

        [DataMember]
        public WaitDurationUnit Unit
        {
            get => _unit;
            set
            {
                if (_unit != value)
                {
                    _unit = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Unit)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Progress)));
                }
            }
        }

        public void Cancel()
        {
            _executionCancellation?.Cancel();
        }

        public async Task<bool> ExecuteAsync(CancellationToken cancellationToken)
        {
            var targetDuration = GetDelay();
            if (State != ActionState.Paused || RemainingTime <= TimeSpan.Zero || RemainingTime > targetDuration)
            {
                RemainingTime = targetDuration;
            }

            State = ActionState.Running;
            await Task.Yield();

            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _executionCancellation = linkedCancellation;

            try
            {
                while (RemainingTime > TimeSpan.Zero)
                {
                    var delay = RemainingTime < UpdateInterval ? RemainingTime : UpdateInterval;
                    await Task.Delay(delay, linkedCancellation.Token);
                    RemainingTime -= delay;
                }

                State = ActionState.Completed;
                return true;
            }
            catch (OperationCanceledException) when (linkedCancellation.IsCancellationRequested)
            {
                State = ActionState.Paused;
                return false;
            }
            catch
            {
                State = ActionState.Failed;
                return false;
            }
            finally
            {
                if (ReferenceEquals(_executionCancellation, linkedCancellation))
                {
                    _executionCancellation = null;
                }
            }
        }

        private TimeSpan GetDelay()
        {
            return Unit switch
            {
                WaitDurationUnit.Minutes => TimeSpan.FromMinutes(Duration),
                _ => TimeSpan.FromSeconds(Duration)
            };
        }

        private static string GetUnitText(int duration, WaitDurationUnit unit)
        {
            return unit switch
            {
                WaitDurationUnit.Minutes => duration == 1 ? "minute" : "minutes",
                _ => duration == 1 ? "second" : "seconds"
            };
        }

        private static string FormatRemainingTime(TimeSpan remaining)
        {
            if (remaining >= TimeSpan.FromMinutes(1))
            {
                return $"{Math.Ceiling(remaining.TotalMinutes)} min";
            }

            return $"{Math.Ceiling(remaining.TotalSeconds)} sec";
        }
    }
}
