namespace BsaBrowserCli.Filtering
{
    internal struct Filter(FilteringTypes type, string pattern)
    {
        public FilteringTypes Type { get; set; } = type;
        public string Pattern { get; set; } = pattern;
    }
}
