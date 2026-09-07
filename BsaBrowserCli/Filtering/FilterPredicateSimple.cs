using System.Management.Automation;

namespace BsaBrowserCli.Filtering
{
    internal class FilterPredicateSimple(string pattern) : IFilterPredicate
    {
        readonly WildcardPattern _pattern = new(
                $"*{WildcardPattern.Escape(pattern).Replace("`*", "*")}*",
                WildcardOptions.Compiled | WildcardOptions.IgnoreCase);

        public bool Match(string value)
        {
            return _pattern.IsMatch(value);
        }
    }
}
