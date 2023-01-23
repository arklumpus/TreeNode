using MathNet.Numerics.LinearAlgebra;
using System;
using System.Collections.Generic;
using System.Linq;
using PhyloTree.TreeBuilding;
using MathNet.Numerics.Distributions;

/// <summary>
/// Contains classes and methods that can be used to simulate sequence evolution.
/// </summary>
namespace PhyloTree.SequenceSimulation
{
    /// <summary>
    /// Contains methods to simulate sequence evolution.
    /// </summary>
    public static partial class SequenceSimulation
    {
        /// <summary>
        /// The random number generator used to simulate sequence evolution. If you change this, please ensure that it is thread-safe.
        /// </summary>
        public static Random RandomNumberGenerator { get; set; } = new ThreadSafeRandom();

        /// <summary>
        /// Generate a random sequence of the specified length using the provided state frequencies.
        /// </summary>
        /// <param name="length">The length of the sequence.</param>
        /// <param name="stateFrequencies">The frequencies for the character states.</param>
        /// <returns>An <see cref="T:int[]"/> array where each element represents a character in the sequence.</returns>
        internal static int[] RandomSequence(int length, double[] stateFrequencies)
        {
            int[] samples = MathNet.Numerics.Distributions.Categorical.Samples(RandomNumberGenerator, stateFrequencies).Take(length).ToArray();

            return samples;
        }

        /// <summary>
        /// Converts a sequence stored as an <see cref="T:int[]"/> array into a <see cref="string"/>.
        /// </summary>
        /// <param name="sequence">The sequence as an <see cref="T:int[]"/> array.</param>
        /// <param name="states">The character states.</param>
        /// <returns>A <see cref="string"/> corresponding to the <paramref name="sequence"/> where each element has been replaced by the corresponding state.</returns>
        internal static string SequenceToString(int[] sequence, char[] states)
        {
            string tbr = new string('\0', sequence.Length);

            unsafe
            {
                fixed (char* sequencePtr = tbr)
                fixed (int* sequenceIntPtr = sequence)
                fixed (char* statePtr = states)
                {
                    for (int i = 0; i < sequence.Length; i++)
                    {
                        if (sequenceIntPtr[i] >= 0)
                        {
                            sequencePtr[i] = statePtr[sequenceIntPtr[i]];
                        }
                        else
                        {
                            sequencePtr[i] = '-';
                        }
                    }
                }
            }

            return tbr;
        }

        /// <summary>
        /// Simulates the evolution of a sequence along a branch.
        /// </summary>
        /// <param name="branchLength">The length of the branch.</param>
        /// <param name="ancestralSequence">The ancestral sequence that is evolving along the branch.</param>
        /// <param name="rateMatrix">The rate matrix according to which the sequence is evolving.</param>
        /// <param name="equilibriumFrequencies">The equilibrium frequencies for the rate matrix (used to sample new states for insertions).</param>
        /// <param name="cachedExponential">The cached exponential of the rate matrix.</param>
        /// <param name="indelRates">The insertion and deletion rate.</param>
        /// <param name="insertionSizeDistribution">The size distribution for insertions.</param>
        /// <param name="deletionSizeDistribution">The size distribution for deletions.</param>
        /// <param name="positionRates">The site-specific evolutionary rates.</param>
        /// <param name="gapProfile">The site-specific indel probabilities.</param>
        /// <returns>The evolved sequence, an array containing all insertions that have happened during its evolution, the updated site-specific
        /// evolutionary rates (modified to have the same length of the sequence that has evolved), and the updated site-specific indel probabilities
        /// (again, modified to have the same length of the sequence that has evolved).</returns>
        internal static (int[] sequence, int[][] insertions, double[] positionRates, double[] gapProfile) Evolve(double branchLength, int[] ancestralSequence, Matrix<double> rateMatrix, double[] equilibriumFrequencies, MatrixExponential cachedExponential = null, double[] indelRates = null, IDiscreteDistribution insertionSizeDistribution = null, IDiscreteDistribution deletionSizeDistribution = null, double[] positionRates = null, double[] gapProfile = null)
        {
            int[] newSequence = new int[ancestralSequence.Length];

            if (positionRates == null)
            {
                Matrix<double> matrixExp = rateMatrix.FastExponential(branchLength, cachedExponential).Result;

                double[][] newStateProbabilities = new double[rateMatrix.RowCount][];

                for (int i = 0; i < rateMatrix.RowCount; i++)
                {
                    Vector<double> initialState = Vector<double>.Build.Dense(rateMatrix.RowCount);
                    initialState[i] = 1;

                    newStateProbabilities[i] = (initialState * matrixExp).AsArray();
                }

                unsafe
                {
                    fixed (int* newSequencePtr = newSequence)
                    fixed (int* ancestralSequencePtr = ancestralSequence)
                    {
                        int[] counts = new int[rateMatrix.RowCount];
                        int[] indices = new int[rateMatrix.RowCount];
                        int[][] samples = new int[rateMatrix.RowCount][];

                        for (int i = 0; i < ancestralSequence.Length; i++)
                        {
                            if (ancestralSequencePtr[i] >= 0)
                            {
                                counts[ancestralSequencePtr[i]]++;
                            }
                        }

                        for (int i = 0; i < counts.Length; i++)
                        {
                            samples[i] = new int[counts[i]];
                            MathNet.Numerics.Distributions.Categorical.Samples(RandomNumberGenerator, samples[i], newStateProbabilities[i]);
                        }


                        for (int i = 0; i < ancestralSequence.Length; i++)
                        {
                            if (ancestralSequencePtr[i] >= 0)
                            {
                                int oldState = ancestralSequencePtr[i];
                                newSequencePtr[i] = samples[oldState][indices[oldState]];
                                indices[oldState]++;
                            }
                            else
                            {
                                newSequencePtr[i] = ancestralSequencePtr[i];
                            }
                        }
                    }
                }
            }
            else
            {
                unsafe
                {
                    fixed (int* newSequencePtr = newSequence)
                    fixed (int* ancestralSequencePtr = ancestralSequence)
                    {
                        Vector<double> initialState = Vector<double>.Build.Dense(rateMatrix.RowCount);

                        for (int i = 0; i < ancestralSequence.Length; i++)
                        {
                            if (ancestralSequencePtr[i] >= 0)
                            {
                                Matrix<double> matrixExp = rateMatrix.FastExponential(branchLength * positionRates[i], cachedExponential).Result;

                                for (int j = 0; j < rateMatrix.RowCount; j++)
                                {
                                    initialState[j] = 0;
                                }
                                initialState[ancestralSequencePtr[i]] = 1;

                                double[] newStateProbabilities = (initialState * matrixExp).AsArray();

                                newSequencePtr[i] = MathNet.Numerics.Distributions.Categorical.Sample(RandomNumberGenerator, newStateProbabilities);
                            }
                            else
                            {
                                newSequencePtr[i] = ancestralSequencePtr[i];
                            }
                        }
                    }
                }
            }

            int[][] insertions;

            if (indelRates != null && indelRates[0] + indelRates[1] > 0)
            {
                List<int[]> insertionList = new List<int[]>();

                double indelRate = rateMatrix.Trace() * (indelRates[0] + indelRates[1]);

                double time = Exponential.Sample(RandomNumberGenerator, -indelRate);

                while (time < branchLength)
                {
                    int type = Categorical.Sample(RandomNumberGenerator, indelRates);

                    if (type == 0)
                    {
                        int length = insertionSizeDistribution.Sample();

                        int start;

                        if (gapProfile == null)
                        {
                            start = RandomNumberGenerator.Next(0, newSequence.Length - length);
                        }
                        else
                        {
                            start = Categorical.Sample(gapProfile);
                        }

                        //Console.WriteLine("Insertion: {0} {1}", start, length);

                        int[] newNewSequence = new int[newSequence.Length + length];

                        for (int i = 0; i < start; i++)
                        {
                            newNewSequence[i] = newSequence[i];
                        }

                        for (int i = start; i < newSequence.Length; i++)
                        {
                            newNewSequence[i + length] = newSequence[i];
                        }

                        int[] insertion = RandomSequence(length, equilibriumFrequencies);

                        for (int i = 0; i < length; i++)
                        {
                            newNewSequence[i + start] = insertion[i];
                        }

                        newSequence = newNewSequence;

                        if (gapProfile != null)
                        {
                            double[] newGapProfile = new double[gapProfile.Length + length];
                            for (int i = 0; i < start; i++)
                            {
                                newGapProfile[i] = gapProfile[i];
                            }

                            for (int i = start; i < gapProfile.Length; i++)
                            {
                                newGapProfile[i + length] = gapProfile[i];
                            }

                            for (int i = 0; i < length; i++)
                            {
                                newGapProfile[i + start] = gapProfile[start];
                            }

                            gapProfile = newGapProfile;
                        }

                        if (positionRates != null)
                        {
                            double[] newPositionRates = new double[positionRates.Length + length];
                            for (int i = 0; i < start; i++)
                            {
                                newPositionRates[i] = positionRates[i];
                            }

                            for (int i = start; i < positionRates.Length; i++)
                            {
                                newPositionRates[i + length] = positionRates[i];
                            }

                            for (int i = 0; i < length; i++)
                            {
                                newPositionRates[i + start] = positionRates[start];
                            }

                            positionRates = newPositionRates;
                        }

                        insertionList.Add(new int[] { start, length });
                    }
                    else if (type == 1)
                    {
                        int length = deletionSizeDistribution.Sample();

                        int start;

                        if (gapProfile == null)
                        {
                            start = RandomNumberGenerator.Next(0, newSequence.Length - length);
                        }
                        else
                        {
                            double[] startWeights = new double[newSequence.Length - length];

                            for (int i = 0; i < newSequence.Length - length; i++)
                            {
                                for (int j = 0; j < length; j++)
                                {
                                    startWeights[i] += gapProfile[i + j];
                                }
                            }

                            start = Categorical.Sample(startWeights);
                        }

                        for (int i = 0; i < length; i++)
                        {
                            newSequence[start + i] = -1;
                        }
                    }

                    time += Exponential.Sample(RandomNumberGenerator, -indelRate);
                }

                insertions = insertionList.ToArray();
            }
            else
            {
                insertions = new int[0][];
            }


            return (newSequence, insertions, positionRates, gapProfile);
        }

        /// <summary>
        /// Applies the specified insertion by inserting gaps in the specified sequence.
        /// </summary>
        /// <param name="sequence">The sequence on which the insertion should be applied.</param>
        /// <param name="insertion">The insertion (first element: start, second element: length).</param>
        /// <returns>A sequence where gaps (-1) have been inserted in the position corresponding to the insertion.</returns>
        private static int[] ApplyInsertion(int[] sequence, int[] insertion)
        {
            int[] tbr = new int[sequence.Length + insertion[1]];

            for (int i = 0; i < insertion[0]; i++)
            {
                tbr[i] = sequence[i];
            }

            for (int i = insertion[0]; i < sequence.Length; i++)
            {
                tbr[i + insertion[1]] = sequence[i];
            }

            for (int i = 0; i < insertion[1]; i++)
            {
                tbr[insertion[0] + i] = -1;
            }

            return tbr;
        }
    }
}
