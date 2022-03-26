using wordleword;
using Spectre.Console;
using System.Threading.Channels;
using MathNet.Numerics.Statistics;
using static System.Diagnostics.Debug;
using System.Text.Json;

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
            .ToListAsync();

        var possibleSolutions = await Wordlist
            .OpenAsync("solutions.csv")
            .ToListAsync();

        // Foreach word, test how many wordle matches it has against all the other words
        var channel = Channel.CreateUnbounded<GuessResult>();

        ctx.Status($"Started {words.Count} word tests");

        // Start a child task to test each word
        _ = Task.Factory.StartNew(() =>
        {
            Parallel.ForEach(words, body: guess =>
                {
                    var counts = new List<double>(capacity: possibleSolutions.Count);

                    foreach (var word in possibleSolutions)
                    {
                        var pattern = Wordle.MakeGuess(word, guess);
                        var matches = 0;

                        foreach (var other in possibleSolutions)
                        {
                            if (Wordle.CheckWord(other, guess, pattern))
                                ++matches;
                        }

                        counts.Add(matches);
                    }

                    GuessResult result = new (guess, counts.Average(), counts.Median(), counts.StandardDeviation());

                    while (!channel.Writer.TryWrite(result))
                        Thread.SpinWait(1);
                });

            channel.Writer.Complete();
        });

        // Read the results
        var results = new List<GuessResult>();
        await foreach (var result in channel.Reader.ReadAllAsync())
        {
            ctx.Status($"{result.Guess} - {result.Mean} matches");
            results.Add(result);
        }

        ctx.Status("Outputting results");

        // Sort the results
        results.Sort((a, b) => b.Mean.CompareTo(a.Mean));

        await using var fs = File.OpenWrite("results.csv");
        await using var sw = new StreamWriter(fs);
        await sw.WriteLineAsync("Guess,Mean,Median,StdDev");
        foreach (var result in results)
            await sw.WriteLineAsync($"{result.Guess},{result.Mean},{result.Median},{result.Std}");

        ctx.Status("Get a graph for SOARE");

        /* Get a graph for SOARE */
        Channel<int> soareCounts = Channel.CreateUnbounded<int>();
        _ = Task.Factory.StartNew(() =>
        {
            Parallel.ForEach(possibleSolutions, word => 
            {
                var pattern = Wordle.MakeGuess(word, "soare");
                var matches = possibleSolutions
                    .Where((other) => Wordle.CheckWord(other, "soare", pattern))
                    .Count();

                while (!soareCounts.Writer.TryWrite(matches))
                    Thread.SpinWait(1);
            });

            soareCounts.Writer.Complete();
        });

        await using (var soarFile = File.OpenWrite("soare.data"))
        {
            await JsonSerializer.SerializeAsync(
                soarFile, soareCounts.Reader.ReadAllAsync(), 
                new JsonSerializerOptions { WriteIndented = true });
        }

        ctx.Status("Done");
    });

record struct GuessResult(string Guess, double Mean, double Median, double Std);