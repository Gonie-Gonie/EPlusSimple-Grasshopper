using GH_IO.Serialization;
using Grasshopper.Kernel.Types;

namespace Dragons.SimpleDragon.Grasshopper.Types;

/// <summary>
/// Common Grasshopper wrapper for immutable SimpleDragon domain values.
/// </summary>
public abstract class SimpleDragonGoo<T> : GH_Goo<T>
    where T : class
{
    private const string HasValueKey = "HasValue";
    private const string SnapshotKey = "Snapshot";

    protected SimpleDragonGoo()
    {
    }

    protected SimpleDragonGoo(T value)
        : base(value)
    {
    }

    protected abstract SimpleDragonGoo<T> Create(T value);

    protected abstract SimpleDragonGoo<T> CreateEmpty();

    protected abstract string DisplayText(T value);

    protected virtual string? InvalidReason(T value)
    {
        return null;
    }

    public override bool IsValid => Value is not null && InvalidReason(Value) is null;

    public override string IsValidWhyNot => Value is null
        ? TypeName + " contains no value."
        : InvalidReason(Value) ?? string.Empty;

    public override IGH_Goo Duplicate()
    {
        return Value is null
            ? CreateEmpty()
            : Create(SimpleDragonGooSnapshot.Deserialize<T>(SimpleDragonGooSnapshot.Serialize(Value)));
    }

    public override bool CastFrom(object source)
    {
        switch (source)
        {
            case T value:
                Value = value;
                return true;
            case SimpleDragonGoo<T> goo when goo.Value is not null:
                Value = goo.Value;
                return true;
            case GH_ObjectWrapper wrapper when wrapper.Value is T wrapped:
                Value = wrapped;
                return true;
            default:
                return false;
        }
    }

#pragma warning disable CA1715 // Grasshopper fixes the public generic parameter name.
    public override bool CastTo<Q>(ref Q target)
    {
        if (Value is Q direct)
        {
            target = direct;
            return true;
        }

        if (Value is not null && typeof(Q).IsAssignableFrom(typeof(GH_ObjectWrapper)))
        {
            target = (Q)(object)new GH_ObjectWrapper(Value);
            return true;
        }

        if (typeof(Q) == typeof(string))
        {
            target = (Q)(object)ToString();
            return true;
        }

        return false;
    }
#pragma warning restore CA1715

    public override object? ScriptVariable()
    {
        return Value;
    }

    public override bool Write(GH_IWriter writer)
    {
        if (!base.Write(writer))
        {
            return false;
        }

        bool hasValue = Value is not null;
        writer.SetBoolean(HasValueKey, hasValue);
        if (hasValue)
        {
            writer.SetString(SnapshotKey, SimpleDragonGooSnapshot.Serialize(Value!));
        }

        return true;
    }

    public override bool Read(GH_IReader reader)
    {
        if (!base.Read(reader))
        {
            return false;
        }

        if (!reader.ItemExists(HasValueKey) || !reader.GetBoolean(HasValueKey))
        {
            Value = default!;
            return true;
        }

        Value = SimpleDragonGooSnapshot.Deserialize<T>(reader.GetString(SnapshotKey));
        return true;
    }

    public override string ToString()
    {
        return Value is null ? "Null " + TypeName : DisplayText(Value);
    }
}
