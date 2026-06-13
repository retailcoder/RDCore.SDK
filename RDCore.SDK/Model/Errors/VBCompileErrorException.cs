using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.SDK.Model.Errors
{
    /// <summary>
    /// An exception that represents a <em>semantic compile-time</em> error, thrown during the evaluation of <strong>static semantics</strong>.
    /// </summary>
    /// <param name="location">The document location of the faulted AST node.</param>
    /// <param name="id">The formal <c>VBCompileErrorId</c> value for this specific syntax error.</param>
    /// <param name="message">An optional error message. "Syntax error" unless specified otherwise.</param>
    /// <param name="verbose">An optional detailed message about the specific semantics involved in this error, as applicable.</param>
    public class VBCompileErrorException(Location location, VBCompileErrorId id, string message, string? verbose = null) 
        : SdkException($"{Exceptions.VBCompileError} {message}", verbose)
    {
        public VBCompileErrorId VBCompileErrorId { get; } = id;
        public Range Location { get; } = location.Range;
    }
}
