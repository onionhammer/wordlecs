using wordleword;
using Spectre.Console;
using System.Threading.Channels;
using System.Diagnostics;
using static System.Diagnostics.Debug;

Assert(Enumerable.SequenceEqual(Wordle.MakeGuess("abcde", "abcde"), new [] { 'g', 'g', 'g', 'g', 'g' }));
Assert(Enumerable.SequenceEqual(Wordle.MakeGuess("abcde", "xbcde"), new [] { '-', 'g', 'g', 'g', 'g' }));
Assert(Enumerable.SequenceEqual(Wordle.MakeGuess("abcde", "edcba"), new [] { 'y', 'y', 'g', 'y', 'y' }));
Assert(Enumerable.SequenceEqual(Wordle.MakeGuess("xxxxx", "abcde"), new [] { '-', '-', '-', '-', '-' }));
Assert(Enumerable.SequenceEqual(Wordle.MakeGuess("aabbc", "abcde"), new [] { 'g', 'y', 'y', '-', '-' }));
Assert(Enumerable.SequenceEqual(Wordle.MakeGuess("aaabb", "aabbb"), new [] { 'g', 'g', '-', 'g', 'g' }));
Assert(Enumerable.SequenceEqual(Wordle.MakeGuess("perky", "peers"), new [] { 'g', 'g', '-', 'y', '-' }));

await AnsiConsole
    .Status()
    .StartAsync("Loading word list...", async ctx =>
    {
        var words = await Wordlist
            .OpenAsync("wordlist.csv")
            .OrderBy(word => word) // Sort alphabetically
            .ToHashSetAsync();

        // Foreach word, test how many wordle matches it has against all the other words
        var channel = Channel.CreateUnbounded<(string word, int matches)>();

        // Start a child task to test each word
        var task = Task.Factory.StartNew(() =>
        {
            Parallel.ForEach(
                words,
                parallelOptions: new ()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                },
                body: (word, state) =>
                {
                    while (!channel.Writer.TryWrite((word, 5)))
                        Thread.SpinWait(1);
                });

            channel.Writer.Complete();
        });

        // Read the results
        var results = new List<(string word, int matches)>();
        await foreach (var (guess, count) in channel.Reader.ReadAllAsync())
        {
            ctx.Status($"{guess} - {count} matches");
            results.Add((guess, count));
        }

        await task;
        ctx.Status("Done");
    });
