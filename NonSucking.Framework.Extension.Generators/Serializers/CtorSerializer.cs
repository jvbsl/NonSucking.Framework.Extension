﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using VaVare.Models.References;

using VaVare.Statements;
using NonSucking.Framework.Extension.Generators.Vavare;

namespace NonSucking.Framework.Extension.Generators.Serializers
{
    internal enum DeclareOrAndAssign
    {
        DeclareOnly,
        DeclareAndAssign
    }

    internal static class CtorSerializer
    {
        internal static StatementSyntax CallCtorAndSetProps(INamedTypeSymbol typeSymbol, ICollection<StatementSyntax> statements, string instanceName, DeclareOrAndAssign declareAndAssign)
        {
            var constructors
                = typeSymbol
                .Constructors
                .OrderByDescending(constructor => constructor.Parameters.Length)
                .ToList();

            var localDeclarations
                = statements
                .OfType<LocalDeclarationStatementSyntax>()
                .Concat(
                    statements
                    .OfType<BlockSyntax>()
                    .SelectMany(x => x.Statements.OfType<LocalDeclarationStatementSyntax>())
                 )
                .SelectMany(declaration => declaration.Declaration.Variables)
                .Select(variable => variable.Identifier.Text)
                .Where(text => text.StartsWith("@"))
                .Select(text => text.Substring(1))
                .ToList();

            var currentType
                = SyntaxFactory
                .ParseTypeName(typeSymbol.ToDisplayString());


            if (typeSymbol.TypeKind == TypeKind.Interface || typeSymbol.IsAbstract)
            {
                return Statement
                 .Declaration
                 .Assign(instanceName, SyntaxFactory.ParseTypeName(" default"));
            }

            var ctorCallStatement
                = GetStatementForCtorCall(constructors, localDeclarations, currentType, instanceName, declareAndAssign, out var ctorArguments);

            var propertyAssignments
                = AssignMissingSetterProperties(typeSymbol, localDeclarations, ctorArguments, instanceName);

            return BlockHelper.GetBlockWithoutBraces(new StatementSyntax[] { ctorCallStatement, propertyAssignments });
        }


        internal static StatementSyntax AssignMissingSetterProperties(ITypeSymbol typeSymbol, List<string> localDeclarations, List<string> ctorArguments, string variableName)
        {

            //TODO Set Public props which have a set method via !IPropertySymbol.IsReadOnly
            localDeclarations
                = localDeclarations
                .Where(declaration =>
                    !ctorArguments.Any(argument => Helper.MatchIdentifierWithPropName(argument, declaration))
                )
                .ToList();

            var properties
                = typeSymbol
                .GetMembers()
                .OfType<IPropertySymbol>()
                .Where(property =>
                    !property.IsReadOnly
                    && property.SetMethod is not null
                    && !ctorArguments.Any(argument => Helper.MatchIdentifierWithPropName(argument, property.Name))
                );

            var blockStatements = new List<StatementSyntax>();

            foreach (var property in properties)
            {
                var variableReference
                     = new VariableReference(variableName, new MemberReference(property.Name));

                var declaration
                    = localDeclarations
                    .FirstOrDefault(declaration => Helper.MatchIdentifierWithPropName(declaration, property.Name));

                if (declaration is null)
                {
                    continue;
                }

                VariableReference declarationReference;

                if (property.Type.TypeKind == TypeKind.Array)
                {
                    var method = new MethodReference("ToArray");
                    declarationReference = new MemberReference(declaration, method);
                }
                else
                {
                    declarationReference = new VariableReference(declaration);
                }

                var statement
                    = Statement
                    .Declaration
                    .Assign(variableReference, declarationReference);

                blockStatements.Add(statement);
            }


            return BlockHelper.GetBlockWithoutBraces(blockStatements);

        }

        internal static StatementSyntax GetStatementForCtorCall(List<IMethodSymbol> constructors, List<string> localDeclarations, TypeSyntax currentType, string instanceName, DeclareOrAndAssign declareAndAssign, out List<string> ctorArguments)
        {
            ctorArguments = new List<string>();
            if (constructors.Count == 0)
            {
                var arguments = SyntaxFactory.ParseArgumentList("()");

                return DeclareAssignCtor(currentType, instanceName, declareAndAssign, arguments);
            }

            foreach (var constructor in constructors)
            {
                bool constructorMatch = true;

                foreach (var parameter in constructor.Parameters)
                {


                    var matchedDeclaration
                        = localDeclarations
                        .FirstOrDefault(identifier => Helper.MatchIdentifierWithPropName(identifier, parameter.Name));

                    if (string.IsNullOrWhiteSpace(matchedDeclaration))
                    {
                        constructorMatch = false;
                        break;
                    }

                    ctorArguments.Add(matchedDeclaration);
                }


                if (constructorMatch)
                {
                    var ctorArgumentsString
                        = string.Join(",", ctorArguments);

                    var arguments
                        = SyntaxFactory
                        .ParseArgumentList($"({ctorArgumentsString})");

                    //var semanticModel = cont.Compilation.GetSemanticModel(currentType.SyntaxTree);

                    //var symb = semanticModel.GetSymbolInfo(currentType);

                    //var text = currentType.GetText();
                    return DeclareAssignCtor(currentType, instanceName, declareAndAssign, arguments);

                }
                else
                {
                    ctorArguments.Clear();
                }
            }

            //TODO diagnostic
            throw new NotSupportedException();
        }

        internal static StatementSyntax DeclareAssignCtor(TypeSyntax currentType, string instanceName, DeclareOrAndAssign declareAndAssign, ArgumentListSyntax arguments)
        {
            if (declareAndAssign == DeclareOrAndAssign.DeclareAndAssign)
            {
                return Statement
                    .Declaration
                    .DeclareAndAssign(instanceName, currentType, arguments);
            }
            else if (declareAndAssign == DeclareOrAndAssign.DeclareOnly)
            {
                return Statement
                    .Declaration
                    .Assign(instanceName, currentType, arguments);
            }
            else
            {
                //TODO: Error
                return null;
            }
        }
    }
}
