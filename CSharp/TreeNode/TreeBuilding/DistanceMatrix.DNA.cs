using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhyloTree.TreeBuilding
{
    public static partial class DistanceMatrix
    {
        /// <summary>
        /// Converts a DNA sequence stored as a string into a <see cref="T:byte[]"/> array. Each byte contains 3 nucleotide position.
        /// The allowed symbols are ACTGUN?- (uppercase and lowercase). All other characters are treated as gaps (<c>?</c> is equivalent to <c>N</c>).
        /// If the sequence length is not a multiple of 3, it is padded with gaps.
        /// </summary>
        /// <param name="sequence">The sequence to convert.</param>
        /// <returns>A <see cref="T:byte[]"/> array representing the sequence.</returns>
        private static byte[] ConvertDNASequence(string sequence)
        {
            byte[] tbr = new byte[(sequence.Length + 2) / 3];

            int length = sequence.Length;

            int currIndex = 0;
            int currPosition = 0;

            unsafe
            {
                fixed (byte* byteString = tbr)
                fixed (char* charString = sequence)
                {
                    for (int i = 0; i < sequence.Length; i++)
                    {
                        switch (charString[i])
                        {
                            case 'A':
                            case 'a':
                                byteString[currIndex] += (byte)((currPosition == 0 ? 1 : currPosition == 1 ? 6 : 36) * 1);
                                break;

                            case 'C':
                            case 'c':
                                byteString[currIndex] += (byte)((currPosition == 0 ? 1 : currPosition == 1 ? 6 : 36) * 2);
                                break;

                            case 'G':
                            case 'g':
                                byteString[currIndex] += (byte)((currPosition == 0 ? 1 : currPosition == 1 ? 6 : 36) * 3);
                                break;

                            case 'T':
                            case 't':
                            case 'U':
                            case 'u':
                                byteString[currIndex] += (byte)((currPosition == 0 ? 1 : currPosition == 1 ? 6 : 36) * 4);
                                break;

                            case 'N':
                            case 'n':
                            case '?':
                                byteString[currIndex] += (byte)((currPosition == 0 ? 1 : currPosition == 1 ? 6 : 36) * 5);
                                break;

                            default:
                                break;
                        }

                        currPosition = (currPosition + 1) % 3;

                        if (currPosition == 0)
                        {
                            currIndex++;
                        }
                    }
                }
            }

            return tbr;
        }

        /// <summary>
        /// Builds the match matrix used for the JC and K80 evolutionary models.
        /// </summary>
        private static void BuildDNAMatchMatrixJCK80()
        {
            for (int x = 0; x < 256; x++)
            {
                DNAMatchMatrixJCK80[x] = new byte[256];
                for (int y = 0; y < 256; y++)
                {
                    // Every byte contains 3 positions. We cache all the possible comparisons between bytes (i.e., between 3 positions).
                    // Here, we store the number of matches, the number of mismatches, the number of transitions and the number of transversions.
                    // Each can have a value between 0 and 3, thus 2 bits for each and a total of 8 bits (1 byte).
                    // The matrix should be small enough that this code doesn't need to be very optimised.
                    byte x1 = (byte)(x % 6);
                    byte y1 = (byte)(y % 6);

                    byte x2 = (byte)((x - x1) % 36 / 6);
                    byte y2 = (byte)((y - y1) % 36 / 6);

                    byte x3 = (byte)((x - x1 - x2 * 6) % 216 / 36);
                    byte y3 = (byte)((y - y1 - y2 * 6) % 216 / 36);

                    int match = 0;
                    int mismatch = 0;
                    int transitions = 0;
                    int transversions = 0;

                    // Same letter, no gap.
                    if (x1 == y1 && x1 != 0)
                    {
                        match++;
                    }
                    // One is N, the other is not a gap.
                    else if ((x1 != 0 && y1 == 5) || (x1 == 5 && y1 != 0))
                    {
                        match++;
                    }
                    // Different letter, and neither is a gap.
                    else if (x1 != y1 && x1 != 0 && y1 != 0)
                    {
                        mismatch++;
                    }

                    // Purine transition.
                    if ((x1 == 1 && y1 == 3) || (x1 == 3 && y1 == 1) ||
                        // Pyrimidine transition.
                        (x1 == 2 && y1 == 4) || (x1 == 4 && y1 == 2))
                    {
                        transitions++;
                    }

                    // Transversion from a purine.
                    if (((x1 == 1 || x1 == 3) && (y1 == 2 || y1 == 4)) ||
                        // Transversion from a pyrimidine.
                        ((x1 == 2 || x1 == 4) && (y1 == 1 || y1 == 3)))
                    {
                        transversions++;
                    }

                    // Same letter, no gap.
                    if (x2 == y2 && x2 != 0)
                    {
                        match++;
                    }
                    // One is N, the other is not a gap.
                    else if ((x2 != 0 && y2 == 5) || (x2 == 5 && y2 != 0))
                    {
                        match++;
                    }
                    // Different letter, and neither is a gap.
                    else if (x2 != y2 && x2 != 0 && y2 != 0)
                    {
                        mismatch++;
                    }

                    // Purine transition.
                    if ((x2 == 1 && y2 == 3) || (x2 == 3 && y2 == 1) ||
                        // Pyrimidine transition.
                        (x2 == 2 && y2 == 4) || (x2 == 4 && y2 == 2))
                    {
                        transitions++;
                    }

                    // Transversion from a purine.
                    if (((x2 == 1 || x2 == 3) && (y2 == 2 || y2 == 4)) ||
                        // Transversion from a pyrimidine.
                        ((x2 == 2 || x2 == 4) && (y2 == 1 || y2 == 3)))
                    {
                        transversions++;
                    }

                    // Same letter, no gap.
                    if (x3 == y3 && x3 != 0)
                    {
                        match++;
                    }
                    // One is N, the other is not a gap.
                    else if ((x3 != 0 && y3 == 5) || (x3 == 5 && y3 != 0))
                    {
                        match++;
                    }
                    // Different letter, and neither is a gap.
                    else if (x3 != y3 && x3 != 0 && y3 != 0)
                    {
                        mismatch++;
                    }

                    // Purine transition.
                    if ((x3 == 1 && y3 == 3) || (x3 == 3 && y3 == 1) ||
                        // Pyrimidine transition.
                        (x3 == 2 && y3 == 4) || (x3 == 4 && y3 == 2))
                    {
                        transitions++;
                    }

                    // Transversion from a purine.
                    if (((x3 == 1 || x3 == 3) && (y3 == 2 || y3 == 4)) ||
                        // Transversion from a pyrimidine.
                        ((x3 == 2 || x3 == 4) && (y3 == 1 || y3 == 3)))
                    {
                        transversions++;
                    }

                    DNAMatchMatrixJCK80[x][y] = (byte)(match + 4 * mismatch + 16 * transitions + 64 * transversions);
                }
            }
        }


        /// <summary>
        /// Builds the match matrix used for the GTR evolutionary model.
        /// </summary>
        private static void BuildDNAMatchMatrixGTR()
        {
            for (int x = 0; x < 256; x++)
            {
                DNAMatchMatrixGTR[x] = new uint[256];
                for (int y = 0; y < 256; y++)
                {
                    // Every byte contains 3 positions. We cache all the possible comparisons between bytes (i.e., between 3 positions).
                    // Here, we store the number of matches (AA, CC, GG, TT) and the number of state changes (AC/CA, AG/GA, AT/TA, CG/GC,
                    // CT/TC, GT/TG). Each can have a value between 0 and 3, thus 2 bits for each and a total of 20 bits (3 bytes, stored
                    // as a uint taking up 4 bytes). The matrix should be small enough that this code doesn't need to be very optimised.
                    byte x1 = (byte)(x % 6);
                    byte y1 = (byte)(y % 6);

                    byte comp1 = (byte)(x1 | (y1 << 3));

                    byte x2 = (byte)((x - x1) % 36 / 6);
                    byte y2 = (byte)((y - y1) % 36 / 6);

                    byte comp2 = (byte)(x2 | (y2 << 3));

                    byte x3 = (byte)((x - x1 - x2 * 6) % 216 / 36);
                    byte y3 = (byte)((y - y1 - y2 * 6) % 216 / 36);

                    byte comp3 = (byte)(x3 | (y3 << 3));

                    int aa = 0;
                    int cc = 0;
                    int gg = 0;
                    int tt = 0;

                    int ac = 0;
                    int ag = 0;
                    int at = 0;
                    int cg = 0;
                    int ct = 0;
                    int gt = 0;

                    switch (comp1)
                    {
                        // AA
                        case 0b001001:
                        // AN
                        case 0b101001:
                        // NA
                        case 0b001101:
                            aa++;
                            break;

                        // AC
                        case 0b010001:
                        // CA
                        case 0b001010:
                            ac++;
                            break;

                        // AG
                        case 0b011001:
                        // GA
                        case 0b001011:
                            ag++;
                            break;

                        // AT
                        case 0b100001:
                        // TA
                        case 0b001100:
                            at++;
                            break;


                        // CC
                        case 0b010010:
                        // CN
                        case 0b101010:
                        // NC
                        case 0b010101:
                            cc++;
                            break;

                        // CG
                        case 0b011010:
                        // GC
                        case 0b010011:
                            cg++;
                            break;

                        // CT
                        case 0b100010:
                        // TC
                        case 0b010100:
                            ct++;
                            break;

                        // GG
                        case 0b011011:
                        // GN
                        case 0b101011:
                        // NG
                        case 0b011101:
                            gg++;
                            break;

                        // GT
                        case 0b100011:
                        // TG
                        case 0b011100:
                            gt++;
                            break;

                        // TT
                        case 0b100100:
                        // TN
                        case 0b101100:
                        // NT
                        case 0b100101:
                            tt++;
                            break;
                    }

                    switch (comp2)
                    {
                        // AA
                        case 0b001001:
                        // AN
                        case 0b101001:
                        // NA
                        case 0b001101:
                            aa++;
                            break;

                        // AC
                        case 0b010001:
                        // CA
                        case 0b001010:
                            ac++;
                            break;

                        // AG
                        case 0b011001:
                        // GA
                        case 0b001011:
                            ag++;
                            break;

                        // AT
                        case 0b100001:
                        // TA
                        case 0b001100:
                            at++;
                            break;


                        // CC
                        case 0b010010:
                        // CN
                        case 0b101010:
                        // NC
                        case 0b010101:
                            cc++;
                            break;

                        // CG
                        case 0b011010:
                        // GC
                        case 0b010011:
                            cg++;
                            break;

                        // CT
                        case 0b100010:
                        // TC
                        case 0b010100:
                            ct++;
                            break;

                        // GG
                        case 0b011011:
                        // GN
                        case 0b101011:
                        // NG
                        case 0b011101:
                            gg++;
                            break;

                        // GT
                        case 0b100011:
                        // TG
                        case 0b011100:
                            gt++;
                            break;

                        // TT
                        case 0b100100:
                        // TN
                        case 0b101100:
                        // NT
                        case 0b100101:
                            tt++;
                            break;
                    }

                    switch (comp3)
                    {
                        // AA
                        case 0b001001:
                        // AN
                        case 0b101001:
                        // NA
                        case 0b001101:
                            aa++;
                            break;

                        // AC
                        case 0b010001:
                        // CA
                        case 0b001010:
                            ac++;
                            break;

                        // AG
                        case 0b011001:
                        // GA
                        case 0b001011:
                            ag++;
                            break;

                        // AT
                        case 0b100001:
                        // TA
                        case 0b001100:
                            at++;
                            break;


                        // CC
                        case 0b010010:
                        // CN
                        case 0b101010:
                        // NC
                        case 0b010101:
                            cc++;
                            break;

                        // CG
                        case 0b011010:
                        // GC
                        case 0b010011:
                            cg++;
                            break;

                        // CT
                        case 0b100010:
                        // TC
                        case 0b010100:
                            ct++;
                            break;

                        // GG
                        case 0b011011:
                        // GN
                        case 0b101011:
                        // NG
                        case 0b011101:
                            gg++;
                            break;

                        // GT
                        case 0b100011:
                        // TG
                        case 0b011100:
                            gt++;
                            break;

                        // TT
                        case 0b100100:
                        // TN
                        case 0b101100:
                        // NT
                        case 0b100101:
                            tt++;
                            break;
                    }

                    DNAMatchMatrixGTR[x][y] = (uint)(aa + (cc << 2) + (gg << 4) + (tt << 6) + (ac << 8) + (ag << 10) + (at << 12) + (cg << 14) + (ct << 16) + (gt << 18));
                }
            }
        }


        /// <summary>
        /// Compares two DNA sequences using the Hamming distance.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <returns>The (normalised) Hamming distance between the two sequences.</returns>
        private static float CompareDNASequencesHamming(byte[] sequence1, byte[] sequence2)
        {
            int length = sequence1.Length;

            int match = 0;
            int mismatch = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result = DNAMatchMatrixJCK80[seq1[i]][seq2[i]];

                        match += result & 3;
                        mismatch += (result >> 2) & 3;
                    }
                }
            }

            return mismatch / (float)(match + mismatch);
        }

        /// <summary>
        /// Compares a DNA sequence with two other sequences using the Hamming distance. Faster than
        /// calling <see cref="CompareDNASequencesHamming(byte[], byte[])"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="dist12">When the method returns, this variable will contain the (normalised) Hamming distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the (normalised) Hamming distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareDNASequencesHamming(byte[] sequence1, byte[] sequence2, byte[] sequence3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            int match12 = 0;
            int mismatch12 = 0;

            int match13 = 0;
            int mismatch13 = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                fixed (byte* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int i1 = seq1[i];

                        byte result12 = DNAMatchMatrixJCK80[i1][seq2[i]];

                        match12 += result12 & 3;
                        mismatch12 += (result12 >> 2) & 3;

                        byte result13 = DNAMatchMatrixJCK80[i1][seq3[i]];

                        match13 += result13 & 3;
                        mismatch13 += (result13 >> 2) & 3;
                    }
                }
            }

            dist12 = mismatch12 / (float)(match12 + mismatch12);
            dist13 = mismatch13 / (float)(match13 + mismatch13);
        }

        /// <summary>
        /// Computes a distance matrix between DNA sequences using the Hamming distance.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixHamming(IReadOnlyList<byte[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1);
            object progressLock = new object();

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareDNASequencesHamming(sequences[i], sequences[j], sequences[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareDNASequencesHamming(sequences[i], sequences[j]);
                }

                if (progressCallback != null)
                {
                    lock (progressLock)
                    {
                        progress += i;
                        progressCallback((double)progress / total);
                    }
                }
            });
        }

        /// <summary>
        /// Compares two DNA sequences using the JC model of evolution.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <returns>The JC distance between the two sequences.</returns>
        private static float CompareDNASequencesJC(byte[] sequence1, byte[] sequence2)
        {
            int length = sequence1.Length;

            int match = 0;
            int mismatch = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result = DNAMatchMatrixJCK80[seq1[i]][seq2[i]];

                        match += result & 3;
                        mismatch += (result >> 2) & 3;
                    }
                }
            }

            return (float)(-0.75 * Math.Log(1 - 1.3333333333333333333333333333333333333333333333333 * mismatch / (match + mismatch)));
        }

        /// <summary>
        /// Compares a DNA sequence with two other sequences using the JC model of evolution. Faster than
        /// calling <see cref="CompareDNASequencesJC(byte[], byte[])"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="dist12">When the method returns, this variable will contain the JC distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the JC distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareDNASequencesJC(byte[] sequence1, byte[] sequence2, byte[] sequence3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            int match12 = 0;
            int mismatch12 = 0;

            int match13 = 0;
            int mismatch13 = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                fixed (byte* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int i1 = seq1[i];

                        byte result12 = DNAMatchMatrixJCK80[i1][seq2[i]];

                        match12 += result12 & 3;
                        mismatch12 += (result12 >> 2) & 3;

                        byte result13 = DNAMatchMatrixJCK80[i1][seq3[i]];

                        match13 += result13 & 3;
                        mismatch13 += (result13 >> 2) & 3;
                    }
                }
            }

            dist12 = (float)(-0.75 * Math.Log(1 - 1.3333333333333333333333333333333333333333333333333 * mismatch12 / (match12 + mismatch12)));
            dist13 = (float)(-0.75 * Math.Log(1 - 1.3333333333333333333333333333333333333333333333333 * mismatch13 / (match13 + mismatch13)));
        }

        /// <summary>
        /// Computes a distance matrix between DNA sequences using the JC model of evolution.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixJC(IReadOnlyList<byte[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1);
            object progressLock = new object();

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareDNASequencesJC(sequences[i], sequences[j], sequences[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareDNASequencesJC(sequences[i], sequences[j]);
                }

                if (progressCallback != null)
                {
                    lock (progressLock)
                    {
                        progress += i;
                        progressCallback((double)progress / total);
                    }
                }
            });
        }


        /// <summary>
        /// Compares two DNA sequences using the K80 model of evolution.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <returns>The K80 distance between the two sequences.</returns>
        private static float CompareDNASequencesK80(byte[] sequence1, byte[] sequence2)
        {
            int length = sequence1.Length;

            int match = 0;
            int transitions = 0;
            int transversions = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result = DNAMatchMatrixJCK80[seq1[i]][seq2[i]];

                        match += result & 3;
                        transitions += (result >> 4) & 3;
                        transversions += (result >> 6) & 3;
                    }
                }
            }

            int total = transitions + transversions + match;
            double transversionProp = (double)transversions / total;

            return (float)(-0.5 * Math.Log((1 - 2.0 * transitions / total - transversionProp) * Math.Sqrt(1 - 2 * transversionProp)));
        }


        /// <summary>
        /// Compares a DNA sequence with two other sequences using the K80 model of evolution. Faster than
        /// calling <see cref="CompareDNASequencesK80(byte[], byte[])"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="dist12">When the method returns, this variable will contain the K80 distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the K80 distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareDNASequencesK80(byte[] sequence1, byte[] sequence2, byte[] sequence3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            int match12 = 0;
            int transitions12 = 0;
            int transversions12 = 0;

            int match13 = 0;
            int transitions13 = 0;
            int transversions13 = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                fixed (byte* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int i1 = seq1[i];

                        byte result12 = DNAMatchMatrixJCK80[i1][seq2[i]];

                        match12 += result12 & 3;
                        transitions12 += (result12 >> 4) & 3;
                        transversions12 += (result12 >> 6) & 3;

                        byte result13 = DNAMatchMatrixJCK80[i1][seq3[i]];

                        match13 += result13 & 3;
                        transitions13 += (result13 >> 4) & 3;
                        transversions13 += (result13 >> 6) & 3;
                    }
                }
            }

            int total12 = transitions12 + transversions12 + match12;
            double transversionProp12 = (double)transversions12 / total12;

            dist12 = (float)(-0.5 * Math.Log((1 - 2.0 * transitions12 / total12 - transversionProp12) * Math.Sqrt(1 - 2 * transversionProp12)));

            int total13 = transitions13 + transversions13 + match13;
            double transversionProp13 = (double)transversions13 / total13;

            dist13 = (float)(-0.5 * Math.Log((1 - 2.0 * transitions13 / total13 - transversionProp13) * Math.Sqrt(1 - 2 * transversionProp13)));
        }

        /// <summary>
        /// Computes a distance matrix between DNA sequences using the K80 model of evolution.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixK80(IReadOnlyList<byte[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1);
            object progressLock = new object();

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareDNASequencesK80(sequences[i], sequences[j], sequences[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareDNASequencesK80(sequences[i], sequences[j]);
                }

                if (progressCallback != null)
                {
                    lock (progressLock)
                    {
                        progress += i;
                        progressCallback((double)progress / total);
                    }
                }
            });
        }

        /// <summary>
        /// Compares two DNA sequences using the GTR model of evolution.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <remarks>From Waddel &amp; Steel, 1997.</remarks>
        /// <returns>The GTR distance between the two sequences.</returns>
        private static float CompareDNASequencesGTR(byte[] sequence1, byte[] sequence2)
        {
            int length = sequence1.Length;

            uint aa = 0;
            uint cc = 0;
            uint gg = 0;
            uint tt = 0;

            uint ac = 0;
            uint ag = 0;
            uint at = 0;

            uint cg = 0;
            uint ct = 0;

            uint gt = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        uint result = DNAMatchMatrixGTR[seq1[i]][seq2[i]];

                        aa += result & 3;
                        cc += (result >> 2) & 3;
                        gg += (result >> 4) & 3;
                        tt += (result >> 6) & 3;

                        ac += (result >> 8) & 3;
                        ag += (result >> 10) & 3;
                        at += (result >> 12) & 3;

                        cg += (result >> 14) & 3;
                        ct += (result >> 16) & 3;

                        gt += (result >> 18) & 3;
                    }
                }
            }

            double total = aa + cc + gg + tt + ac + ag + at + cg + ct + gt;

            Matrix<double> fSharp = Matrix<double>.Build.Dense(4, 4);

            fSharp[0, 0] = aa / total;
            fSharp[0, 1] = ac / total * 0.5;
            fSharp[0, 2] = ag / total * 0.5;
            fSharp[0, 3] = at / total * 0.5;

            fSharp[1, 0] = fSharp[0, 1];
            fSharp[1, 1] = cc / total;
            fSharp[1, 2] = cg / total * 0.5;
            fSharp[1, 3] = ct / total * 0.5;

            fSharp[2, 0] = fSharp[0, 2];
            fSharp[2, 1] = fSharp[1, 2];
            fSharp[2, 2] = gg / total;
            fSharp[2, 3] = gt / total * 0.5;

            fSharp[3, 0] = fSharp[0, 3];
            fSharp[3, 1] = fSharp[1, 3];
            fSharp[3, 2] = fSharp[2, 3];
            fSharp[3, 3] = tt / total;

            Matrix<double> Pi = Matrix<double>.Build.DiagonalOfDiagonalVector(fSharp.RowSums());

            Matrix<double> P = Pi.Inverse() * fSharp;

            Evd<double> P_EVD = P.Evd();

            double[] diagonal = new double[4];

            // Why -280? No idea, but it produces results comparable to PAUP*.
            for (int i = 0; i < 4; i++)
            {
                if (P_EVD.EigenValues[i].Imaginary == 0 && P_EVD.EigenValues[i].Real > 0)
                {
                    diagonal[i] = Math.Max(-280, Math.Log(P_EVD.EigenValues[i].Real));
                }
                else
                {
                    diagonal[i] = -280;
                }
            }

            Matrix<double> logPsi = Matrix<double>.Build.DiagonalOfDiagonalArray(diagonal);

            return (float)(-(Pi * (P_EVD.EigenVectors * logPsi * P_EVD.EigenVectors.Inverse())).Trace());
        }

        /// <summary>
        /// Compares a DNA sequence with two other sequences using the GTR model of evolution. Faster than
        /// calling <see cref="CompareDNASequencesGTR(byte[], byte[])"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="dist12">When the method returns, this variable will contain the GTR distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the GTR distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareDNASequencesGTR(byte[] sequence1, byte[] sequence2, byte[] sequence3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            uint aa12 = 0;
            uint cc12 = 0;
            uint gg12 = 0;
            uint tt12 = 0;

            uint ac12 = 0;
            uint ag12 = 0;
            uint at12 = 0;

            uint cg12 = 0;
            uint ct12 = 0;

            uint gt12 = 0;

            uint aa13 = 0;
            uint cc13 = 0;
            uint gg13 = 0;
            uint tt13 = 0;

            uint ac13 = 0;
            uint ag13 = 0;
            uint at13 = 0;

            uint cg13 = 0;
            uint ct13 = 0;

            uint gt13 = 0;

            unsafe
            {
                fixed (byte* seq1 = sequence1)
                fixed (byte* seq2 = sequence2)
                fixed (byte* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int i1 = seq1[i];

                        uint result12 = DNAMatchMatrixGTR[i1][seq2[i]];

                        aa12 += result12 & 3;
                        cc12 += (result12 >> 2) & 3;
                        gg12 += (result12 >> 4) & 3;
                        tt12 += (result12 >> 6) & 3;

                        ac12 += (result12 >> 8) & 3;
                        ag12 += (result12 >> 10) & 3;
                        at12 += (result12 >> 12) & 3;

                        cg12 += (result12 >> 14) & 3;
                        ct12 += (result12 >> 16) & 3;

                        gt12 += (result12 >> 18) & 3;

                        uint result13 = DNAMatchMatrixGTR[i1][seq3[i]];

                        aa13 += result13 & 3;
                        cc13 += (result13 >> 2) & 3;
                        gg13 += (result13 >> 4) & 3;
                        tt13 += (result13 >> 6) & 3;

                        ac13 += (result13 >> 8) & 3;
                        ag13 += (result13 >> 10) & 3;
                        at13 += (result13 >> 12) & 3;

                        cg13 += (result13 >> 14) & 3;
                        ct13 += (result13 >> 16) & 3;

                        gt13 += (result13 >> 18) & 3;
                    }
                }
            }

            double total12 = aa12 + cc12 + gg12 + tt12 + ac12 + ag12 + at12 + cg12 + ct12 + gt12;

            Matrix<double> fSharp12 = Matrix<double>.Build.Dense(4, 4);

            fSharp12[0, 0] = aa12 / total12;
            fSharp12[0, 1] = ac12 / total12 * 0.5;
            fSharp12[0, 2] = ag12 / total12 * 0.5;
            fSharp12[0, 3] = at12 / total12 * 0.5;

            fSharp12[1, 0] = fSharp12[0, 1];
            fSharp12[1, 1] = cc12 / total12;
            fSharp12[1, 2] = cg12 / total12 * 0.5;
            fSharp12[1, 3] = ct12 / total12 * 0.5;

            fSharp12[2, 0] = fSharp12[0, 2];
            fSharp12[2, 1] = fSharp12[1, 2];
            fSharp12[2, 2] = gg12 / total12;
            fSharp12[2, 3] = gt12 / total12 * 0.5;

            fSharp12[3, 0] = fSharp12[0, 3];
            fSharp12[3, 1] = fSharp12[1, 3];
            fSharp12[3, 2] = fSharp12[2, 3];
            fSharp12[3, 3] = tt12 / total12;

            Matrix<double> Pi12 = Matrix<double>.Build.DiagonalOfDiagonalVector(fSharp12.RowSums());

            Matrix<double> P12 = Pi12.Inverse() * fSharp12;

            Evd<double> P_EVD12 = P12.Evd();

            double[] diagonal12 = new double[4];

            // Why -280? No idea, but it produces results comparable to PAUP*.
            for (int i = 0; i < 4; i++)
            {
                if (P_EVD12.EigenValues[i].Imaginary == 0 && P_EVD12.EigenValues[i].Real > 0)
                {
                    diagonal12[i] = Math.Max(-280, Math.Log(P_EVD12.EigenValues[i].Real));
                }
                else
                {
                    diagonal12[i] = -280;
                }
            }

            Matrix<double> logPsi12 = Matrix<double>.Build.DiagonalOfDiagonalArray(diagonal12);

            dist12 = (float)(-(Pi12 * (P_EVD12.EigenVectors * logPsi12 * P_EVD12.EigenVectors.Inverse())).Trace());

            double total13 = aa13 + cc13 + gg13 + tt13 + ac13 + ag13 + at13 + cg13 + ct13 + gt13;

            Matrix<double> fSharp13 = Matrix<double>.Build.Dense(4, 4);

            fSharp13[0, 0] = aa13 / total13;
            fSharp13[0, 1] = ac13 / total13 * 0.5;
            fSharp13[0, 2] = ag13 / total13 * 0.5;
            fSharp13[0, 3] = at13 / total13 * 0.5;

            fSharp13[1, 0] = fSharp13[0, 1];
            fSharp13[1, 1] = cc13 / total13;
            fSharp13[1, 2] = cg13 / total13 * 0.5;
            fSharp13[1, 3] = ct13 / total13 * 0.5;

            fSharp13[2, 0] = fSharp13[0, 2];
            fSharp13[2, 1] = fSharp13[1, 2];
            fSharp13[2, 2] = gg13 / total13;
            fSharp13[2, 3] = gt13 / total13 * 0.5;

            fSharp13[3, 0] = fSharp13[0, 3];
            fSharp13[3, 1] = fSharp13[1, 3];
            fSharp13[3, 2] = fSharp13[2, 3];
            fSharp13[3, 3] = tt13 / total13;

            Matrix<double> Pi13 = Matrix<double>.Build.DiagonalOfDiagonalVector(fSharp13.RowSums());

            Matrix<double> P13 = Pi13.Inverse() * fSharp13;

            Evd<double> P_EVD13 = P13.Evd();

            double[] diagonal13 = new double[4];

            // Why -280? No idea, but it produces results comparable to PAUP*.
            for (int i = 0; i < 4; i++)
            {
                if (P_EVD13.EigenValues[i].Imaginary == 0 && P_EVD13.EigenValues[i].Real > 0)
                {
                    diagonal13[i] = Math.Max(-280, Math.Log(P_EVD13.EigenValues[i].Real));
                }
                else
                {
                    diagonal13[i] = -280;
                }
            }

            Matrix<double> logPsi13 = Matrix<double>.Build.DiagonalOfDiagonalArray(diagonal13);

            dist13 = (float)(-(Pi13 * (P_EVD13.EigenVectors * logPsi13 * P_EVD13.EigenVectors.Inverse())).Trace());
        }

        /// <summary>
        /// Computes a distance matrix between DNA sequences using the GTR model of evolution.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixGTR(IReadOnlyList<byte[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1);
            object progressLock = new object();

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareDNASequencesGTR(sequences[i], sequences[j], sequences[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareDNASequencesGTR(sequences[i], sequences[j]);
                }

                if (progressCallback != null)
                {
                    lock (progressLock)
                    {
                        progress += i;
                        progressCallback((double)progress / total);
                    }
                }
            });
        }
    }
}


