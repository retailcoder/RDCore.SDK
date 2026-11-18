namespace RDCore.SDK.Workspace.Forms;

/// <summary>
/// Reads a form's document: the file beside a form's code module that holds the form's controls, in a format a library of its own owns, such as the RDForms
/// document (<see cref="FormDesigner.RDFormsDocumentExtension"/>) of an RDForms form's <c>.rdform</c> module.
/// </summary>
/// <remarks>
/// The SDK says which document a module pairs with (<see cref="FormDesigner.RDFormsDocumentOf"/>) and what a form declares (<see cref="FormDescriptor"/>);
/// the library that owns the document's format reads it. A program that loads form modules registers its readers with
/// <see cref="FormDesigner.Register"/> before it reads one.
/// </remarks>
public interface IFormDocumentReader
{
    /// <summary>
    /// The extension of the documents it reads, with its dot, such as <c>.xaml</c>.
    /// </summary>
    string DocumentExtension { get; }

    /// <summary>
    /// What the form whose document is <paramref name="document"/> declares.
    /// </summary>
    /// <param name="document">The document's content, as its file holds it.</param>
    /// <returns>
    /// The form: of the <see cref="FormDesignerKind"/> its document is (<see cref="FormDesignerKind.RDForms"/> for an RDForms document), its properties as the
    /// document writes them, its controls, and the document's text as its <see cref="FormDescriptor.DesignerText"/>.
    /// </returns>
    /// <exception cref="FormDesignerFormatException">The document holds no form (<see cref="FormDesignerFormatProblem.InvalidDocument"/>).</exception>
    FormDescriptor Read(ReadOnlyMemory<byte> document);
}
