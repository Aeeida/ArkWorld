namespace Game.Shared.Core.Extensions;

public static class StringExtensions
{
    public static bool IsNullOrWhiteSpace(this string? value) =>
        string.IsNullOrWhiteSpace(value);

    public static string ToSnakeCase(this string value) =>
        string.Concat(value.Select((ch, i) =>
            i > 0 && char.IsUpper(ch) ? "_" + ch : ch.ToString())).ToLowerInvariant();
}

public static class SpanExtensions
{
    public static bool TryParseGuid(this ReadOnlySpan<char> span, out Guid result) =>
        Guid.TryParse(span, out result);

    public static int WriteUtf8(this Span<byte> destination, ReadOnlySpan<char> source) =>
        System.Text.Encoding.UTF8.GetBytes(source, destination);
}

public static class CollectionExtensions
{
    public static IReadOnlyList<T> AsReadOnlyList<T>(this IEnumerable<T> source) =>
        source as IReadOnlyList<T> ?? source.ToList().AsReadOnly();
}
