namespace TrainingPlatform.Reports.Services;

public interface INavLinks
{
    string MvcBaseUrl { get; }
    string ReportsBaseUrl { get; }

    string Mvc(string relativePath);
    string Reports(string relativePath);
}

public sealed class NavLinks : INavLinks
{
    public NavLinks(IConfiguration config)
    {
        MvcBaseUrl = (config["AppUrls:MvcBaseUrl"] ?? string.Empty).TrimEnd('/');
        ReportsBaseUrl = (config["AppUrls:ReportsBaseUrl"] ?? string.Empty).TrimEnd('/');
    }

    public string MvcBaseUrl { get; }
    public string ReportsBaseUrl { get; }

    public string Mvc(string relativePath) => Combine(MvcBaseUrl, relativePath);
    public string Reports(string relativePath) => Combine(ReportsBaseUrl, relativePath);

    private static string Combine(string baseUrl, string relativePath)
    {
        if (string.IsNullOrEmpty(baseUrl)) return relativePath;
        return $"{baseUrl}/{relativePath.TrimStart('/')}";
    }
}
