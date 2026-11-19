using RDCore.SDK.Model.Values.Abstract;

namespace RDCore.SDK.Semantics;

/// <summary>
/// A conditional-compilation constant of the project (<strong>MS-VBAL §5.6.10</strong>, the conditional compilation binding context): one that a
/// <c>.rdproj</c> <c>#Const</c> or a <c>--define</c> argument defines, with the value the host's session gives it.
/// </summary>
/// <param name="Name">The name of the constant.</param>
/// <param name="Value">The value of the constant.</param>
public sealed record class ConditionalConstantFact(string Name, VBTypedValue Value);
