using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("RDCore.SDK.Tests")]

// a type the protocol's requests carry, which a consumer built against an earlier package finds in this assembly.
[assembly: TypeForwardedTo(typeof(RDCore.SDK.Runtime.Abstract.Execution.ErrorBreakMode))]
