using System.Text;

namespace Woolly.Tests;

public static class RandomString
{
    private const string Characters = "abcdefghjkmnpqrstuvwxyz23456789";
    private static readonly object Lock = new();
    private static readonly Random Random = new();

    public static string Create(int length)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length));
        StringBuilder value = new();
        lock (Lock)
        {
            for (var i = 0; i < length; i++)
            {
                value.Append(Characters[Random.Next(0, Characters.Length)]);
            }
        }
        return value.ToString();
    }
}
