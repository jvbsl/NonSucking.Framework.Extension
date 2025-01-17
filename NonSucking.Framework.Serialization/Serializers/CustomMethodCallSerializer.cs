﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Text;

using VaVare.Statements;
using VaVare.Generators.Common.Arguments.ArgumentTypes;
using System.Linq;
using System.IO;
using NonSucking.Framework.Serialization.Attributes;
using System.Linq.Expressions;

namespace NonSucking.Framework.Serialization
{
    internal static class CustomMethodCallSerializer
    {
        internal static bool TryDeserialize(MemberInfo property, NoosonGeneratorContext context, string readerName, List<StatementSyntax> statements)
        {
            var methodName = "Deserialize";
            bool isClassAttribute = false;
            if (!property.Symbol.TryGetAttribute(AttributeTemplates.Custom, out var propAttrData))
            {
                isClassAttribute = property.TypeSymbol.TryGetAttribute(AttributeTemplates.Custom, out propAttrData);
                if (!isClassAttribute)
                    return false;

            }

            if (propAttrData.NamedArguments.IsEmpty)
            {
                context.AddDiagnostic("0010", "", $"You must atleast provide one argument for {AttributeTemplates.Custom.Name}. Otherwise this value won't be deserialized!", property.Symbol, DiagnosticSeverity.Error);
                return true;
            }

            var customMethodName = propAttrData.NamedArguments.FirstOrDefault(x => x.Key == "DeserializeMethodName").Value.Value as string;
            if (!string.IsNullOrWhiteSpace(customMethodName))
            {
                methodName = customMethodName;
            }

            var customType = propAttrData.NamedArguments.FirstOrDefault(x => x.Key == "DeserializeImplementationType").Value.Value as ISymbol;
            InvocationExpressionSyntax invocationExpression;

            if (customType != null)
            {
                //Static call
                invocationExpression
                   = Statement
                   .Expression
                   .Invoke(customType.ToDisplayString(), methodName, arguments: new[] { new ValueArgument((object)readerName) })
                   .AsExpression();
            }
            else if (isClassAttribute || (property.Symbol is not IPropertySymbol && property.Symbol is not IFieldSymbol))
            {
                //Static on target type
                invocationExpression
                        = Statement
                        .Expression
                        .Invoke(property.TypeSymbol.ToDisplayString(), methodName, arguments: new[] { new ValueArgument((object)readerName) })
                        .AsExpression();
            }
            else
            {
                //Static on this
                invocationExpression
                        = Statement
                        .Expression
                        .Invoke(methodName, arguments: new[] { new ValueArgument((object)readerName) })
                        .AsExpression();
            }

            statements.Add(Statement
                .Declaration
                .DeclareAndAssign($"{Helper.GetRandomNameFor(property.Name, property.Parent)}", invocationExpression));

            return true;
        }




        internal static bool TrySerialize(MemberInfo property, NoosonGeneratorContext context, string writerName,List<StatementSyntax> statements)
        {
            
            var methodName = "Serialize";
            bool isClassAttribute = false;
            if (!property.Symbol.TryGetAttribute(AttributeTemplates.Custom, out var propAttrData))
            {
                isClassAttribute = property.TypeSymbol.TryGetAttribute(AttributeTemplates.Custom, out propAttrData);
                if (!isClassAttribute)
                    return false;

            }

            if (propAttrData.NamedArguments.IsEmpty)
            {
                context.AddDiagnostic("0009", "", $"You must atleast provide one argument for {AttributeTemplates.Custom.Name}. Otherwise this value won't be serialized!", property.Symbol, DiagnosticSeverity.Error);
                return true;
            }

            var customMethodName = propAttrData.NamedArguments.FirstOrDefault(x => x.Key == "SerializeMethodName").Value.Value as string;
            if (!string.IsNullOrWhiteSpace(customMethodName))
            {
                methodName = customMethodName;
            }
            StatementSyntax statement;
            var customType = propAttrData.NamedArguments.FirstOrDefault(x => x.Key == "SerializeImplementationType").Value.Value as ISymbol;
            if (customType != null)
            {
                //Static call
                statement
                   = Statement
                   .Expression
                   .Invoke(customType.ToDisplayString(), methodName, arguments: new[] { new ValueArgument((object)writerName), new ValueArgument((object)property.Name) })
                   .AsStatement();
            }
            else if (isClassAttribute || (property.Symbol is not IPropertySymbol && property.Symbol is not IFieldSymbol))
            {
                //non Static
                statement
                        = Statement
                        .Expression
                        .Invoke(Helper.GetMemberAccessString(property), methodName, arguments: new[] { new ValueArgument((object)writerName) })
                        .AsStatement();
            }
            else
            {
                //Non Static on this
                statement
                        = Statement
                        .Expression
                        .Invoke(methodName, arguments: new[] { new ValueArgument((object)writerName) })
                        .AsStatement();
            }

            statements.Add(statement);

            return true;

        }
    }
}
