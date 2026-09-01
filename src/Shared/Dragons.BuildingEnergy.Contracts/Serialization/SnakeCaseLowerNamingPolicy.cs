using System.Text;
using System.Text.Json;
using Dragons.BuildingEnergy.Contracts.Internal;

namespace Dragons.BuildingEnergy.Contracts;

/// <summary>
/// Converts property and enum names to stable lower snake case on every target framework.
/// </summary>
public sealed class SnakeCaseLowerNamingPolicy : JsonNamingPolicy
{
    private SnakeCaseLowerNamingPolicy()
    {
    }

    /// <summary>
    /// Gets the shared stateless naming policy.
    /// </summary>
    public static SnakeCaseLowerNamingPolicy Instance { get; } = new();

    /// <inheritdoc />
    public override string ConvertName(string name)
    {
        ContractGuard.NotNull(name, nameof(name));

        StringBuilder builder = new(name.Length + 4);
        bool previousWasSeparator = true;

        for (int index = 0; index < name.Length; index++)
        {
            char current = name[index];
            if (!char.IsLetterOrDigit(current))
            {
                if (!previousWasSeparator && builder.Length > 0)
                {
                    builder.Append('_');
                    previousWasSeparator = true;
                }

                continue;
            }

            bool isUpper = char.IsUpper(current);
            bool hasPrevious = index > 0;
            bool previousIsLowerOrDigit = hasPrevious
                && (char.IsLower(name[index - 1]) || char.IsDigit(name[index - 1]));
            bool acronymEndsHere = isUpper
                && hasPrevious
                && char.IsUpper(name[index - 1])
                && index + 1 < name.Length
                && char.IsLower(name[index + 1]);

            if (!previousWasSeparator && isUpper && (previousIsLowerOrDigit || acronymEndsHere))
            {
                builder.Append('_');
            }

            builder.Append(char.ToLowerInvariant(current));
            previousWasSeparator = false;
        }

        if (builder.Length > 0 && builder[builder.Length - 1] == '_')
        {
            builder.Length--;
        }

        return builder.ToString();
    }
}
