using System.Management.Automation;

namespace BsaBrowserCli.Filtering
{
    internal class FilterPredicateSimpleExclude(string pattern) : IFilterPredicate
    {
        readonly WildcardPattern _pattern = new(
                $"*{WildcardPattern.Escape(pattern).Replace("`*", "*")}*",
                WildcardOptions.Compiled | WildcardOptions.IgnoreCase);

        public bool Match(string value)
        {
            // Return true if pattern DOESN'T match
            return _pattern.IsMatch(value) == false;
        }
    }
}
