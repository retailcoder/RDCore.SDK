using RDCore.SDK.Model.Symbols.VBProject;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.Symbols.Abstract;

/// <summary>
/// The document a symbol is written in (<see cref="WorkspaceSymbol.DocumentUri"/>), given to it and to the symbols it holds.
/// </summary>
public static class SymbolDocuments
{
    /// <summary>
    /// The symbol, written in <paramref name="document"/>: a <see cref="WorkspaceSymbol"/> with that <see cref="WorkspaceSymbol.DocumentUri"/>, and with it
    /// the symbols it holds, which are written where it is: its parameters and locals, the fields of a <c>Type</c>, and its
    /// <see cref="WorkspaceSymbol.Alternatives"/>.
    /// </summary>
    /// <param name="symbol">The symbol.</param>
    /// <param name="document">The document it is written in, or <see langword="null"/> to leave it as it is.</param>
    /// <returns>The symbol itself when <paramref name="document"/> is <see langword="null"/> or it is no <see cref="WorkspaceSymbol"/>; otherwise a copy of
    /// it in <paramref name="document"/>.</returns>
    public static Symbol InDocument(this Symbol symbol, Uri? document)
    {
        ArgumentNullException.ThrowIfNull(symbol);
        if (document is null || symbol is not WorkspaceSymbol written)
        {
            return symbol;
        }

        ImmutableArray<T> All<T>(ImmutableArray<T> symbols) where T : Symbol
            => symbols.IsDefaultOrEmpty ? symbols : [.. symbols.Select(nested => (T)nested.InDocument(document))];

        var alternatives = All(written.Alternatives);
        return written switch
        {
            VBProcedureMemberSymbol procedure => procedure with
            {
                DocumentUri = document, Alternatives = alternatives, Parameters = All(procedure.Parameters), Locals = All(procedure.Locals),
            },
            VBReturningMemberSymbol returning => returning with
            {
                DocumentUri = document, Alternatives = alternatives, Parameters = All(returning.Parameters), Locals = All(returning.Locals),
            },
            VBEventMemberSymbol @event => @event with { DocumentUri = document, Alternatives = alternatives, Parameters = All(@event.Parameters) },
            VBUserDefinedTypeMemberSymbol type => type with { DocumentUri = document, Alternatives = alternatives, Members = All(type.Members) },
            _ => written with { DocumentUri = document, Alternatives = alternatives },
        };
    }
}
