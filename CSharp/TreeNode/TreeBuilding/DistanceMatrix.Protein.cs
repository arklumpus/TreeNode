using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace PhyloTree.TreeBuilding
{
    public static partial class DistanceMatrix
    {
        // From rapidNJ source code.
        private static readonly int[] DayhoffPAMs = { 195, 196, 197, 198, 199, 200, 200, 201, 202, 203, 204, 205, 206, 207, 208, 209, 209, 210, 211, 212, 213, 214, 215, 216, 217, 218, 219, 220, 221, 222, 223, 224, 226, 227, 228, 229, 230, 231, 232, 233, 234, 236, 237, 238, 239, 240, 241, 243, 244, 245, 246, 248, 249, 250, 252, 253, 254, 255, 257, 258, 260, 261, 262, 264, 265, 267, 268, 270, 271, 273, 274, 276, 277, 279, 281, 282, 284, 285, 287, 289, 291, 292, 294, 296, 298, 299, 301, 303, 305, 307, 309, 311, 313, 315, 317, 319, 321, 323, 325, 328, 330, 332, 335, 337, 339, 342, 344, 347, 349, 352, 354, 357, 360, 362, 365, 368, 371, 374, 377, 380, 383, 386, 389, 393, 396, 399, 403, 407, 410, 414, 418, 422, 426, 430, 434, 438, 442, 447, 451, 456, 461, 466, 471, 476, 482, 487, 493, 498, 504, 511, 517, 524, 531, 538, 545, 553, 560, 569, 577, 586, 595, 605, 615, 626, 637, 649, 661, 675, 688, 703, 719, 736, 754, 775, 796, 819, 845, 874, 907, 945, 988 };

        private static readonly sbyte[,] BLOSUM62 = new sbyte[28, 28]
        {
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 4, 0, -2, -1, -2, 0, -2, -1, -1, -1, -1, -2, -1, -1, -1, 1, 0, 0, -3, -2, 0, 0, -2, -1, 0, 0, -4 },
            { 0, 0, 9, -3, -4, -2, -3, -3, -1, -3, -1, -1, -3, -3, -3, -3, -1, -1, -1, -2, -2, 0, 0, -3, -3, 0, -2, -4 },
            { 0, -2, -3, 6, 2, -3, -1, -1, -3, -1, -4, -3, 1, -1, 0, -2, 0, -1, -3, -4, -3, 0, 0, 4, 1, 0, -1, -4 },
            { 0, -1, -4, 2, 5, -3, -2, 0, -3, 1, -3, -2, 0, -1, 2, 0, 0, -1, -2, -3, -2, 0, 0, 1, 4, 0, -1, -4 },
            { 0, -2, -2, -3, -3, 6, -3, -1, 0, -3, 0, 0, -3, -4, -3, -3, -2, -2, -1, 1, 3, 0, 0, -3, -3, 0, -1, -4 },
            { 0, 0, -3, -1, -2, -3, 6, -2, -4, -2, -4, -3, 0, -2, -2, -2, 0, -2, -3, -2, -3, 0, 0, -1, -2, 0, -1, -4 },
            { 0, -2, -3, -1, 0, -1, -2, 8, -3, -1, -3, -2, 1, -2, 0, 0, -1, -2, -3, -2, 2, 0, 0, 0, 0, 0, -1, -4 },
            { 0, -1, -1, -3, -3, 0, -4, -3, 4, -3, 2, 1, -3, -3, -3, -3, -2, -1, 3, -3, -1, 0, 0, -3, -3, 0, -1, -4 },
            { 0, -1, -3, -1, 1, -3, -2, -1, -3, 5, -2, -1, 0, -1, 1, 2, 0, -1, -2, -3, -2, 0, 0, 0, 1, 0, -1, -4 },
            { 0, -1, -1, -4, -3, 0, -4, -3, 2, -2, 4, 2, -3, -3, -2, -2, -2, -1, 1, -2, -1, 0, 0, -4, -3, 0, -1, -4 },
            { 0, -1, -1, -3, -2, 0, -3, -2, 1, -1, 2, 5, -2, -2, 0, -1, -1, -1, 1, -1, -1, 0, 0, -3, -1, 0, -1, -4 },
            { 0, -2, -3, 1, 0, -3, 0, 1, -3, 0, -3, -2, 6, -2, 0, 0, 1, 0, -3, -4, -2, 0, 0, 3, 0, 0, -1, -4 },
            { 0, -1, -3, -1, -1, -4, -2, -2, -3, -1, -3, -2, -2, 7, -1, -2, -1, -1, -2, -4, -3, 0, 0, -2, -1, 0, -2, -4 },
            { 0, -1, -3, 0, 2, -3, -2, 0, -3, 1, -2, 0, 0, -1, 5, 1, 0, -1, -2, -2, -1, 0, 0, 0, 3, 0, -1, -4 },
            { 0, -1, -3, -2, 0, -3, -2, 0, -3, 2, -2, -1, 0, -2, 1, 5, -1, -1, -3, -3, -2, 0, 0, -1, 0, 0, -1, -4 },
            { 0, 1, -1, 0, 0, -2, 0, -1, -2, 0, -2, -1, 1, -1, 0, -1, 4, 1, -2, -3, -2, 0, 0, 0, 0, 0, 0, -4 },
            { 0, 0, -1, -1, -1, -2, -2, -2, -1, -1, -1, -1, 0, -1, -1, -1, 1, 5, 0, -2, -2, 0, 0, -1, -1, 0, 0, -4 },
            { 0, 0, -1, -3, -2, -1, -3, -3, 3, -2, 1, 1, -3, -2, -2, -3, -2, 0, 4, -3, -1, 0, 0, -3, -2, 0, -1, -4 },
            { 0, -3, -2, -4, -3, 1, -2, -2, -3, -3, -2, -1, -4, -4, -2, -3, -3, -2, -3, 11, 2, 0, 0, -4, -3, 0, -2, -4 },
            { 0, -2, -2, -3, -2, 3, -3, 2, -1, -2, -1, -1, -2, -3, -1, -2, -2, -2, -1, 2, 7, 0, 0, -3, -2, 0, -1, -4 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, -2, -3, 4, 1, -3, -1, 0, -3, 0, -4, -3, 3, -2, 0, -1, 0, -1, -3, -4, -3, 0, 0, 4, 1, 0, -1, -4 },
            { 0, -1, -3, 1, 4, -3, -2, 0, -3, 1, -3, -1, 0, -1, 3, 0, 0, -1, -2, -3, -2, 0, 0, 1, 4, 0, -1, -4 },
            { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            { 0, 0, -2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -2, -1, -1, 0, 0, -1, -2, -1, 0, 0, -1, -1, 0, -1, -4 },
            { 0, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, -4, 0, 0, -4, -4, 0, -4, 1 },
        };

        /// <summary>
        /// Converts a protein sequence stored as a string into a <see cref="T:ushort[]"/> array. Each ushort contains 2 amino acid positions.
        /// The allowed symbols are the usual 20 1-letter amino acid abbreviations, plus U for Sec, O for Pyl, B for Asn or Asp, Z for
        /// Gln or Glu, J for Ile or Leu, X for any amino acid, * for stop codons and - for gaps (uppercase and lowercase). All other
        /// characters are treated as gaps. If the sequence length is not a multiple of 2, it is padded with gaps.
        /// </summary>
        /// <param name="sequence">The sequence to convert.</param>
        /// <returns>A <see cref="T:ushort[]"/> array representing the sequence.</returns>
        public static ushort[] ConvertProteinSequence(string sequence)
        {
            ushort[] tbr = new ushort[(sequence.Length + 1) / 2];

            int length = sequence.Length;

            int currIndex = 0;
            int currPosition = 0;

            unsafe
            {
                fixed (ushort* ushortString = tbr)
                fixed (char* charString = sequence)
                {
                    for (int i = 0; i < sequence.Length; i++)
                    {
                        switch (charString[i])
                        {
                            case 'A':
                            case 'a':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 1);
                                break;

                            case 'C':
                            case 'c':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 2);
                                break;

                            case 'D':
                            case 'd':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 3);
                                break;

                            case 'E':
                            case 'e':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 4);
                                break;

                            case 'F':
                            case 'f':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 5);
                                break;

                            case 'G':
                            case 'g':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 6);
                                break;

                            case 'H':
                            case 'h':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 7);
                                break;

                            case 'I':
                            case 'i':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 8);
                                break;

                            case 'K':
                            case 'k':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 9);
                                break;

                            case 'L':
                            case 'l':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 10);
                                break;

                            case 'M':
                            case 'm':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 11);
                                break;

                            case 'N':
                            case 'n':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 12);
                                break;

                            case 'P':
                            case 'p':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 13);
                                break;

                            case 'Q':
                            case 'q':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 14);
                                break;

                            case 'R':
                            case 'r':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 15);
                                break;

                            case 'S':
                            case 's':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 16);
                                break;

                            case 'T':
                            case 't':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 17);
                                break;

                            case 'V':
                            case 'v':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 18);
                                break;

                            case 'W':
                            case 'w':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 19);
                                break;

                            case 'Y':
                            case 'y':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 20);
                                break;

                            // Selenocysteine
                            case 'U':
                            case 'u':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 21);
                                break;

                            // Pyrrolysine
                            case 'O':
                            case 'o':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 22);
                                break;

                            // Asparagine or aspartate
                            case 'B':
                            case 'b':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 23);
                                break;

                            // Glutamine or glutamate
                            case 'Z':
                            case 'z':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 24);
                                break;

                            // Leucine or isoleucine
                            case 'J':
                            case 'j':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 25);
                                break;

                            // Any amino acid
                            case 'X':
                            case 'x':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 26);
                                break;

                            // Stop codon
                            case '*':
                                ushortString[currIndex] += (ushort)((currPosition == 0 ? 1 : 28) * 27);
                                break;

                            default:
                                break;
                        }

                        currPosition = (currPosition + 1) % 2;

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
        /// Builds the match matrix used for the JC and K83 evolutionary models.
        /// </summary>
        private static void BuildProteinMatchMatrixJCK83()
        {
            for (int x = 0; x < 784; x++)
            {
                ProteinMatchMatrixJCK83[x] = new byte[784];
                for (int y = 0; y < 784; y++)
                {
                    // Every ushort contains 2 positions. We cache all the possible comparisons between ushorts (i.e., between 2 positions).
                    // Here, we store the number of matches and the number of mismatches. Each can have a value between 0 and 2, thus 2 bits
                    // for each and a total of 4 bits (stored as 1 byte). The matrix should be small enough that this code doesn't need to be
                    // very optimised.
                    byte x1 = (byte)(x % 28);
                    byte y1 = (byte)(y % 28);

                    byte x2 = (byte)(x / 28);
                    byte y2 = (byte)(y / 28);

                    int match = 0;
                    int mismatch = 0;

                    // Same letter, no gap.
                    if (x1 == y1 && x1 != 0)
                    {
                        match++;
                    }
                    // One is X, the other is not a gap.
                    else if ((x1 != 0 && y1 == 26) || (x1 == 26 && y1 != 0))
                    {
                        match++;
                    }
                    // One is B, the other is N or D.
                    else if (((x1 == 12 || x1 == 3) && y1 == 23) || ((y1 == 12 || y1 == 3) && x1 == 23))
                    {
                        match++;
                    }
                    // One is Z, the other is Q or E.
                    else if (((x1 == 14 || x1 == 4) && y1 == 24) || ((y1 == 14 || y1 == 4) && x1 == 24))
                    {
                        match++;
                    }
                    // One is J, the other is I or L.
                    else if (((x1 == 10 || x1 == 8) && y1 == 25) || ((y1 == 10 || y1 == 8) && x1 == 25))
                    {
                        match++;
                    }
                    // Different letter, and neither is a gap.
                    else if (x1 != y1 && x1 != 0 && y1 != 0)
                    {
                        mismatch++;
                    }

                    // Same letter, no gap.
                    if (x2 == y2 && x2 != 0)
                    {
                        match++;
                    }
                    // One is X, the other is not a gap.
                    else if ((x2 != 0 && y2 == 26) || (x2 == 26 && y2 != 0))
                    {
                        match++;
                    }
                    // One is B, the other is N or D.
                    else if (((x2 == 12 || x2 == 3) && y2 == 23) || ((y2 == 12 || y2 == 3) && x2 == 23))
                    {
                        match++;
                    }
                    // One is Z, the other is Q or E.
                    else if (((x2 == 14 || x2 == 4) && y2 == 24) || ((y2 == 14 || y2 == 4) && x2 == 24))
                    {
                        match++;
                    }
                    // One is J, the other is I or L.
                    else if (((x2 == 10 || x2 == 8) && y2 == 25) || ((y2 == 10 || y2 == 8) && x2 == 25))
                    {
                        match++;
                    }
                    // Different letter, and neither is a gap.
                    else if (x2 != y2 && x2 != 0 && y2 != 0)
                    {
                        mismatch++;
                    }

                    ProteinMatchMatrixJCK83[x][y] = (byte)(match + 4 * mismatch);
                }
            }
        }

        /// <summary>
        /// Builds the match matrix used for the BLOSUM62 evolutionary model.
        /// </summary>
        private static void BuildProteinMatchMatrixBLOSUM62()
        {
            for (int x = 0; x < 784; x++)
            {
                ProteinMatchMatrixBLOSUM62[x] = new byte[784];
                for (int y = 0; y < 784; y++)
                {
                    // Every ushort contains 2 positions. We cache all the possible comparisons between ushorts (i.e., between 2 positions).
                    // Here, we store the score corresponding to these comparisons, as well as the number of non-gap positions. Scores in
                    // the BLOSUM62 matrix range from -4 to 11, thus the comparison between two positions can score from -8 to 22. We translate
                    // this to a value between 0 and 30, which takes up 5 bits. 
                    // The matrix should be small enough that this code doesn't
                    // need to be very optimised.
                    byte x1 = (byte)(x % 28);
                    byte y1 = (byte)(y % 28);

                    byte x2 = (byte)(x / 28);
                    byte y2 = (byte)(y / 28);

                    byte score = (byte)(BLOSUM62[x1, y1] + BLOSUM62[x2, y2] + 8);

                    byte length = 0;

                    // Ignore gaps, U, O and J.
                    if ((x1 != 0 && x1 != 21 && x1 != 22 && x1 != 25) &&
                        (y1 != 0 && y1 != 21 && y1 != 22 && y1 != 25))
                    {
                        length++;
                    }

                    // Ignore gaps, U, O and J.
                    if ((x2 != 0 && x2 != 21 && x2 != 22 && x2 != 25) &&
                        (y2 != 0 && y2 != 21 && y2 != 22 && y2 != 25))
                    {
                        length++;
                    }

                    ProteinMatchMatrixBLOSUM62[x][y] = (byte)(score + (length << 5));
                }
            }
        }

        /// <summary>
        /// Compares two protein sequences using the Hamming distance.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <returns>The (normalised) Hamming distance between the two sequences.</returns>
        private static float CompareProteinSequencesHamming(ushort[] sequence1, ushort[] sequence2)
        {
            int length = sequence1.Length;

            int match = 0;
            int mismatch = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result = ProteinMatchMatrixJCK83[seq1[i]][seq2[i]];

                        match += result & 3;
                        mismatch += (result >> 2) & 3;
                    }
                }
            }

            return (float)mismatch / (match + mismatch);
        }


        /// <summary>
        /// Compares a protein sequence with two other sequences using the JC model of evolution. Faster than
        /// calling <see cref="CompareProteinSequencesHamming(ushort[], ushort[])"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="dist12">When the method returns, this variable will contain the JC distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the JC distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareProteinSequencesHamming(ushort[] sequence1, ushort[] sequence2, ushort[] sequence3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            int match12 = 0;
            int mismatch12 = 0;

            int match13 = 0;
            int mismatch13 = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                fixed (ushort* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int i1 = seq1[i];

                        byte result12 = ProteinMatchMatrixJCK83[i1][seq2[i]];

                        match12 += result12 & 3;
                        mismatch12 += (result12 >> 2) & 3;

                        byte result13 = ProteinMatchMatrixJCK83[i1][seq3[i]];

                        match13 += result13 & 3;
                        mismatch13 += (result13 >> 2) & 3;
                    }
                }
            }

            dist12 = (float)mismatch12 / (match12 + mismatch12);
            dist13 = (float)mismatch13 / (match13 + mismatch13);
        }

        /// <summary>
        /// Computes a distance matrix between protein sequences using the Hamming distance.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixHamming(IReadOnlyList<ushort[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1);
            object progressLock = new object();

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareProteinSequencesHamming(sequences[i], sequences[j], sequences[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareProteinSequencesHamming(sequences[i], sequences[j]);
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
        /// Compares two protein sequences using the JC model of evolution.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <returns>The JC distance between the two sequences.</returns>
        private static float CompareProteinSequencesJC(ushort[] sequence1, ushort[] sequence2)
        {
            int length = sequence1.Length;

            int match = 0;
            int mismatch = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result = ProteinMatchMatrixJCK83[seq1[i]][seq2[i]];

                        match += result & 3;
                        mismatch += (result >> 2) & 3;
                    }
                }
            }

            return (float)(-0.95 * Math.Log(1 - 1.052631578947368 * mismatch / (match + mismatch)));
        }


        /// <summary>
        /// Compares a protein sequence with two other sequences using the JC model of evolution. Faster than
        /// calling <see cref="CompareProteinSequencesJC(ushort[], ushort[])"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="dist12">When the method returns, this variable will contain the JC distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the JC distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareProteinSequencesJC(ushort[] sequence1, ushort[] sequence2, ushort[] sequence3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            int match12 = 0;
            int mismatch12 = 0;

            int match13 = 0;
            int mismatch13 = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                fixed (ushort* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int i1 = seq1[i];

                        byte result12 = ProteinMatchMatrixJCK83[i1][seq2[i]];

                        match12 += result12 & 3;
                        mismatch12 += (result12 >> 2) & 3;

                        byte result13 = ProteinMatchMatrixJCK83[i1][seq3[i]];

                        match13 += result13 & 3;
                        mismatch13 += (result13 >> 2) & 3;
                    }
                }
            }

            dist12 = (float)(-0.95 * Math.Log(1 - 1.052631578947368 * mismatch12 / (match12 + mismatch12)));
            dist13 = (float)(-0.95 * Math.Log(1 - 1.052631578947368 * mismatch13 / (match13 + mismatch13)));
        }

        /// <summary>
        /// Computes a distance matrix between protein sequences using the JC model of evolution.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixJC(IReadOnlyList<ushort[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1);
            object progressLock = new object();

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareProteinSequencesJC(sequences[i], sequences[j], sequences[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareProteinSequencesJC(sequences[i], sequences[j]);
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
        /// Compares two protein sequences using the K83 formula approximating the Dayhoff model of evolution.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <returns>The K83 distance between the two sequences.</returns>
        private static float CompareProteinSequencesK83(ushort[] sequence1, ushort[] sequence2)
        {
            int length = sequence1.Length;

            int match = 0;
            int mismatch = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result = ProteinMatchMatrixJCK83[seq1[i]][seq2[i]];

                        match += result & 3;
                        mismatch += (result >> 2) & 3;
                    }
                }
            }

            double dist = (double)mismatch / (match + mismatch);

            if (dist < 0.75)
            {
                return (float)-Math.Log(1 - dist - 0.2 * dist * dist);
            }
            else if (dist <= 0.93)
            {
                return DayhoffPAMs[(int)((dist * 1000) - 750)] * 0.01f;
            }
            else
            {
                return 10;
            }
        }


        /// <summary>
        /// Compares a protein sequence with two other sequences using the K83 formula approximating the Dayhoff model of evolution. Faster than
        /// calling <see cref="CompareProteinSequencesK83(ushort[], ushort[])"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="dist12">When the method returns, this variable will contain the K83 distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the K83 distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareProteinSequencesK83(ushort[] sequence1, ushort[] sequence2, ushort[] sequence3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            int match12 = 0;
            int mismatch12 = 0;

            int match13 = 0;
            int mismatch13 = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                fixed (ushort* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        int i1 = seq1[i];

                        byte result12 = ProteinMatchMatrixJCK83[i1][seq2[i]];

                        match12 += result12 & 3;
                        mismatch12 += (result12 >> 2) & 3;

                        byte result13 = ProteinMatchMatrixJCK83[i1][seq3[i]];

                        match13 += result13 & 3;
                        mismatch13 += (result13 >> 2) & 3;
                    }
                }
            }

            double tmpDist12 = (double)mismatch12 / (match12 + mismatch12);

            if (tmpDist12 < 0.75)
            {
                dist12 = (float)-Math.Log(1 - tmpDist12 - 0.2 * tmpDist12 * tmpDist12);
            }
            else if (tmpDist12 <= 0.93)
            {
                dist12 = DayhoffPAMs[(int)((tmpDist12 * 1000) - 750)] * 0.01f;
            }
            else
            {
                dist12 = 10;
            }

            double tmpDist13 = (double)mismatch13 / (match13 + mismatch13);

            if (tmpDist13 < 0.75)
            {
                dist13 = (float)-Math.Log(1 - tmpDist13 - 0.2 * tmpDist13 * tmpDist13);
            }
            else if (tmpDist13 <= 0.93)
            {
                dist13 = DayhoffPAMs[(int)((tmpDist13 * 1000) - 750)] * 0.01f;
            }
            else
            {
                dist13 = 10;
            }
        }

        /// <summary>
        /// Computes a distance matrix between protein sequences using the K83 formula approximating the Dayhoff model of evolution.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixK83(IReadOnlyList<ushort[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1);
            object progressLock = new object();

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareProteinSequencesK83(sequences[i], sequences[j], sequences[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareProteinSequencesK83(sequences[i], sequences[j]);
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
        /// Computes the BLOSUM62 score of a sequence with itself.
        /// </summary>
        /// <param name="sequence">The sequence whose score will be computed.</param>
        /// <returns>The BLOSUM62 score of a sequence with itself.</returns>
        private static int SelfScore(ushort[] sequence)
        {
            int length = sequence.Length;
            int score = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence)
                {
                    for (int i = 0; i < length; i++)
                    {
                        score += (ProteinMatchMatrixBLOSUM62[seq1[i]][seq1[i]] & 31) - 8;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// Compares two protein sequences using the Scoredist correction with the BLOSUM62 matrix.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="selfScore1">The score obtained by comparing <paramref name="sequence1"/> with itself.</param>
        /// <param name="selfScore2">The score obtained by comparing <paramref name="sequence2"/> with itself.</param>
        /// <returns>The distance between the two sequences.</returns>
        public static float CompareProteinSequencesBLOSUM62(ushort[] sequence1, ushort[] sequence2, int selfScore1, int selfScore2)
        {
            int length = sequence1.Length;

            int score = 0;
            int seqLength = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result = ProteinMatchMatrixBLOSUM62[seq1[i]][seq2[i]];
                        score += (result & 31) - 8;
                        seqLength += result >> 5;
                    }
                }
            }

            double expectedScore = seqLength * -0.5209;

            double sigmaN = score - expectedScore;

            if (sigmaN <= 0)
            {
                return 300;
            }

            double sigmaUN = (selfScore1 + selfScore2) * 0.5 - expectedScore;

            return Math.Min(300, (float)(-Math.Log(sigmaN / sigmaUN) * 133.70));
        }

        /// <summary>
        /// Compares a protein sequence with two other sequences using the Scoredist correction with the BLOSUM62 matrix. Faster than
        /// calling <see cref="CompareProteinSequencesBLOSUM62(ushort[], ushort[], int, int)"/> twice.
        /// </summary>
        /// <param name="sequence1">The first sequence.</param>
        /// <param name="sequence2">The second sequence.</param>
        /// <param name="sequence3">The third sequence.</param>
        /// <param name="selfScore1">The score obtained by comparing <paramref name="sequence1"/> with itself.</param>
        /// <param name="selfScore2">The score obtained by comparing <paramref name="sequence2"/> with itself.</param>
        /// <param name="selfScore3">The score obtained by comparing <paramref name="sequence3"/> with itself.</param>
        /// <param name="dist12">When the method returns, this variable will contain the distance between <paramref name="sequence1"/> and <paramref name="sequence2"/>.</param>
        /// <param name="dist13">When the method returns, this variable will contain the distance between <paramref name="sequence1"/> and <paramref name="sequence3"/>.</param>
        private static void CompareProteinSequencesBLOSUM62(ushort[] sequence1, ushort[] sequence2, ushort[] sequence3, int selfScore1, int selfScore2, int selfScore3, out float dist12, out float dist13)
        {
            int length = sequence1.Length;

            int score12 = 0;
            int score13 = 0;

            int seqLength12 = 0;
            int seqLength13 = 0;

            unsafe
            {
                fixed (ushort* seq1 = sequence1)
                fixed (ushort* seq2 = sequence2)
                fixed (ushort* seq3 = sequence3)
                {
                    for (int i = 0; i < length; i++)
                    {
                        byte result12 = ProteinMatchMatrixBLOSUM62[seq1[i]][seq2[i]];
                        byte result13 = ProteinMatchMatrixBLOSUM62[seq1[i]][seq3[i]];

                        score12 += (result12 & 31) - 8;
                        score13 += (result13 & 31) - 8;

                        seqLength12 += result12 >> 5;
                        seqLength13 += result13 >> 5;
                    }
                }
            }

            double expectedScore12 = seqLength12 * -0.5209;
            double expectedScore13 = seqLength13 * -0.5209;

            double sigmaN12 = score12 - expectedScore12;

            if (sigmaN12 <= 0)
            {
                dist12 = 300;
            }
            else
            {
                double sigmaUN12 = (selfScore1 + selfScore2) * 0.5 - expectedScore12;
                dist12 = Math.Min(300, (float)(-Math.Log(sigmaN12 / sigmaUN12) * 133.70));
            }

            double sigmaN13 = score13 - expectedScore13;

            if (sigmaN13 <= 0)
            {
                dist13 = 300;
            }
            else
            {
                double sigmaUN13 = (selfScore1 + selfScore3) * 0.5 - expectedScore13;
                dist13 = Math.Min(300, (float)(-Math.Log(sigmaN13 / sigmaUN13) * 133.70));
            }
        }

        /// <summary>
        /// Computes a distance matrix between protein sequences using the Scoredist correction with the BLOSUM62 matrix.
        /// </summary>
        /// <param name="sequences">The sequences whose distance matrix will be computed.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <param name="matrix">A pre-allocated lower triangular <see cref="T:float[][]"/> jagged array matrix that will contain the distances between the sequences.</param>
        private static void ComputeDistanceMatrixBLOSUM62(IReadOnlyList<ushort[]> sequences, int numCores, Action<double> progressCallback, float[][] matrix)
        {
            int[] selfScores = new int[sequences.Count];

            long progress = 0;
            double total = sequences.Count * 0.5 * (sequences.Count - 1) + sequences.Count;
            object progressLock = new object();

            Parallel.For(0, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                selfScores[i] = SelfScore(sequences[i]);
            });

            if (progressCallback != null)
            {
                lock (progressLock)
                {
                    progress += sequences.Count;
                    progressCallback((double)progress / total);
                }
            }

            Parallel.For(1, sequences.Count, new ParallelOptions() { MaxDegreeOfParallelism = numCores }, i =>
            {
                for (int j = 0; j < i - 1; j += 2)
                {
                    CompareProteinSequencesBLOSUM62(sequences[i], sequences[j], sequences[j + 1], selfScores[i], selfScores[j], selfScores[j + 1], out matrix[i][j], out matrix[i][j + 1]);
                }

                for (int j = i - 1; j < i; j++)
                {
                    matrix[i][j] = CompareProteinSequencesBLOSUM62(sequences[i], sequences[j], selfScores[i], selfScores[j]);
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

