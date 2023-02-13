using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using PhyloTree.SequenceSimulation;
using PhyloTree.TreeBuilding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PhyloTree.SequenceScores
{
    /// <summary>
    /// Contains methods to compute likelihood scores on a tree.
    /// </summary>
    public static class LikelihoodScores
    {
        private static int MaxInd(this double[] arr, double[] multipArr)
        {
            int tbr = 0;

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] > arr[tbr] && multipArr[i] > 0)
                {
                    tbr = i;
                }
            }

            return tbr;
        }

        private static int MaxInd(this double[] arr)
        {
            int tbr = 0;

            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] > arr[tbr])
                {
                    tbr = i;
                }
            }

            return tbr;
        }

        private static double Log1p(double x)
        {
            if (x <= -1)
            {
                return double.NegativeInfinity;
            }
            else if (Math.Abs(x) > 0.0001)
            {
                return Math.Log(1 + x);
            }
            else
            {
                return (1 - 0.5 * x) * x;
            }
        }

        const double Tolerance = 1e-7;

        private static void TimesLogVectorAndAdd(this Matrix<double> mat, double[] logVector, double[] addToVector)
        {
            int maxInd = logVector.MaxInd();

            for (int i = 0; i < mat.RowCount; i++)
            {
                double toBeAdded = logVector[maxInd] + Math.Log(mat[i, maxInd]);

                double log1pArg = 0;

                for (var j = 0; j < mat.ColumnCount; j++)
                {
                    if (j != maxInd)
                    {
                        log1pArg += mat[i, j] / mat[i, maxInd] * Math.Exp(logVector[j] - logVector[maxInd]);
                    }
                }

                if (!double.IsNaN(log1pArg))
                {
                    toBeAdded += Log1p(log1pArg);
                    addToVector[i] += toBeAdded;
                    if (addToVector[i] > Tolerance)
                    {
                        addToVector[i] = double.NaN;
                    }
                    else if (addToVector[i] > 0)
                    {
                        addToVector[i] = 0;
                    }
                }
                else
                {
                    double logArg = 0;
                    for (var j = 0; j < mat.ColumnCount; j++)
                    {
                        logArg += mat[i, j] * Math.Exp(logVector[j]);
                    }

                    addToVector[i] += Math.Log(logArg);

                    if (addToVector[i] > Tolerance)
                    {
                        addToVector[i] = double.NaN;
                    }
                    else if (addToVector[i] > 0)
                    {
                        addToVector[i] = 0;
                    }
                }
            }
        }

        private static double LogSumExpTimes(double[] logs, double[] multipliers)
        {
            int maxInd = logs.MaxInd(multipliers);

            double log1pArg = 0;

            for (int i = 0; i < logs.Length; i++)
            {
                if (i != maxInd)
                {
                    log1pArg += Math.Exp(logs[i] - logs[maxInd]) * (multipliers[i] / multipliers[maxInd]);
                }
            }

            if (!double.IsNaN(log1pArg))
            {
                return logs[maxInd] + Math.Log(multipliers[maxInd]) + Log1p(log1pArg);
            }
            else
            {
                double logArg = 0;

                for (int i = 0; i < logs.Length; i++)
                {
                    logArg += Math.Exp(logs[i]) * multipliers[i];
                }

                return Math.Log(logArg);
            }
        }

        /// <summary>
        /// Computes the likelihood for the specified character using the specified rate matrix and evolutionary rate.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The character states at the tips of the tree.
        /// This <see cref="Dictionary{String, Char}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="rate">The overall evolutionary rate of the character.</param>
        /// <returns>The log-likelihood for the specified character.</returns>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static double GetLogLikelihood(this TreeNode tree, Dictionary<string, char> tipStates, RateMatrix rateMatrix, double rate)
        {
            char[] states = rateMatrix.GetStates();

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            Matrix<double> actualMatrix = rateMatrix.GetMatrix();

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

            double[] equilibriumFrequencies = rateMatrix.GetEquilibriumFrequencies();
            double[][] logLikelihoods = new double[nodes.Count][];
            MatrixExponential cachedExp = actualMatrix.FastExponential(1);

            for (int i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i].Children.Count == 0)
                {
                    char state;

                    if (!tipStates.TryGetValue(nodes[i].Name, out state) && !tipStates.TryGetValue(nodes[i].Id, out state))
                    {
                        throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                    }

                    logLikelihoods[i] = new double[states.Length];

                    if (state != '-')
                    {
                        int index = stateIndices[state];
                        for (int j = 0; j < states.Length; j++)
                        {
                            if (j != index)
                            {
                                logLikelihoods[i][j] = double.NegativeInfinity;
                            }
                            else
                            {
                                logLikelihoods[i][j] = 0;
                            }
                        }
                    }
                }
                else
                {
                    logLikelihoods[i] = new double[states.Length];

                    for (int j = 0; j < children[i].Length; j++)
                    {
                        Matrix<double> matrix = actualMatrix.FastExponential(rate * nodes[children[i][j]].Length, cachedExp).Result;
                        matrix.TimesLogVectorAndAdd(logLikelihoods[children[i][j]], logLikelihoods[i]);
                    }
                }
            }

            return LogSumExpTimes(logLikelihoods[0], equilibriumFrequencies);
        }

        /// <summary>
        /// Estimates the maximum-likelihood evolutionary rate for the specified character under the specified rate matrix and computes the
        /// likelihood.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The character states at the tips of the tree.
        /// This <see cref="Dictionary{String, Char}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <returns>The maximum-likelihood rate estimate and the log-likelihood for the specified character.</returns>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static (double rateMLE, double logLikelihood) GetLogLikelihood(this TreeNode tree, Dictionary<string, char> tipStates, RateMatrix rateMatrix)
        {
            char[] states = rateMatrix.GetStates();

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            Matrix<double> actualMatrix = rateMatrix.GetMatrix();

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

            double[] equilibriumFrequencies = rateMatrix.GetEquilibriumFrequencies();

            double likelihoodFunction(double rate)
            {
                double[][] logLikelihoods = new double[nodes.Count][];
                MatrixExponential cachedExp = actualMatrix.FastExponential(1);

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        char state;

                        if (!tipStates.TryGetValue(nodes[i].Name, out state) && !tipStates.TryGetValue(nodes[i].Id, out state))
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }

                        logLikelihoods[i] = new double[states.Length];

                        if (state != '-')
                        {
                            int index = stateIndices[state];
                            for (int j = 0; j < states.Length; j++)
                            {
                                if (j != index)
                                {
                                    logLikelihoods[i][j] = double.NegativeInfinity;
                                }
                                else
                                {
                                    logLikelihoods[i][j] = 0;
                                }
                            }
                        }
                    }
                    else
                    {
                        logLikelihoods[i] = new double[states.Length];

                        for (int j = 0; j < children[i].Length; j++)
                        {
                            Matrix<double> matrix = actualMatrix.FastExponential(rate * nodes[children[i][j]].Length, cachedExp).Result;
                            matrix.TimesLogVectorAndAdd(logLikelihoods[children[i][j]], logLikelihoods[i]);
                        }
                    }
                }

                return LogSumExpTimes(logLikelihoods[0], equilibriumFrequencies);
            }

            double mleRate = MathNet.Numerics.FindMinimum.OfScalarFunction(x => -likelihoodFunction(x), 1);

            return (mleRate, likelihoodFunction(mleRate));
        }

        /// <summary>
        /// Computes the likelihood for a sequence alignment on a tree, using the specified rate matrix and evolutionary rate,
        /// and returns the likelihood for each site.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="rate">The overall evolutionary rate of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>A <see cref="T:double[]"/> array containing the log-likelihood for each site in the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static double[] GetLogLikelihoods(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, RateMatrix rateMatrix, double rate, int maxParallelism = -1)
        {
            char[] states = rateMatrix.GetStates();

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            Matrix<double> actualMatrix = rateMatrix.GetMatrix();

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

            double[] siteLikelihoods = new double[sequenceLength];


            double[] equilibriumFrequencies = rateMatrix.GetEquilibriumFrequencies();
            MatrixExponential cachedExp = actualMatrix.FastExponential(1);

            Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, siteIndex =>
            {
                double[][] logLikelihoods = new double[nodes.Count][];

                for (int i = nodes.Count - 1; i >= 0; i--)
                {
                    if (nodes[i].Children.Count == 0)
                    {
                        IReadOnlyList<char> seq;

                        if (!tipStates.TryGetValue(nodes[i].Name, out seq) && !tipStates.TryGetValue(nodes[i].Id, out seq))
                        {
                            throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                        }

                        logLikelihoods[i] = new double[states.Length];

                        if (seq[siteIndex] != '-')
                        {
                            int index = stateIndices[seq[siteIndex]];
                            for (int j = 0; j < states.Length; j++)
                            {
                                if (j != index)
                                {
                                    logLikelihoods[i][j] = double.NegativeInfinity;
                                }
                                else
                                {
                                    logLikelihoods[i][j] = 0;
                                }
                            }
                        }
                    }
                    else
                    {
                        logLikelihoods[i] = new double[states.Length];

                        for (int j = 0; j < children[i].Length; j++)
                        {
                            Matrix<double> matrix = actualMatrix.FastExponential(rate * nodes[children[i][j]].Length, cachedExp).Result;
                            matrix.TimesLogVectorAndAdd(logLikelihoods[children[i][j]], logLikelihoods[i]);
                        }
                    }
                }

                siteLikelihoods[siteIndex] = LogSumExpTimes(logLikelihoods[0], equilibriumFrequencies);
            }); ;

            return siteLikelihoods;
        }

        /// <summary>
        /// Computes the likelihood for a sequence alignment on a tree, using the specified rate matrix and evolutionary rate,
        /// and returns the total likelihood for the tree.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="rate">The overall evolutionary rate of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The log-likelihood for the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static double GetLogLikelihood(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, RateMatrix rateMatrix, double rate, int maxParallelism = -1)
        {
            return GetLogLikelihoods(tree, tipStates, rateMatrix, rate, maxParallelism).Sum();
        }


        /// <summary>
        /// Computes the likelihood for a sequence alignment on a tree, using the specified rate matrix and evolutionary rate,
        /// and returns the likelihood for each site.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="rate">The overall evolutionary rate of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>A <see cref="T:double[]"/> array containing the log-likelihood for each site in the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static double[] GetLogLikelihoods(this TreeNode tree, Dictionary<string, Sequence> tipStates, RateMatrix rateMatrix, double rate, int maxParallelism)
        {
            return GetLogLikelihoods(tree, new Dictionary<string, IReadOnlyList<char>>(from el in tipStates select new KeyValuePair<string, IReadOnlyList<char>>(el.Key, el.Value)), rateMatrix, rate, maxParallelism);
        }

        /// <summary>
        /// Computes the likelihood for a sequence alignment on a tree, using the specified rate matrix and evolutionary rate,
        /// and returns the total likelihood for the tree.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="rate">The overall evolutionary rate of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The log-likelihood for the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static double GetLogLikelihood(this TreeNode tree, Dictionary<string, Sequence> tipStates, RateMatrix rateMatrix, double rate, int maxParallelism = -1)
        {
            return GetLogLikelihoods(tree, tipStates, rateMatrix, rate, maxParallelism).Sum();
        }

        /// <summary>
        /// Computes the likelihood for a sequence alignment on a tree, using the specified rate matrix and evolutionary rate,
        /// and returns the likelihood for each site.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="rate">The overall evolutionary rate of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>A <see cref="T:double[]"/> array containing the log-likelihood for each site in the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static double[] GetLogLikelihoods(this TreeNode tree, Dictionary<string, string> tipStates, RateMatrix rateMatrix, double rate, int maxParallelism = -1)
        {
            return GetLogLikelihoods(tree, new Dictionary<string, IReadOnlyList<char>>(from el in tipStates select new KeyValuePair<string, IReadOnlyList<char>>(el.Key, el.Value.ToCharArray())), rateMatrix, rate, maxParallelism);
        }

        /// <summary>
        /// Computes the likelihood for a sequence alignment on a tree, using the specified rate matrix and evolutionary rate,
        /// and returns the total likelihood for the tree.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="rate">The overall evolutionary rate of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The log-likelihood for the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static double GetLogLikelihood(this TreeNode tree, Dictionary<string, string> tipStates, RateMatrix rateMatrix, double rate, int maxParallelism = -1)
        {
            return GetLogLikelihoods(tree, tipStates, rateMatrix, rate, maxParallelism).Sum();
        }


        /// <summary>
        /// Estimates the maximum-likelihood evolutionary rate for the specified sequence alignment under the specified rate matrix and computes the
        /// likelihood.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>A <see cref="T:double[]"/> array containing the log-likelihood for each site in the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static (double rateMLE, double[] logLikelihoods) GetLogLikelihoods(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, RateMatrix rateMatrix, int maxParallelism = -1)
        {
            char[] states = rateMatrix.GetStates();

            Dictionary<char, int> stateIndices = new Dictionary<char, int>();

            for (int i = 0; i < states.Length; i++)
            {
                stateIndices[states[i]] = i;
            }

            Matrix<double> actualMatrix = rateMatrix.GetMatrix();

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

            double[] equilibriumFrequencies = rateMatrix.GetEquilibriumFrequencies();
            MatrixExponential cachedExp = actualMatrix.FastExponential(1);

            double[] likelihoodFunction(double rate)
            {
                double[] siteLikelihoods = new double[sequenceLength];

                Parallel.For(0, sequenceLength, new ParallelOptions() { MaxDegreeOfParallelism = maxParallelism }, siteIndex =>
                {
                    double[][] logLikelihoods = new double[nodes.Count][];

                    for (int i = nodes.Count - 1; i >= 0; i--)
                    {
                        if (nodes[i].Children.Count == 0)
                        {
                            IReadOnlyList<char> seq;

                            if (!tipStates.TryGetValue(nodes[i].Name, out seq) && !tipStates.TryGetValue(nodes[i].Id, out seq))
                            {
                                throw new MissingDataException("Tip " + nodes[i].Name + " (id: " + nodes[i].Id + ") has no associated state!");
                            }

                            logLikelihoods[i] = new double[states.Length];

                            if (seq[siteIndex] != '-')
                            {
                                int index = stateIndices[seq[siteIndex]];
                                for (int j = 0; j < states.Length; j++)
                                {
                                    if (j != index)
                                    {
                                        logLikelihoods[i][j] = double.NegativeInfinity;
                                    }
                                    else
                                    {
                                        logLikelihoods[i][j] = 0;
                                    }
                                }
                            }
                        }
                        else
                        {
                            logLikelihoods[i] = new double[states.Length];

                            for (int j = 0; j < children[i].Length; j++)
                            {
                                Matrix<double> matrix = actualMatrix.FastExponential(rate * nodes[children[i][j]].Length, cachedExp).Result;
                                matrix.TimesLogVectorAndAdd(logLikelihoods[children[i][j]], logLikelihoods[i]);
                            }
                        }
                    }

                    siteLikelihoods[siteIndex] = LogSumExpTimes(logLikelihoods[0], equilibriumFrequencies);
                });

                return siteLikelihoods;
            }

            double mleRate = MathNet.Numerics.FindMinimum.OfScalarFunction(x => -likelihoodFunction(x).Sum(), 1);

            return (mleRate, likelihoodFunction(mleRate));
        }


        /// <summary>
        /// Estimates the maximum-likelihood evolutionary rate for the specified sequence alignment under the specified rate matrix and returns
        /// the total likelihood for the alignment.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{TKey, TValue}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The log-likelihood for the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static (double rateMLE, double logLikelihood) GetLogLikelihood(this TreeNode tree, Dictionary<string, IReadOnlyList<char>> tipStates, RateMatrix rateMatrix, int maxParallelism = -1)
        {
            (double rateMLE, double[] logLikelihoods) = GetLogLikelihoods(tree, tipStates, rateMatrix, maxParallelism);

            return (rateMLE, logLikelihoods.Sum());
        }

        /// <summary>
        /// Estimates the maximum-likelihood evolutionary rate for the specified sequence alignment under the specified rate matrix and computes the
        /// likelihood.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>A <see cref="T:double[]"/> array containing the log-likelihood for each site in the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static (double rateMLE, double[] logLikelihoods) GetLogLikelihoods(this TreeNode tree, Dictionary<string, Sequence> tipStates, RateMatrix rateMatrix, int maxParallelism = -1)
        {
            return GetLogLikelihoods(tree, new Dictionary<string, IReadOnlyList<char>>(from el in tipStates select new KeyValuePair<string, IReadOnlyList<char>>(el.Key, el.Value)), rateMatrix, maxParallelism);
        }

        /// <summary>
        /// Estimates the maximum-likelihood evolutionary rate for the specified sequence alignment under the specified rate matrix and returns
        /// the total likelihood for the alignment.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, Sequence}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The log-likelihood for the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static (double rateMLE, double logLikelihood) GetLogLikelihood(this TreeNode tree, Dictionary<string, Sequence> tipStates, RateMatrix rateMatrix, int maxParallelism = -1)
        {
            (double rateMLE, double[] logLikelihoods) = GetLogLikelihoods(tree, tipStates, rateMatrix, maxParallelism);

            return (rateMLE, logLikelihoods.Sum());
        }


        /// <summary>
        /// Estimates the maximum-likelihood evolutionary rate for the specified sequence alignment under the specified rate matrix and computes the
        /// likelihood.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>A <see cref="T:double[]"/> array containing the log-likelihood for each site in the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static (double rateMLE, double[] logLikelihoods) GetLogLikelihoods(this TreeNode tree, Dictionary<string, string> tipStates, RateMatrix rateMatrix, int maxParallelism = -1)
        {
            return GetLogLikelihoods(tree, new Dictionary<string, IReadOnlyList<char>>(from el in tipStates select new KeyValuePair<string, IReadOnlyList<char>>(el.Key, el.Value.ToCharArray())), rateMatrix, maxParallelism);
        }

        /// <summary>
        /// Estimates the maximum-likelihood evolutionary rate for the specified sequence alignment under the specified rate matrix and returns
        /// the total likelihood for the alignment.
        /// </summary>
        /// <param name="tree">The tree on which the likelihood should be computed.</param>
        /// <param name="tipStates">The aligned sequences at the tips of the tree.
        /// This <see cref="Dictionary{String, String}"/> should contain an entry for each terminal node of the tree,
        /// where the key is either the <see cref="TreeNode.Name"/> or <see cref="TreeNode.Id"/> and the value
        /// is the character state sequence.</param>
        /// <param name="rateMatrix">The rate matrix describing the evolution of the character.</param>
        /// <param name="maxParallelism">The maximum number of concurrent computations to run.</param>
        /// <returns>The log-likelihood for the alignment.</returns>
        /// <exception cref="ArgumentException">Thrown when the sequences do not all have the same length.</exception>
        /// <exception cref="MissingDataException">Thrown when the <paramref name="tipStates"/> dictionary does not
        /// contain an entry for one of the tips in the tree.</exception>
        public static (double rateMLE, double logLikelihood) GetLogLikelihood(this TreeNode tree, Dictionary<string, string> tipStates, RateMatrix rateMatrix, int maxParallelism = -1)
        {
            (double rateMLE, double[] logLikelihoods) = GetLogLikelihoods(tree, tipStates, rateMatrix, maxParallelism);

            return (rateMLE, logLikelihoods.Sum());
        }


    }
}
