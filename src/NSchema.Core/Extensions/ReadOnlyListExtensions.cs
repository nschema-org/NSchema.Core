namespace NSchema.Extensions;

internal static class ReadOnlyListExtensions
{
    extension<T>(IReadOnlyList<T> source)
    {
        public IReadOnlyList<T> ForEach(Action<T> action)
        {
            foreach (var item in source)
            {
                action(item);
            }
            return source;
        }
    }
}
