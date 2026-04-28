using System.Runtime.Serialization;
using FleetAutomate.Expressions;

namespace FleetAutomate.Persistence;

[DataContract(Name = "TestFlowDocument")]
public sealed class TestFlowDocument
{
    [DataMember(Name = "formatVersion", Order = 0)]
    public int FormatVersion { get; set; } = 2;

    [DataMember(Name = "id", Order = 1)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [DataMember(Name = "name", Order = 2)]
    public string Name { get; set; } = string.Empty;

    [DataMember(Name = "description", Order = 3)]
    public string Description { get; set; } = string.Empty;

    [DataMember(Name = "isEnabled", Order = 4)]
    public bool IsEnabled { get; set; } = true;

    [DataMember(Name = "actions", Order = 5)]
    public List<ActionDocument> Actions { get; set; } = [];

    [DataMember(Name = "environment", Order = 6)]
    public List<VariableDocument> Environment { get; set; } = [];

    [DataMember(Name = "globalElements", Order = 7)]
    public List<ElementDocument> GlobalElements { get; set; } = [];
}

[DataContract(Name = "ActionDocument")]
public sealed class ActionDocument
{
    [DataMember(Name = "id", Order = 0)]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [DataMember(Name = "typeId", Order = 1)]
    public string TypeId { get; set; } = string.Empty;

    [DataMember(Name = "version", Order = 2)]
    public int Version { get; set; } = 1;

    [DataMember(Name = "properties", Order = 3)]
    public List<PropertyDocument> Properties { get; set; } = [];

    [DataMember(Name = "children", Order = 4)]
    public List<ActionChildCollectionDocument> Children { get; set; } = [];
}

[DataContract(Name = "ActionChildCollection")]
public sealed class ActionChildCollectionDocument
{
    [DataMember(Name = "name", Order = 0)]
    public string Name { get; set; } = string.Empty;

    [DataMember(Name = "actions", Order = 1)]
    public List<ActionDocument> Actions { get; set; } = [];
}

[DataContract(Name = "Property")]
public sealed class PropertyDocument
{
    [DataMember(Name = "name", Order = 0)]
    public string Name { get; set; } = string.Empty;

    [DataMember(Name = "typeId", Order = 1)]
    public string TypeId { get; set; } = TypeIds.String;

    [DataMember(Name = "value", Order = 2)]
    public string? Value { get; set; }
}

[DataContract(Name = "Variable")]
public sealed class VariableDocument
{
    [DataMember(Name = "name", Order = 0)]
    public string Name { get; set; } = string.Empty;

    [DataMember(Name = "typeId", Order = 1)]
    public string TypeId { get; set; } = TypeIds.Object;

    [DataMember(Name = "value", Order = 2)]
    public string? Value { get; set; }
}

[DataContract(Name = "Element")]
public sealed class ElementDocument
{
    [DataMember(Name = "key", Order = 0)]
    public string Key { get; set; } = string.Empty;

    [DataMember(Name = "identifierType", Order = 1)]
    public string? IdentifierType { get; set; }

    [DataMember(Name = "identifier", Order = 2)]
    public string? Identifier { get; set; }
}
