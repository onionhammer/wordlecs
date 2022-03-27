using wordleword;
using Spectre.Console;
using System.Text.Json;
using System.Threading.Channels;
using MathNet.Numerics.Statistics;
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
            .ToListAsync();

        var possibleSolutions = await Wordlist
            .OpenAsync("solutions.csv")
            .ToListAsync();

        // Foreach word, test how many wordle matches it has against all the other words
        var guessChannel = Channel.CreateUnbounded<GuessResult>();
        var wordsGuessed = 0;

        ctx.Status($"Started {words.Count:n0} word tests");

        var completion = new TaskCompletionSource();

        // Start a child task to test each word
        _ = Task.Run(() =>
        {
            Parallel.ForEach(
                words, 
                parallelOptions: new() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                body: (guess) =>
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
                        Interlocked.Increment(ref wordsGuessed);
                    }

                    GuessResult result = new (guess, counts.Average(), counts.Median(), counts.StandardDeviation());

                    while (!guessChannel.Writer.TryWrite(result))
                        Thread.SpinWait(1);
                });

            guessChannel.Writer.Complete();
            completion.TrySetResult();
        });

        // Sampler
        _ = Task.Run(async () => 
        {
            var sinceLast = 0;

            while (!completion.Task.IsCompleted)
            {
                await Task.Delay(1_000);

                var diff = wordsGuessed - sinceLast;
                
                ctx.Status($"{wordsGuessed:n0} words tested, {diff:n0} words/s");
                sinceLast = wordsGuessed;
            }
        });

        // Read the results
        var results = new List<GuessResult>();
        await foreach (var result in guessChannel.Reader.ReadAllAsync())
        {
            AnsiConsole.MarkupLine($"{result.Guess} - {result.Mean} matches");
            results.Add(result);
        }

        ctx.Status("Outputting results");

        // Sort the results
        results.Sort((a, b) => a.Mean.CompareTo(b.Mean));

        await using var fs = File.OpenWrite("results.csv");
        await using var sw = new StreamWriter(fs);
        await sw.WriteLineAsync("Guess,Mean,Median,StdDev");
        foreach (var result in results)
            await sw.WriteLineAsync($"{result.Guess},{result.Mean},{result.Median},{result.Std}");

        ctx.Status("Get a graph for SOARE");

        /* Get a graph for SOARE */
        Channel<int> soareCountChannel = Channel.CreateUnbounded<int>();
        _ = Task.Run(() =>
        {
            Parallel.ForEach(
                possibleSolutions,
                parallelOptions: new() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                body: word => 
                {
                    var pattern = Wordle.MakeGuess(word, "soare");
                    var matches = possibleSolutions
                        .Where((other) => Wordle.CheckWord(other, "soare", pattern))
                        .Count();

                    while (!soareCountChannel.Writer.TryWrite(matches))
                        Thread.SpinWait(1);
                });

            soareCountChannel.Writer.Complete();
        });

        await using (var soarFile = File.OpenWrite("soare.data"))
        {
            await JsonSerializer.SerializeAsync(
                soarFile, soareCountChannel.Reader.ReadAllAsync(), 
                new JsonSerializerOptions { WriteIndented = true });
        }

        ctx.Status("Done");
    });

record struct GuessResult(string Guess, double Mean, double Median, double Std);