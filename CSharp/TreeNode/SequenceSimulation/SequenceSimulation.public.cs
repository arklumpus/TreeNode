using MathNet.Numerics.LinearAlgebra;
using PhyloTree.TreeBuilding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PhyloTree.SequenceSimulation
{
    partial class SequenceSimulation
    {
        /// <summary>
        /// Simulates the evolution of the specified ancestral sequence over a phylogenetic <paramref name="tree"/>, with the specified rate matrix, <paramref name="scale"/> factor,
        /// and insertion/deletion model.
        /// </summary>
        /// <param name="tree">The tree over which the sequence evolves. This is assumed to be rooted (i.e., the ancestral sequence is placed at the root of the tree).</param>
        /// <param name="ancestralSequence">The ancestral sequence whose evolution is being simulated.</param>
        /// <param name="rateMatrix">The rate matrix that governs the evolution of the sequence.</param>
        /// <param name="scale">A scaling factor. If this is different from 1, the effect is the same as multiplying the branch lengths of the tree or the rate matrix by
        /// the supplied value.</param>
        /// <param name="indelModel">The model for insertions/deletions. If this is null, no insertions/deletions are allowed to happen.</param>
        /// <returns>A <see cref="Dictionary{String, Sequence}"/> containing entries for all the nodes in the tree that have names. For each entry, the key is a <see langword="string"/>
        /// containing the <see cref="TreeNode.Name"/> of the node, and the value is a <see cref="Sequence"/> containing the sequence. The sequences are all aligned.</returns>
        public static Dictionary<string, Sequence> SimulateSequences(this TreeNode tree, Sequence ancestralSequence, RateMatrix rateMatrix, double scale = 1, IndelModel indelModel = null)
        {
            Matrix<double> transitionRateMatrix = rateMatrix.GetMatrix();
            double[] equilibriumFrequencies = rateMatrix.GetEquilibriumFrequencies();
            char[] states = rateMatrix.GetStates();

            Matrix<double> mRateMatrix = transitionRateMatrix * scale;

            int[] ancestralSequenceInt = ancestralSequence.intSequence;

            List<TreeNode> nodes = tree.GetChildrenRecursive();
            int[][] sequences = new int[nodes.Count][];

            Dictionary<string, (int, int)> indices = new Dictionary<string, (int, int)>(nodes.Count);

            for (int i = 0; i < nodes.Count; i++)
            {
                indices[nodes[i].Id] = (i, nodes[i].Children.Count);
            }

            MatrixExponential cachedExponential = mRateMatrix.FastExponential(1);

            sequences[0] = ancestralSequenceInt;

            double[] indelRates = indelModel != null ? new double[] { indelModel.InsertionRate, indelModel.DeletionRate } : null;

            double[] positionRates = ancestralSequence.Conservation?.ToArray();
            double[] gapProfile = ancestralSequence.IndelProfile?.ToArray();


            for (int i = 1; i < nodes.Count; i++)
            {
                int[][] insertions;

                (sequences[i], insertions, positionRates, gapProfile) = Evolve(nodes[i].Length, sequences[indices[nodes[i].Parent.Id].Item1], mRateMatrix, equilibriumFrequencies, cachedExponential, indelRates, indelModel?.InsertionSizeDistribution, indelModel?.DeletionSizeDistribution, positionRates, gapProfile);

                if (insertions.Length > 0)
                {
                    for (int j = 0; j < i; j++)
                    {
                        for (int k = 0; k < insertions.Length; k++)
                        {
                            sequences[j] = ApplyInsertion(sequences[j], insertions[k]);
                        }
                    }
                }
            }

            Dictionary<string, Sequence> leafSequences = new Dictionary<string, Sequence>();

            foreach (TreeNode leaf in tree.GetChildrenRecursiveLazy())
            {
                if (!string.IsNullOrEmpty(leaf.Name))
                {
                    leafSequences[leaf.Name] = new Sequence(sequences[indices[leaf.Id].Item1], states, positionRates, gapProfile);
                }
            }

            return leafSequences;
        }

        /// <summary>
        /// Simulates the evolution of a random ancestral sequence with the specified length over a phylogenetic <paramref name="tree"/>, with the specified rate matrix,
        /// <paramref name="scale"/> factor, and insertion/deletion model.
        /// </summary>
        /// <param name="tree">The tree over which the sequence evolves. This is assumed to be rooted (i.e., the ancestral sequence is placed at the root of the tree).</param>
        /// <param name="ancestralSequenceLength">The length of the ancestral sequence whose evolution is being simulated. Note that if insertions/deletions are allowed to happen,
        /// the final length of the (aligned) sequences may differ from this.</param>
        /// <param name="rateMatrix">The rate matrix that governs the evolution of the sequence.</param>
        /// <param name="scale">A scaling factor. If this is different from 1, the effect is the same as multiplying the branch lengths of the tree or the rate matrix by
        /// the supplied value.</param>
        /// <param name="indelModel">The model for insertions/deletions. If this is null, no insertions/deletions are allowed to happen.</param>
        /// <returns>A <see cref="Dictionary{String, Sequence}"/> containing entries for all the nodes in the tree that have names. For each entry, the key is a <see langword="string"/>
        /// containing the <see cref="TreeNode.Name"/> of the node, and the value is a <see cref="Sequence"/> containing the sequence. The sequences are all aligned.</returns>
        public static Dictionary<string, Sequence> SimulateSequences(this TreeNode tree, int ancestralSequenceLength, RateMatrix rateMatrix, double scale = 1, IndelModel indelModel = null)
        {
            Sequence ancestralSequence = Sequence.RandomSequence(ancestralSequenceLength, rateMatrix);

            return SimulateSequences(tree, ancestralSequence, rateMatrix, scale, indelModel);
        }

        /// <summary>
        /// Simulates the evolution of the specified ancestral sequence over a phylogenetic <paramref name="tree"/>, with the specified rate matrix, <paramref name="scale"/> factor,
        /// and insertion/deletion model.
        /// </summary>
        /// <param name="tree">The tree over which the sequence evolves. This is assumed to be rooted (i.e., the ancestral sequence is placed at the root of the tree).</param>
        /// <param name="ancestralSequence">The ancestral sequence whose evolution is being simulated.</param>
        /// <param name="rateMatrix">The rate matrix that governs the evolution of the sequence.</param>
        /// <param name="scale">A scaling factor. If this is different from 1, the effect is the same as multiplying the branch lengths of the tree or the rate matrix by
        /// the supplied value.</param>
        /// <param name="indelModel">The model for insertions/deletions. If this is null, no insertions/deletions are allowed to happen.</param>
        /// <returns>A <see cref="Dictionary{String, Sequence}"/> containing entries for all the nodes in the tree. For each entry, the key is a <see langword="string"/>
        /// containing the <see cref="TreeNode.Id"/> of the node, and the value is a <see cref="Sequence"/> containing the sequence. The sequences are all aligned.</returns>
        public static Dictionary<string, Sequence> SimulateAllSequences(this TreeNode tree, Sequence ancestralSequence, RateMatrix rateMatrix, double scale = 1, IndelModel indelModel = null)
        {
            Matrix<double> transitionRateMatrix = rateMatrix.GetMatrix();
            double[] equilibriumFrequencies = rateMatrix.GetEquilibriumFrequencies();
            char[] states = rateMatrix.GetStates();

            Matrix<double> mRateMatrix = transitionRateMatrix * scale;

            int[] ancestralSequenceInt = ancestralSequence.intSequence;

            List<TreeNode> nodes = tree.GetChildrenRecursive();
            int[][] sequences = new int[nodes.Count][];

            Dictionary<string, (int, int)> indices = new Dictionary<string, (int, int)>(nodes.Count);

            for (int i = 0; i < nodes.Count; i++)
            {
                indices[nodes[i].Id] = (i, nodes[i].Children.Count);
            }

            MatrixExponential cachedExponential = mRateMatrix.FastExponential(1);

            sequences[0] = ancestralSequenceInt;

            double[] indelRates = indelModel != null ? new double[] { indelModel.InsertionRate, indelModel.DeletionRate } : null;

            double[] positionRates = ancestralSequence.Conservation?.ToArray();
            double[] gapProfile = ancestralSequence.IndelProfile?.ToArray();


            for (int i = 1; i < nodes.Count; i++)
            {
                int[][] insertions;

                (sequences[i], insertions, positionRates, gapProfile) = Evolve(nodes[i].Length, sequences[indices[nodes[i].Parent.Id].Item1], mRateMatrix, equilibriumFrequencies, cachedExponential, indelRates, indelModel?.InsertionSizeDistribution, indelModel?.DeletionSizeDistribution, positionRates, gapProfile);

                if (insertions.Length > 0)
                {
                    for (int j = 0; j < i; j++)
                    {
                        for (int k = 0; k < insertions.Length; k++)
                        {
                            sequences[j] = ApplyInsertion(sequences[j], insertions[k]);
                        }
                    }
                }
            }

            Dictionary<string, Sequence> leafSequences = new Dictionary<string, Sequence>(nodes.Count);

            foreach (TreeNode node in nodes)
            {
                leafSequences[node.Id] = new Sequence(sequences[indices[node.Id].Item1], states, positionRates, gapProfile);
            }

            return leafSequences;
        }

        /// <summary>
        /// Simulates the evolution of a random ancestral sequence with the specified length over a phylogenetic <paramref name="tree"/>, with the specified rate matrix,
        /// <paramref name="scale"/> factor, and insertion/deletion model.
        /// </summary>
        /// <param name="tree">The tree over which the sequence evolves. This is assumed to be rooted (i.e., the ancestral sequence is placed at the root of the tree).</param>
        /// <param name="ancestralSequenceLength">The length of the ancestral sequence whose evolution is being simulated. Note that if insertions/deletions are allowed to happen,
        /// the final length of the (aligned) sequences may differ from this.</param>
        /// <param name="rateMatrix">The rate matrix that governs the evolution of the sequence.</param>
        /// <param name="scale">A scaling factor. If this is different from 1, the effect is the same as multiplying the branch lengths of the tree or the rate matrix by
        /// the supplied value.</param>
        /// <param name="indelModel">The model for insertions/deletions. If this is null, no insertions/deletions are allowed to happen.</param>
        /// <returns>A <see cref="Dictionary{String, Sequence}"/> containing entries for all the nodes in the tree. For each entry, the key is a <see langword="string"/>
        /// containing the <see cref="TreeNode.Id"/> of the node, and the value is a <see cref="Sequence"/> containing the sequence. The sequences are all aligned.</returns>
        public static Dictionary<string, Sequence> SimulateAllSequences(this TreeNode tree, int ancestralSequenceLength, RateMatrix rateMatrix, double scale = 1, IndelModel indelModel = null)
        {
            Sequence ancestralSequence = Sequence.RandomSequence(ancestralSequenceLength, rateMatrix);

            return SimulateAllSequences(tree, ancestralSequence, rateMatrix, scale, indelModel);
        }

        /// <summary>
        /// Represents a function that can be evaluated to return the scaling factor to use in order to obtain the specified average <paramref name="conservation" />.
        /// </summary>
        /// <param name="conservation">The average conservation whose corresponding scaling factor will be returned.</param>
        /// <returns>The scaling factor that will produce the speicfied average <paramref name="conservation" />.</returns>
        public delegate double GetScale(double conservation);

        /// <summary>
        /// Returns a method that can be evaluated to determine the scaling factor that, when applied to a sequence simulation done using the specified <paramref name="tree"/>
        /// and rate matrix, will produce at the tips a sequence alignment with the specified (average) percent identity.
        /// </summary>
        /// <param name="tree">The <see cref="TreeNode"/> on which the sequence simulations will be performed.</param>
        /// <param name="rateMatrix">The rate matrix that will be used for the sequence simulations.</param>
        /// <param name="minRate">Minimum rate value to test.</param>
        /// <param name="maxRate">Maximum rate value to test.</param>
        /// <returns>A method that can be evaluated to determine the scaling factor that, when applied to a sequence simulation done using the specified <paramref name="tree"/>
        /// and rate matrix, will produce at the tips a sequence alignment with the specified (average) percent identity.</returns>
        public static GetScale ConservationToScale(TreeNode tree, RateMatrix rateMatrix, double minRate = 1e-5, double maxRate = 1e4)
        {
            Matrix<double> transitionRateMatrix = rateMatrix.GetMatrix();
            double[] equilibriumFrequencies = rateMatrix.GetEquilibriumFrequencies();

            int steps = 101;

            double logMinRate = Math.Log(minRate);
            double logMaxRate = Math.Log(maxRate);

            double[][] data = new double[steps][];

            List<TreeNode> nodes = tree.GetChildrenRecursive();

            for (int i = 0; i < steps; i++)
            {
                double rate = Math.Exp(logMinRate + (logMaxRate - logMinRate) * i / (steps - 1));
                Dictionary<string, Sequence> sequences = SimulateAllSequences(tree, 100, rateMatrix, rate);

                int[][] counts = new int[100][];

                for (int j = 0; j < 100; j++)
                {
                    counts[j] = new int[rateMatrix.States.Length];
                }

                int totalSeqs = 0;

                foreach (TreeNode node in nodes)
                {
                    if (node.Children.Count == 0)
                    {
                        totalSeqs++;

                        for (int j = 0; j < sequences[node.Id].Length; j++)
                        {
                            if (sequences[node.Id].intSequence[j] >= 0)
                            {
                                counts[j][sequences[node.Id].intSequence[j]]++;
                            }
                        }
                    }
                }

                double averageConservation = (from el in counts select (double)el.Max() / totalSeqs).Average();

                if (i > 0)
                {
                    data[i] = new double[] { rate, Math.Min(averageConservation, data[i - 1][1]) };
                }
                else
                {
                    data[i] = new double[] { rate, averageConservation };
                }
            }

            return value =>
            {
                if (value > data[0][1])
                {
                    if (data[1][1] - data[0][1] != 0)
                    {
                        return data[0][0] + (data[0][0] - data[1][0]) * (data[0][1] - value) / (data[1][1] - data[0][1]);
                    }
                    else
                    {
                        return data[0][0];
                    }
                }
                if (value < data[data.Length - 1][1])
                {
                    if (data[data.Length - 1][1] - data[data.Length - 2][1] != 0)
                    {
                        return data[data.Length - 1][0] + (data[data.Length - 1][0] - data[data.Length - 2][0]) * (value - data[data.Length - 1][1]) / (data[data.Length - 1][1] - data[data.Length - 2][1]);
                    }
                    else
                    {
                        return data[data.Length - 1][0];
                    }
                }

                for (int i = 0; i < data.Length - 1; i++)
                {
                    if (data[i][1] > value && data[i + 1][1] <= value)
                    {
                        return data[i][0] + (data[i + 1][0] - data[i][0]) * (value - data[i][1]) / (data[i + 1][1] - data[i][1]);
                    }
                }

                return double.NaN;
            };
        }


        /// <summary>
        /// Converts a sequence alignment where the sequences are stored as <see cref="Sequence"/>s into an alignment where the sequences are stored as <see langword="string"/>s.
        /// </summary>
        /// <param name="alignment">The alignment to convert.</param>
        /// <returns>A <see cref="Dictionary{String, String}"/> where both keys and values are string.</returns>
        public static Dictionary<string, string> ToStringAlignment(this Dictionary<string, Sequence> alignment)
        {
            return new Dictionary<string, string>(from el in alignment select new KeyValuePair<string, string>(el.Key, el.Value));
        }
    }
}
