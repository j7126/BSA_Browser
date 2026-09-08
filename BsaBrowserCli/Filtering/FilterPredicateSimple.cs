using System.Management.Automation;

namespace BsaBrowserCli.Filtering
{
    internal class FilterPredicateSimple(string pattern) : IFilterPredicate
    {
        private readonly WildcardPattern _pattern = new(
                $"*{WildcardPattern.Escape(pattern).Replace("`*", "*")}*",
                WildcardOptions.Compiled | WildcardOptions.IgnoreCase);

        public bool Match(string value)
        {
            return this._pattern.IsMatch(value);
        }
    }
}
