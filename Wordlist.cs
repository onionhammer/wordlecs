using System.Runtime.CompilerServices;

namespace wordleword;

class Wordlist
{
    public async static IAsyncEnumerable<string> OpenAsync(
        string filename,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Read words from CSV file
        using var reader = new StreamReader(filename);

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            if (await reader.ReadLineAsync() is string result)
                yield return result;
        }
    }
}