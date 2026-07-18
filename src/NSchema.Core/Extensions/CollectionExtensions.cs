namespace NSchema.Extensions;

internal static class CollectionExtensions
{
    extension<T>(IList<T> source)
    {
        /// <summary>
        /// Removes every item matching the predicate, back to front so removal hooks fire per item.
        /// </summary>
        public void RemoveWhere(Func<T, bool> predicate)
        {
            for (var i = source.Count - 1; i >= 0; i--)
            {
                if (predicate(source[i]))
                {
                    source.RemoveAt(i);
                }
            }
        }
    }
}
