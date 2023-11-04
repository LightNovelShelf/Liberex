using EFCore.NamingConventions.Internal;
using System.Globalization;
using System.Text.Json;

namespace Liberex.Internal;

public class SnakeCaseNamingPolicy : JsonNamingPolicy
{
    private readonly SnakeCaseNameRewriter _snakeCaseNameRewriter = new(CultureInfo.InvariantCulture);

    public static SnakeCaseNamingPolicy Instance { get; } = new SnakeCaseNamingPolicy();

    public override string ConvertName(string name)
    {
        return _snakeCaseNameRewriter.RewriteName(name);
    }
}