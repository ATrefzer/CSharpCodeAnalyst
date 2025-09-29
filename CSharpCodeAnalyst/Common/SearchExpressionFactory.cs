using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CSharpCodeAnalyst.Common
{
    internal class SearchExpressionFactory
    {
        public static IExpression CreateSearchExpression(string searchText)
        {
            // Or binds less.
            var orTerms = searchText
                .ToLowerInvariant()
                .Split(['|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            var orExpressions = new List<IExpression>();
            foreach (var orTerm in orTerms)
            {
                var andExpressions = orTerm
                    .Split([' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(IExpression (t) => new Term(t))
                    .ToArray();

                orExpressions.Add(new Term.And(andExpressions));
            }

            if (orExpressions.Count == 1)
            {
                return orExpressions[0];
            }

            var root = new Term.Or(orExpressions.ToArray());
            return root;
        }

    }
}
