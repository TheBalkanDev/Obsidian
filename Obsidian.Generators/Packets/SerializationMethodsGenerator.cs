﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Obsidian.Generators.Packets
{
    [Generator]
    public class SerializationMethodsGenerator : ISourceGenerator
    {
        private static readonly DiagnosticDescriptor noSerializationMethod = new DiagnosticDescriptor("DBG001", "This data type doesn't have serialization method associated with it.", "This data type doesn't have serialization method associated with it.", "SerializationMethodGeneration", DiagnosticSeverity.Warning, true);
        
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxProvider());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxProvider syntaxProvider)
                return;

            Compilation compilation = context.Compilation;

            INamedTypeSymbol attributeSymbol = compilation.GetTypeByMetadataName("Obsidian.Serializer.Attributes.FieldAttribute");

            var memberSymbols = new List<(TypeSyntax type, ISymbol symbol, MemberDeclarationSyntax member)>();
            foreach (MemberDeclarationSyntax member in syntaxProvider.WithContext(context).GetSyntaxNodes())
            {
                SemanticModel model = compilation.GetSemanticModel(member.SyntaxTree);
                if (member is FieldDeclarationSyntax field)
                {
                    foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    {
                        ISymbol symbol = model.GetDeclaredSymbol(variable);
                        if (symbol.GetAttributes().Any(attribute => attribute.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                        {
                            memberSymbols.Add((field.Declaration.Type, symbol, field));
                        }
                    }
                }
                else if (member is PropertyDeclarationSyntax property)
                {
                    ISymbol symbol = model.GetDeclaredSymbol(member);
                    if (symbol.GetAttributes().Any(attribute => attribute.AttributeClass.Equals(attributeSymbol, SymbolEqualityComparer.Default)))
                    {
                        memberSymbols.Add((property.Type, symbol, property));
                    }
                }
            }

            foreach (var group in memberSymbols.GroupBy(member => member.symbol.ContainingType))
            {
                string classSource = ProcessClass(group.Key, group.ToList(), attributeSymbol, context, syntaxProvider);
                context.AddSource($"{group.Key.Name}_Serialization.cs", SourceText.From(classSource, Encoding.UTF8));
            }    
        }

        /// <summary>
        /// <see cref=""/>
        /// </summary>
        /// <param name="classSymbol"></param>
        /// <returns></returns>
        private string ProcessClass(INamedTypeSymbol classSymbol, List<(TypeSyntax type, ISymbol symbol, MemberDeclarationSyntax member)> members, ISymbol attributeSymbol, GeneratorExecutionContext context, SyntaxProvider syntaxProvider)
        {
            string @namespace = classSymbol.ContainingNamespace.ToDisplayString();

            var source = new StringBuilder($@"using Obsidian.Net;

namespace {@namespace}
{{
    public partial class {classSymbol.Name}
    {{
");
            string classOffset = "\t\t";

            source.AppendXML("summary", $"Serializes data from this packet into <see cref=\"MinecraftStream\"/>.\n<b>AUTOGENERATED</b>");
            source.AppendXML("param", @"name=""stream""", "Target stream that this packet's data is written to.", true);
            source.Append($"{classOffset}public void Serialize(MinecraftStream stream)\n{classOffset}{{\n");
            CreateSerializationMethod(source, members);
            source.Append($"{classOffset}}}\n\n");

            source.AppendXML("summary", $"Deserializes byte data into <see cref=\"{classSymbol.Name}\"/> packet.\n<b>AUTOGENERATED</b>");
            source.AppendXML("param", @"name=""data""", "Data used to populate the packet.", true);
            source.AppendXML("returns", "Deserialized packet.", true);
            source.Append($"{classOffset}public static {classSymbol.Name} Deserialize(byte[] data)\n{classOffset}{{\n");
            source.AppendCode("using var stream = new MinecraftStream(data);");
            source.AppendCode("return Deserialize(stream);");
            foreach (var keyValuePair in syntaxProvider.WriteMethods)
            {
                source.AppendComment($"{keyValuePair.Key}: {keyValuePair.Value}");
            }
            source.Append($"{classOffset}}}\n\n");

            source.AppendXML("summary", $"Deserializes data from <see cref=\"MinecraftStream\"/> into <see cref=\"{classSymbol.Name}\"/> packet.\n<b>AUTOGENERATED</b>");
            source.AppendXML("param", @"name=""stream""", "Stream that is read from to populate the packet.", true);
            source.AppendXML("returns", "Deserialized packet.", true);
            source.Append($"{classOffset}public static {classSymbol.Name} Deserialize(MinecraftStream stream)\n{classOffset}{{\n");
            CreateDeserializationMethod(source, classSymbol, members, syntaxProvider);
            source.Append($"{classOffset}}}");

            source.Append(@"
    }
}");
            return source.ToString();
        }

        private void CreateSerializationMethod(StringBuilder builder, List<(TypeSyntax type, ISymbol symbol, MemberDeclarationSyntax member)> members)
        {

        }

        private void CreateDeserializationMethod(StringBuilder builder, INamedTypeSymbol classSymbol, List<(TypeSyntax type, ISymbol symbol, MemberDeclarationSyntax member)> members, SyntaxProvider syntaxProvider)
        {
            builder.AppendCode($"var packet = new {classSymbol}();");
            foreach (var member in members)
            {
                builder.AppendCode($"packet.{member.symbol.Name} = stream.{GetReadMethod(member, syntaxProvider)}();");
            }
            builder.AppendCode("return packet;");
        }

        private string GetReadMethod((TypeSyntax type, ISymbol symbol, MemberDeclarationSyntax member) member, SyntaxProvider syntaxProvider)
        {
            string dataType, methodName;
            var attribute = member.member.AttributeLists.SelectMany(attributeList => attributeList.Attributes).FirstOrDefault(attribute => attribute.Name.ToString() == "Field");
            var argument = attribute?.ArgumentList.DescendantNodes().FirstOrDefault(node => node is IdentifierNameSyntax identifier && identifier.Identifier.Text == "Type");
            var typeAccess = argument?.Parent.Parent.DescendantNodes().FirstOrDefault(node => node is MemberAccessExpressionSyntax) as MemberAccessExpressionSyntax;
            if (typeAccess is not null)
            {
                dataType = typeAccess.GetText().ToString().Split('.').Last();
                if (syntaxProvider.ReadMethods.TryGetValue(dataType, out methodName))
                {
                    return methodName;
                }
                else
                {
                    syntaxProvider.Context.ReportDiagnostic(Diagnostic.Create(noSerializationMethod, argument.GetLocation(), member.symbol.Name));
                    return string.Empty;
                }
            }

            var typeName = member.type.GetText().ToString();
            if (syntaxProvider.ReadMethods.TryGetValue(typeName, out methodName))
            {
                return methodName;
            }
            else
            {
                syntaxProvider.Context.ReportDiagnostic(Diagnostic.Create(noSerializationMethod, member.type.GetLocation(), member.symbol.Name));
                return string.Empty;
            }
        }

        private string GetWriteMethod((TypeSyntax type, ISymbol symbol, MemberDeclarationSyntax member) member, SyntaxProvider syntaxProvider)
        {
            return string.Empty;
        }
    }

    internal class SyntaxProvider : ExecutionSyntaxProvider<MemberDeclarationSyntax>
    {
        public Dictionary<string, string> WriteMethods { get; } = new Dictionary<string, string>();
        public Dictionary<string, string> ReadMethods { get; } = new Dictionary<string, string>();

        public SyntaxProvider() : base(member => (member is FieldDeclarationSyntax || member is PropertyDeclarationSyntax) && member.AttributeLists.Count > 0)
        {
        }
        
        protected override bool HandleNode(MemberDeclarationSyntax node)
        {
            if (node is MethodDeclarationSyntax methodDeclaration)
            {
                var attributes = methodDeclaration.AttributeLists.SelectMany(list => list.Attributes);
                var attribute = attributes.FirstOrDefault(attribute => attribute.Name.ToString() == "ReadMethod" || attribute.Name.ToString() == "WriteMethod");
                if (attribute is not null)
                {
                    string dataType = attribute.ArgumentList.Arguments.First().GetText().ToString().Split('.').Last();
                    string methodName = methodDeclaration.Identifier.Text;
                    if (attribute.Name.ToString() == "ReadMethod")
                    {
                        ReadMethods[dataType] = methodName;
                        ReadMethods[methodDeclaration.ReturnType.GetText().ToString()] = methodName;
                    }
                    else
                    {
                        WriteMethods[dataType] = methodName;
                        WriteMethods[methodDeclaration.ParameterList.Parameters.First().Type.GetText().ToString()] = methodName;
                    }
                }
            }
            return base.HandleNode(node);
        }
    }

    internal static class Extensions
    {
        private static readonly string prefix = "\t\t///";
        
        public static StringBuilder AppendXML(this StringBuilder stringBuilder, string type, string content, bool inline = false)
        {
            if (inline)
            {
                return stringBuilder.AppendLine($"{prefix} <{type}>{content.Replace('\n', ' ')}</{type}>");
            }
            else
            {
                return stringBuilder.AppendLine($"{prefix} <{type}>").AppendLine(string.Join("<br/>\n", content.Split('\n').Select(c => $"{prefix} {c}"))).AppendLine($"{prefix} </{type}>");
            }
        }

        public static StringBuilder AppendXML(this StringBuilder stringBuilder, string type, string attributes, string content, bool inline = false)
        {
            if (inline)
            {
                return stringBuilder.AppendLine($"{prefix} <{type} {attributes}>{content.Replace('\n', ' ')}</{type}>");
            }
            else
            {
                return stringBuilder.AppendLine($"{prefix} <{type} {attributes}>").AppendLine(string.Join("<br/>\n", content.Split('\n').Select(c => $"{prefix} {c}"))).AppendLine($"{prefix} </{type}>");
            }
        }

        public static StringBuilder AppendCode(this StringBuilder stringBuilder, string code)
        {
            return stringBuilder.AppendLine($"\t\t\t{code}");
        }

        public static StringBuilder AppendComment(this StringBuilder stringBuilder, string comment)
        {
            return stringBuilder.AppendLine($"\t\t\t// {comment}");
        }
    }
}
