using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Interactions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Asahi.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp), Shared]
public class BotModuleCodeFixProvider : CodeFixProvider
{
    public sealed override ImmutableArray<string> FixableDiagnosticIds => [BotModuleAnalyzer.DiagnosticId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null) return;

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var classDeclarationSyntax = root.FindToken(diagnosticSpan.Start).Parent?.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();
        
        if (classDeclarationSyntax is null) return;

        // Register three different code fixes
        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add Guild installation context",
                createChangedDocument: c => AddAttributesAsync(context.Document, classDeclarationSyntax, true, false, c),
                equivalenceKey: "AddGuildContext"),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add User installation context",
                createChangedDocument: c => AddAttributesAsync(context.Document, classDeclarationSyntax, false, true, c),
                equivalenceKey: "AddUserContext"),
            diagnostic);

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add both Guild and User installation contexts",
                createChangedDocument: c => AddAttributesAsync(context.Document, classDeclarationSyntax, true, true, c),
                equivalenceKey: "AddBothContexts"),
            diagnostic);
    }

    private static async Task<Document> AddAttributesAsync(
        Document document, 
        ClassDeclarationSyntax cd,
        bool includeGuild,
        bool includeUser,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        
        if (root == null || semanticModel == null) return document;

        var classSymbol = ModelExtensions.GetDeclaredSymbol(semanticModel, cd, cancellationToken);
        if (classSymbol == null) return document;

        var hasCommandContextType = false;
        var hasIntegrationType = false;

        foreach (var attribute in classSymbol.GetAttributes())
        {
            if (attribute.AttributeClass?.Name == nameof(CommandContextTypeAttribute))
                hasCommandContextType = true;
            else if (attribute.AttributeClass?.Name == nameof(IntegrationTypeAttribute))
                hasIntegrationType = true;
        }

        var newAttributes = new SyntaxList<AttributeListSyntax>();
        newAttributes = newAttributes.AddRange(cd.AttributeLists);

    if (!hasCommandContextType)
    {
        var contextArguments = new List<AttributeArgumentSyntax>();
        
        if (includeGuild)
        {
            contextArguments.Add(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(nameof(InteractionContextType)),
                        SyntaxFactory.IdentifierName(nameof(InteractionContextType.Guild)))));
        }
        
        if (includeUser)
        {
            contextArguments.Add(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(nameof(InteractionContextType)),
                        SyntaxFactory.IdentifierName(nameof(InteractionContextType.BotDm)))));
                        
            contextArguments.Add(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(nameof(InteractionContextType)),
                        SyntaxFactory.IdentifierName(nameof(InteractionContextType.PrivateChannel)))));
        }

        var commandContextAttr = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(nameof(CommandContextTypeAttribute)))
                .WithArgumentList(
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SeparatedList(contextArguments)))));
        
        newAttributes = newAttributes.Add(commandContextAttr);
    }

    if (!hasIntegrationType)
    {
        var integrationArguments = new List<AttributeArgumentSyntax>();
        
        if (includeGuild)
        {
            integrationArguments.Add(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(nameof(ApplicationIntegrationType)),
                        SyntaxFactory.IdentifierName(nameof(ApplicationIntegrationType.GuildInstall)))));
        }
        
        if (includeUser)
        {
            integrationArguments.Add(
                SyntaxFactory.AttributeArgument(
                    SyntaxFactory.MemberAccessExpression(
                        SyntaxKind.SimpleMemberAccessExpression,
                        SyntaxFactory.IdentifierName(nameof(ApplicationIntegrationType)),
                        SyntaxFactory.IdentifierName(nameof(ApplicationIntegrationType.UserInstall)))));
        }

        var integrationAttr = SyntaxFactory.AttributeList(
            SyntaxFactory.SingletonSeparatedList(
                SyntaxFactory.Attribute(SyntaxFactory.IdentifierName(nameof(IntegrationTypeAttribute)))
                .WithArgumentList(
                    SyntaxFactory.AttributeArgumentList(
                        SyntaxFactory.SeparatedList(integrationArguments)))));
        
        newAttributes = newAttributes.Add(integrationAttr);
    }

        var newClassDecl = cd.WithAttributeLists(newAttributes);
        var newRoot = root.ReplaceNode(cd, newClassDecl);
        
        return document.WithSyntaxRoot(newRoot);
    }
}