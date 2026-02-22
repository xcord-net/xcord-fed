using System.Text.RegularExpressions;
using System.Web;

namespace Xcord.Infrastructure.Services;

/// <summary>
/// Simple OpenGraph metadata parser for HTML content.
/// </summary>
public sealed class OpenGraphParser
{
    // Regex patterns for extracting OpenGraph meta tags
    private static readonly Regex OgTitleRegex = new(
        @"<meta\s+property=[""']og:title[""']\s+content=[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OgDescriptionRegex = new(
        @"<meta\s+property=[""']og:description[""']\s+content=[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OgImageRegex = new(
        @"<meta\s+property=[""']og:image[""']\s+content=[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex OgSiteNameRegex = new(
        @"<meta\s+property=[""']og:site_name[""']\s+content=[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ThemeColorRegex = new(
        @"<meta\s+name=[""']theme-color[""']\s+content=[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Fallback regex patterns for non-OpenGraph meta tags
    private static readonly Regex TitleTagRegex = new(
        @"<title>([^<]+)</title>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DescriptionMetaRegex = new(
        @"<meta\s+name=[""']description[""']\s+content=[""']([^""']+)[""']",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Parse OpenGraph metadata from HTML content.
    /// </summary>
    /// <param name="html">HTML content</param>
    /// <returns>Parsed OpenGraph data</returns>
    public OpenGraphData Parse(string html)
    {
        var data = new OpenGraphData();

        // Extract og:title (fallback to <title> tag)
        var titleMatch = OgTitleRegex.Match(html);
        if (titleMatch.Success)
        {
            data.Title = DecodeHtml(titleMatch.Groups[1].Value);
        }
        else
        {
            // Fallback to <title> tag
            var titleTagMatch = TitleTagRegex.Match(html);
            if (titleTagMatch.Success)
            {
                data.Title = DecodeHtml(titleTagMatch.Groups[1].Value);
            }
        }

        // Extract og:description (fallback to meta description)
        var descriptionMatch = OgDescriptionRegex.Match(html);
        if (descriptionMatch.Success)
        {
            data.Description = DecodeHtml(descriptionMatch.Groups[1].Value);
        }
        else
        {
            // Fallback to meta description
            var descMetaMatch = DescriptionMetaRegex.Match(html);
            if (descMetaMatch.Success)
            {
                data.Description = DecodeHtml(descMetaMatch.Groups[1].Value);
            }
        }

        // Extract og:image
        var imageMatch = OgImageRegex.Match(html);
        if (imageMatch.Success)
        {
            data.ImageUrl = DecodeHtml(imageMatch.Groups[1].Value);
        }

        // Extract og:site_name
        var siteNameMatch = OgSiteNameRegex.Match(html);
        if (siteNameMatch.Success)
        {
            data.SiteName = DecodeHtml(siteNameMatch.Groups[1].Value);
        }

        // Extract theme-color
        var themeColorMatch = ThemeColorRegex.Match(html);
        if (themeColorMatch.Success)
        {
            var color = themeColorMatch.Groups[1].Value.Trim();
            // Normalize color format (ensure # prefix for hex colors)
            if (!string.IsNullOrEmpty(color) && !color.StartsWith('#') && IsHexColor(color))
            {
                color = '#' + color;
            }
            data.Color = color;
        }

        return data;
    }

    /// <summary>
    /// Decode HTML entities in a string.
    /// </summary>
    private static string DecodeHtml(string text)
    {
        return HttpUtility.HtmlDecode(text);
    }

    /// <summary>
    /// Check if a string is a valid hex color (without # prefix).
    /// </summary>
    private static bool IsHexColor(string color)
    {
        return color.Length == 6 && color.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}

/// <summary>
/// Parsed OpenGraph metadata.
/// </summary>
public sealed class OpenGraphData
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public string? SiteName { get; set; }
    public string? Color { get; set; }
}
