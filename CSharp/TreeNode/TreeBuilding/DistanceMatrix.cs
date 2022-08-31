using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PhyloTree.TreeBuilding
{
    /// <summary>
    /// The sequence evolution model used to compute the distance matrix from the alignment.
    /// </summary>
    public enum EvolutionModel
    {
        /// <summary>
        /// Normalised Hamming distance (proportion of different nucleotides/amino acids).
        /// </summary>
        Hamming = 0,

        /// <summary>
        /// Jukes-Cantor model (assuming equal nucleotide/amino acid frequencies and substitution rates).
        /// </summary>
        JukesCantor = 1,

        /// <summary>
        /// For DNA alignments, this represents the Kimura 1980 model (assuming equal base frequencies, and unequal transition/transversion rates).
        /// For protein alignments, this represents the Kimura 1983 model that approximates PAM distances based only on the fraction of differing amino acids.
        /// </summary>
        Kimura = 2,

        /// <summary>
        /// Only applicable for DNA alignments. This represents the GTR distance, computed as in Waddell &amp; Steel, 1997 (doi: 10.1006/mpev.1997.0452).
        /// Note that since this process involves matrix diagonalisation, it is about an order of magnitude slower than other distance metrics.
        /// </summary>
        GTR = 3,

        /// <summary>
        /// Only applicable for Protein alignments. This represents a distance computed using the Scoredist algorithm with the BLOSUM62 matrix, as in
        /// Sonnhammer &amp; Hollich, 2005 (doi: 10.1186/1471-2105-6-108). Note that in this case U, O and J are treated as gaps. 
        /// </summary>
        BLOSUM62 = 4
    }

    /// <summary>
    /// The kind of sequences in the alignment.
    /// </summary>
    public enum AlignmentType
    {
        /// <summary>
        /// DNA sequences.
        /// </summary>
        DNA = 0,

        /// <summary>
        /// Protein sequences.
        /// </summary>
        Protein = 1,

        /// <summary>
        /// The kind of sequences will be determined based on the first sequence.
        /// </summary>
        Autodetect = 2
    }
    
    /// <summary>
    /// Contains methods to compute distance matrices.
    /// </summary>
    public static partial class DistanceMatrix
    {
        private static readonly byte[][] DNAMatchMatrixJCK80;
        private static readonly uint[][] DNAMatchMatrixGTR;
        private static readonly byte[][] ProteinMatchMatrixJCK83;
        private static readonly byte[][] ProteinMatchMatrixBLOSUM62;

        static DistanceMatrix()
        {
            DNAMatchMatrixJCK80 = new byte[256][];
            BuildDNAMatchMatrixJCK80();

            DNAMatchMatrixGTR = new uint[256][];
            BuildDNAMatchMatrixGTR();

            ProteinMatchMatrixJCK83 = new byte[784][];
            BuildProteinMatchMatrixJCK83();

            ProteinMatchMatrixBLOSUM62 = new byte[784][];
            BuildProteinMatchMatrixBLOSUM62();
        }

        /// <summary>
        /// Post-process a distance matrix removing invalid entries and replacing them with twice the maximum distance in the matrix.
        /// </summary>
        /// <param name="distMat">The matrix to post-process.</param>
        private static void PostProcessDistanceMatrix(float[][] distMat)
        {
            List<(int, int)> indicesToAdjust = new List<(int, int)>();

            float maxDist = float.MinValue;

            for (int i = 0; i < distMat.Length; i++)
            {
                for (int j = 0; j < distMat[i].Length; j++)
                {
                    if (!float.IsFinite(distMat[i][j]) || distMat[i][j] < 0)
                    {
                        indicesToAdjust.Add((i, j));
                    }
                    else
                    {
                        maxDist = Math.Max(distMat[i][j], maxDist);
                    }
                }
            }

            for (int i = 0; i < indicesToAdjust.Count; i++)
            {
                distMat[indicesToAdjust[i].Item1][indicesToAdjust[i].Item2] = 2 * maxDist;
            }
        }

        /// <summary>
        /// Build a distance matrix from aligned DNA sequences stored as a <see cref="T:byte[]"/> array where each <see cref="byte"/> corresponds to three positions.
        /// </summary>
        /// <param name="sequences">The aligned sequences.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:float[][]"/> jagged array containing the lower-triangular distance matrix.</returns>
        public static float[][] BuildFromAlignment(IReadOnlyList<byte[]> sequences, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = -1, Action<double> progressCallback = null)
        {
            float[][] allocatedMatrix = new float[sequences.Count][];

            for (int i = 0; i < sequences.Count; i++)
            {
                allocatedMatrix[i] = new float[i];
            }

            BuildFromAlignment(allocatedMatrix, sequences, evolutionModel, numCores, progressCallback);

            return allocatedMatrix;
        }

        /// <summary>
        /// Build a distance matrix from aligned protein sequences stored as a <see cref="T:ushort[]"/> array where each <see cref="ushort"/> corresponds to two positions.
        /// </summary>
        /// <param name="sequences">The aligned sequences.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:float[][]"/> jagged array containing the lower-triangular distance matrix.</returns>
        public static float[][] BuildFromAlignment(IReadOnlyList<ushort[]> sequences, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = -1, Action<double> progressCallback = null)
        {
            float[][] allocatedMatrix = new float[sequences.Count][];

            for (int i = 0; i < sequences.Count; i++)
            {
                allocatedMatrix[i] = new float[i];
            }

            BuildFromAlignment(allocatedMatrix, sequences, evolutionModel, numCores, progressCallback);

            return allocatedMatrix;
        }

        /// <summary>
        /// Build a distance matrix from aligned DNA sequences stored as a <see cref="T:byte[]"/> array where each <see cref="byte"/> corresponds to three positions.
        /// </summary>
        /// <param name="allocatedMatrix">A pre-allocated <see cref="T:float[][]"/> jagged array that will contain the lower-triangular distance matrix.</param>
        /// <param name="sequences">The aligned sequences.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        public static void BuildFromAlignment(float[][] allocatedMatrix, IReadOnlyList<byte[]> sequences, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = -1, Action<double> progressCallback = null)
        {
            if (numCores <= 0)
            {
                numCores = -1;
            }

            switch (evolutionModel)
            {
                case EvolutionModel.Hamming:
                    ComputeDistanceMatrixHamming(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                case EvolutionModel.JukesCantor:
                    ComputeDistanceMatrixJC(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                case EvolutionModel.Kimura:
                    ComputeDistanceMatrixK80(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                case EvolutionModel.GTR:
                    ComputeDistanceMatrixGTR(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                default:
                    throw new ArgumentException(evolutionModel.ToString() + "is not a valid evolutionary model for DNA sequences!", nameof(evolutionModel));
            }

            PostProcessDistanceMatrix(allocatedMatrix);
        }

        /// <summary>
        /// Build a distance matrix from aligned protein sequences stored as a <see cref="T:ushort[]"/> array where each <see cref="ushort"/> corresponds to two positions.
        /// </summary>
        /// <param name="allocatedMatrix">A pre-allocated <see cref="T:float[][]"/> jagged array that will contain the lower-triangular distance matrix.</param>
        /// <param name="sequences">The aligned sequences.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        public static void BuildFromAlignment(float[][] allocatedMatrix, IReadOnlyList<ushort[]> sequences, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            if (numCores <= 0)
            {
                numCores = -1;
            }

            switch (evolutionModel)
            {
                case EvolutionModel.Hamming:
                    ComputeDistanceMatrixHamming(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                case EvolutionModel.JukesCantor:
                    ComputeDistanceMatrixJC(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                case EvolutionModel.Kimura:
                    ComputeDistanceMatrixK83(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                case EvolutionModel.BLOSUM62:
                    ComputeDistanceMatrixBLOSUM62(sequences, numCores, progressCallback, allocatedMatrix);
                    break;

                default:
                    throw new ArgumentException(evolutionModel.ToString() + "is not a valid evolutionary model for amino acid sequences!", nameof(evolutionModel));
            }

            PostProcessDistanceMatrix(allocatedMatrix);
        }

        /// <summary>
        /// Build a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="allocatedMatrix">A pre-allocated <see cref="T:float[][]"/> jagged array that will contain the lower-triangular distance matrix.</param>
        /// <param name="sequences">The aligned sequences.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        public static void BuildFromAlignment(float[][] allocatedMatrix, IReadOnlyList<string> sequences, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            if (alignmentType == AlignmentType.Autodetect)
            {
                alignmentType = AlignmentType.DNA;

                foreach (string seq in sequences)
                {
                    foreach (char c in seq)
                    {
                        if (!(c < 65 ||
                              c == 'A' ||
                              c == 'a' ||
                              c == 'C' ||
                              c == 'c' ||
                              c == 'G' ||
                              c == 'g' ||
                              c == 'T' ||
                              c == 't' ||
                              c == 'U' ||
                              c == 'u' ||
                              c == 'N' ||
                              c == 'n'))
                        {
                            alignmentType = AlignmentType.Protein;
                            break;
                        }
                    }

                    break;
                }
            }

            int length = sequences[0].Length;

            if (alignmentType == AlignmentType.DNA)
            {
                List<byte[]> sequenceBytes = new List<byte[]>(sequences.Count);

                for (int i = 0; i < sequences.Count; i++)
                {
                    if (length != sequences[i].Length)
                    {
                        throw new ArgumentOutOfRangeException("Not all of the sequences have the same length!", nameof(sequences));
                    }


                    sequenceBytes.Add(ConvertDNASequence(sequences[i]));
                }

                BuildFromAlignment(allocatedMatrix, sequenceBytes, evolutionModel, numCores, progressCallback);
            }
            else if (alignmentType == AlignmentType.Protein)
            {
                List<ushort[]> sequenceUShorts = new List<ushort[]>(sequences.Count);

                for (int i = 0; i < sequences.Count; i++)
                {
                    if (length != sequences[i].Length)
                    {
                        throw new ArgumentOutOfRangeException("Not all of the sequences have the same length!", nameof(sequences));
                    }


                    sequenceUShorts.Add(ConvertProteinSequence(sequences[i]));
                }

                BuildFromAlignment(allocatedMatrix, sequenceUShorts, evolutionModel, numCores, progressCallback);
            }
        }

        /// <summary>
        /// Build a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="sequences">The aligned sequences.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:float[][]"/> jagged array containing the lower-triangular distance matrix.</returns>
        public static float[][] BuildFromAlignment(IReadOnlyList<string> sequences, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            float[][] allocatedMatrix = new float[sequences.Count][];

            for (int i = 0; i < sequences.Count; i++)
            {
                allocatedMatrix[i] = new float[i];
            }

            BuildFromAlignment(allocatedMatrix, sequences, alignmentType, evolutionModel, numCores, progressCallback);

            return allocatedMatrix;
        }

        /// <summary>
        /// Build a bootstrap replicate of a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="allocatedMatrix">A pre-allocated <see cref="T:float[][]"/> jagged array that will contain the lower-triangular distance matrix.</param>
        /// <param name="sequences">The aligned sequences. These will be resampled randomly.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        public static void BootstrapReplicateFromAlignment(float[][] allocatedMatrix, IReadOnlyList<string> sequences, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            if (alignmentType == AlignmentType.Autodetect)
            {
                alignmentType = AlignmentType.DNA;

                foreach (string seq in sequences)
                {
                    foreach (char c in seq)
                    {
                        if (!(c < 65 ||
                              c == 'A' ||
                              c == 'a' ||
                              c == 'C' ||
                              c == 'c' ||
                              c == 'G' ||
                              c == 'g' ||
                              c == 'T' ||
                              c == 't' ||
                              c == 'U' ||
                              c == 'u' ||
                              c == 'N' ||
                              c == 'n'))
                        {
                            alignmentType = AlignmentType.Protein;
                            break;
                        }
                    }

                    break;
                }
            }

            if (alignmentType == AlignmentType.DNA)
            {
                List<byte[]> sequenceBytes = BootstrapDNASequences(sequences);

                BuildFromAlignment(allocatedMatrix, sequenceBytes, evolutionModel, numCores, progressCallback);
            }
            else if (alignmentType == AlignmentType.Protein)
            {
                List<ushort[]> sequenceUShorts = BootstrapProteinSequences(sequences);

                BuildFromAlignment(allocatedMatrix, sequenceUShorts, evolutionModel, numCores, progressCallback);
            }
        }

        /// <summary>
        /// Build a bootstrap replicate of a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="sequences">The aligned sequences. These will be resampled randomly.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:float[][]"/> jagged array containing the lower-triangular distance matrix.</returns>
        public static float[][] BootstrapReplicateFromAlignment(IReadOnlyList<string> sequences, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            float[][] allocatedMatrix = new float[sequences.Count][];

            for (int i = 0; i < sequences.Count; i++)
            {
                allocatedMatrix[i] = new float[i];
            }

            BootstrapReplicateFromAlignment(allocatedMatrix, sequences, alignmentType, evolutionModel, numCores, progressCallback);

            return allocatedMatrix;
        }

        /// <summary>
        /// Build a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="alignment">The aligned sequences.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:float[][]"/> jagged array containing the lower-triangular distance matrix.</returns>
        public static float[][] BuildFromAlignment(Dictionary<string, string> alignment, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            return BuildFromAlignment(alignment.Values.ToList(), alignmentType, evolutionModel, numCores, progressCallback);
        }

        /// <summary>
        /// Build a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="allocatedMatrix">A pre-allocated <see cref="T:float[][]"/> jagged array that will contain the lower-triangular distance matrix.</param>
        /// <param name="alignment">The aligned sequences.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        public static void BuildFromAlignment(float[][] allocatedMatrix, Dictionary<string, string> alignment, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            BuildFromAlignment(allocatedMatrix, alignment.Values.ToList(), alignmentType, evolutionModel, numCores, progressCallback);
        }

        /// <summary>
        /// Build a bootstrap replicate of a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="alignment">The aligned sequences. These will be resampled randomly.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>A <see cref="T:float[][]"/> jagged array containing the lower-triangular distance matrix.</returns>
        public static float[][] BootstrapReplicateFromAlignment(Dictionary<string, string> alignment, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            return BootstrapReplicateFromAlignment(alignment.Values.ToList(), alignmentType, evolutionModel, numCores, progressCallback);
        }

        /// <summary>
        /// Build a bootstrap replicate of a distance matrix from aligned DNA or protein sequences.
        /// </summary>
        /// <param name="allocatedMatrix">A pre-allocated <see cref="T:float[][]"/> jagged array that will contain the lower-triangular distance matrix.</param>
        /// <param name="alignment">The aligned sequences. These will be resampled randomly.</param>
        /// <param name="alignmentType">The type of sequences in the alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use to compute the distance matrix.</param>
        /// <param name="numCores">The maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        public static void BootstrapReplicateFromAlignment(float[][] allocatedMatrix, Dictionary<string, string> alignment, AlignmentType alignmentType = AlignmentType.Autodetect, EvolutionModel evolutionModel = EvolutionModel.Kimura, int numCores = 0, Action<double> progressCallback = null)
        {
            BootstrapReplicateFromAlignment(allocatedMatrix, alignment.Values.ToList(), alignmentType, evolutionModel, numCores, progressCallback);
        }

        /// <summary>
        /// Convert DNA sequences into <see cref="T:byte[]"/> arrays in which each byte corresponds to 3 positions.
        /// </summary>
        /// <param name="sequences">The sequences to convert.</param>
        /// <returns>An <see cref="T:IEnumerable{byte[]}"/> that, when enumerated, will contain the converted sequences.</returns>
        public static IEnumerable<byte[]> ConvertDNASequences(IEnumerable<string> sequences)
        {
            foreach (string sequence in sequences)
            {
                yield return ConvertDNASequence(sequence);
            }
        }

        /// <summary>
        /// Convert DNA sequences into <see cref="T:byte[]"/> arrays in which each <see cref="byte"/> corresponds to 3 positions.
        /// </summary>
        /// <param name="sequences">The sequences to convert.</param>
        /// <returns>A <see cref="T:List{byte[]}"/> that contains the converted sequences.</returns>
        public static List<byte[]> ConvertDNASequences(IReadOnlyList<string> sequences)
        {
            return ConvertDNASequences((IEnumerable<string>)sequences).ToList();
        }

        /// <summary>
        /// Resample a sequence taking the specified positions.
        /// </summary>
        /// <param name="sequence">The sequence to resample.</param>
        /// <param name="sampledPositions">The columns to resample.</param>
        /// <returns>A <see cref="string"/> containing the resampled sequence.</returns>
        private static unsafe string BootstrapSequence(string sequence, int[] sampledPositions)
        {
            string tbr = new string('\0', sequence.Length);

            fixed (char* sequencePtr = sequence)
            fixed (char* tbrPtr = tbr)
            fixed (int* valuesPtr = sampledPositions)
            {
                for (int i = 0; i < sampledPositions.Length; i++)
                {
                    tbrPtr[i] = sequencePtr[valuesPtr[i]];
                }
            }

            return tbr;
        }

        /// <summary>
        /// Computes a bootstrap replicate of a DNA sequence alignment.
        /// </summary>
        /// <param name="sequences">The aligned DNA sequences.</param>
        /// <returns>An <see cref="T:IEnumerable{byte[]}"/> that, when enumerated, will contain the bootstrapped sequences, converted into <see cref="T:byte[]"/> arrays where each byte corresponds to 3 positions.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if not all of the sequences have the same length.</exception>
        public static IEnumerable<byte[]> BootstrapDNASequences(IEnumerable<string> sequences)
        {
            int[] values = null;
            int length = -1;

            foreach (string sequence in sequences)
            {
                if (values == null)
                {
                    length = sequence.Length;
                    values = new int[sequence.Length];
                    MathNet.Numerics.Distributions.DiscreteUniform.Samples(values, 0, sequence.Length - 1);
                }

                if (length != sequence.Length)
                {
                    throw new ArgumentOutOfRangeException("Not all of the sequences have the same length!", nameof(sequences));
                }

                yield return ConvertDNASequence(BootstrapSequence(sequence, values));
            }
        }

        /// <summary>
        /// Computes a bootstrap replicate of a DNA sequence alignment.
        /// </summary>
        /// <param name="sequences">The aligned DNA sequences.</param>
        /// <returns>An <see cref="T:List{byte[]}"/> that contains the bootstrapped sequences, converted into <see cref="T:byte[]"/> arrays where each <see cref="byte"/> corresponds to 3 positions.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if not all of the sequences have the same length.</exception>
        public static List<byte[]> BootstrapDNASequences(IReadOnlyList<string> sequences)
        {
            return BootstrapDNASequences((IEnumerable<string>)sequences).ToList();
        }

        /// <summary>
        /// Convert protein sequences into <see cref="T:ushort[]"/> arrays in which each <see cref="ushort"/> corresponds to 2 positions.
        /// </summary>
        /// <param name="sequences">The sequences to convert.</param>
        /// <returns>An <see cref="T:IEnumerable{ushort[]}"/> that, when enumerated, will contain the converted sequences.</returns>
        public static IEnumerable<ushort[]> ConvertProteinSequences(IEnumerable<string> sequences)
        {
            foreach (string sequence in sequences)
            {
                yield return ConvertProteinSequence(sequence);
            }
        }

        /// <summary>
        /// Convert protein sequences into <see cref="T:ushort[]"/> arrays in which each <see cref="ushort"/> corresponds to 2 positions.
        /// </summary>
        /// <param name="sequences">The sequences to convert.</param>
        /// <returns>A <see cref="T:List{ushort[]}"/> that contains the converted sequences.</returns>
        public static List<ushort[]> ConvertProteinSequences(IReadOnlyList<string> sequences)
        {
            return ConvertProteinSequences((IEnumerable<string>)sequences).ToList();
        }

        /// <summary>
        /// Computes a bootstrap replicate of a protein sequence alignment.
        /// </summary>
        /// <param name="sequences">The aligned protein sequences.</param>
        /// <returns>An <see cref="T:IEnumerable{ushort[]}"/> that, when enumerated, will contain the bootstrapped sequences, converted into <see cref="T:ushort[]"/> arrays where each <see cref="ushort"/> corresponds to 2 positions.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if not all of the sequences have the same length.</exception>
        public static IEnumerable<ushort[]> BootstrapProteinSequences(IEnumerable<string> sequences)
        {
            int[] values = null;
            int length = -1;

            foreach (string sequence in sequences)
            {
                if (values == null)
                {
                    values = new int[sequence.Length];
                    MathNet.Numerics.Distributions.DiscreteUniform.Samples(values, 0, sequence.Length - 1);
                    length = sequence.Length;
                }

                if (length != sequence.Length)
                {
                    throw new ArgumentOutOfRangeException("Not all of the sequences have the same length!", nameof(sequences));
                }

                yield return ConvertProteinSequence(BootstrapSequence(sequence, values));
            }
        }

        /// <summary>
        /// Computes a bootstrap replicate of a protein sequence alignment.
        /// </summary>
        /// <param name="sequences">The aligned protein sequences.</param>
        /// <returns>A <see cref="T:List{ushort[]}"/> that contains the bootstrapped sequences, converted into <see cref="T:ushort[]"/> arrays where each <see cref="ushort"/> corresponds to 2 positions.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if not all of the sequences have the same length.</exception>
        public static List<ushort[]> BootstrapProteinSequences(IReadOnlyList<string> sequences)
        {
            return BootstrapProteinSequences((IEnumerable<string>)sequences).ToList();
        }
    }
}
