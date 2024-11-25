using System.Collections.Immutable;
using System.Linq;
using Discord.Interactions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Asahi.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class BotModuleAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DA0002";
    private const string Title = "Bot module is missing context definitions";
    private const string MessageFormat = "Bot module '{0}' is missing context definitions, specifically: {1}";
    private const string Description = "Bot modules should explicitly define what contexts they can be used in.";
    private const string Category = "Usage";

#pragma warning disable RS2008 // Enable analyzer release tracking
    private static readonly DiagnosticDescriptor Rule =
        new(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true,
            description: Description);
#pragma warning restore RS2008 // Enable analyzer release tracking

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterSyntaxNodeAction(AnalyzeClass, SyntaxKind.ClassDeclaration);
    }

    private static bool InheritsFromInteractionModuleBase(ITypeSymbol? type)
    {
        while (type != null)
        {
            if (type.Name == nameof(InteractionModuleBase))
            {
                return true;
            }
            type = type.BaseType;
        }
        return false;
    }

    private static bool HasSlashCommands(INamedTypeSymbol classSymbol)
    {
        // check methods in this class
        foreach (var member in classSymbol.GetMembers())
        {
            if (member is IMethodSymbol method)
            {
                foreach (var attribute in method.GetAttributes())
                {
                    if (attribute.AttributeClass?.Name == nameof(SlashCommandAttribute))
                    {
                        return true;
                    }
                }
            }
        }

        // check nested classes
        foreach (var member in classSymbol.GetTypeMembers())
        {
            if (HasSlashCommands(member))
            {
                return true;
            }
        }

        return false;
    }

    private static INamedTypeSymbol? FindTopmostModuleParent(INamedTypeSymbol classSymbol)
    {
        INamedTypeSymbol? currentClass = classSymbol;
        INamedTypeSymbol? topmostModule = null;

        // traverse up through the containing types
        while (currentClass != null)
        {
            if (InheritsFromInteractionModuleBase(currentClass))
            {
                topmostModule = currentClass;
            }
            currentClass = currentClass.ContainingType;
        }

        return topmostModule;
    }

    private static void CheckAndReportDiagnostic(SyntaxNodeAnalysisContext context, INamedTypeSymbol classSymbol, Location location)
    {
        var hasCommandContextType = false;
        var hasIntegrationType = false;

        foreach (var attribute in classSymbol.GetAttributes())
        {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass == null) continue;

            if (attributeClass.Name == nameof(CommandContextTypeAttribute))
            {
                hasCommandContextType = true;
            }
            else if (attributeClass.Name == nameof(IntegrationTypeAttribute))
            {
                hasIntegrationType = true;
            }
        }

        if (!hasCommandContextType || !hasIntegrationType)
        {
            var missingAttributes = new System.Text.StringBuilder();
            if (!hasCommandContextType)
            {
                missingAttributes.Append(nameof(CommandContextTypeAttribute));
            }

            if (!hasIntegrationType)
            {
                if (!hasCommandContextType)
                {
                    missingAttributes.Append(" and ");
                }

                missingAttributes.Append(nameof(IntegrationTypeAttribute));
            }

            var diagnostic = Diagnostic.Create(
                Rule,
                location,
                classSymbol.Name,
                missingAttributes.ToString());

            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeClass(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;

        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);
        if (classSymbol == null) return;
        
        if (classSymbol.ContainingType != null) return;

        if (!InheritsFromInteractionModuleBase(classSymbol)) return;

        if (!HasSlashCommands(classSymbol)) return;

        var topmostModule = FindTopmostModuleParent(classSymbol);
        
        if (topmostModule != null)
        {
            var location = (topmostModule.DeclaringSyntaxReferences
                    .FirstOrDefault()?.GetSyntax() as ClassDeclarationSyntax)?
                .Identifier.GetLocation() ?? classDeclaration.Identifier.GetLocation();

            CheckAndReportDiagnostic(context, topmostModule, location);
        }
        else
        {
            CheckAndReportDiagnostic(context, classSymbol, classDeclaration.Identifier.GetLocation());
        }
    }
}