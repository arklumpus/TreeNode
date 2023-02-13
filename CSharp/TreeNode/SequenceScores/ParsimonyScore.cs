using PhyloTree.SequenceSimulation;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PhyloTree.SequenceScores
{
    /// <summary>
    /// Exception that is thrown when not enough data has been supplied.
    /// </summary>
    public class MissingDataException : Exception
    {
        /// <inheritdoc/>
        public MissingDataException(string message) : base(message) { }
    }

    /// <summary>
    /// Contains methods to compute parsimony scores for a tree.
    /// </summary>
    public static class ParsimonyScore
    {
        /// <summary>
        /// Computes the parsimony score (minimum number of state changes) for a character on a tree.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character states at the tips of the tree.
        /// This <see cref="Dictionary{String, Char}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state.</param>
        /// <returns>The parsimony score (minimum number of state changes) for the specified character.</returns>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>The character states can be anything; the only thing that is taken into account when computing
        /// parsimony score is whether two states are the same or not. Note that this is case sensitive.</remarks>
        public static int GetParsimonyScore(this TreeNode tree, Dictionary<string, char> tipStates)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            HashSet<char>[] states = new HashSet<char>[nodes.Count];

            int count = 0;

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i].Children.Count == 0)
                {
                    char state;

                    if (tipStates.TryGetValue(nodes[i].Name, out state))
                    {
                        states[i] = new HashSet<char>() { state };
                    }
                    else if (tipStates.TryGetValue(nodes[i].Id, out state))
                    {
                        states[i] = new HashSet<char>() { state };
                    }
                    else
                    {
                        throw new Exception("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                    }
                }
                else
                {
                    HashSet<char> intersection = new HashSet<char>(states[children[i][0]]);

                    for (int j = 1; j < nodes[i].Children.Count; j++)
                    {
                        intersection.IntersectWith(states[children[i][j]]);
                    }

                    if (intersection.Count > 0)
                    {
                        states[i] = intersection;
                    }
                    else
                    {
                        HashSet<char> union = new HashSet<char>(states[children[i][0]]);

                        for (int j = 1; j < nodes[i].Children.Count; j++)
                        {
                            union.UnionWith(states[children[i][j]]);
                        }

                        states[i] = union;

                        count++;
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// Computes the parsimony score (minimum number of state changes) for a character sequence on a tree,
        /// returning the score for each character in the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>An <see cref="T:int[]"/> array containing the parsimony score (minimum number of state changes)
        /// for each character in the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>The character states can be anything; the only thing that is taken into account when computing
        /// parsimony score is whether two states are the same or not. Note that this is case sensitive.</remarks>
        public static int[] GetParsimonyScores(this TreeNode tree, Dictionary<string, string> tipStates, int maxParallelism = -1)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            int sequenceLength = 0;

            foreach (KeyValuePair<string, string> kvp in tipStates)
            {
                if (sequenceLength == 0)
                {
                    sequenceLength = kvp.Value.Length;
                }
                else
                {
                    if (sequenceLength != kvp.Value.Length)
                    {
                        throw new ArgumentException("Not all the sequences have the same length!");
                    }
                }
            }

            int[] siteScores = new int[sequenceLength];


            Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, k =>
            {
                HashSet<char>[] states = new HashSet<char>[nodes.Count];

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        string seq;

                        if (tipStates.TryGetValue(nodes[i].Name, out seq))
                        {
                            states[i] = new HashSet<char>() { seq[k] };
                        }
                        else if (tipStates.TryGetValue(nodes[i].Id, out seq))
                        {
                            states[i] = new HashSet<char>() { seq[k] };
                        }
                        else
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }
                    }
                    else
                    {
                        HashSet<char> intersection = new HashSet<char>(states[children[i][0]]);

                        for (int j = 1; j < nodes[i].Children.Count; j++)
                        {
                            intersection.IntersectWith(states[children[i][j]]);
                        }

                        if (intersection.Count > 0)
                        {
                            states[i] = intersection;
                        }
                        else
                        {
                            HashSet<char> union = new HashSet<char>(states[children[i][0]]);

                            for (int j = 1; j < nodes[i].Children.Count; j++)
                            {
                                union.UnionWith(states[children[i][j]]);
                            }

                            states[i] = union;

                            siteScores[k]++;
                        }
                    }
                }
            });

            return siteScores;
        }

        /// <summary>
        /// Computes the parsimony score (minimum number of state changes) for a character sequence on a tree,
        /// returning the total score for the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The total parsimony score (minimum number of state changes) for the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>The character states can be anything; the only thing that is taken into account when computing
        /// parsimony score is whether two states are the same or not. Note that this is case sensitive.</remarks>
        public static int GetParsimonyScore(this TreeNode tree, Dictionary<string, string> tipStates, int maxParallelism = -1)
        {
            return GetParsimonyScores(tree, tipStates, maxParallelism).Sum();
        }

        /// <summary>
        /// Computes the parsimony score (minimum number of state changes) for a character sequence on a tree,
        /// returning the score for each character in the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>An <see cref="T:int[]"/> array containing the parsimony score (minimum number of state changes)
        /// for each character in the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>The character states can be anything; the only thing that is taken into account when computing
        /// parsimony score is whether two states are the same or not. Note that this is case sensitive.</remarks>
        public static int[] GetParsimonyScores(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, int maxParallelism = -1)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            int sequenceLength = 0;

            foreach (KeyValuePair<string, IReadOnlyList<char>> kvp in tipStates)
            {
                if (sequenceLength == 0)
                {
                    sequenceLength = kvp.Value.Count;
                }
                else
                {
                    if (sequenceLength != kvp.Value.Count)
                    {
                        throw new ArgumentException("Not all the sequences have the same length!");
                    }
                }
            }

            int[] siteScores = new int[sequenceLength];


            Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, k =>
            {
                HashSet<char>[] states = new HashSet<char>[nodes.Count];

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        IReadOnlyList<char> seq;

                        if (tipStates.TryGetValue(nodes[i].Name, out seq))
                        {
                            states[i] = new HashSet<char>() { seq[k] };
                        }
                        else if (tipStates.TryGetValue(nodes[i].Id, out seq))
                        {
                            states[i] = new HashSet<char>() { seq[k] };
                        }
                        else
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }
                    }
                    else
                    {
                        HashSet<char> intersection = new HashSet<char>(states[children[i][0]]);

                        for (int j = 1; j < nodes[i].Children.Count; j++)
                        {
                            intersection.IntersectWith(states[children[i][j]]);
                        }

                        if (intersection.Count > 0)
                        {
                            states[i] = intersection;
                        }
                        else
                        {
                            HashSet<char> union = new HashSet<char>(states[children[i][0]]);

                            for (int j = 1; j < nodes[i].Children.Count; j++)
                            {
                                union.UnionWith(states[children[i][j]]);
                            }

                            states[i] = union;

                            siteScores[k]++;
                        }
                    }
                }
            });

            return siteScores;
        }

        /// <summary>
        /// Computes the parsimony score (minimum number of state changes) for a character sequence on a tree,
        /// returning the total score for the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The total parsimony score (minimum number of state changes) for the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>The character states can be anything; the only thing that is taken into account when computing
        /// parsimony score is whether two states are the same or not. Note that this is case sensitive.</remarks>
        public static int GetParsimonyScore(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, int maxParallelism = -1)
        {
            return GetParsimonyScores(tree, tipStates, maxParallelism).Sum();
        }

        /// <summary>
        /// Computes the parsimony score (minimum number of state changes) for a character sequence on a tree,
        /// returning the score for each character in the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>An <see cref="T:int[]"/> array containing the parsimony score (minimum number of state changes)
        /// for each character in the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>The character states can be anything; the only thing that is taken into account when computing
        /// parsimony score is whether two states are the same or not. Note that this is case sensitive.</remarks>
        public static int[] GetParsimonyScores(this TreeNode tree, Dictionary<string, Sequence> tipStates, int maxParallelism = -1)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            int sequenceLength = 0;

            foreach (KeyValuePair<string, Sequence> kvp in tipStates)
            {
                if (sequenceLength == 0)
                {
                    sequenceLength = kvp.Value.Length;
                }
                else
                {
                    if (sequenceLength != kvp.Value.Length)
                    {
                        throw new ArgumentException("Not all the sequences have the same length!");
                    }
                }
            }

            int[] siteScores = new int[sequenceLength];


            Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, k =>
            {
                HashSet<char>[] states = new HashSet<char>[nodes.Count];

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        Sequence seq;

                        if (tipStates.TryGetValue(nodes[i].Name, out seq))
                        {
                            states[i] = new HashSet<char>() { seq[k] };
                        }
                        else if (tipStates.TryGetValue(nodes[i].Id, out seq))
                        {
                            states[i] = new HashSet<char>() { seq[k] };
                        }
                        else
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }
                    }
                    else
                    {
                        HashSet<char> intersection = new HashSet<char>(states[children[i][0]]);

                        for (int j = 1; j < nodes[i].Children.Count; j++)
                        {
                            intersection.IntersectWith(states[children[i][j]]);
                        }

                        if (intersection.Count > 0)
                        {
                            states[i] = intersection;
                        }
                        else
                        {
                            HashSet<char> union = new HashSet<char>(states[children[i][0]]);

                            for (int j = 1; j < nodes[i].Children.Count; j++)
                            {
                                union.UnionWith(states[children[i][j]]);
                            }

                            states[i] = union;

                            siteScores[k]++;
                        }
                    }
                }
            });

            return siteScores;
        }

        /// <summary>
        /// Computes the parsimony score (minimum number of state changes) for a character sequence on a tree,
        /// returning the total score for the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The total parsimony score (minimum number of state changes) for the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>The character states can be anything; the only thing that is taken into account when computing
        /// parsimony score is whether two states are the same or not. Note that this is case sensitive.</remarks>
        public static int GetParsimonyScore(this TreeNode tree, Dictionary<string, Sequence> tipStates, int maxParallelism = -1)
        {
            return GetParsimonyScores(tree, tipStates, maxParallelism).Sum();
        }

        /// <summary>
        /// Computes the Sankoff parsimony score for a character on a tree.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character states at the tips of the tree.
        /// This <see cref="Dictionary{String, Char}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state.</param>
        /// <param name="states">The character states.</param>
        /// <param name="transitionCosts">The transition cost matrix. Indices in this matrix should correspond to the
        /// states in the <paramref name="states"/> array.</param>
        /// <returns>The Sankoff parsimony score for the specified character.</returns>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>Note that diagonal entries in the cost matrix may not be 0, in which case even retaining the same
        /// state across a branch will incur a cost. This may or may not be useful.</remarks>
        public static double GetSankoffParsimonyScore(this TreeNode tree, Dictionary<string, char> tipStates, char[] states, double[,] transitionCosts)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            double[][] stateScores = new double[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i].Children.Count == 0)
                {
                    char state;

                    if (!tipStates.TryGetValue(nodes[i].Name, out state) && !tipStates.TryGetValue(nodes[i].Id, out state))
                    {
                        throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                    }

                    stateScores[i] = new double[states.Length];

                    int index = stateIndices[state];
                    for (int j = 0; j < states.Length; j++)
                    {
                        if (j != index)
                        {
                            stateScores[i][j] = double.PositiveInfinity;
                        }
                        else
                        {
                            stateScores[i][j] = 0;
                        }
                    }
                }
                else
                {
                    stateScores[i] = new double[states.Length];

                    for (int j = 0; j < states.Length; j++)
                    {
                        stateScores[i][j] = 0;

                        for (int l = 0; l < children[i].Length; l++)
                        {
                            double scoreL = double.PositiveInfinity;

                            for (int k = 0; k < states.Length; k++)
                            {
                                scoreL = Math.Min(scoreL, transitionCosts[j, k] + stateScores[children[i][l]][k]);
                            }

                            stateScores[i][j] += scoreL;
                        }
                    }
                }
            }

            return stateScores[0].Min();
        }

        /// <summary>
        /// Computes the Sankoff parsimony score for a character sequence on a tree,
        /// returning the score for each character in the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="states">The character states.</param>
        /// <param name="transitionCosts">The transition cost matrix. Indices in this matrix should correspond to the
        /// states in the <paramref name="states"/> array.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>An <see cref="T:double[]"/> array containing the Sankoff parsimony score
        /// for each character in the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>Note that diagonal entries in the cost matrix may not be 0, in which case even retaining the same
        /// state across a branch will incur a cost. This may or may not be useful.</remarks>
        public static double[] GetSankoffParsimonyScores(this TreeNode tree, Dictionary<string, string> tipStates, char[] states, double[,] transitionCosts, int maxParallelism = -1)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            int sequenceLength = 0;

            foreach (KeyValuePair<string, string> kvp in tipStates)
            {
                if (sequenceLength == 0)
                {
                    sequenceLength = kvp.Value.Length;
                }
                else
                {
                    if (sequenceLength != kvp.Value.Length)
                    {
                        throw new ArgumentException("Not all the sequences have the same length!");
                    }
                }
            }

            double[] siteScores = new double[sequenceLength];

            Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, siteIndex =>
            {
                double[][] stateScores = new double[nodes.Count][];

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        string seq;

                        if (!tipStates.TryGetValue(nodes[i].Name, out seq) && !tipStates.TryGetValue(nodes[i].Id, out seq))
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }

                        char state = seq[siteIndex];

                        stateScores[i] = new double[states.Length];

                        int index = stateIndices[state];
                        for (int j = 0; j < states.Length; j++)
                        {
                            if (j != index)
                            {
                                stateScores[i][j] = double.PositiveInfinity;
                            }
                            else
                            {
                                stateScores[i][j] = 0;
                            }
                        }
                    }
                    else
                    {
                        stateScores[i] = new double[states.Length];

                        for (int j = 0; j < states.Length; j++)
                        {
                            stateScores[i][j] = 0;

                            for (int l = 0; l < children[i].Length; l++)
                            {
                                double scoreL = double.PositiveInfinity;

                                for (int k = 0; k < states.Length; k++)
                                {
                                    scoreL = Math.Min(scoreL, transitionCosts[j, k] + stateScores[children[i][l]][k]);
                                }

                                stateScores[i][j] += scoreL;
                            }
                        }
                    }
                }

                siteScores[siteIndex] = stateScores[0].Min();
            });

            return siteScores;
        }

        /// <summary>
        /// Computes the Sankoff parsimony score for a character sequence on a tree,
        /// returning the total score for the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="states">The character states.</param>
        /// <param name="transitionCosts">The transition cost matrix. Indices in this matrix should correspond to the
        /// states in the <paramref name="states"/> array.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The total Sankoff parsimony score for the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>Note that diagonal entries in the cost matrix may not be 0, in which case even retaining the same
        /// state across a branch will incur a cost. This may or may not be useful.</remarks>
        public static double GetSankoffParsimonyScore(this TreeNode tree, Dictionary<string, string> tipStates, char[] states, double[,] transitionCosts, int maxParallelism = -1)
        {
            return tree.GetSankoffParsimonyScores(tipStates, states, transitionCosts, maxParallelism).Sum();
        }

        /// <summary>
        /// Computes the Sankoff parsimony score for a character sequence on a tree,
        /// returning the score for each character in the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="states">The character states.</param>
        /// <param name="transitionCosts">The transition cost matrix. Indices in this matrix should correspond to the
        /// states in the <paramref name="states"/> array.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>An <see cref="T:double[]"/> array containing the Sankoff parsimony score
        /// for each character in the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>Note that diagonal entries in the cost matrix may not be 0, in which case even retaining the same
        /// state across a branch will incur a cost. This may or may not be useful.</remarks>
        public static double[] GetSankoffParsimonyScores(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, char[] states, double[,] transitionCosts, int maxParallelism = -1)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            int sequenceLength = 0;

            foreach (KeyValuePair<string, IReadOnlyList<char>> kvp in tipStates)
            {
                if (sequenceLength == 0)
                {
                    sequenceLength = kvp.Value.Count;
                }
                else
                {
                    if (sequenceLength != kvp.Value.Count)
                    {
                        throw new ArgumentException("Not all the sequences have the same length!");
                    }
                }
            }

            double[] siteScores = new double[sequenceLength];

            Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, siteIndex =>
            {
                double[][] stateScores = new double[nodes.Count][];

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        IReadOnlyList<char> seq;

                        if (!tipStates.TryGetValue(nodes[i].Name, out seq) && !tipStates.TryGetValue(nodes[i].Id, out seq))
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }

                        char state = seq[siteIndex];

                        stateScores[i] = new double[states.Length];

                        int index = stateIndices[state];
                        for (int j = 0; j < states.Length; j++)
                        {
                            if (j != index)
                            {
                                stateScores[i][j] = double.PositiveInfinity;
                            }
                            else
                            {
                                stateScores[i][j] = 0;
                            }
                        }
                    }
                    else
                    {
                        stateScores[i] = new double[states.Length];

                        for (int j = 0; j < states.Length; j++)
                        {
                            stateScores[i][j] = 0;

                            for (int l = 0; l < children[i].Length; l++)
                            {
                                double scoreL = double.PositiveInfinity;

                                for (int k = 0; k < states.Length; k++)
                                {
                                    scoreL = Math.Min(scoreL, transitionCosts[j, k] + stateScores[children[i][l]][k]);
                                }

                                stateScores[i][j] += scoreL;
                            }
                        }
                    }
                }

                siteScores[siteIndex] = stateScores[0].Min();
            });

            return siteScores;
        }

        /// <summary>
        /// Computes the Sankoff parsimony score for a character sequence on a tree,
        /// returning the total score for the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="states">The character states.</param>
        /// <param name="transitionCosts">The transition cost matrix. Indices in this matrix should correspond to the
        /// states in the <paramref name="states"/> array.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The total Sankoff parsimony score for the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>Note that diagonal entries in the cost matrix may not be 0, in which case even retaining the same
        /// state across a branch will incur a cost. This may or may not be useful.</remarks>
        public static double GetSankoffParsimonyScore(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, char[] states, double[,] transitionCosts, int maxParallelism = -1)
        {
            return tree.GetSankoffParsimonyScores(tipStates, states, transitionCosts, maxParallelism).Sum();
        }


        /// <summary>
        /// Computes the Sankoff parsimony score for a character sequence on a tree,
        /// returning the score for each character in the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="states">The character states.</param>
        /// <param name="transitionCosts">The transition cost matrix. Indices in this matrix should correspond to the
        /// states in the <paramref name="states"/> array.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>An <see cref="T:double[]"/> array containing the Sankoff parsimony score
        /// for each character in the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>Note that diagonal entries in the cost matrix may not be 0, in which case even retaining the same
        /// state across a branch will incur a cost. This may or may not be useful.</remarks>
        public static double[] GetSankoffParsimonyScores(this TreeNode tree, Dictionary<string, Sequence> tipStates, char[] states, double[,] transitionCosts, int maxParallelism = -1)
        {
            List<TreeNode> nodes = tree.GetChildrenRecursive();

            Dictionary<string, int> indices = new Dictionary<string, int>();
            int[][] children = new int[nodes.Count][];

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                indices[nodes[i].Id] = i;

                children[i] = new int[nodes[i].Children.Count];

                for (int j = 0; j < nodes[i].Children.Count; j++)
                {
                    children[i][j] = indices[nodes[i].Children[j].Id];
                }
            }

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            int sequenceLength = 0;

            foreach (KeyValuePair<string, Sequence> kvp in tipStates)
            {
                if (sequenceLength == 0)
                {
                    sequenceLength = kvp.Value.Length;
                }
                else
                {
                    if (sequenceLength != kvp.Value.Length)
                    {
                        throw new ArgumentException("Not all the sequences have the same length!");
                    }
                }
            }

            double[] siteScores = new double[sequenceLength];

            Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, siteIndex =>
            {
                double[][] stateScores = new double[nodes.Count][];

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        Sequence seq;

                        if (!tipStates.TryGetValue(nodes[i].Name, out seq) && !tipStates.TryGetValue(nodes[i].Id, out seq))
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }

                        char state = seq[siteIndex];

                        stateScores[i] = new double[states.Length];

                        int index = stateIndices[state];
                        for (int j = 0; j < states.Length; j++)
                        {
                            if (j != index)
                            {
                                stateScores[i][j] = double.PositiveInfinity;
                            }
                            else
                            {
                                stateScores[i][j] = 0;
                            }
                        }
                    }
                    else
                    {
                        stateScores[i] = new double[states.Length];

                        for (int j = 0; j < states.Length; j++)
                        {
                            stateScores[i][j] = 0;

                            for (int l = 0; l < children[i].Length; l++)
                            {
                                double scoreL = double.PositiveInfinity;

                                for (int k = 0; k < states.Length; k++)
                                {
                                    scoreL = Math.Min(scoreL, transitionCosts[j, k] + stateScores[children[i][l]][k]);
                                }

                                stateScores[i][j] += scoreL;
                            }
                        }
                    }
                }

                siteScores[siteIndex] = stateScores[0].Min();
            });

            return siteScores;
        }

        /// <summary>
        /// Computes the Sankoff parsimony score for a character sequence on a tree,
        /// returning the total score for the sequence.
        /// </summary>
        /// <param name="tree">The tree for which the parsimony score is computed.</param>
        /// <param name="tipStates">The character state sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="states">The character states.</param>
        /// <param name="transitionCosts">The transition cost matrix. Indices in this matrix should correspond to the
        /// states in the <paramref name="states"/> array.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The total Sankoff parsimony score for the sequence.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        /// <remarks>Note that diagonal entries in the cost matrix may not be 0, in which case even retaining the same
        /// state across a branch will incur a cost. This may or may not be useful.</remarks>
        public static double GetSankoffParsimonyScore(this TreeNode tree, Dictionary<string, Sequence> tipStates, char[] states, double[,] transitionCosts, int maxParallelism = -1)
        {
            return tree.GetSankoffParsimonyScores(tipStates, states, transitionCosts, maxParallelism).Sum();
        }
    }
}
