using System.Text.Json.Nodes;
using Remora.Results;

namespace Boyfriend.Data.Options;

public interface IOption {
    string Name { get; }
    object GetAsObject(JsonNode settings);
    Result Set(JsonNode         settings, string from);
}
