using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.MVC.Models.Validation;

// Allows full URLs and paths starting with "/". The built-in [Url] rejects those paths, which we need for uploaded images.
public class RelativeOrAbsoluteUrlAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return true;

        s = s.Trim();
        if (s.StartsWith('/')) return true;

        return Uri.TryCreate(s, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp ||
                uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme == Uri.UriSchemeFtp);
    }
}
