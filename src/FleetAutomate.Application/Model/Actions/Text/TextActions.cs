using System.Globalization;
using System.Runtime.Serialization;

namespace FleetAutomate.Model.Actions.Text;

public enum TextCase
{
    Upper,
    Lower,
    Title
}

[DataContract]
public sealed class ChangeTextCaseAction : ActionBase
{
    private string _text = string.Empty;
    private TextCase _case = TextCase.Upper;

    public override string Name => "Change Text Case";
    protected override string DefaultDescription => "Convert text case";

    [DataMember]
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    [DataMember]
    public TextCase Case
    {
        get => _case;
        set => SetProperty(ref _case, value);
    }

    [IgnoreDataMember]
    public string Result { get; private set; } = string.Empty;

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Result = Case switch
        {
            TextCase.Lower => Text.ToLowerInvariant(),
            TextCase.Title => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Text.ToLower(CultureInfo.CurrentCulture)),
            _ => Text.ToUpperInvariant()
        };
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class ReplaceTextAction : ActionBase
{
    private string _text = string.Empty;
    private string _searchText = string.Empty;
    private string _replacementText = string.Empty;
    private bool _ignoreCase;

    public override string Name => "Replace Text";
    protected override string DefaultDescription => "Replace text";

    [DataMember]
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    [DataMember]
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    [DataMember]
    public string ReplacementText
    {
        get => _replacementText;
        set => SetProperty(ref _replacementText, value);
    }

    [DataMember]
    public bool IgnoreCase
    {
        get => _ignoreCase;
        set => SetProperty(ref _ignoreCase, value);
    }

    [IgnoreDataMember]
    public string Result { get; private set; } = string.Empty;

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            throw new InvalidOperationException("SearchText cannot be empty.");
        }

        Result = Text.Replace(
            SearchText,
            ReplacementText,
            IgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SubstringAction : ActionBase
{
    private string _text = string.Empty;
    private int _startIndex;
    private int _length;

    public override string Name => "Substring";
    protected override string DefaultDescription => "Extract part of text";

    [DataMember]
    public string Text
    {
        get => _text;
        set => SetProperty(ref _text, value);
    }

    [DataMember]
    public int StartIndex
    {
        get => _startIndex;
        set => SetProperty(ref _startIndex, value);
    }

    [DataMember]
    public int Length
    {
        get => _length;
        set => SetProperty(ref _length, value);
    }

    [IgnoreDataMember]
    public string Result { get; private set; } = string.Empty;

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        if (StartIndex < 0 || StartIndex > Text.Length)
        {
            throw new InvalidOperationException("StartIndex is outside the text range.");
        }

        if (Length < 0 || StartIndex + Length > Text.Length)
        {
            throw new InvalidOperationException("Length is outside the text range.");
        }

        Result = Text.Substring(StartIndex, Length);
        return Task.CompletedTask;
    }
}
