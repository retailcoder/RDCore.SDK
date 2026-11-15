using System.Diagnostics.CodeAnalysis;

namespace RDCore.SDK.Runtime.Vbrun;

/// <summary>
/// What the <c>VBRUN.PropertyBag</c> a container gives a UserControl's <c>ReadProperties</c> event holds: the values the container keeps
/// for the placed control, read by name, as VB6 reads them while the program runs.
/// </summary>
/// <remarks>
/// <para>
/// The container gives each value by name, and the bag reads it when <c>ReadProperty</c> asks for it, so that a value the container works
/// out from the control (such as the size it keeps for it) is the control's at that time. Names are matched without regard to case.
/// </para>
/// <list type="table">
/// <listheader><term>Call</term><description>Result</description></listheader>
/// <item><term><c>ReadProperty</c> of a name the bag holds</term><description>The value, of the type the container gives, whatever the default.</description></item>
/// <item><term><c>ReadProperty</c> of another name, with a default</term><description>The default, as it is given.</description></item>
/// <item><term><c>ReadProperty</c> of another name, with no default</term><description>Run-time error 327.</description></item>
/// <item><term><c>ReadProperty</c> of an empty name</term><description>Run-time error 5.</description></item>
/// <item><term><c>WriteProperty</c></term><description>Run-time error 425: the bag is read only while the program runs.</description></item>
/// </list>
/// <para>
/// The object is the container's bag only. The bag that <c>New PropertyBag</c> makes, whose values a program writes and reads through
/// its <c>Contents</c>, is not modelled.
/// </para>
/// </remarks>
/// <typeparam name="TValue">The host's type of a value.</typeparam>
public sealed class PropertyBagObject<TValue>
    where TValue : notnull
{
    private readonly Dictionary<string, Func<TValue>> _values;

    /// <summary>
    /// Creates the bag a container gives a placed UserControl's <c>ReadProperties</c>.
    /// </summary>
    /// <param name="values">
    /// The values the container keeps for the control, by name, each read when it is asked for. A value the host does not read can throw
    /// its own exception, which <see cref="TryReadProperty(string, out TValue)"/> lets through.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="values"/>, a name or a value is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Two of <paramref name="values"/> have the same name, without regard to case.</exception>
    public PropertyBagObject(IEnumerable<KeyValuePair<string, Func<TValue>>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = new Dictionary<string, Func<TValue>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in values)
        {
            ArgumentNullException.ThrowIfNull(name, nameof(values));
            ArgumentNullException.ThrowIfNull(value, nameof(values));
            if (!_values.TryAdd(name, value))
            {
                throw new ArgumentException($"The bag holds two values named '{name}'.", nameof(values));
            }
        }
    }

    /// <summary>
    /// Reads a value, as <c>ReadProperty</c> does with no default.
    /// </summary>
    /// <param name="name">The value's name, in any case.</param>
    /// <param name="value">The value, when the bag holds one of that name.</param>
    /// <returns>
    /// <see cref="PropertyBagFailure.None"/> when the bag holds the value; <see cref="PropertyBagFailure.DataValueNotFound"/> when it does
    /// not; <see cref="PropertyBagFailure.InvalidProcedureCall"/> for an empty name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    public PropertyBagFailure TryReadProperty(string name, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(name);
        value = default;
        if (name.Length == 0)
        {
            return PropertyBagFailure.InvalidProcedureCall;
        }

        if (!_values.TryGetValue(name, out var read))
        {
            return PropertyBagFailure.DataValueNotFound;
        }

        value = read();
        return PropertyBagFailure.None;
    }

    /// <summary>
    /// Reads a value, as <c>ReadProperty</c> does with a default.
    /// </summary>
    /// <param name="name">The value's name, in any case.</param>
    /// <param name="defaultValue">The value read when the bag holds none of that name, as it is.</param>
    /// <param name="value">The value the bag holds, or <paramref name="defaultValue"/>.</param>
    /// <returns>
    /// <see cref="PropertyBagFailure.None"/>, or <see cref="PropertyBagFailure.InvalidProcedureCall"/> for an empty name.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="defaultValue"/> is <see langword="null"/>.</exception>
    public PropertyBagFailure TryReadProperty(string name, TValue defaultValue, [MaybeNullWhen(false)] out TValue value)
    {
        ArgumentNullException.ThrowIfNull(defaultValue);
        var failure = TryReadProperty(name, out value);
        if (failure == PropertyBagFailure.DataValueNotFound)
        {
            value = defaultValue;
            return PropertyBagFailure.None;
        }

        return failure;
    }

    /// <summary>
    /// Writes a value, as <c>WriteProperty</c> does while the program runs: the bag refuses it, and keeps what it holds.
    /// </summary>
    /// <param name="name">The value's name.</param>
    /// <param name="value">The value.</param>
    /// <returns><see cref="PropertyBagFailure.InvalidObjectUse"/>.</returns>
    public PropertyBagFailure TryWriteProperty(string name, TValue value) => PropertyBagFailure.InvalidObjectUse;
}

/// <summary>
/// Why a <see cref="PropertyBagObject{TValue}"/> call fails, as the run-time error number VB6 raises for it.
/// </summary>
public enum PropertyBagFailure
{
    /// <summary>
    /// The call succeeds.
    /// </summary>
    None = 0,

    /// <summary>
    /// The name is empty: run-time error 5, "Invalid procedure call or argument".
    /// </summary>
    InvalidProcedureCall = 5,

    /// <summary>
    /// The bag holds no value of that name, and no default is given: run-time error 327, "Data value named '…' not found".
    /// </summary>
    DataValueNotFound = 327,

    /// <summary>
    /// The bag is read only: run-time error 425, "Invalid object use".
    /// </summary>
    InvalidObjectUse = 425,
}
