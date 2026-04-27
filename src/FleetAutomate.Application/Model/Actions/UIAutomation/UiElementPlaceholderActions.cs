using System.ComponentModel;
using System.Runtime.Serialization;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using FlaUI.UIA3;
using FleetAutomate.Helpers;
using FleetAutomate.Model.Flow;

namespace FleetAutomate.Model.Actions.UIAutomation;

public sealed record UiElementActionContext(
    string IdentifierType,
    string ElementIdentifier,
    string? SearchScope,
    bool AddToGlobalDictionary,
    string? GlobalDictionaryKey,
    GlobalElementDictionary? ElementDictionary);

public interface IUiElementAutomationBackend
{
    bool ElementExists(UiElementActionContext context);
    void Focus(UiElementActionContext context);
    void SelectRadioButton(UiElementActionContext context);
    void SetCheckBoxState(UiElementActionContext context, bool isChecked);
    void SelectComboBoxItem(UiElementActionContext context, string itemText);
    void SelectTabItem(UiElementActionContext context);
}

[DataContract]
public sealed class IfWindowContainsElementAction : UiElementActionBase
{
    public override string Name => "If Window Contains Element";
    protected override string DefaultDescription => "Check if window contains element";

    [IgnoreDataMember]
    public bool Exists { get; private set; }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Exists = Backend.ElementExists(CreateContext());
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SetFocusOnElementAction : UiElementActionBase
{
    public override string Name => "Set Focus on Element";
    protected override string DefaultDescription => "Focus on UI element";

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.Focus(CreateContext());
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SelectRadioButtonAction : UiElementActionBase
{
    public override string Name => "Select Radio Button";
    protected override string DefaultDescription => "Select radio button";

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.SelectRadioButton(CreateContext());
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SetCheckBoxStateAction : UiElementActionBase
{
    private bool _isChecked = true;

    public override string Name => "Set CheckBox State";
    protected override string DefaultDescription => "Check or uncheck checkbox";

    [DataMember]
    public bool IsChecked
    {
        get => _isChecked;
        set => SetProperty(ref _isChecked, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.SetCheckBoxState(CreateContext(), IsChecked);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SelectComboBoxItemAction : UiElementActionBase
{
    private string _itemText = string.Empty;

    public override string Name => "Select Item in ComboBox";
    protected override string DefaultDescription => "Select combobox item";

    [DataMember]
    public string ItemText
    {
        get => _itemText;
        set => SetProperty(ref _itemText, value);
    }

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.SelectComboBoxItem(CreateContext(), ItemText);
        return Task.CompletedTask;
    }
}

[DataContract]
public sealed class SelectTabItemAction : UiElementActionBase
{
    public override string Name => "Select Tab Item";
    protected override string DefaultDescription => "Switch to tab";

    protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        Backend.SelectTabItem(CreateContext());
        return Task.CompletedTask;
    }
}

[DataContract]
public abstract class UiElementActionBase : ActionBase, IUIElementAction, INotifyPropertyChanged
{
    private string _elementIdentifier = string.Empty;
    private string _identifierType = "XPath";
    private string? _searchScope;
    private bool _addToGlobalDictionary;
    private string? _globalDictionaryKey;
    private IUiElementAutomationBackend? _backend;

    [DataMember]
    public string ElementIdentifier
    {
        get => _elementIdentifier;
        set => SetProperty(ref _elementIdentifier, value);
    }

    [DataMember]
    public string IdentifierType
    {
        get => _identifierType;
        set => SetProperty(ref _identifierType, value);
    }

    [DataMember]
    public string? SearchScope
    {
        get => _searchScope;
        set => SetProperty(ref _searchScope, value);
    }

    [DataMember]
    public bool AddToGlobalDictionary
    {
        get => _addToGlobalDictionary;
        set => SetProperty(ref _addToGlobalDictionary, value);
    }

    [DataMember]
    public string? GlobalDictionaryKey
    {
        get => _globalDictionaryKey;
        set => SetProperty(ref _globalDictionaryKey, value);
    }

    [IgnoreDataMember]
    public GlobalElementDictionary? ElementDictionary { get; set; }

    [IgnoreDataMember]
    public IUiElementAutomationBackend Backend
    {
        get => _backend ??= FlaUiElementAutomationBackend.Instance;
        set => _backend = value ?? throw new ArgumentNullException(nameof(value));
    }

    protected UiElementActionContext CreateContext()
    {
        if (string.IsNullOrWhiteSpace(ElementIdentifier))
        {
            throw new InvalidOperationException("ElementIdentifier cannot be empty.");
        }

        return new UiElementActionContext(
            IdentifierType,
            ElementIdentifier,
            SearchScope,
            AddToGlobalDictionary,
            GlobalDictionaryKey,
            ElementDictionary);
    }
}

internal sealed class FlaUiElementAutomationBackend : IUiElementAutomationBackend
{
    public static FlaUiElementAutomationBackend Instance { get; } = new();

    private FlaUiElementAutomationBackend()
    {
    }

    public bool ElementExists(UiElementActionContext context)
    {
        using var automation = new UIA3Automation();
        return FindElement(automation, context, addToDictionary: false) != null;
    }

    public void Focus(UiElementActionContext context)
    {
        using var automation = new UIA3Automation();
        FindRequiredElement(automation, context).Focus();
    }

    public void SelectRadioButton(UiElementActionContext context)
    {
        using var automation = new UIA3Automation();
        var element = FindRequiredElement(automation, context);
        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return;
        }

        element.Click();
    }

    public void SetCheckBoxState(UiElementActionContext context, bool isChecked)
    {
        using var automation = new UIA3Automation();
        var element = FindRequiredElement(automation, context);
        if (!element.Patterns.Toggle.IsSupported)
        {
            throw new InvalidOperationException("Element does not support Toggle pattern.");
        }

        var toggle = element.Patterns.Toggle.Pattern;
        var current = toggle.ToggleState.ValueOrDefault == FlaUI.Core.Definitions.ToggleState.On;
        if (current != isChecked)
        {
            toggle.Toggle();
        }
    }

    public void SelectComboBoxItem(UiElementActionContext context, string itemText)
    {
        if (string.IsNullOrWhiteSpace(itemText))
        {
            throw new InvalidOperationException("ItemText cannot be empty.");
        }

        using var automation = new UIA3Automation();
        var element = FindRequiredElement(automation, context);
        element.AsComboBox().Select(itemText);
    }

    public void SelectTabItem(UiElementActionContext context)
    {
        using var automation = new UIA3Automation();
        var element = FindRequiredElement(automation, context);
        if (element.Patterns.SelectionItem.IsSupported)
        {
            element.Patterns.SelectionItem.Pattern.Select();
            return;
        }

        element.Click();
    }

    private static AutomationElement FindRequiredElement(AutomationBase automation, UiElementActionContext context)
    {
        return FindElement(automation, context, addToDictionary: true)
            ?? throw new InvalidOperationException($"Element '{context.ElementIdentifier}' was not found.");
    }

    private static AutomationElement? FindElement(AutomationBase automation, UiElementActionContext context, bool addToDictionary)
    {
        var desktop = automation.GetDesktop();
        var root = desktop;
        if (!string.IsNullOrWhiteSpace(context.SearchScope) && context.ElementDictionary != null)
        {
            root = context.ElementDictionary.GetElement(context.SearchScope) ?? desktop;
        }

        var element = UIAutomationHelper.FindElement(root, context.IdentifierType, context.ElementIdentifier, "UiElementAction");
        if (element != null && addToDictionary && context.AddToGlobalDictionary && context.ElementDictionary != null)
        {
            var key = string.IsNullOrWhiteSpace(context.GlobalDictionaryKey)
                ? context.ElementIdentifier
                : context.GlobalDictionaryKey;
            context.ElementDictionary.SetElement(key, element);
        }

        return element;
    }
}
