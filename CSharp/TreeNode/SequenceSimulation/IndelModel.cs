using MathNet.Numerics.Distributions;

namespace PhyloTree.SequenceSimulation
{
    /// <summary>
    /// Represents a model for sequence insertion/deletion.
    /// </summary>
    public class IndelModel
    {
        /// <summary>
        /// The rate of insertions, expressed as a multiple of the rate of sequence mutation.
        /// </summary>
        public double InsertionRate { get; }

        /// <summary>
        /// The rate of deletions, expressed as a multiple of the rate of sequence mutation.
        /// </summary>
        public double DeletionRate { get; }

        /// <summary>
        /// The size distribution for insertions.
        /// </summary>
        public IDiscreteDistribution InsertionSizeDistribution { get; }

        /// <summary>
        /// The size distribution for deletions.
        /// </summary>
        public IDiscreteDistribution DeletionSizeDistribution { get; }

        /// <summary>
        /// Creates a new <see cref="IndelModel"/> with the specified insertion rate, deletion rate, insertion size distribution and deletion size distribution.
        /// </summary>
        /// <param name="insertionRate">The insertion rate, expressed as a multiple of the rate of sequence mutation.</param>
        /// <param name="deletionRate">The deletion rate, expressed as a multiple of the rate of sequence mutation.</param>
        /// <param name="insertionSizeDistribution">The size distribution for insertions.</param>
        /// <param name="deletionSizeDistribution">The size distribution for deletions.</param>
        public IndelModel(double insertionRate, double deletionRate, IDiscreteDistribution insertionSizeDistribution, IDiscreteDistribution deletionSizeDistribution)
        {
            InsertionRate = insertionRate;
            DeletionRate = deletionRate;
            InsertionSizeDistribution = insertionSizeDistribution;
            DeletionSizeDistribution = deletionSizeDistribution;
        }

        /// <summary>
        /// Creates a new <see cref="IndelModel"/> with the specified rate and size distribution for insertions and deletions.
        /// </summary>
        /// <param name="indelRate">The insertion/deletion rate, expressed as a multiple of the rate of sequence mutation.</param>
        /// <param name="indelSizeDistribution">The size distribution for insertions and deletions.</param>
        public IndelModel(double indelRate, IDiscreteDistribution indelSizeDistribution) : this(indelRate, indelRate, indelSizeDistribution, indelSizeDistribution) { }

        /// <summary>
        /// Creates a new <see cref="IndelModel"/> with the specified insertion rate, deletion rate, and size distribution for insertions and deletions.
        /// </summary>
        /// <param name="insertionRate">The insertion rate, expressed as a multiple of the rate of sequence mutation.</param>
        /// <param name="deletionRate">The deletion rate, expressed as a multiple of the rate of sequence mutation.</param>
        /// <param name="indelSizeDistribution">The size distribution for insertions and deletions.</param>
        public IndelModel(double insertionRate, double deletionRate, IDiscreteDistribution indelSizeDistribution) : this(insertionRate, deletionRate, indelSizeDistribution, indelSizeDistribution) { }

        /// <summary>
        /// Creates a new <see cref="IndelModel"/> with the specified rate for insertions and deletions, insertion size distribution and deletion size distribution.
        /// </summary>
        /// <param name="indelRate">The insertion/deletion rate, expressed as a multiple of the rate of sequence mutation.</param>
        /// <param name="insertionSizeDistribution">The size distribution for insertions.</param>
        /// <param name="deletionSizeDistribution">The size distribution for deletions.</param>
        public IndelModel(double indelRate, IDiscreteDistribution insertionSizeDistribution, IDiscreteDistribution deletionSizeDistribution) : this(indelRate, indelRate, insertionSizeDistribution, deletionSizeDistribution) { }
    }

    /// <summary>
    /// Represents an insertion event.
    /// </summary>
    public struct Insertion
    {
        /// <summary>
        /// The position in the ancestral sequence at which the insertion occurred.
        /// </summary>
        public int Start { get; }

        /// <summary>
        /// The length of the insertion.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// The end of the insertion in the new sequence.
        /// </summary>
        public int End => Start + Length;

        /// <summary>
        /// Creates a new <see cref="Insertion"/>
        /// </summary>
        /// <param name="start">The position in the ancestral sequence at which the insertion occurred.</param>
        /// <param name="length">The length of the insertion.</param>
        public Insertion(int start, int length)
        {
            this.Start = start;
            this.Length = length;
        }
    }
}
