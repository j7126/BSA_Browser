using System.Management.Automation;

namespace BsaBrowserCli.Filtering
{
    internal class FilterPredicateSimpleExclude(string pattern) : IFilterPredicate
    {
        private readonly WildcardPattern _pattern = new(
                $"*{WildcardPattern.Escape(pattern).Replace("`*", "*")}*",
                WildcardOptions.Compiled | WildcardOptions.IgnoreCase);

        public bool Match(string value)
        {
            // Return true if pattern DOESN'T match
            return !this._pattern.IsMatch(value);
        }
    }
}
