using System;

namespace wordleword;

public class Wordle
{
    /// <summary>
    /// Make a guess
    /// </summary>
    /// <param name="word">The word to guess (like "perky")</param>
    /// <param name="guess">The guess (like "peers")</param>
    /// <returns>Green and yellow array of indicators (like ['g', 'g', '-', 'y', '-'])</returns>
    public static char[] MakeGuess(string word, string guess)
    {
        var ret = new char[] { '-', '-', '-', '-', '-' };

        var remaining = "";
        for (var i = 0; i < 5; i++)
        {
            if (word[i] == guess[i])
                ret[i] = 'g';
            else
                remaining += word[i];
        }

        for (var i = 0; i < 5; i++)
        {
            var loc = remaining.IndexOf(guess[i]);
            if (ret[i] != 'g' && loc != -1)
            {
                ret[i] = 'y';
                remaining = string.Concat(remaining.AsSpan(0, loc), remaining.AsSpan(loc + 1));
            }
        }

        return ret;
    }

    /// <summary>
    /// Determine if the word matches the guess given the pattern
    /// </summary>
    /// <param name="word">word like "perky"</param>
    /// <param name="guess">word like "peers"</param>
    /// <param name="pattern">like ["g", "g", "-", "y", "-"]</param>
    /// <returns>true if the word matches the guess given the pattern</returns>
    public static bool CheckWord(string word, string guess, char[] pattern)
    {
        var remaining = "";

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
                    remaining += w;
            }
        }

        for (var i = 0; i < 5; ++i)
        {
            if (pattern[i] == 'y')
            {
                var loc = remaining.IndexOf(guess[i]);
                if (loc == -1)
                    return false;
                else
                    remaining = string.Concat(remaining.AsSpan(0, loc), remaining.AsSpan(loc + 1));
            }
        }

        return true;
    }
}