using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord.Interactions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

namespace Asahi.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public class SlashCommandSummaryCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [SlashCommandSummaryAnalyzer.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var parameter = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ParameterSyntax>().First();
        
        if (parameter is null) return;

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add Summary attribute",
                createChangedDocument: c => AddSummaryAttributeAsync(context.Document, parameter, c),
                equivalenceKey: nameof(SlashCommandSummaryCodeFixProvider)),
            diagnostic);
    }

    private static async Task<Document> AddSummaryAttributeAsync(Document document, ParameterSyntax parameter, CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null) return document;

        var attributeList = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(
                    SyntaxFactory.IdentifierName(nameof(SummaryAttribute)),
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SingletonSeparatedList(
                            SyntaxFactory.AttributeArgument(
                                SyntaxFactory.LiteralExpression(
                                    SyntaxKind.StringLiteralExpression,
                                    SyntaxFactory.Literal($"Description for {parameter.Identifier.Text}")))
                            .WithNameColon(
                                SyntaxFactory.NameColon(
                                    // nameof doesnt work on parameters
                                    SyntaxFactory.IdentifierName("description"))))))));

        var newParameter = parameter.AddAttributeLists(attributeList.WithAdditionalAnnotations(Formatter.Annotation));

        var newRoot = root.ReplaceNode(parameter, newParameter);

        return document.WithSyntaxRoot(newRoot);
    }
}