using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DeSwitcher.Compiler
{
    public sealed class CompatRewriter : CSharpSyntaxRewriter
    {
        private readonly Dictionary<string, string> map;

        public CompatRewriter(Dictionary<string, string> map)
        {
            this.map = map;
        }

        public override SyntaxNode VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
        {
            var visited = (MemberAccessExpressionSyntax)base.VisitMemberAccessExpression(node);
            string owner = visited.Expression.ToString().Replace("global::", "").Trim();
            string key = owner + "." + visited.Name.Identifier.ValueText;
            if (map.TryGetValue(key, out string target))
                return visited.WithExpression(SyntaxFactory.ParseExpression(target).WithTriviaFrom(visited.Expression));
            return visited;
        }
    }
}
