using System.Text.Json.Serialization;
namespace SimT.Contracts.Scenes;
public sealed partial record MapScene
{
    [JsonPropertyName("focusCityId")]
    public long? FocusCityId { get; init; }
}
