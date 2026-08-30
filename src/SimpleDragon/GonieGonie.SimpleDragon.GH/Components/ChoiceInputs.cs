using System.Text;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;

namespace GonieGonie.SimpleDragon.Grasshopper.Components;

/// <summary>
/// Registers finite, human-readable choices without exposing enum ordinals on the canvas.
/// </summary>
internal abstract class ChoiceInputs : GH_Component
{
    private ChoiceInputs()
        : base("Choice Inputs", "Choice Inputs", "Finite-choice input support.", "SimpleDragon", "Internal")
    {
    }

    internal static int Add<TManager>(
        TManager manager,
        string name,
        string nickname,
        string description,
        string defaultValue,
        params string[] allowedValues)
        where TManager : class
    {
        GH_InputParamManager typedManager = manager as GH_InputParamManager
            ?? throw new ArgumentException("A Grasshopper input manager is required.", nameof(manager));
        string[] choices = ValidateChoices(defaultValue, allowedValues);
        return typedManager.AddParameter(
            new ChoiceStringParam(defaultValue, choices),
            name,
            nickname,
            description + " Choices: " + string.Join(", ", choices.Select(Humanize)) + ".",
            GH_ParamAccess.item);
    }

    internal static int AddEnum<TEnum, TManager>(
        TManager manager,
        string name,
        string nickname,
        string description,
        TEnum defaultValue,
        params TEnum[] allowedValues)
        where TEnum : struct
        where TManager : class
    {
        EnsureEnum<TEnum>();
        TEnum[] choices = allowedValues.Length == 0
            ? ((TEnum[])Enum.GetValues(typeof(TEnum)))
            : allowedValues;
        return Add(
            manager,
            name,
            nickname,
            description,
            EnumName(defaultValue),
            choices.Select(EnumName).ToArray());
    }

    internal static string Parse(string value, string inputName, params string[] allowedValues)
    {
        string normalized = Normalize(value);
        string? match = allowedValues.FirstOrDefault(
            candidate => string.Equals(Normalize(candidate), normalized, StringComparison.Ordinal));
        return match ?? throw new ArgumentException(
            inputName + " must be " + string.Join(", ", allowedValues.Select(Humanize)) + ".",
            inputName);
    }

    internal static TEnum ParseEnum<TEnum>(
        string value,
        string inputName,
        params TEnum[] allowedValues)
        where TEnum : struct
    {
        EnsureEnum<TEnum>();
        TEnum[] choices = allowedValues.Length == 0
            ? ((TEnum[])Enum.GetValues(typeof(TEnum)))
            : allowedValues;
        string canonical = Parse(value, inputName, choices.Select(EnumName).ToArray());
        return (TEnum)Enum.Parse(typeof(TEnum), canonical, ignoreCase: false);
    }

    private static string[] ValidateChoices(string defaultValue, string[] allowedValues)
    {
        if (allowedValues is null || allowedValues.Length == 0)
        {
            throw new ArgumentException("At least one finite choice is required.", nameof(allowedValues));
        }

        string[] choices = allowedValues
            .Select(value => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("A finite choice cannot be blank.", nameof(allowedValues))
                : value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        _ = Parse(defaultValue, nameof(defaultValue), choices);
        return choices;
    }

    private static string EnumName<TEnum>(TEnum value)
        where TEnum : struct =>
        Enum.GetName(typeof(TEnum), value)
        ?? throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown enum value.");

    private static void EnsureEnum<TEnum>()
        where TEnum : struct
    {
        if (!typeof(TEnum).IsEnum)
        {
            throw new InvalidOperationException(typeof(TEnum).FullName + " is not an enum type.");
        }
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
    }

    private static string Humanize(string value)
    {
        var result = new StringBuilder(value.Length + 8);
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            char previous = index == 0 ? '\0' : value[index - 1];
            char next = index + 1 < value.Length ? value[index + 1] : '\0';
            bool wordBoundary = index > 0
                && (char.IsUpper(current) && (char.IsLower(previous) || char.IsUpper(previous) && char.IsLower(next))
                    || char.IsDigit(current) && !char.IsDigit(previous));
            if (wordBoundary)
            {
                result.Append(' ');
            }

            result.Append(current);
        }

        return result.ToString();
    }

    private sealed class ChoiceStringParam : Param_String
    {
        private readonly string[] _allowedValues;

        internal ChoiceStringParam(string defaultValue, string[] allowedValues)
        {
            _allowedValues = allowedValues;
            PersistentData.Append(new GH_String(defaultValue));
        }

        public override void AppendAdditionalMenuItems(ToolStripDropDown menu)
        {
            base.AppendAdditionalMenuItems(menu);
            Menu_AppendSeparator(menu);
            foreach (string value in _allowedValues)
            {
                string captured = value;
                Menu_AppendItem(
                    menu,
                    Humanize(captured),
                    (_, _) => Select(captured),
                    enabled: true,
                    @checked: IsSelected(captured));
            }
        }

        private bool IsSelected(string value)
        {
            GH_String? selected = PersistentData.AllData(true).OfType<GH_String>().SingleOrDefault();
            return selected is not null
                && string.Equals(selected.Value, value, StringComparison.Ordinal);
        }

        private void Select(string value)
        {
            RecordUndoEvent("Select " + Name);
            PersistentData.Clear();
            PersistentData.Append(new GH_String(value));
            OnObjectChanged(GH_ObjectEventType.PersistentData);
            ExpireSolution(true);
        }
    }
}
