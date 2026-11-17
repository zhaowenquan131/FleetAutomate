using System.Runtime.Serialization;

namespace FleetAutomate.Model.Project;

[DataContract]
public sealed class ProjectFileReference
{
    [DataMember]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [OnDeserializing]
    private void InitializeId(StreamingContext context) => Id = string.Empty;

    [OnDeserialized]
    private void RestoreLegacyId(StreamingContext context)
    {
        if (!string.IsNullOrWhiteSpace(Id)) return;
        var path = FilePath.Replace('/', '\\').ToUpperInvariant();
        Id = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(path)))[..32];
    }

    [DataMember]
    public string FilePath { get; set; } = string.Empty;
}

[DataContract]
public sealed class UiPathReference
{
    [DataMember]
    public string Name { get; set; } = string.Empty;

    [DataMember]
    public string XPath { get; set; } = string.Empty;
}
