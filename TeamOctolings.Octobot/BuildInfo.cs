namespace TeamOctolings.Octobot;

public static class BuildInfo
{
    public const string RepositoryUrl = "https://github.com/TeamOctolings/Octobot";

    public const string IssuesUrl = $"{RepositoryUrl}/issues";

    public const string WikiUrl = $"{RepositoryUrl}/wiki";

    private const string Commit = ThisAssembly.Git.Commit;

    private const string Branch = ThisAssembly.Git.Branch;

    public static bool IsDirty => ThisAssembly.Git.IsDirty;

    public static string Version => IsDirty ? $"{Branch}-{Commit}-dirty" : $"{Branch}-{Commit}";
}
