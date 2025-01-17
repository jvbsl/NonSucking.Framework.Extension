﻿using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using static NonSucking.Framework.Serialization.NoosonGenerator;

using VaVare.Statements;
using NonSucking.Framework.Serialization.Serializers;

namespace NonSucking.Framework.Serialization
{
    internal static class PublicPropertySerializer
    {

        internal static bool TrySerialize(MemberInfo property, NoosonGeneratorContext context, string readerName, List<StatementSyntax> statements)
        {
            var propsAndFields
                = Helper.GetMembersWithBase(property.TypeSymbol)
                .Where(property =>
                    property.Name != "this[]")
               .Select(x => x with { Parent = property.Name });
            var props = propsAndFields.Select(x => x.Symbol).OfType<IPropertySymbol>();
            var writeOnlies = props.Where(x => x.IsWriteOnly || x.GetMethod is null);
            foreach (var onlyWrite in writeOnlies)
            {
                context.AddDiagnostic("0007",
                       "",
                       "Properties who are write only are not supported. Implemented a custom serializer method or ignore this property.",
                       property.TypeSymbol,
                       DiagnosticSeverity.Error
                       );
            }

            var initOnlies = props.Where(x => x.SetMethod?.IsInitOnly ?? false);
            foreach (var onlyWrite in initOnlies)
            {
                context.AddDiagnostic("0011",
                       "",
                       "Properties who are init only are (currently) not supported. Implemented a custom serializer method or ignore this property.",
                       property.TypeSymbol,
                       DiagnosticSeverity.Error
                       );
            }

            propsAndFields = FilterPropsForNotWriteOnly(propsAndFields);

            statements.AddRange(
                GenerateStatementsForProps(
                   propsAndFields
                       .Select(x => x with { Parent = property.FullName })
                       .ToArray(),
                   context,
                   MethodType.Serialize
               ).SelectMany(x => x));

            return true;

        }

        private static IEnumerable<MemberInfo> FilterPropsForNotWriteOnly(IEnumerable<MemberInfo> props)
        {
            props = props.Where(x =>
            {
                if (x.Symbol is IPropertySymbol ps
                    && !ps.IsWriteOnly
                    && ps.SetMethod is not null
                    && !ps.SetMethod.IsInitOnly
                    && ps.GetMethod is not null)
                {
                    return true;
                }
                else if (x.Symbol is IFieldSymbol fs)
                {
                    return true;
                }
                return false;

            });
            return props;
        }

        internal static bool TryDeserialize(MemberInfo property, NoosonGeneratorContext context, string readerName, List<StatementSyntax> statements)
        {
            var propsAndFields
               = Helper.GetMembersWithBase(property.TypeSymbol)
               .Where(property =>
                   property.Name != "this[]")
               .Select(x => x with { Parent = property.Name });
            var props = propsAndFields.Select(x => x.Symbol).OfType<IPropertySymbol>();

            var writeOnlies = props.Where(x => x.IsWriteOnly || x.GetMethod is null);
            foreach (var onlyWrite in writeOnlies)
            {
                context.AddDiagnostic("0007",
                       "",
                       "Properties who are write only are not supported. Implemented a custom serializer method or ignore this property.",
                       property.TypeSymbol,
                       DiagnosticSeverity.Error
                       );
            }

            var initOnlies = props.Where(x => x.SetMethod?.IsInitOnly ?? false);
            foreach (var onlyWrite in initOnlies)
            {
                context.AddDiagnostic("0011",
                       "",
                       "Properties who are init only are (currently) not supported. Implemented a custom serializer method or ignore this property.",
                       property.TypeSymbol,
                       DiagnosticSeverity.Error
                       );
            }

            propsAndFields = FilterPropsForNotWriteOnly(propsAndFields);


            string randomForThisScope = Helper.GetRandomNameFor("", property.Parent);
            var statementList
                = GenerateStatementsForProps(
                    propsAndFields.ToArray(),
                    context,
                    MethodType.Deserialize
                ).SelectMany(x => x).ToList();

            string memberName = $"{Helper.GetRandomNameFor(property.Name, property.Parent)}";

            var declaration
                = Statement
                .Declaration
                .Declare(memberName, SyntaxFactory.ParseTypeName(property.TypeSymbol.ToDisplayString()));

            try
            {

                var ctorSyntax = CtorSerializer.CallCtorAndSetProps((INamedTypeSymbol)property.TypeSymbol, statementList, memberName, DeclareOrAndAssign.DeclareOnly);
                statementList.AddRange(ctorSyntax);

            }
            catch (NotSupportedException)
            {
                context.AddDiagnostic("0006",
                   "",
                   "No instance could be created with the constructors in this type. Add a custom ctor call, property mapping or a ctor with matching arguments.",
                   property.Symbol,
                   DiagnosticSeverity.Error
                   );
            }

            statementList.Insert(0, declaration);
            statements.AddRange(statementList);
            return true;
        }
    }
}
