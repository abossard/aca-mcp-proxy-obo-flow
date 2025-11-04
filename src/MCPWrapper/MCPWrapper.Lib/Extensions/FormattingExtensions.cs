using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MCPWrapper.Lib.Extensions;

public static class FormattingExtensions
{
    /// <summary>
    /// Gets a string property from a JsonElement, returning empty string if null or missing
    /// </summary>
    /// <param name="element">The JsonElement to extract the property from</param>
    /// <param name="propertyName">The name of the property to extract</param>
    /// <returns>The string value or empty string if null/missing</returns>
    public static string GetStringProperty(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && 
               property.ValueKind != JsonValueKind.Null ? 
               property.GetString() ?? string.Empty : 
               string.Empty;
    }

    /// <summary>
    /// Gets a decimal property from a JsonElement, parsing from string representation
    /// </summary>
    /// <param name="element">The JsonElement to extract the property from</param>
    /// <param name="propertyName">The name of the property to extract</param>
    /// <returns>The decimal value or null if unable to parse</returns>
    public static decimal? GetDecimalProperty(this JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property) && 
            property.ValueKind != JsonValueKind.Null)
        {
            var stringValue = property.GetString();
            if (!string.IsNullOrEmpty(stringValue) && 
                decimal.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
        }
        return null;
    }

    /// <summary>
    /// Parses SAP date format (/Date(milliseconds)/) to DateTime
    /// </summary>
    /// <param name="sapDateString">The SAP date string to parse</param>
    /// <returns>The parsed DateTime or null if unable to parse</returns>
    public static DateTime? ParseSapDate(this string? sapDateString)
    {
        if (string.IsNullOrEmpty(sapDateString))
            return null;

        // SAP date format: /Date(1492098664000)/
        var match = Regex.Match(sapDateString, @"/Date\((\d+)\)/");
        if (match.Success && long.TryParse(match.Groups[1].Value, out var milliseconds))
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).DateTime;
        }

        // Fallback to standard date parsing
        if (DateTime.TryParse(sapDateString, out var dateTime))
        {
            return dateTime;
        }

        return null;
    }

    /// <summary>
    /// Formats a DateTime to SAP date format (/Date(milliseconds)/)
    /// </summary>
    /// <param name="dateTime">The DateTime to format</param>
    /// <returns>The SAP formatted date string</returns>
    public static string ToSapDateFormat(this DateTime dateTime)
    {
        var utcDateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
        var milliseconds = ((DateTimeOffset)utcDateTime).ToUnixTimeMilliseconds();
        return $"/Date({milliseconds})/";
    }

}
