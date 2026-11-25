using RDCore.SDK.Model.AST.Directives;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.Source;
using RDCore.SDK.Model.Symbols;
using RDCore.SDK.Model.Values.Intrinsic;
using System.Collections.Immutable;

namespace RDCore.SDK.Model.AST.Declarations;

/// <summary>
/// Helpers over a parsed <see cref="ModuleNode"/>.
/// </summary>
public static class ModuleNodeExtensions
{
    /// <summary>
    /// The module name declared by the module-level <c>Attribute VB_Name</c> directive, or
    /// <c>null</c> when the module declares none.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <remarks>
    /// This is the authoritative programmatic name of a module; callers that also need the file-name
    /// fallback should route through <c>ModuleName.Resolve</c>. The name is the value of the directive
    /// <see cref="GetDeclaredNameDirective"/> gives, unquoted as a string literal.
    /// </remarks>
    public static string? GetDeclaredName(this ModuleNode module)
        => module.GetDeclaredNameDirective() is { } attribute ? Unquote(attribute.Value) : null;

    /// <summary>
    /// The module-level <c>Attribute VB_Name</c> directive that declares the module's name (<strong>MS-VBAL §4.2</strong>), or <see langword="null"/>
    /// when the module declares none: the first such directive the module writes.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <remarks>
    /// Its location is where the module says its name, which is where a diagnostic about that name is reported. A member-qualified attribute
    /// (<c>Attribute Foo.VB_Description</c>) carries a <see cref="AttributeDirectiveNode.Binding"/> and does not declare a module name.
    /// </remarks>
    public static AttributeDirectiveNode? GetDeclaredNameDirective(this ModuleNode module)
    {
        foreach (var child in module.Children)
        {
            if (child is AttributeDirectiveNode { Binding: null } attribute
                && string.Equals(attribute.Name, Tokens.VB_Name, StringComparison.OrdinalIgnoreCase))
            {
                return attribute;
            }
        }

        return null;
    }

    /// <summary>
    /// The <see cref="ModuleDirectives"/> a module declares, read from its <c>Option</c> directives and its <c>Attribute VB_PredeclaredId</c>,
    /// <c>VB_Creatable</c>, <c>VB_Exposed</c> and <c>VB_Extensible</c> declarations.
    /// </summary>
    /// <param name="module">The module, or <c>null</c> for a module that did not parse.</param>
    /// <returns>
    /// The module's directives, or <see cref="ModuleDirectives.None"/> when <paramref name="module"/> is
    /// <c>null</c>: a module nobody could read declares nothing, which is also what every default says.
    /// </returns>
    /// <remarks>
    /// Every consumer that needs the whole set reads it here rather than assembling its own, so a
    /// directive added to <see cref="ModuleDirectives"/> reaches all of them at once. <c>Strict</c> is
    /// not among them: it comes from an RD-VBA annotation rather than an <c>Option</c> directive.
    /// </remarks>
    public static ModuleDirectives GetModuleDirectives(this ModuleNode? module) => module is null
        ? ModuleDirectives.None
        : new ModuleDirectives(
            Explicit: module.HasOptionExplicit(),
            Compare: module.GetOptionCompare(),
            Base: module.GetOptionBase(),
            PrivateModule: module.HasOptionPrivateModule(),
            PredeclaredId: module.IsPredeclared(),
            Creatable: module.IsCreatable(),
            Extensible: module.IsExtensible(),
            Exposed: module.IsExposed());

    /// <summary>
    /// Whether the module declares <c>Option Private Module</c> (<strong>MS-VBAL §5.2.1.4</strong>).
    /// </summary>
    public static bool HasOptionPrivateModule(this ModuleNode module)
    {
        foreach (var child in module.Children)
        {
            if (child is ModuleOptionDirectiveNode { ModuleOption: ModuleOptions.OptionPrivateModule })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the module declares <c>Option Explicit</c> (<strong>MS-VBAL §5.2.1.3</strong>).
    /// </summary>
    public static bool HasOptionExplicit(this ModuleNode module)
    {
        foreach (var child in module.Children)
        {
            if (child is ModuleOptionDirectiveNode { ModuleOption: ModuleOptions.OptionExplicit })
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The comparison mode the module declares with an <c>Option Compare</c> directive (<strong>MS-VBAL §5.2.1.1</strong>),
    /// <see cref="OptionCompare.Binary"/> when it declares none.
    /// </summary>
    public static OptionCompare GetOptionCompare(this ModuleNode module)
    {
        foreach (var child in module.Children.OfType<ModuleOptionDirectiveNode>())
        {
            switch (child.ModuleOption)
            {
                case ModuleOptions.OptionCompareText:
                    return OptionCompare.Text;
                case ModuleOptions.OptionCompareDatabase:
                    return OptionCompare.Database;
                case ModuleOptions.OptionCompareBinary:
                    return OptionCompare.Binary;
            }
        }

        return OptionCompare.Binary;
    }

    /// <summary>
    /// The lower bound an array dimension declared without one takes in this module
    /// (<strong>MS-VBAL §5.2.1.2</strong>): <c>1</c> when the module declares <c>Option Base 1</c>,
    /// and <c>0</c> when it declares <c>Option Base 0</c> or no <c>Option Base</c> at all.
    /// </summary>
    public static int GetOptionBase(this ModuleNode module)
    {
        foreach (var child in module.Children.OfType<ModuleOptionDirectiveNode>())
        {
            switch (child.ModuleOption)
            {
                case ModuleOptions.OptionBase1:
                    return 1;
                case ModuleOptions.OptionBase0:
                    return 0;
            }
        }

        return 0;
    }

    /// <summary>
    /// Whether a class module is instantiable via <c>New</c>, per its <c>Attribute VB_Creatable</c>
    /// directive. Defaults to <c>true</c> — VBE's own default for a class module that declares no
    /// such attribute — so this is meaningful to call on any module, not just class modules.
    /// </summary>
    public static bool IsCreatable(this ModuleNode module)
    {
        foreach (var attribute in module.Children.OfType<AttributeDirectiveNode>())
        {
            if (attribute.Binding is null && string.Equals(attribute.Name, Tokens.VB_Creatable, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(attribute.Value.Trim(), "True", StringComparison.OrdinalIgnoreCase);
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a class module is accessible within the projects that reference its project, per its <c>Attribute VB_Exposed</c> directive
    /// (<strong>MS-VBAL §5.2.4.1.1</strong>). Defaults to <see langword="false"/>, the <em>Private</em> instancing mode, for a module that declares no
    /// such attribute.
    /// </summary>
    /// <param name="module">The module.</param>
    public static bool IsExposed(this ModuleNode module)
    {
        foreach (var attribute in module.Children.OfType<AttributeDirectiveNode>())
        {
            if (attribute.Binding is null && string.Equals(attribute.Name, Tokens.VB_Exposed, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(attribute.Value.Trim(), "True", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a class module has a predeclared default instance, per its
    /// <c>Attribute VB_PredeclaredId</c> directive (<strong>MS-VBAL §5.2.4.1.2</strong>). Defaults to
    /// <c>false</c> — VBE's own default for a class module that declares no such attribute.
    /// </summary>
    public static bool IsPredeclared(this ModuleNode module)
    {
        foreach (var attribute in module.Children.OfType<AttributeDirectiveNode>())
        {
            if (attribute.Binding is null && string.Equals(attribute.Name, Tokens.VB_PredeclaredId, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(attribute.Value.Trim(), "True", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a module is an extensible module (<strong>MS-VBAL §4.2.1</strong>) - what a host's document modules are -
    /// per its <c>Attribute VB_Extensible</c> directive. Defaults to <c>false</c>: a module that declares no such
    /// attribute is an ordinary one.
    /// </summary>
    public static bool IsExtensible(this ModuleNode module)
    {
        foreach (var attribute in module.Children.OfType<AttributeDirectiveNode>())
        {
            if (attribute.Binding is null && string.Equals(attribute.Name, Tokens.VB_Extensible, StringComparison.OrdinalIgnoreCase))
            {
                return string.Equals(attribute.Value.Trim(), "True", StringComparison.OrdinalIgnoreCase);
            }
        }

        return false;
    }

    /// <summary>
    /// The value of a member's own <c>Attribute &lt;member&gt;.VB_UserMemId</c> directive, or
    /// <c>null</c> when the module declares none for that member. RD-VBA's own use of the value:
    /// <c>WellKnownDispIds.Value</c> (<c>0</c>) denotes the class's default member,
    /// <c>WellKnownDispIds.NewEnum</c> (<c>-4</c>) its enumeration member.
    /// </summary>
    /// <remarks>
    /// Unlike a module-level attribute (<c>VB_Name</c>, <c>VB_Creatable</c>, ...), a member-qualified
    /// attribute statement is written lexically inside the member's own body — the parser attaches it
    /// as a child of that member's own <see cref="MemberDeclarationNode"/>, not the module's — so this
    /// searches the named member's <c>Children</c>, not the module's own <see cref="Abstract.SyntaxNode.Children"/> directly.
    /// </remarks>
    /// <param name="module">The module that declares the member.</param>
    /// <param name="memberName">The name of the member.</param>
    public static int? GetMemberUserMemId(this ModuleNode module, string memberName)
        => int.TryParse(module.GetMemberAttribute(memberName, Tokens.VB_UserMemId)?.Trim(), out var value) ? value : null;

    /// <summary>
    /// The text of the module's own <c>Attribute VB_Description</c> directive (<strong>RD-VBAL §3.1.1.7</strong>): what the module is, as the VBE's Object
    /// Browser shows it. <see langword="null"/> when the module declares none.
    /// </summary>
    /// <param name="module">The module.</param>
    /// <remarks>
    /// The value is a string literal: its quotes are not part of the text, and a doubled quote in it is one. An empty string describes nothing. A
    /// <c>'@ModuleDescription</c> annotation is trivia, which no node of the tree holds: <see cref="DocStrings.GetModuleDescription"/> reads the
    /// attribute, else the annotation.
    /// </remarks>
    public static string? GetModuleDescription(this ModuleNode module)
        => module.GetModuleDescriptionDirective() is { } attribute ? DescriptionOf(attribute.Value) : null;

    /// <summary>
    /// The module's own <c>Attribute VB_Description</c> directive that describes it (<see cref="GetModuleDescription(ModuleNode)"/>): the first one that
    /// describes something. <see langword="null"/> when the module declares none.
    /// </summary>
    /// <param name="module">The module.</param>
    public static AttributeDirectiveNode? GetModuleDescriptionDirective(this ModuleNode module)
        => module.Children.OfType<AttributeDirectiveNode>().FirstOrDefault(attribute => attribute.Binding is null
            && string.Equals(attribute.Name, Tokens.VB_Description, StringComparison.OrdinalIgnoreCase) && DescriptionOf(attribute.Value) is not null);

    /// <summary>
    /// The text of a member's own <c>Attribute &lt;member&gt;.VB_Description</c> directive (<strong>RD-VBAL §3.1.1.7</strong>): what the member does, as the
    /// VBE's Object Browser and a completion list show it. <see langword="null"/> when the module declares none for that member.
    /// </summary>
    /// <param name="module">The module that declares the member.</param>
    /// <param name="memberName">The name of the member, compared without regard to case.</param>
    /// <remarks>
    /// The value is a string literal: its quotes are not part of the text, and a doubled quote in it is one. An empty string describes nothing. A
    /// <c>'@Description</c> annotation is trivia, which no node of the tree holds: <see cref="DocStrings.GetMemberDescription"/> reads the attribute,
    /// else the annotation.
    /// </remarks>
    public static string? GetMemberDescription(this ModuleNode module, string memberName)
        => DescriptionOf(module.GetMemberAttribute(memberName, Tokens.VB_Description));

    /// <summary>
    /// The text of a module variable's <c>Attribute &lt;variable&gt;.VB_VarDescription</c> directive: what the variable holds, as the VBE's Object Browser
    /// shows it. <see langword="null"/> when the module declares none for that variable.
    /// </summary>
    /// <param name="module">The module that declares the variable.</param>
    /// <param name="variableName">The name of the variable, compared without regard to case.</param>
    /// <remarks>
    /// The directive is written at module level, after the variable's declaration, and not in a procedure's body as a member's attributes are. Its value is
    /// read as <see cref="GetMemberDescription(ModuleNode, string)"/> reads a member's. A <c>'@VariableDescription</c> annotation is trivia:
    /// <see cref="DocStrings.GetMemberDescription"/> reads the attribute, else the annotation.
    /// </remarks>
    public static string? GetVariableDescription(this ModuleNode module, string variableName)
        => module.Children.OfType<AttributeDirectiveNode>()
            .Where(attribute => string.Equals(attribute.Binding, variableName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(attribute.Name, Tokens.VB_VarDescription, StringComparison.OrdinalIgnoreCase))
            .Select(attribute => DescriptionOf(attribute.Value))
            .FirstOrDefault(text => text is not null);

    // the text of a description attribute's value, a string literal: null for one that is not a string, and for the empty string, which describes nothing.
    private static string? DescriptionOf(string? value)
        => value?.Trim() is { Length: >= 2 } literal && literal[0] == '"' && literal[^1] == '"'
            && VBStringValue.UnquoteLiteralToken(literal) is { Length: > 0 } text
            ? text
            : null;

    // the value of a member's own attribute, as it is written; the attribute is written in the member's body (see GetMemberUserMemId). The first a module
    // writes for that member, of the first member of that name that writes one.
    private static string? GetMemberAttribute(this ModuleNode module, string memberName, string attributeName)
        => MemberAttributesOf(module).TryGetValue((memberName, attributeName), out var value) ? value : null;

    // every member attribute of a module, by member and attribute name, gathered once for each module node: a symbol is made of each declaration of a
    // module, and asking each for its attributes by walking every member of the module is quadratic in their number - ten thousand declarations take
    // seconds. A node is immutable, so what is gathered of it stays true of it.
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<ModuleNode, Dictionary<(string Member, string Attribute), string>> _memberAttributes = [];

    private static Dictionary<(string Member, string Attribute), string> MemberAttributesOf(ModuleNode module)
        => _memberAttributes.GetValue(module, static node =>
        {
            var attributes = new Dictionary<(string Member, string Attribute), string>(MemberAttributeComparer.Instance);
            foreach (var member in node.Children.OfType<MemberDeclarationNode>())
            {
                foreach (var attribute in member.Children.OfType<AttributeDirectiveNode>())
                {
                    if (attribute.Binding is { } binding && string.Equals(binding, member.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        _ = attributes.TryAdd((binding, attribute.Name), attribute.Value);
                    }
                }
            }

            return attributes;
        });

    // member and attribute names are compared as VBA compares names: without regard to case.
    private sealed class MemberAttributeComparer : IEqualityComparer<(string Member, string Attribute)>
    {
        public static readonly MemberAttributeComparer Instance = new();

        public bool Equals((string Member, string Attribute) x, (string Member, string Attribute) y)
            => string.Equals(x.Member, y.Member, StringComparison.OrdinalIgnoreCase) && string.Equals(x.Attribute, y.Attribute, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string Member, string Attribute) obj)
            => HashCode.Combine(StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Member), StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Attribute));
    }

    /// <summary>
    /// The interface names named by this module's own <c>Implements</c> directives
    /// (<strong>MS-VBAL §5.2.4.2</strong>), exactly as written — unresolved, in source order. A
    /// project-qualified name (<c>Implements Project.IFoo</c>) yields just <c>IFoo</c>: RDCore only
    /// ever composes one project's modules at a time, so the qualifier can only ever mean this same
    /// project. A half-typed <c>Implements</c> with no name at all is skipped.
    /// </summary>
    public static ImmutableArray<string> GetImplementedInterfaceNames(this ModuleNode module)
        => [.. ImplementsDirectivesOf(module).Select(directive => directive.Name)];

    /// <summary>
    /// Where each of <see cref="GetImplementedInterfaceNames"/> is written: the file and the range of its <c>Implements</c> directive,
    /// one for each name and in the same order. Each location's file is the one its directive is written in.
    /// </summary>
    /// <param name="module">The module whose directives are read.</param>
    public static ImmutableArray<SourceLocation> GetImplementedInterfaceLocations(this ModuleNode module)
        => [.. ImplementsDirectivesOf(module).Select(directive => directive.Location)];

    private static IEnumerable<(string Name, SourceLocation Location)> ImplementsDirectivesOf(ModuleNode module)
    {
        foreach (var directive in module.Children.OfType<ImplementsDirectiveNode>())
        {
            var name = directive.NameExpression switch
            {
                SimpleNameExpressionNode simple => simple.IdentifierName,
                MemberAccessExpressionNode { Member: { } member } => member.IdentifierName,
                _ => null,
            };
            if (name is not null)
            {
                yield return (name, directive.Location);
            }
        }
    }

    // AttributeDirectiveNode.Value is the raw parse-tree text; a VB_Name value is a string literal.
    private static string? Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return VBStringValue.UnquoteLiteralToken(trimmed);
        }

        return trimmed.Length == 0 ? null : trimmed;
    }
}
