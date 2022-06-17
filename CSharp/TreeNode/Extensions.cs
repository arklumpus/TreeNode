using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

/// <summary>
/// Contains useful extension methods.
/// </summary>
namespace PhyloTree.Extensions
{
    /// <summary>
    /// Useful extension methods
    /// </summary>
    public static class TypeExtensions
    {
        /// <summary>
        /// Determines whether <paramref name="haystack"/> contains all of the elements in <paramref name="needle"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the collections.</typeparam>
        /// <param name="haystack">The collection in which to search.</param>
        /// <param name="needle">The items to be searched.</param>
        /// <returns><c>true</c> if haystack contains all of the elements that are in needle or needle is empty.</returns>
        public static bool ContainsAll<T>(this IEnumerable<T> haystack, IEnumerable<T> needle)
        {
            Contract.Requires(needle != null);

            foreach (T t in needle)
            {
                if (!haystack.Contains(t))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Compute the median of a list of values.
        /// </summary>
        /// <param name="array">The list of values whose median is to be computed.</param>
        /// <returns>The median of the list of values.</returns>
        public static double Median(this IEnumerable<double> array)
        {
            List<double> ordered = new List<double>(array);
            ordered.Sort();

            if (ordered.Count % 2 == 0)
            {
                return 0.5 * (ordered[ordered.Count / 2] + ordered[ordered.Count / 2 - 1]);
            }
            else
            {
                return ordered[ordered.Count / 2];
            }
        }

        /// <summary>
        /// Determines whether <paramref name="haystack"/> contains at least one of the elements in <paramref name="needle"/>.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the collections.</typeparam>
        /// <param name="haystack">The collection in which to search.</param>
        /// <param name="needle">The items to be searched.</param>
        /// <returns><c>true</c> if haystack contains at least one of the elements that are in needle. Returns <c>false</c> if needle is empty.</returns>
        public static bool ContainsAny<T>(this IEnumerable<T> haystack, IEnumerable<T> needle)
        {
            Contract.Requires(needle != null);

            foreach (T t in needle)
            {
                if (haystack.Contains(t))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Computes the intersection between two sets.
        /// </summary>
        /// <typeparam name="T">The type of the elements in the collections.</typeparam>
        /// <param name="set1">The first set.</param>
        /// <param name="set2">The second set.</param>
        /// <returns>The intersection between the two sets.</returns>
        public static IEnumerable<T> Intersection<T>(this IEnumerable<T> set1, IEnumerable<T> set2)
        {
            Contract.Requires(set1 != null);
            Contract.Requires(set2 != null);

            foreach (T element in set1)
            {
                if (set2.Contains(element))
                {
                    yield return element;
                }
            }
        }

        /// <summary>
        /// Constructs a consensus tree.
        /// </summary>
        /// <param name="trees">The collection of trees whose consensus is to be computed.</param>
        /// <param name="rooted">Whether the consensus tree should be rooted or not.</param>
        /// <param name="clockLike">Whether the trees are to be treated as clock-like trees or not. This has an effect on how the branch lengths of the consensus tree are computed.</param>
        /// <param name="threshold">The (inclusive) threshold for splits to be included in the consensus tree. Use <c>0</c> to get all compatible splits, <c>0.5</c> for a majority-rule consensus or <c>1</c> for a strict consensus.</param>
        /// <param name="useMedian">If this is <c>true</c>, the lengths of the branches in the tree will be computed based on the median length/age of the splits used to build the tree. Otherwise, the mean will be used.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked as the trees are processed.</param>
        /// <returns>A rooted consensus tree.</returns>
        public static TreeNode GetConsensus(this IEnumerable<TreeNode> trees, bool rooted, bool clockLike, double threshold, bool useMedian, Action<double> progressAction = null)
        {
            Contract.Requires(trees != null);

            Dictionary<string, List<double>> splits = new Dictionary<string, List<double>>();

            int totalTrees = 0;

            Split.LengthTypes lengthType = clockLike ? Split.LengthTypes.Age : Split.LengthTypes.Length;

            int count = -1;

            if (trees is IReadOnlyList<TreeNode> list)
            {
                count = list.Count;
            }

            foreach (TreeNode tree in trees)
            {
                List<Split> treeSplits = tree.GetSplits(lengthType);

                for (int i = 0; i < treeSplits.Count; i++)
                {
                    if (splits.TryGetValue(treeSplits[i].Name, out List<double> splitLengths))
                    {
                        splits[treeSplits[i].Name].Add(treeSplits[i].Length);
                    }
                    else
                    {
                        splits.Add(treeSplits[i].Name, new List<double>() { treeSplits[i].Length });
                    }
                }

                totalTrees++;

                if (count > 0)
                {
                    progressAction?.Invoke((double)totalTrees / count);
                }
                else
                {
                    progressAction?.Invoke(totalTrees);
                }
            }

            List<Split> orderedSplits = new List<Split>(from el in splits orderby el.Value.Count descending where ((double)el.Value.Count / (double)totalTrees) >= threshold select new Split(el.Key, (useMedian ? el.Value.Median() : el.Value.Average()), lengthType, ((double)el.Value.Count / (double)totalTrees)));

            List<Split> finalSplits = new List<Split>();

            for (int i = 0; i < orderedSplits.Count; i++)
            {
                if (orderedSplits[i].IsCompatible(finalSplits))
                {
                    finalSplits.Add(orderedSplits[i]);
                }
            }

            return Split.BuildTree(finalSplits, rooted);
        }

        /// <summary>
        /// Reads the next non-whitespace character, taking into account quoting and escaping.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> to read from.</param>
        /// <param name="escaping">A <see cref="bool"/> indicating whether the next character will be escaped.</param>
        /// <param name="escaped">A <see cref="bool"/> indicating whether the current character will be escaped.</param>
        /// <param name="openQuotes">A <see cref="bool"/> indicating whether double quotes have been opened.</param>
        /// <param name="openApostrophe">A <see cref="bool"/> indicating whether single quotes have been opened.</param>
        /// <param name="eof">A <see cref="bool"/> indicating whether we have arrived at the end of the file.</param>
        /// <returns>The next non-whitespace character.</returns>
        public static char NextToken(this TextReader reader, ref bool escaping, out bool escaped, ref bool openQuotes, ref bool openApostrophe, out bool eof)
        {
            Contract.Requires(reader != null);

            int i = reader.Read();

            if (i < 0)
            {
                eof = true;
                escaped = false;
                return (char)i;
            }

            eof = false;
            char c = (char)i;

            if (!escaping)
            {
                escaped = false;
                if (!openQuotes && !openApostrophe)
                {
                    while (Char.IsWhiteSpace(c))
                    {
                        i = reader.Read();

                        if (i < 0)
                        {
                            eof = true;
                            escaped = false;
                            return (char)i;
                        }

                        c = (char)i;
                    }

                    switch (c)
                    {
                        case '\\':
                            escaping = true;
                            break;
                        case '"':
                            openQuotes = true;
                            break;
                        case '\'':
                            openApostrophe = true;
                            break;
                    }
                }
                else if (openQuotes)
                {
                    switch (c)
                    {
                        case '"':
                            openQuotes = false;
                            break;
                        case '\\':
                            escaping = true;
                            break;
                    }
                }
                else if (openApostrophe)
                {
                    switch (c)
                    {
                        case '\'':
                            openApostrophe = false;
                            break;
                        case '\\':
                            escaping = true;
                            break;
                    }
                }
            }
            else
            {
                escaping = false;
                escaped = true;
            }

            return c;
        }

        /// <summary>
        /// Reads the next word, taking into account whitespaces, square brackets, commas and semicolons.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> to read from.</param>
        /// <param name="eof">A <see cref="bool"/> indicating whether we have arrived at the end of the file.</param>
        /// <returns>The next word.</returns>
        public static string NextWord(this TextReader reader, out bool eof)
        {
            Contract.Requires(reader != null);

            StringBuilder sb = new StringBuilder();

            int c = reader.Read();

            while (c >= 0 && Char.IsWhiteSpace((char)c))
            {
                c = reader.Read();
            }

            if (c >= 0)
            {
                sb.Append((char)c);
            }

            if ((char)c == '[' || (char)c == ']' || (char)c == ',' || (char)c == ';')
            {
                eof = false;
                return sb.ToString();
            }

            c = reader.Peek();

            while (c >= 0 && !Char.IsWhiteSpace((char)c))
            {
                if ((char)c == '[' || (char)c == ']' || (char)c == ',' || (char)c == ';')
                {
                    break;
                }
                c = reader.Read();
                sb.Append((char)c);
                c = reader.Peek();
            }

            if (c < 0)
            {
                eof = true;
            }
            else
            {
                eof = false;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Reads the next word, taking into account whitespaces, square brackets, commas and semicolons.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> to read from.</param>
        /// <param name="eof">A <see cref="bool"/> indicating whether we have arrived at the end of the file.</param>
        /// <param name="headingTrivia">A string containing any whitespace that was discarding before the start of the word.</param>
        /// <returns>The next word.</returns>
        public static string NextWord(this TextReader reader, out bool eof, out string headingTrivia)
        {
            Contract.Requires(reader != null);

            StringBuilder sb = new StringBuilder();

            StringBuilder headingTriviaBuilder = new StringBuilder();
            StringBuilder trailingTriviaBuilder = new StringBuilder();

            int c = reader.Read();

            while (c >= 0 && Char.IsWhiteSpace((char)c))
            {
                headingTriviaBuilder.Append((char)c);
                c = reader.Read();
            }

            headingTrivia = headingTriviaBuilder.ToString();

            if (c >= 0)
            {
                sb.Append((char)c);
            }

            if ((char)c == '[' || (char)c == ']' || (char)c == ',' || (char)c == ';')
            {
                eof = false;
                return sb.ToString();
            }

            c = reader.Peek();

            while (c >= 0 && !Char.IsWhiteSpace((char)c))
            {
                if ((char)c == '[' || (char)c == ']' || (char)c == ',' || (char)c == ';')
                {
                    break;
                }
                c = reader.Read();
                sb.Append((char)c);
                c = reader.Peek();
            }

            if (c < 0)
            {
                eof = true;
            }
            else
            {
                eof = false;
            }

            return sb.ToString();
        }
    }
}