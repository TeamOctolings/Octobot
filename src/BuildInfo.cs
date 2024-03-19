namespace Octobot;

public static class BuildInfo
{
    public static string RepositoryUrl
    {
        get
        {
            return ThisAssembly.Git.RepositoryUrl;
        }
    }

    public static string IssuesUrl
    {
        get
        {
            return $"{RepositoryUrl}/issues";
        }
    }

    private static string Commit
    {
        get
        {
            return ThisAssembly.Git.Commit;
        }
    }

    private static string Branch
    {
        get
        {
            return ThisAssembly.Git.Branch;
        }
    }

    public static bool IsDirty
    {
        get
        {
            return ThisAssembly.Git.IsDirty;
        }
    }

    public static string Version
    {
        get
        {
            return IsDirty ? $"{Branch}-{Commit}-dirty" : $"{Branch}-{Commit}";
        }
    }
}
