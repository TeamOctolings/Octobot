using System.Text.Json.Nodes;
using Remora.Results;

namespace TeamOctolings.Octobot.Data.Options;

public interface IGuildOption
{
    string Name { get; }
    string Display(JsonNode settings);
    Result Set(JsonNode settings, string from);
    Result Reset(JsonNode settings);
}
