namespace Summary.Common.EFCore.Diagnostic
{
    public class CursorPagedResult<T>
    {
        public IEnumerable<T> Data { get; set; } = [];

        public string? NextCursor { get; set; }

        public string? PreviousCursor { get; set; }

        public bool HasNextPage { get; set; }

        public bool HasPreviousPage { get; set; }
    }
}