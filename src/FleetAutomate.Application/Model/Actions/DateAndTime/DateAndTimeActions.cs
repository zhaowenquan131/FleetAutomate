using System.Globalization;
using System.Runtime.Serialization;

namespace FleetAutomate.Model.Actions.DateAndTime;

public enum DateTimeUnit
{
    Seconds,
    Minutes,
    Hours,
    Days,
    Months,
    Years
}

[DataContract]
public sealed class GetCurrentDateTimeAction : ActionBase
{
    public override string Name => "Get Current Date/Time";
    protected override string DefaultDescription => "Get current date and time";

    [IgnoreDataMember]
    public DateTimeOffset Value { get; private set; }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Value = DateTimeOffset.Now;
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class FormatDateTimeAction : ActionBase
{
    private DateTimeOffset _value;
    private string _format = "O";
    private string _cultureName = CultureInfo.CurrentCulture.Name;

    public override string Name => "Format Date/Time";
    protected override string DefaultDescription => "Format a date and time value";

    [DataMember]
    public DateTimeOffset Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    [DataMember]
    public string Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    [DataMember]
    public string CultureName
    {
        get => _cultureName;
        set => SetProperty(ref _cultureName, value);
    }

    [IgnoreDataMember]
    public string Result { get; private set; } = string.Empty;

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var culture = string.IsNullOrWhiteSpace(CultureName)
            ? CultureInfo.CurrentCulture
            : CultureInfo.GetCultureInfo(CultureName);
        Result = Value.ToString(string.IsNullOrWhiteSpace(Format) ? "O" : Format, culture);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class AddDateTimeAction : ActionBase
{
    private DateTimeOffset _value;
    private double _amount;
    private DateTimeUnit _unit = DateTimeUnit.Days;

    public override string Name => "Add to Date/Time";
    protected override string DefaultDescription => "Add a duration to a date and time value";

    [DataMember]
    public DateTimeOffset Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    [DataMember]
    public double Amount
    {
        get => _amount;
        set => SetProperty(ref _amount, value);
    }

    [DataMember]
    public DateTimeUnit Unit
    {
        get => _unit;
        set => SetProperty(ref _unit, value);
    }

    [IgnoreDataMember]
    public DateTimeOffset Result { get; private set; }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Result = Unit switch
        {
            DateTimeUnit.Seconds => Value.AddSeconds(Amount),
            DateTimeUnit.Minutes => Value.AddMinutes(Amount),
            DateTimeUnit.Hours => Value.AddHours(Amount),
            DateTimeUnit.Months => Value.AddMonths(CheckedIntegerAmount()),
            DateTimeUnit.Years => Value.AddYears(CheckedIntegerAmount()),
            _ => Value.AddDays(Amount)
        };
        return Task.CompletedTask;
    }

    private int CheckedIntegerAmount()
    {
        if (Amount % 1 != 0)
        {
            throw new InvalidOperationException($"{Unit} requires a whole-number Amount.");
        }

        return checked((int)Amount);
    }
}
