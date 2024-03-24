namespace Octobot;

public static class BuildInfo
{
    public static string RepositoryUrl => "https://github.com/TeamOctolings/Octobot";

    public static string IssuesUrl => $"{RepositoryUrl}/issues";

    public static string WikiUrl => $"{RepositoryUrl}/wiki";

    private static string Commit => ThisAssembly.Git.Commit;

    private static string Branch => ThisAssembly.Git.Branch;

    public static bool IsDirty => ThisAssembly.Git.IsDirty;

    public static string Version => IsDirty ? $"{Branch}-{Commit}-dirty" : $"{Branch}-{Commit}";
}
