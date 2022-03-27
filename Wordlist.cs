namespace wordleword;

class Wordlist
{
    public async static Task<List<string>> OpenAsync(
        string filename,
        CancellationToken cancellationToken = default)
    {
        // Read words from CSV file
        using var reader = new StreamReader(filename);
        var list = new List<string>();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            if (await reader.ReadLineAsync() is string result)
                list.Add(result);
        }

        return list;
    }
}