using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;

namespace PhyloTree.SequenceSimulation
{
    /// <summary>
    /// Represents a sequence of characters.
    /// </summary>
    public class Sequence : IReadOnlyList<char>
    {
        internal readonly int[] intSequence;
        private string stringSequence;

        /// <summary>
        /// The possible states that make up the sequence. Note that some states may not actually be present in the sequence.
        /// </summary>
        public ImmutableArray<char> States { get; }

        /// <summary>
        /// The conservation profile of the sequence. If this is <see langword="null"/>, all positions in the sequence are equally conserved.
        /// </summary>
        public ImmutableArray<double>? Conservation { get; }

        /// <summary>
        /// The indel profile of the sequence. If this is <see langword="null"/>, all positions in the sequence have equal
        /// probability of being affected by an indel event.
        /// </summary>
        public ImmutableArray<double>? IndelProfile { get; }

        /// <summary>
        /// Returns the sequence as a <see langword="string"/>.
        /// </summary>
        public string StringSequence
        {
            get
            {
                return stringSequence;
            }
        }

        /// <summary>
        /// The length of the sequence.
        /// </summary>
        public int Length
        {
            get { return intSequence.Length; }
        }

        /// <inheritdoc/>
        public int Count => this.Length;

        /// <inheritdoc/>
        public char this[int index] => this.stringSequence[index];

        /// <summary>
        /// Creates a new <see cref="Sequence"/>.
        /// </summary>
        /// <param name="sequence">The sequence.</param>
        public Sequence(ReadOnlySpan<char> sequence)
        {
            List<char> states = new List<char>();
            Dictionary<char, int> characterIndices = new Dictionary<char, int>();

            intSequence = new int[sequence.Length];

            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] == '-')
                {
                    intSequence[i] = -1;
                }
                else if (!characterIndices.TryGetValue(sequence[i], out intSequence[i]))
                {
                    characterIndices[sequence[i]] = states.Count;
                    states.Add(sequence[i]);
                }
            }

            this.States = ImmutableArray.Create<char>(states.ToArray());
            this.Conservation = null;
            this.IndelProfile = null;
            this.stringSequence = new string(sequence);
        }

        /// <summary>
        /// Creates a new <see cref="Sequence"/>.
        /// </summary>
        /// <param name="sequence">The sequence.</param>
        /// <param name="states">The possible states for the sequence.</param>
        public Sequence(ReadOnlySpan<char> sequence, IReadOnlyList<char> states)
        {
            Dictionary<char, int> characterIndices = new Dictionary<char, int>(from el in Enumerable.Range(0, states.Count) select new KeyValuePair<char, int>(states[el], el));

            intSequence = new int[sequence.Length];

            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] == '-')
                {
                    intSequence[i] = -1;
                }
                else if (!characterIndices.TryGetValue(sequence[i], out intSequence[i]))
                {
                    throw new ArgumentException("State " + sequence[i] + " is not a valid character state!");
                }
            }

            this.States = ImmutableArray.Create<char>(states.ToArray());
            this.Conservation = null;
            this.IndelProfile = null;
            this.stringSequence = new string(sequence);
        }

        /// <summary>
        /// Creates a new <see cref="Sequence"/>.
        /// </summary>
        /// <param name="sequence">The sequence.</param>
        /// <param name="states">The possible states for the sequence.</param>
        /// <param name="conservation">The conservation profile for the sequence.</param>
        public Sequence(ReadOnlySpan<char> sequence, IReadOnlyList<char> states, IReadOnlyList<double> conservation)
        {
            Dictionary<char, int> characterIndices = new Dictionary<char, int>(from el in Enumerable.Range(0, states.Count) select new KeyValuePair<char, int>(states[el], el));

            intSequence = new int[sequence.Length];

            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] == '-')
                {
                    intSequence[i] = -1;
                }
                else if (!characterIndices.TryGetValue(sequence[i], out intSequence[i]))
                {
                    throw new ArgumentException("State " + sequence[i] + " is not a valid character state!");
                }
            }

            this.States = ImmutableArray.Create<char>(states.ToArray());

            if (conservation != null)
            {
                this.Conservation = ImmutableArray.Create<double>(conservation.ToArray());
            }

            this.IndelProfile = null;
            this.stringSequence = new string(sequence);
        }

        /// <summary>
        /// Creates a new <see cref="Sequence"/>.
        /// </summary>
        /// <param name="sequence">The sequence.</param>
        /// <param name="states">The possible states for the sequence.</param>
        /// <param name="conservation">The conservation profile for the sequence.</param>
        /// <param name="indelProfile">The indel profile for the sequence.</param>
        public Sequence(ReadOnlySpan<char> sequence, IReadOnlyList<char> states, IReadOnlyList<double> conservation, IReadOnlyList<double> indelProfile)
        {
            Dictionary<char, int> characterIndices = new Dictionary<char, int>(from el in Enumerable.Range(0, states.Count) select new KeyValuePair<char, int>(states[el], el));

            intSequence = new int[sequence.Length];

            for (int i = 0; i < sequence.Length; i++)
            {
                if (sequence[i] == '-')
                {
                    intSequence[i] = -1;
                }
                else if (!characterIndices.TryGetValue(sequence[i], out intSequence[i]))
                {
                    throw new ArgumentException("State " + sequence[i] + " is not a valid character state!");
                }
            }

            this.States = ImmutableArray.Create<char>(states.ToArray());

            if (conservation != null)
            {
                this.Conservation = ImmutableArray.Create<double>(conservation.ToArray());
            }

            if (indelProfile != null)
            {
                this.IndelProfile = ImmutableArray.Create<double>(indelProfile.ToArray());
            }

            this.stringSequence = new string(sequence);
        }

        internal Sequence(int[] intSequence, char[] states, double[] conservation = null, double[] indelProfile = null)
        {
            this.intSequence = (int[])intSequence.Clone();
            this.States = ImmutableArray.Create<char>(states);
            
            if (conservation != null)
            {
                this.Conservation = ImmutableArray.Create<double>(conservation);
            }

            if (indelProfile != null)
            {
                this.IndelProfile = ImmutableArray.Create<double>(indelProfile); 
            }

            this.stringSequence = SequenceSimulation.SequenceToString(intSequence, states);
        }

        /// <summary>
        /// Converts a <see cref="Sequence"/> to a <see langword="string"/>
        /// </summary>
        /// <param name="sequence">The <see cref="Sequence"/> to convert.</param>
        public static implicit operator string(Sequence sequence)
        {
            return sequence.stringSequence;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.stringSequence;
        }

        /// <inheritdoc/>
        public IEnumerator<char> GetEnumerator()
        {
            return this.stringSequence.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }


        /// <summary>
        /// Create a random sequence with the specified <paramref name="length"/>, using the states and equilibrium frequencies from the <paramref name="rateMatrix"/>.
        /// </summary>
        /// <param name="length">The length of the sequence.</param>
        /// <param name="rateMatrix">The rate matrix.</param>
        /// <returns>A random sequence.</returns>
        public static Sequence RandomSequence(int length, RateMatrix rateMatrix) => new Sequence(SequenceSimulation.RandomSequence(length, rateMatrix.GetEquilibriumFrequencies()), rateMatrix.GetStates());

        /// <summary>
        /// Create a random sequence with the specified <paramref name="length"/> containing all <paramref name="states"/> with equal probability.
        /// </summary>
        /// <param name="length">The length of the sequence.</param>
        /// <param name="states">The character states to use for the sequence.</param>
        /// <returns>A random sequence</returns>
        public static Sequence RandomSequence(int length, IReadOnlyList<char> states) => new Sequence(SequenceSimulation.RandomSequence(length, Enumerable.Repeat(1.0, states.Count).ToArray()), states.ToArray());

        /// <summary>
        /// Create a random sequence with the specified <paramref name="length"/> containing all <paramref name="states"/> with equal probability.
        /// </summary>
        /// <param name="length">The length of the sequence.</param>
        /// <param name="states">The character states to use for the sequence.</param>
        /// <returns>A random sequence</returns>
        public static Sequence RandomSequence(int length, char[] states) => new Sequence(SequenceSimulation.RandomSequence(length, Enumerable.Repeat(1.0, states.Length).ToArray()), states);

        /// <summary>
        /// Create a random sequence with the specified <paramref name="length"/> containing each state with probabilities given by <paramref name="stateFrequencies"/>.
        /// </summary>
        /// <param name="length">The length of the sequence.</param>
        /// <param name="states">The character states to use for the sequence.</param>
        /// <param name="stateFrequencies">The frequency for each state.</param>
        /// <returns>A random sequence</returns>
        public static Sequence RandomSequence(int length, IReadOnlyList<char> states, IReadOnlyList<double> stateFrequencies) => new Sequence(SequenceSimulation.RandomSequence(length, stateFrequencies.ToArray()), states.ToArray());

        /// <summary>
        /// Create a random sequence with the specified <paramref name="length"/> containing each state with probabilities given by <paramref name="stateFrequencies"/>.
        /// </summary>
        /// <param name="length">The length of the sequence.</param>
        /// <param name="states">The character states to use for the sequence.</param>
        /// <param name="stateFrequencies">The frequency for each state.</param>
        /// <returns>A random sequence</returns>
        public static Sequence RandomSequence(int length, char[] states, double[] stateFrequencies) => new Sequence(SequenceSimulation.RandomSequence(length, stateFrequencies), states);

        /// <summary>
        /// Simulates the evolution of the <see cref="Sequence"/> over the specified amount of <paramref name="time"/>, using the specified rate matrix.
        /// No indels are allowed to happen.
        /// </summary>
        /// <param name="rateMatrix">The rate matrix to simulate sequence evolution.</param>
        /// <param name="time">The length of time over which the sequence evolves.</param>
        /// <returns>The evolved sequence.</returns>
        public Sequence Evolve(RateMatrix rateMatrix, double time)
        {
            (int[] newSequence, int[][] _, double[] positionRates, double[] gapProfile) = SequenceSimulation.Evolve(time, this.intSequence, rateMatrix.GetMatrix(), rateMatrix.GetEquilibriumFrequencies(), rateMatrix.GetExponential(), positionRates: this.Conservation?.ToArray());

            return new Sequence(newSequence, rateMatrix.States.ToArray(), positionRates, gapProfile);
        }

        /// <summary>
        /// Simulates the evolution of the <see cref="Sequence"/> over the specified amount of <paramref name="time"/>, using the specified rate matrix.
        /// Insertions and deletions happen according to the specified <paramref name="indelModel"/>.
        /// </summary>
        /// <param name="rateMatrix">The rate matrix to simulate sequence evolution.</param>
        /// <param name="indelModel">The insertion/deletion model.</param>
        /// <param name="time">The length of time over which the sequence evolves.</param>
        /// <returns>The evolved sequence.</returns>
        public Sequence Evolve(RateMatrix rateMatrix, IndelModel indelModel, double time)
        {
            (int[] newSequence, int[][] _, double[] positionRates, double[] gapProfile) = SequenceSimulation.Evolve(time, this.intSequence, rateMatrix.GetMatrix(), rateMatrix.GetEquilibriumFrequencies(), rateMatrix.GetExponential(), new double[] { indelModel.InsertionRate, indelModel.DeletionRate }, indelModel.InsertionSizeDistribution, indelModel.DeletionSizeDistribution, this.Conservation?.ToArray(), this.IndelProfile?.ToArray());

            return new Sequence(newSequence, rateMatrix.States.ToArray(), positionRates, gapProfile);
        }

        /// <summary>
        /// Simulates the evolution of the <see cref="Sequence"/> over the specified amount of <paramref name="time"/>, using the specified rate matrix.
        /// Insertions and deletions happen according to the specified <paramref name="indelModel"/>.
        /// </summary>
        /// <param name="rateMatrix">The rate matrix to simulate sequence evolution.</param>
        /// <param name="indelModel">The insertion/deletion model.</param>
        /// <param name="time">The length of time over which the sequence evolves.</param>
        /// <param name="insertions">An array containing all the insertion events that have occurred during the evolution of the sequence (useful to
        /// map the evolved sequence back onto the ancestral sequence).</param>
        /// <returns>The evolved sequence and an array containing all the insertion events that happened during its evolution.</returns>
        public Sequence Evolve(RateMatrix rateMatrix, IndelModel indelModel, double time, out Insertion[] insertions)
        {
            (int[] newSequence, int[][] insertionEvents, double[] positionRates, double[] gapProfile) = SequenceSimulation.Evolve(time, this.intSequence, rateMatrix.GetMatrix(), rateMatrix.GetEquilibriumFrequencies(), rateMatrix.GetExponential(), new double[] { indelModel.InsertionRate, indelModel.DeletionRate }, indelModel.InsertionSizeDistribution, indelModel.DeletionSizeDistribution, this.Conservation?.ToArray(), this.IndelProfile?.ToArray());

            if (insertionEvents != null)
            {
                insertions = new Insertion[insertionEvents.Length];
                for (int i = 0; i < insertionEvents.Length; i++)
                {
                    insertions[i] = new Insertion(insertionEvents[i][0], insertionEvents[i][1]);
                }
            }
            else
            {
                insertions = null;
            }

            return new Sequence(newSequence, rateMatrix.States.ToArray(), positionRates, gapProfile);
        }

        /// <summary>
        /// Simulates the evolution of the sequence over a phylogenetic <paramref name="tree"/>, with the specified rate matrix, <paramref name="scale"/> factor,
        /// and insertion/deletion model.
        /// </summary>
        /// <param name="tree">The tree over which the sequence evolves. This is assumed to be rooted (i.e., the ancestral sequence is placed at the root of the tree).</param>
        /// <param name="rateMatrix">The rate matrix that governs the evolution of the sequence.</param>
        /// <param name="scale">A scaling factor. If this is different from 1, the effect is the same as multiplying the branch lengths of the tree or the rate matrix by
        /// the supplied value.</param>
        /// <param name="indelModel">The model for insertions/deletions. If this is null, no insertions/deletions are allowed to happen.</param>
        /// <returns>A <see cref="Dictionary{String, Sequence}"/> containing entries for all the nodes in the tree that have names. For each entry, the key is a <see langword="string"/>
        /// containing the <see cref="TreeNode.Name"/> of the node, and the value is a <see cref="Sequence"/> containing the sequence. The sequences are all aligned.</returns>
        public Dictionary<string, Sequence> Evolve(TreeNode tree, RateMatrix rateMatrix, double scale = 1, IndelModel indelModel = null)
        {
            return tree.SimulateSequences(this, rateMatrix, scale, indelModel);
        }

        /// <summary>
        /// Simulates the evolution of the sequence over a phylogenetic <paramref name="tree"/>, with the specified rate matrix, <paramref name="scale"/> factor,
        /// and insertion/deletion model.
        /// </summary>
        /// <param name="tree">The tree over which the sequence evolves. This is assumed to be rooted (i.e., the ancestral sequence is placed at the root of the tree).</param>
        /// <param name="rateMatrix">The rate matrix that governs the evolution of the sequence.</param>
        /// <param name="scale">A scaling factor. If this is different from 1, the effect is the same as multiplying the branch lengths of the tree or the rate matrix by
        /// the supplied value.</param>
        /// <param name="indelModel">The model for insertions/deletions. If this is null, no insertions/deletions are allowed to happen.</param>
        /// <returns>A <see cref="Dictionary{String, Sequence}"/> containing entries for all the nodes in the tree. For each entry, the key is a <see langword="string"/>
        /// containing the <see cref="TreeNode.Id"/> of the node, and the value is a <see cref="Sequence"/> containing the sequence. The sequences are all aligned.</returns>
        public Dictionary<string, Sequence> EvolveAll(TreeNode tree, RateMatrix rateMatrix, double scale = 1, IndelModel indelModel = null)
        {
            return tree.SimulateAllSequences(this, rateMatrix, scale, indelModel);
        }

    }
}
