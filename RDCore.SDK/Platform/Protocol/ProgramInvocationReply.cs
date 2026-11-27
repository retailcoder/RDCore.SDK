using NewtonsoftConverter = Newtonsoft.Json.JsonConverterAttribute;

namespace RDCore.SDK.Platform.Protocol;

// an invocation's result holds the procedure's value as JSON, which only System.Text.Json writes as the wire has it: the reply is written through it
// (SystemTextJsonBridge).

/// <summary>
/// <see cref="ProgramInvocationResult"/> as the answer to an invocation of a procedure on the platform's JSON-RPC wire.
/// </summary>
/// <remarks>
/// The platform's Newtonsoft serializers write <see cref="ProgramInvocationResult.Value"/> as the JSON it holds, as a client that has only the Protocol
/// package writes and reads it.
/// </remarks>
[NewtonsoftConverter(typeof(SystemTextJsonBridge<ProgramInvocationReply>))]
public sealed record class ProgramInvocationReply : ProgramInvocationResult
{
    /// <summary>
    /// An answer of a procedure that completed and returned no value.
    /// </summary>
    public ProgramInvocationReply() { }

    /// <summary>
    /// An answer of how an invocation ended.
    /// </summary>
    /// <param name="result">How the invocation ended.</param>
    public ProgramInvocationReply(ProgramInvocationResult result) : base(result) { }
}
