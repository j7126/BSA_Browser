using System.Text.RegularExpressions;

namespace BsaBrowserCli.Filtering
{
    internal class FilterPredicateRegex(string pattern) : IFilterPredicate
    {
        private readonly Regex _pattern = new(pattern, RegexOptions.Compiled | RegexOptions.Singleline);

        public bool Match(string value)
        {
            return this._pattern.IsMatch(value);
        }
    }
}
