using System.Runtime.Serialization;
using FlowEnvironment = FleetAutomate.Model.Actions.Logic.Environment;

namespace FleetAutomate.Expressions;

[DataContract]
public sealed class ExpressionDocument
{
    [DataMember(Order = 0)]
    public string TypeId { get; set; } = "text";

    [DataMember(Order = 1)]
    public int Version { get; set; } = 1;

    [DataMember(Order = 2)]
    public string RawText { get; set; } = string.Empty;

    [DataMember(Order = 3)]
    public string ResultTypeId { get; set; } = TypeIds.String;

    [DataMember(Order = 4)]
    public Dictionary<string, string?> Properties { get; set; } = [];
}

public sealed class ExpressionContext
{
    public static ExpressionContext Empty { get; } = new(new FlowEnvironment(), NullExpressionUiQueryService.Instance);

    public ExpressionContext(FlowEnvironment environment)
        : this(environment, DefaultExpressionUiQueryService.Instance)
    {
    }

    public ExpressionContext(FlowEnvironment environment, IExpressionUiQueryService uiQueryService)
    {
        Environment = environment ?? throw new ArgumentNullException(nameof(environment));
        UiQueryService = uiQueryService ?? throw new ArgumentNullException(nameof(uiQueryService));
    }

    public FlowEnvironment Environment { get; }

    public IExpressionUiQueryService UiQueryService { get; }
}

public interface IExpressionUiQueryService
{
    bool Exists(string elementPath);

    bool Exists(string elementPath, string identifierType, int retryTimes) => Exists(elementPath);

    bool ContainsText(string elementPath, string text);

    string? GetProperty(string elementPath, string propertyName);

    int Count(string elementPath);
}

public sealed record ExpressionResult(object? Value, Type ResultType);

public sealed record ExpressionValidationResult(bool IsValid, IReadOnlyList<string> Errors, Type? ResultType = null)
{
    public static ExpressionValidationResult Valid(Type resultType) => new(true, [], resultType);

    public static ExpressionValidationResult Invalid(params string[] errors) => new(false, errors, null);
}

public interface IExpressionEngine
{
    ExpressionValidationResult Validate(string expressionText, ExpressionContext context);

    Task<ExpressionResult> EvaluateAsync(string expressionText, ExpressionContext context, CancellationToken cancellationToken);
}

public static class TypeIds
{
    public const string Object = "object";
    public const string String = "string";
    public const string Int = "int";
    public const string Double = "double";
    public const string Bool = "bool";
    public const string DateTimeOffset = "datetime";

    public static string FromType(Type? type)
    {
        if (type == typeof(string))
        {
            return String;
        }

        if (type == typeof(int))
        {
            return Int;
        }

        if (type == typeof(double) || type == typeof(float) || type == typeof(decimal))
        {
            return Double;
        }

        if (type == typeof(bool))
        {
            return Bool;
        }

        if (type == typeof(DateTimeOffset) || type == typeof(DateTime))
        {
            return DateTimeOffset;
        }

        return Object;
    }

    public static Type ToType(string? typeId)
    {
        return typeId switch
        {
            String => typeof(string),
            Int => typeof(int),
            Double => typeof(double),
            Bool => typeof(bool),
            DateTimeOffset => typeof(DateTimeOffset),
            _ => typeof(object)
        };
    }
}
