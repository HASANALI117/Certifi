using System.ComponentModel.DataAnnotations;

namespace TrainingPlatform.MVC.Models.Validation;

// Accepts:
//   - null / empty / whitespace
//   - absolute URLs (http://, https://, ftp://)
//   - site-relative URLs starting with "/"  (e.g. "/images/foo.jpg")
// The built-in [Url] rejects relative URLs, which breaks editing any course
// whose image was uploaded locally or seeded from /wwwroot/images.
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
