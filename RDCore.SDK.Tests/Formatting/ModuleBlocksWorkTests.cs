using RDCore.SDK.Formatting;
using RDCore.SDK.Model.AST;
using RDCore.SDK.Model.AST.Abstract;
using RDCore.SDK.Model.AST.Declarations;
using RDCore.SDK.Model.AST.Expressions;
using RDCore.SDK.Model.AST.Statements;
using RDCore.SDK.Model.Source;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace RDCore.Tests.Formatting;

/// <summary>
/// The work a line break typed into a module does: in a procedure that has its closer and no <c>#If</c> block around or in it, the lines and the nodes the
/// indenter looks at, and the text parsed again for a closer, are the procedure's, the same in a module of 100 lines as in one of 10,000. Anywhere else, the
/// whole module is looked at. Counted, not timed.
/// </summary>
/// <remarks>
/// The modules are procedures of four lines each (<c>Public Sub Pn()</c>, two <c>Beep</c> lines, <c>End Sub</c>), parsed by a small reader of their own
/// lines: a statement is a keyword statement, a block (<c>If x Then</c>, <c>For i = 1 To 3</c>, <c>Do While x</c>, <c>With x</c>, <c>Select Case x</c>)
/// without its closer is a block of its own line, and a directive is in no node, as the parser has it.
/// </remarks>
[TestClass]
public sealed class ModuleBlocksWorkTests
{
    private static readonly Uri Module = new("file:///c:/ws/Module1.bas");
    private static readonly int[] Procedures = [25, 250, 2_500];
    private static readonly CodeFormattingOptions Options = CodeFormattingOptions.Default;
    private static int _next;

    private static SyntaxNodeId Id() => new(Module.ToString(), [Interlocked.Increment(ref _next)]);

    private static SourceLocation At(int startLine, int startCharacter, int endLine, int endCharacter)
        => new() { Uri = Module, Range = new SourceRange(startLine, startCharacter, endLine, endCharacter) };

    // procedures of four lines, the one in the middle written as its lines say (| is the new line, empty), the lines above and below it around it.
    private static (string Text, int Line) ModuleOf(int procedures, string[] middle, string[]? above = null, string[]? below = null)
    {
        var lines = new List<string>();
        var line = -1;
        for (var index = 0; index < procedures; index++)
        {
            if (index == procedures / 2)
            {
                lines.AddRange(above ?? []);
                lines.Add($"Public Sub P{index}()");
                foreach (var text in middle)
                {
                    line = text == "|" ? lines.Count : line;
                    lines.Add(text == "|" ? string.Empty : text);
                }

                lines.Add("End Sub");
                lines.AddRange(below ?? []);
                continue;
            }

            lines.AddRange([$"Public Sub P{index}()", "    Beep", "    Beep", "End Sub"]);
        }

        return (string.Join("\r\n", lines) + "\r\n", line);
    }

    // the openers of the blocks these tests write, each with its closer.
    private static readonly Dictionary<string, string> Closers = new(StringComparer.Ordinal)
    {
        ["If x Then"] = "End If", ["For i = 1 To 3"] = "Next", ["Do While x"] = "Loop", ["With x"] = "End With", ["Select Case x"] = "End Select",
    };

    // what the parser has of the modules of these tests.
    private static ModuleParseResult Parse(string text)
    {
        var lines = text.Split("\r\n");
        var top = new List<SyntaxNode>();
        (int Line, string Name, List<SyntaxNode> Body)? member = null;
        var blocks = new Stack<(int Line, string Opener, List<SyntaxNode> Body)>();
        void Add(SyntaxNode node)
        {
            if (blocks.TryPeek(out var block))
            {
                block.Body.Add(node);
            }
            else if (member is { } procedure)
            {
                procedure.Body.Add(node);
            }
            else
            {
                top.Add(node);
            }
        }

        // a block the parser found no closer of is a block of its own line, the statements after it those of the block around it.
        void CutShort(int left = 0)
        {
            while (blocks.Count > left && blocks.TryPop(out var block))
            {
                Add(Block(block.Opener, block.Line, block.Line, []));
                foreach (var node in block.Body)
                {
                    Add(node);
                }
            }
        }

        for (var line = 0; line < lines.Length; line++)
        {
            var code = lines[line].Trim();
            var at = lines[line].Length - lines[line].TrimStart().Length;
            if (code.StartsWith("Public Sub ", StringComparison.Ordinal))
            {
                member = (line, code["Public Sub ".Length..code.IndexOf('(')], new List<SyntaxNode>());
            }
            else if (code == "End Sub" && member is { } procedure)
            {
                CutShort();
                top.Add(new MemberDeclarationNode(Id(), At(procedure.Line, 0, line, lines[line].Length), [.. procedure.Body], procedure.Name, MemberKind.Procedure));
                member = null;
            }
            else if (Closers.ContainsKey(code))
            {
                blocks.Push((line, code, new List<SyntaxNode>()));
            }
            else if (blocks.Select(block => Closers[block.Opener]).ToList().IndexOf(code) is >= 0 and var depth)
            {
                // a closer closes the innermost block it is the closer of: the blocks in that one have none.
                CutShort(blocks.Count - depth);
                var block = blocks.Pop();
                Add(Block(block.Opener, block.Line, line, block.Body));
            }
            else if (code.Length > 0 && !code.StartsWith('#'))
            {
                Add(new KeywordStatementNode(Id(), At(line, at, line, at + code.Length), code, []));
            }
        }

        if (member is { } open)
        {
            CutShort();
            top.Add(new MemberDeclarationNode(Id(), At(open.Line, 0, lines.Length - 1, 0), [.. open.Body], open.Name, MemberKind.Procedure));
        }

        return new ModuleParseResult
        {
            SyntaxTree = new ModuleNode(Id(), new SourceLocation { Uri = Module, Range = new SourceRange(SourcePosition.Zero, new SourcePosition(lines.Length - 1, 0)) },
                [.. top]),
        };

        // the block an opener starts, from its line to the line of its closer; its expressions are names, at where they are written.
        StatementNode Block(string opener, int start, int end, List<SyntaxNode> body)
        {
            var location = At(start, lines[start].Length - lines[start].TrimStart().Length, end, lines[end].Length);
            var statements = new StatementBlock([.. body]);
            ExpressionNode Name(string name)
            {
                var at = lines[start].IndexOf(name, location.Range.Start.Character, StringComparison.Ordinal);
                return new SimpleNameExpressionNode(Id(), At(start, at, start, at + name.Length), name);
            }

            return opener switch
            {
                "If x Then" => new IfBlockStatementNode(Id(), location, Name("x"), statements, [], null),
                "For i = 1 To 3" => new ForStatementNode(Id(), location, Name("i"), Name("1"), Name("3"), null, statements),
                "Do While x" => new DoWhileLoopStatementNode(Id(), location, Name("x"), statements),
                "With x" => new WithStatementNode(Id(), location, Name("x"), statements),
                _ => new SelectCaseStatementNode(Id(), location, Name("x"), [], null),
            };
        }
    }

    private static Task<ModuleParseResult> NotParsedAsync(string text, CancellationToken token)
        => throw new AssertFailedException("a line break that adds no closer parses nothing again");

    private static string[] Shown(IEnumerable<OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit> edits)
        => [.. edits.Select(edit => $"{edit.Range.Start.Line}:{edit.Range.Start.Character}-{edit.Range.End.Line}:{edit.Range.End.Character} '{edit.NewText}'")];

    private static string[] Shown(IEnumerable<IndentationEdit> edits)
        => [.. edits.Select(edit => $"{edit.Line}:{edit.Character}-{edit.EndLine}:{edit.EndCharacter} '{edit.NewText}'")];

    [TestMethod]
    public async Task ALineBreakInAProcedure_LooksAtTheProcedureAlone_WhateverTheSizeOfTheModule()
    {
        var work = new List<(int Lines, int Nodes)>();
        foreach (var procedures in Procedures)
        {
            var (text, line) = ModuleOf(procedures, ["    Beep", "|", "    Beep"]);
            var parse = Parse(text);
            var edits = await ModuleBlocks.OnLineBreakAsync(parse, text, line, Options, NotParsedAsync);
            work.Add((ModuleIndenter.LinesIndented, ModuleIndenter.NodesWalked));

            var indenting = Options with { EmptyLineHandlingMethod = EmptyLineHandlingMethod.Indent, VerticallySpaceProcedures = false };
            CollectionAssert.AreEqual(new[] { $"{line}:0-{line}:0 '    '" }, Shown(edits), "the new line is indented as the body it is in");
            CollectionAssert.AreEqual(Shown(ModuleIndenter.Indent(parse, text, indenting, line - 1, line)), Shown(edits),
                "the edits are those the whole module has for the two lines");
        }

        Assert.AreEqual(5, work[0].Lines, "the procedure's five lines are indented, and no other");
        Assert.IsGreaterThan(0, work[0].Nodes, "the procedure's nodes are walked");
        CollectionAssert.AreEqual(Enumerable.Repeat(work[0], work.Count).ToArray(), work.ToArray(), "the work is the same in a module of 100, 1,000 and 10,000 lines");
    }

    [TestMethod]
    public async Task ALineBreakAfterAnOpener_ParsesTheProcedureWithItsCloserAgain_WhateverTheSizeOfTheModule()
    {
        var work = new List<(int Lines, int Nodes)>();
        foreach (var procedures in Procedures)
        {
            var (text, line) = ModuleOf(procedures, ["    If x Then", "|", "    Beep"]);
            var parsed = new List<string>();
            var edits = await ModuleBlocks.OnLineBreakAsync(Parse(text), text, line, Options, (closed, _) =>
            {
                parsed.Add(closed);
                return Task.FromResult(Parse(closed));
            });
            work.Add((ModuleIndenter.LinesIndented, ModuleIndenter.NodesWalked));

            CollectionAssert.AreEqual(new[] { $"Public Sub P{procedures / 2}()\r\n    If x Then\r\n\r\n    End If\r\n    Beep\r\nEnd Sub\r\n" }, parsed,
                "the text parsed again is the procedure, from its header to its End Sub, with the closer in it");
            CollectionAssert.AreEqual(new[] { $"{line}:0-{line}:0 '        '", $"{line + 1}:0-{line + 1}:0 '    End If\r\n'" }, Shown(edits),
                "the new line is in the If's body, and the closer is at the level of the If");
        }

        Assert.IsGreaterThan(0, work[0].Lines, "the procedure's lines are indented");
        Assert.IsGreaterThan(0, work[0].Nodes, "the procedure's nodes are walked");
        CollectionAssert.AreEqual(Enumerable.Repeat(work[0], work.Count).ToArray(), work.ToArray(), "the work is the same in a module of 100, 1,000 and 10,000 lines");
    }

    [TestMethod]
    public async Task ALineBreakInAProcedureInAnIfDirective_LooksAtTheWholeModule()
    {
        foreach (var procedures in Procedures)
        {
            var (text, line) = ModuleOf(procedures, ["    Beep", "|", "    Beep"], above: ["#If VBA7 Then"], below: ["#End If"]);
            _ = await ModuleBlocks.OnLineBreakAsync(Parse(text), text, line, Options, NotParsedAsync);
            Assert.AreEqual(ModuleIndenter.LinesIn(text).Length, ModuleIndenter.LinesIndented, "an #If above the procedure reaches into it");
        }
    }

    [TestMethod]
    public async Task ALineBreakInAProcedureWithADirective_LooksAtTheWholeModule()
    {
        var (text, line) = ModuleOf(Procedures[0], ["#If VBA7 Then", "    Beep", "#End If", "|", "    Beep"]);
        var parse = Parse(text);
        var edits = await ModuleBlocks.OnLineBreakAsync(parse, text, line, Options, NotParsedAsync);

        var indenting = Options with { EmptyLineHandlingMethod = EmptyLineHandlingMethod.Indent, VerticallySpaceProcedures = false };
        Assert.AreEqual(ModuleIndenter.LinesIn(text).Length, ModuleIndenter.LinesIndented, "an #If block in the procedure is read from the module");
        CollectionAssert.AreEqual(Shown(ModuleIndenter.Indent(parse, text, indenting, line - 1, line)), Shown(edits));
    }

    [TestMethod]
    public async Task ALineBreakInAProcedureThatHasNoCloserYet_LooksAtTheWholeModule()
    {
        var (text, line) = ModuleOf(Procedures[0], ["    Beep", "|", "    Beep"]);
        text += "Public Sub Typed()\r\n    Beep\r\n\r\n";
        line = ModuleIndenter.LinesIn(text).Length - 2;
        _ = await ModuleBlocks.OnLineBreakAsync(Parse(text), text, line, Options, NotParsedAsync);

        Assert.AreEqual(ModuleIndenter.LinesIn(text).Length, ModuleIndenter.LinesIndented, "a procedure with no End Sub may end anywhere below");
    }

    [TestMethod]
    public void TheOpenBlocksAtALineOfAProcedure_AreFoundAmongItsOwnNodes_WhateverTheSizeOfTheModule()
    {
        var walked = new List<int>();
        foreach (var procedures in Procedures)
        {
            var (text, line) = ModuleOf(procedures, ["    If x Then", "|", "    Beep"]);
            var open = ModuleBlocks.OpenAt(Parse(text), text, line - 1);
            walked.Add(ModuleIndenter.NodesWalked);

            Assert.AreEqual(new ModuleBlock(line - 1, line - 1, "End If", false), open.Single(), "the If is open, and the procedure is closed");
        }

        Assert.IsGreaterThan(0, walked[0], "the procedure's nodes are walked");
        CollectionAssert.AreEqual(Enumerable.Repeat(walked[0], walked.Count).ToArray(), walked.ToArray(), "the same nodes in every module");
    }

    // a module whose last procedure ends it, with no line end after its End Sub: the procedure before it, then that one, written as its lines say.
    private const string Before = "Public Sub P0()\r\n    Beep\r\nEnd Sub\r\n";

    [TestMethod]
    [DataRow("    If x Then", "        ", "    End If", DisplayName = "an If block gets its End If")]
    [DataRow("    Select Case x", "    ", "    End Select", DisplayName = "a Select Case gets its End Select, the new line where its Case goes")]
    [DataRow("    For i = 1 To 3", "        ", "    Next", DisplayName = "a For loop gets its Next")]
    [DataRow("    Do While x", "        ", "    Loop", DisplayName = "a Do loop gets its Loop")]
    [DataRow("    With x", "        ", "    End With", DisplayName = "a With block gets its End With")]
    public async Task ALineBreakAfterAnOpener_InAProcedureThatEndsTheModuleWithNoLineEnd_ClosesTheBlock(string opener, string indented, string closer)
    {
        var text = Before + $"Public Sub P1()\r\n{opener}\r\n\r\nEnd Sub";
        var parsed = new List<string>();

        var edits = await ModuleBlocks.OnLineBreakAsync(Parse(text), text, 5, Options, (closed, _) =>
        {
            parsed.Add(closed);
            return Task.FromResult(Parse(closed));
        });

        CollectionAssert.AreEqual(new[] { $"Public Sub P1()\r\n{opener}\r\n\r\n{closer}\r\nEnd Sub" }, parsed,
            "the procedure is parsed again with the closer in it, and ends as the module does");
        CollectionAssert.AreEqual(new[] { $"5:0-5:0 '{indented}'", $"6:0-6:0 '{closer}\r\n'" }, Shown(edits));
        Assert.AreEqual(5, ModuleIndenter.LinesIndented, "the procedure's lines, the closer's with them, are indented, and no other");
    }

    [TestMethod]
    public async Task ALineBreakAfterAnOpenerInABlock_InAProcedureThatEndsTheModuleWithNoLineEnd_ClosesItAtItsOwnLevel()
    {
        var text = Before + "Public Sub P1()\r\n    If x Then\r\n        For i = 1 To 3\r\n\r\n    End If\r\nEnd Sub";

        var edits = await ModuleBlocks.OnLineBreakAsync(Parse(text), text, 6, Options, (closed, _) => Task.FromResult(Parse(closed)));

        CollectionAssert.AreEqual(new[] { "6:0-6:0 '            '", "7:0-7:0 '        Next\r\n'" }, Shown(edits));
    }
}
