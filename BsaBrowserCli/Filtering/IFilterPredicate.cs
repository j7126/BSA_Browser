namespace BsaBrowserCli.Filtering
{
    internal interface IFilterPredicate
    {
        bool Match(string value);
    }
}
