using System;

namespace wordleword;

public class Wordle
{
    public static readonly char[] Zero = { '-', '-', '-', '-', '-' };

    /// <summary>
    /// Make a guess
    /// </summary>
    /// <param name="word">The word to guess (like "perky")</param>
    /// <param name="guess">The guess (like "peers")</param>
    /// <returns>Green and yellow array of indicators (like ['g', 'g', '-', 'y', '-'])</returns>
    public static char[] MakeGuess(string word, string guess)
    {
        Span<char> pattern = stackalloc char[] { '-', '-', '-', '-', '-' };

        MakeGuess(word, guess, ref pattern);

        return pattern.ToArray();
    }

    /// <summary>
    /// Make a guess
    /// </summary>
    /// <param name="word">The word to guess (like "perky")</param>
    /// <param name="guess">The guess (like "peers")</param>
    /// <param name="pattern">The pattern to fill in (like ['g', 'g', '-', 'y', '-'])</param>
    public static void MakeGuess(string word, string guess, ref Span<char> ret)
    {
        Span<char> remaining = stackalloc char[guess.Length];
        for (var i = 0; i < 5; i++)
        {
            if (word[i] == guess[i])
                ret[i] = 'g';
            else
                remaining[i] = word[i];
        }

        for (var i = 0; i < 5; i++)
        {
            var loc = remaining.IndexOf(guess[i]);

            if (ret[i] != 'g' && loc != -1)
                ret[i] = 'y';
        }
    }

    /// <summary>
    /// Determine if the word matches the guess given the pattern
    /// </summary>
    /// <param name="word">word like "perky"</param>
    /// <param name="guess">word like "peers"</param>
    /// <param name="pattern">like ["g", "g", "-", "y", "-"]</param>
    /// <returns>true if the word matches the guess given the pattern</returns>
    public static bool CheckWord(string word, string guess, Span<char> pattern)
    {
        Span<char> remaining = stackalloc char[guess.Length];

        for (var i = 0; i < 5; ++i)
        {
            var g = guess[i];
            var w = word[i];

            if (pattern[i] == 'g')
            {
                if (g != w)
                    return false;
            }
            else
            {
                if (g == w)
                    return false;
                else
                    remaining[i] = w;
            }
        }

        for (var i = 0; i < 5; ++i)
        {
            if (pattern[i] == 'y')
            {
                var loc = remaining.IndexOf(guess[i]);
                if (loc == -1)
                    return false;
            }
        }

        return true;
    }
}