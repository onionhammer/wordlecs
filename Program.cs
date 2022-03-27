using Spectre.Console;
using System.Text.Json;
using System.Threading.Channels;
using MathNet.Numerics.Statistics;
using static wordleword.Wordle;
using static System.Diagnostics.Debug;

Assert(Enumerable.SequenceEqual(MakeGuess("abcde", "abcde"), new [] { 'g', 'g', 'g', 'g', 'g' }));
Assert(Enumerable.SequenceEqual(MakeGuess("abcde", "xbcde"), new [] { '-', 'g', 'g', 'g', 'g' }));
Assert(Enumerable.SequenceEqual(MakeGuess("abcde", "edcba"), new [] { 'y', 'y', 'g', 'y', 'y' }));
Assert(Enumerable.SequenceEqual(MakeGuess("xxxxx", "abcde"), new [] { '-', '-', '-', '-', '-' }));
Assert(Enumerable.SequenceEqual(MakeGuess("aabbc", "abcde"), new [] { 'g', 'y', 'y', '-', '-' }));
Assert(Enumerable.SequenceEqual(MakeGuess("aaabb", "aabbb"), new [] { 'g', 'g', '-', 'g', 'g' }));
Assert(Enumerable.SequenceEqual(MakeGuess("perky", "peers"), new [] { 'g', 'g', '-', 'y', '-' }));

Assert(CheckWord("abcde", "abcde", new [] { 'g', 'g', 'g', 'g', 'g' }));
Assert(!CheckWord("abcde", "abcde", new [] { 'g', 'g', 'g', 'g', 'y' }));
Assert(!CheckWord("abcde", "abcde", new [] { 'g', 'g', 'g', 'g', '-' }));
Assert(CheckWord("axxxx", "abcde", new [] { 'g', '-', '-', '-', '-' }));
Assert(CheckWord("abcde", "axxbb", new [] { 'g', '-', '-', 'y', '-' }));

await AnsiConsole
    .Status()
    .StartAsync("Loading word list...", async ctx =>
    {
        var words = await wordleword.Wordlist.OpenAsync("wordlist.csv");
        var possibleSolutions = await wordleword.Wordlist.OpenAsync("solutions.csv");

        // Foreach word, test how many wordle matches it has against all the other words
        var guessChannel = Channel.CreateUnbounded<GuessResult>();
        var statCheckWord = Channel.CreateUnbounded<long>();
        var wordsGuessed  = 0L;

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
                    var solutionsRun = 0;
                    var counts = new List<double>(capacity: possibleSolutions.Count);
                    Span<char> pattern = stackalloc char[5];

                    foreach (var word in possibleSolutions)
                    {
                        Zero.CopyTo(pattern);
                        MakeGuess(word, guess, ref pattern);
                        var matches = 0;

                        foreach (var other in possibleSolutions)
                        {
                            if (CheckWord(other, guess, pattern))
                                ++matches;

                            ++solutionsRun;
                        }

                        counts.Add(matches);

                        // Only increment every few iterations since this is a slower operation
                        if (solutionsRun % 5_000 == 0)
                        {
                            statCheckWord.Writer.TryWrite(solutionsRun);
                            solutionsRun = 0;
                        }
                    }

                    statCheckWord.Writer.TryWrite(solutionsRun);

                    GuessResult result = new (guess, counts.Average(), counts.Median(), counts.StandardDeviation());

                    while (!guessChannel.Writer.TryWrite(result))
                        Thread.SpinWait(1);

                    Interlocked.Increment(ref wordsGuessed);
                });

            guessChannel.Writer.Complete();
            completion.TrySetResult();
        });

        // Sampler
        _ = Task.Run(async () => 
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var total = words.Count;
            var checkWordAvgQueue = new Queue<long>(10);

            while (!completion.Task.IsCompleted)
            {
                await Task.Delay(1_000);

                // Calculate the CheckWord/s average
                var checkWordsRun = 0L;
                while (statCheckWord.Reader.TryRead(out var count))
                    checkWordsRun += count;

                if (checkWordAvgQueue.Count == 10)
                    checkWordAvgQueue.Dequeue();
                checkWordAvgQueue.Enqueue(checkWordsRun);

                // Calculate the words/minute average
                var wordsPerMinute = wordsGuessed / stopwatch.Elapsed.TotalMinutes;
                
                var diff = checkWordAvgQueue.Sum() / checkWordAvgQueue.Count;
                
                ctx.Status($"{wordsGuessed:n0} 'words' tested out of {total:n0} at {wordsPerMinute:n0} words/min, averaging {diff:n0} CheckWord/sec. "
                         + $"{total - wordsGuessed:n0} words left, remaining time: {(total - wordsGuessed) / wordsPerMinute:n0} minutes");
            }
        });

        // Read the results
        var results = new List<GuessResult>();
        await foreach (var result in guessChannel.Reader.ReadAllAsync())
        {
            AnsiConsole.MarkupLine($"{result.Guess} - {result.Median:n0} median");
            results.Add(result);
        }

        ctx.Status("Outputting results");

        // Sort the results
        results.Sort((a, b) => a.Median.CompareTo(b.Median));

        await using var fs = File.Open("results.csv", FileMode.Create);
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
                    Span<char> pattern = stackalloc char[] { '-', '-', '-', '-', '-' };
                    MakeGuess(word, "soare", ref pattern);

                    var matches = 0;
                    foreach (var other in possibleSolutions)
                    {
                        if (CheckWord(other, "soare", pattern))
                            ++matches;
                    }

                    while (!soareCountChannel.Writer.TryWrite(matches))
                        Thread.SpinWait(1);
                });

            soareCountChannel.Writer.Complete();
        });

        await using (var soarFile = File.Open("soare.data", FileMode.Create))
        {
            await JsonSerializer.SerializeAsync(
                soarFile, soareCountChannel.Reader.ReadAllAsync(), 
                new JsonSerializerOptions { WriteIndented = true });
        }

        ctx.Status("Done");
    });

record struct GuessResult(string Guess, double Mean, double Median, double Std);