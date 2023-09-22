using System.Text.Json.Nodes;
using Remora.Results;

namespace Boyfriend.Data.Options;

public interface IOption
{
    string Name { get; }
    string Display(JsonNode settings);
    Result Set(JsonNode settings, string from);
    Result Reset(JsonNode settings);
}
