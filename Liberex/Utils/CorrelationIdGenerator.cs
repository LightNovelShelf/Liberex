namespace Liberex.Utils;

internal static class CorrelationIdGenerator
{
    private static readonly char[] s_encode32Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUV".ToCharArray();

    private static long _lastId = DateTime.UtcNow.Ticks;

    public static string GetNextId() => GenerateId(Interlocked.Increment(ref _lastId));

    private static string GenerateId(long id)
    {
        return string.Create(13, id, (buffer, value) =>
        {
            char[] encode32Chars = s_encode32Chars;

            buffer[12] = encode32Chars[value & 31];
            buffer[11] = encode32Chars[value >> 5 & 31];
            buffer[10] = encode32Chars[value >> 10 & 31];
            buffer[9] = encode32Chars[value >> 15 & 31];
            buffer[8] = encode32Chars[value >> 20 & 31];
            buffer[7] = encode32Chars[value >> 25 & 31];
            buffer[6] = encode32Chars[value >> 30 & 31];
            buffer[5] = encode32Chars[value >> 35 & 31];
            buffer[4] = encode32Chars[value >> 40 & 31];
            buffer[3] = encode32Chars[value >> 45 & 31];
            buffer[2] = encode32Chars[value >> 50 & 31];
            buffer[1] = encode32Chars[value >> 55 & 31];
            buffer[0] = encode32Chars[value >> 60 & 31];
        });
    }
}