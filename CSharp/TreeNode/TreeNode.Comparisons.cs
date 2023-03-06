using PhyloTree.Extensions;
using PhyloTree.Formats;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PhyloTree
{
    public partial class TreeNode
    {
        /// <summary>
        /// Computes the Robinson-Foulds distance between the current tree and another tree.
        /// </summary>
        /// <param name="otherTree">The other tree whose distance to the current tree is computed.</param>
        /// <param name="weighted">If this is <see langword="true" />, the distance is weighted based on the branch lengths; otherwise, it is unweighted.</param>
        /// <returns>The Robinson-Foulds distance between this tree and the <paramref name="otherTree"/>.</returns>
        public double RobinsonFouldsDistance(TreeNode otherTree, bool weighted)
        {
            return RobinsonFouldsDistance(this, otherTree, weighted);
        }

        /// <summary>
        /// Computes the Robinson-Foulds distance between two trees.
        /// </summary>
        /// <param name="tree1">The first tree.</param>
        /// <param name="tree2">The second tree.</param>
        /// <param name="weighted">If this is <see langword="true" />, the distance is weighted based on the branch lengths; otherwise, it is unweighted.</param>
        /// <returns>The Robinson-Foulds distance between <paramref name="tree1"/> and <paramref name="tree2"/>.</returns>
        public static double RobinsonFouldsDistance(TreeNode tree1, TreeNode tree2, bool weighted)
        {
            if (tree1 == null)
            {
                throw new ArgumentNullException(nameof(tree1), "The first tree cannot be null!");
            }

            if (tree2 == null)
            {
                throw new ArgumentNullException(nameof(tree2), "The second tree cannot be null!");
            }

            double[,] distMat = new double[2, 2];

            if (weighted)
            {
                FillDistanceMatrix(new TreeNode[] { tree1, tree2 }, wRFDistances: distMat, maxThreadCount: 1);
            }
            else
            {
                FillDistanceMatrix(new TreeNode[] { tree1, tree2 }, RFDistances: distMat, maxThreadCount: 1);
            }

            return distMat[0, 1];
        }

        /// <summary>
        /// Computes the edge-length distance between the current tree and another tree.
        /// </summary>
        /// <param name="otherTree">The other tree whose distance to the current tree is computed.</param>
        /// <returns>The edge-length distance between this tree and the <paramref name="otherTree"/>.</returns>
        public double EdgeLengthDistance(TreeNode otherTree)
        {
            return EdgeLengthDistance(this, otherTree);
        }

        /// <summary>
        /// Computes the edge-length distance between two trees.
        /// </summary>
        /// <param name="tree1">The first tree.</param>
        /// <param name="tree2">The second tree.</param>
        /// <returns>The edge-length distance between <paramref name="tree1"/> and <paramref name="tree2"/>.</returns>
        public static double EdgeLengthDistance(TreeNode tree1, TreeNode tree2)
        {
            if (tree1 == null)
            {
                throw new ArgumentNullException(nameof(tree1), "The first tree cannot be null!");
            }

            if (tree2 == null)
            {
                throw new ArgumentNullException(nameof(tree2), "The second tree cannot be null!");
            }

            double[,] distMat = new double[2, 2];

            FillDistanceMatrix(new TreeNode[] { tree1, tree2 }, ELDistances: distMat, maxThreadCount: 1);
            return distMat[0, 1];
        }

        /// <summary>
        /// Defines ways of pruning trees during comparisons.
        /// </summary>
        public enum TreeComparisonPruningMode
        {
            /// <summary>
            /// Specifies that the subset of leaves shared between each pair of trees should be used.
            /// </summary>
            Pairwise,

            /// <summary>
            /// Specifies that the subset of leaves common to all trees should be used.
            /// </summary>
            Global
        }

        /// <summary>
        /// Computes a distance matrix containing the Robinson-Foulds distances between each pair of trees in a list.
        /// </summary>
        /// <param name="trees">The list of trees that should be compared.</param>
        /// <param name="weighted">If this is <see langword="true" />, the distance is weighted based on the branch lengths; otherwise, it is unweighted.</param>
        /// <param name="pruningMode">If this is <see cref="TreeComparisonPruningMode.Global"/>, all trees are pruned so that they only contain the subset of leaves that are present in all trees. If this is <see cref="TreeComparisonPruningMode.Pairwise"/>, during each comparisons the two trees are pruned so that they contain the subset of leaves that are in common between both of them.</param>
        /// <param name="maxThreadCount">The maximum number of threads to use for parallelised steps.</param>
        /// <param name="progress">An <see cref="IProgress{T}"/> for progress reporting.</param>
        /// <returns>A square <see langword="double"/>[,] matrix containing the requested distances between the trees.</returns>
        public static double[,] RobinsonFouldsDistances(IReadOnlyList<TreeNode> trees, bool weighted, TreeComparisonPruningMode pruningMode = TreeComparisonPruningMode.Pairwise, int maxThreadCount = -1, IProgress<double> progress = null)
        {
            double[,] distMat = new double[trees.Count, trees.Count];

            if (weighted)
            {
                FillDistanceMatrix(trees, comparePairwise: pruningMode == TreeComparisonPruningMode.Pairwise, wRFDistances: distMat, maxThreadCount: maxThreadCount, progress: progress);
            }
            else
            {
                FillDistanceMatrix(trees, comparePairwise: pruningMode == TreeComparisonPruningMode.Pairwise, RFDistances: distMat, maxThreadCount: maxThreadCount, progress: progress);
            }

            return distMat;
        }

        /// <summary>
        /// Computes a distance matrix containing the edge-length distances between each pair of trees in a list.
        /// </summary>
        /// <param name="trees">The list of trees that should be compared.</param>
        /// <param name="pruningMode">If this is <see cref="TreeComparisonPruningMode.Global"/>, all trees are pruned so that they only contain the subset of leaves that are present in all trees. If this is <see cref="TreeComparisonPruningMode.Pairwise"/>, during each comparisons the two trees are pruned so that they contain the subset of leaves that are in common between both of them.</param>
        /// <param name="maxThreadCount">The maximum number of threads to use for parallelised steps.</param>
        /// <param name="progress">An <see cref="IProgress{T}"/> for progress reporting.</param>
        /// <returns>A square <see langword="double"/>[,] matrix containing the requested distances between the trees.</returns>
        public static double[,] EdgeLengthDistances(IReadOnlyList<TreeNode> trees, TreeComparisonPruningMode pruningMode = TreeComparisonPruningMode.Pairwise, int maxThreadCount = -1, IProgress<double> progress = null)
        {
            double[,] distMat = new double[trees.Count, trees.Count];

            FillDistanceMatrix(trees, comparePairwise: pruningMode == TreeComparisonPruningMode.Pairwise, ELDistances: distMat, maxThreadCount: maxThreadCount, progress: progress);

            return distMat;
        }

        /// <summary>
        /// Computes two distance matrices containing the unweighted and weighted Robinson-Foulds distances between each pair of trees in a list. Much faster than computing the two distance matrices separately.
        /// </summary>
        /// <param name="trees">The list of trees that should be compared.</param>
        /// <param name="RFDistances">When this method returns, this variable will contain the computed Robinson-Foulds distances between the trees.</param>
        /// <param name="weightedRFDistances">When this method returns, this variable will contain the computed weighted Robinson-Foulds distances between the trees.</param>
        /// <param name="pruningMode">If this is <see cref="TreeComparisonPruningMode.Global"/>, all trees are pruned so that they only contain the subset of leaves that are present in all trees. If this is <see cref="TreeComparisonPruningMode.Pairwise"/>, during each comparisons the two trees are pruned so that they contain the subset of leaves that are in common between both of them.</param>
        /// <param name="maxThreadCount">The maximum number of threads to use for parallelised steps.</param>
        /// <param name="progress">An <see cref="IProgress{T}"/> for progress reporting.</param>
        /// <returns>A square <see langword="double"/>[,] matrix containing the requested distances between the trees.</returns>
        public static void RobinsonFouldsDistances(IReadOnlyList<TreeNode> trees, out double[,] RFDistances, out double[,] weightedRFDistances, TreeComparisonPruningMode pruningMode = TreeComparisonPruningMode.Pairwise, int maxThreadCount = -1, IProgress<double> progress = null)
        {
            RFDistances = new double[trees.Count, trees.Count];
            weightedRFDistances = new double[trees.Count, trees.Count];

            FillDistanceMatrix(trees, comparePairwise: pruningMode == TreeComparisonPruningMode.Pairwise, RFDistances: RFDistances, wRFDistances: weightedRFDistances, maxThreadCount: maxThreadCount, progress: progress);
        }

        /// <summary>
        /// Computes three distance matrices containing the unweighted and weighted Robinson-Foulds distances and the edge-length distances between each pair of trees in a list. Much faster than computing the three distance matrices separately.
        /// </summary>
        /// <param name="trees">The list of trees that should be compared.</param>
        /// <param name="RFDistances">When this method returns, this variable will contain the computed Robinson-Foulds distances between the trees.</param>
        /// <param name="weightedRFDistances">When this method returns, this variable will contain the computed weighted Robinson-Foulds distances between the trees.</param>
        /// <param name="ELDistances">When this method returns, this variable will contain the computed edge-length distances between the trees.</param>
        /// <param name="pruningMode">If this is <see cref="TreeComparisonPruningMode.Global"/>, all trees are pruned so that they only contain the subset of leaves that are present in all trees. If this is <see cref="TreeComparisonPruningMode.Pairwise"/>, during each comparisons the two trees are pruned so that they contain the subset of leaves that are in common between both of them.</param>
        /// <param name="maxThreadCount">The maximum number of threads to use for parallelised steps.</param>
        /// <param name="progress">An <see cref="IProgress{T}"/> for progress reporting.</param>
        /// <returns>A square <see langword="double"/>[,] matrix containing the requested distances between the trees.</returns>
        public static void TreeDistances(IReadOnlyList<TreeNode> trees, out double[,] RFDistances, out double[,] weightedRFDistances, out double[,] ELDistances, TreeComparisonPruningMode pruningMode = TreeComparisonPruningMode.Pairwise, int maxThreadCount = -1, IProgress<double> progress = null)
        {
            RFDistances = new double[trees.Count, trees.Count];
            weightedRFDistances = new double[trees.Count, trees.Count];
            ELDistances = new double[trees.Count, trees.Count];

            FillDistanceMatrix(trees, comparePairwise: pruningMode == TreeComparisonPruningMode.Pairwise, RFDistances: RFDistances, wRFDistances: weightedRFDistances, ELDistances: ELDistances, maxThreadCount: maxThreadCount, progress: progress);
        }


        /// <summary>
        /// Fills distance matrices with the Robinson-Foulds and weighted Robinson-Foulds distances between the trees.
        /// </summary>
        /// <param name="trees">The trees to be compared.</param>
        /// <param name="comparePairwise">If this is <see langword="false"/>, only leaves that are in common to all trees are used. If this is <see langword="true" />, for each pair of trees, the leaves that are in common between them are used.</param>
        /// <param name="RFDistances">The matrix to be filled with Robinson-Foulds distances, or <see langword="null"/>.</param>
        /// <param name="wRFDistances">The matrix to be filled with weighted Robinson-Foulds distances, or <see langword="null"/>.</param>
        /// <param name="maxThreadCount">The maximum number of threads to use for parallelised steps.</param>
        /// <param name="progress">An <see cref="IProgress{T}"/> for progress reporting.</param>
        /// <exception cref="ArgumentException">Thrown if at least one of the trees has a tip without a name.</exception>
        private static void FillDistanceMatrix(IReadOnlyList<TreeNode> trees, bool comparePairwise = true, double[,] RFDistances = null, double[,] wRFDistances = null, double[,] ELDistances = null, int maxThreadCount = -1, IProgress<double> progress = null)
        {
            if (maxThreadCount <= 0)
            {
                maxThreadCount = Environment.ProcessorCount;
            }

            Dictionary<string, int> leaves = new Dictionary<string, int>();
            List<int> splitCounts = new List<int>(trees.Count);

            List<double>[] splitLengths = new List<double>[trees.Count];

            int maxSplitCount = 0;
            int totalSplitCounts = 0;

            for (int i = 0; i < trees.Count; i++)
            {
                TreeNode t = trees[i];
                splitLengths[i] = new List<double>();
                int splitCount = 0;
                foreach (TreeNode node in t.GetChildrenRecursiveLazy())
                {
                    if (node.Children.Count == 0)
                    {
                        if (!string.IsNullOrEmpty(node.Name))
                        {
                            leaves[node.Name] = 0;
                        }
                        else
                        {
                            throw new ArgumentException("At least one of the trees contains a tip without a Name!");
                        }
                    }
                    else
                    {
                        splitCount++;
                        splitLengths[i].Add(node.Length);
                    }
                }
                splitCounts.Add(splitCount);
                maxSplitCount = Math.Max(splitCount, maxSplitCount);
                totalSplitCounts += splitCount;
            }

            List<string> leafNames = new List<string>(leaves.Count);

            foreach (KeyValuePair<string, int> kvp in leaves)
            {
                leafNames.Add(kvp.Key);
                leaves[kvp.Key] = leafNames.Count - 1;
            }

            int[] splitOffsets = new int[splitCounts.Count];

            for (int i = 1; i < splitCounts.Count; i++)
            {
                splitOffsets[i] = splitOffsets[i - 1] + splitCounts[i - 1];
            }

            double[][] leafLengths = new double[trees.Count][];

            // Each split consists of 1 bits for each leaf, determining whether the leaf is on one side of the split (0) or the other (1).
            int splitSize = (int)Math.Ceiling(leaves.Count / 8.0);

            // Array used to store whether a certain split has already been analysed or not.
            int foundsSize = (int)Math.Ceiling(maxSplitCount / 8.0);

            // Each tree mask is the same size as a single split.
            int totalMemoryNeeded = splitSize * (totalSplitCounts + trees.Count + maxThreadCount + leaves.Count) + foundsSize * maxThreadCount;

            IntPtr memoryIntPtr = Marshal.AllocHGlobal(totalMemoryNeeded);

            try
            {
                IntPtr comparisonMasksIntPtr = memoryIntPtr;
                IntPtr foundsIntPtr = IntPtr.Add(comparisonMasksIntPtr, splitSize * maxThreadCount);
                IntPtr masksIntPtr = IntPtr.Add(foundsIntPtr, foundsSize * maxThreadCount);
                IntPtr splitsIntPtr = IntPtr.Add(masksIntPtr, splitSize * trees.Count);
                IntPtr leafSplitsIntPtr = IntPtr.Add(splitsIntPtr, splitSize * totalSplitCounts);

                unsafe
                {
                    Unsafe.InitBlockUnaligned((byte*)memoryIntPtr, 0, (uint)totalMemoryNeeded);

                    byte* masks = (byte*)masksIntPtr;
                    byte* splits = (byte*)splitsIntPtr;
                    byte* leafSplits = (byte*)leafSplitsIntPtr;

                    for (int i = 0; i < leaves.Count; i++)
                    {
                        leafSplits[splitSize * i + i / 8] |= (byte)(0b1 << (i % 8));
                    }

                    for (int i = 0; i < trees.Count; i++)
                    {
                        leafLengths[i] = new double[leaves.Count];

                        byte* currMask = masks + i * splitSize;
                        byte* currSplits = splits + splitSize * splitOffsets[i];

                        foreach (TreeNode node in trees[i].GetChildrenRecursiveLazy())
                        {
                            if (node.Children.Count == 0)
                            {
                                int index = leaves[node.Name];

                                int byteIndex = index / 8;
                                int bitIndex = index % 8;

                                currMask[byteIndex] |= (byte)(0b1 << bitIndex);

                                leafLengths[i][index] = node.Length;
                            }
                        }

                        int currSplitIndex = 0;

                        foreach (TreeNode node in trees[i].GetChildrenRecursiveLazy())
                        {
                            if (node.Children.Count > 0)
                            {
                                foreach (TreeNode n in node.GetChildrenRecursiveLazy())
                                {
                                    if (n.Children.Count == 0)
                                    {
                                        int index = leaves[n.Name];

                                        int byteIndex = currSplitIndex * splitSize + index / 8;
                                        int bitIndex = index % 8;

                                        currSplits[byteIndex] |= (byte)(0b1 << bitIndex);
                                    }
                                }

                                currSplitIndex++;
                            }
                        }
                    }

                    int progressCount = 0;
                    object progressLock = new object();

                    ThreadIndexer threadIndexer = new ThreadIndexer(maxThreadCount);

                    byte* comparisonMasks = (byte*)comparisonMasksIntPtr;
                    byte* founds = (byte*)foundsIntPtr;

                    if (!comparePairwise)
                    {
                        Unsafe.InitBlockUnaligned(comparisonMasks, 0b11111111, (uint)splitSize);

                        for (int i = 0; i < trees.Count; i++)
                        {
                            for (int j = 0; j < splitSize; j++)
                            {
                                comparisonMasks[j] &= masks[i * splitSize + j];
                            }
                        }
                    }

                    int comparisonCounts = trees.Count * (trees.Count - 1) / 2;

                    (int, int) getIndices(int index)
                    {
                        int i = trees.Count - 2 - (int)Math.Floor(Math.Sqrt(-8 * index + 4 * trees.Count * (trees.Count - 1) - 7) / 2 - 0.5);
                        int j = index + i + 1 - trees.Count * (trees.Count - 1) / 2 + (trees.Count - i) * (trees.Count - i - 1) / 2;

                        return (i, j);
                    };

                    HashSet<SplitPointer>[] alreadyCheckedSplits1 = new HashSet<SplitPointer>[maxThreadCount];
                    HashSet<SplitPointer>[] alreadyCheckedSplits2 = new HashSet<SplitPointer>[maxThreadCount];

                    Dictionary<SplitPointer, double>[] splitLengths1 = null;
                    Dictionary<SplitPointer, double>[] splitLengths2 = null;

                    if (ELDistances != null)
                    {
                        splitLengths1 = new Dictionary<SplitPointer, double>[maxThreadCount];
                        splitLengths2 = new Dictionary<SplitPointer, double>[maxThreadCount];
                    }

                    for (int i = 0; i < maxThreadCount; i++)
                    {
                        alreadyCheckedSplits1[i] = new HashSet<SplitPointer>(maxSplitCount);
                        alreadyCheckedSplits2[i] = new HashSet<SplitPointer>(maxSplitCount);
                        splitLengths1[i] = new Dictionary<SplitPointer, double>(maxSplitCount);
                        splitLengths2[i] = new Dictionary<SplitPointer, double>(maxSplitCount);
                    }

                    Parallel.For(0, comparisonCounts, new ParallelOptions() { MaxDegreeOfParallelism = maxThreadCount }, index =>
                    {
                        int threadIndex = threadIndexer.GetIndex();

                        (int i, int j) = getIndices(index);

                        int dist = 0;
                        double wDist = 0;

                        byte* currMask;

                        if (!comparePairwise)
                        {
                            currMask = comparisonMasks;
                        }
                        else
                        {
                            currMask = comparisonMasks + threadIndex * splitSize;

                            Unsafe.CopyBlockUnaligned(currMask, masks + i * splitSize, (uint)splitSize);

                            for (int k = 0; k < splitSize; k++)
                            {
                                currMask[k] &= masks[j * splitSize + k];
                            }
                        }

                        byte* currFounds = founds + foundsSize * threadIndex;

                        Unsafe.InitBlockUnaligned(currFounds, 0, (uint)foundsSize);

                        alreadyCheckedSplits1[threadIndex].Clear();
                        alreadyCheckedSplits2[threadIndex].Clear();

                        if (RFDistances != null || wRFDistances != null)
                        {
                            for (int k = 0; k < splitCounts[i]; k++)
                            {
                                if (CheckIfMoreThanOne(splits + (splitOffsets[i] + k) * splitSize, currMask, splitSize))
                                {
                                    if (RFDistances != null)
                                    {
                                        SplitPointer split = new SplitPointer(splits + (splitOffsets[i] + k) * splitSize, currMask, splitSize);

                                        if (alreadyCheckedSplits1[threadIndex].Add(split))
                                        {
                                            bool found = false;

                                            for (int l = 0; l < splitCounts[j]; l++)
                                            {
                                                if (CompareSplits(splits + (splitOffsets[i] + k) * splitSize, splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize))
                                                {
                                                    found = true;
                                                    currFounds[l / 8] |= (byte)(0b1 << (l % 8));
                                                    alreadyCheckedSplits2[threadIndex].Add(new SplitPointer(splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize));
                                                    break;
                                                }
                                            }

                                            if (!found)
                                            {
                                                dist++;
                                                wDist += splitLengths[i][k];
                                            }
                                        }
                                        else if (wRFDistances != null)
                                        {
                                            bool found = false;

                                            for (int l = 0; l < splitCounts[j]; l++)
                                            {
                                                if (CompareSplits(splits + (splitOffsets[i] + k) * splitSize, splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize))
                                                {
                                                    found = true;
                                                    currFounds[l / 8] |= (byte)(0b1 << (l % 8));
                                                    break;
                                                }
                                            }

                                            if (!found)
                                            {
                                                wDist += splitLengths[i][k];
                                            }
                                        }
                                    }
                                    else if (wRFDistances != null)
                                    {
                                        bool found = false;

                                        for (int l = 0; l < splitCounts[j]; l++)
                                        {
                                            if (CompareSplits(splits + (splitOffsets[i] + k) * splitSize, splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize))
                                            {
                                                found = true;
                                                currFounds[l / 8] |= (byte)(0b1 << (l % 8));
                                                break;
                                            }
                                        }

                                        if (!found)
                                        {
                                            wDist += splitLengths[i][k];
                                        }
                                    }
                                }
                            }

                            for (int l = 0; l < splitCounts[j]; l++)
                            {
                                if ((currFounds[l / 8] & (byte)(0b1 << (l % 8))) == 0)
                                {
                                    if (CheckIfMoreThanOne(splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize))
                                    {
                                        if (RFDistances != null)
                                        {
                                            SplitPointer split = new SplitPointer(splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize);

                                            if (alreadyCheckedSplits2[threadIndex].Add(split))
                                            {
                                                bool found = false;

                                                for (int k = 0; k < splitCounts[i]; k++)
                                                {
                                                    if (CompareSplits(splits + (splitOffsets[i] + k) * splitSize, splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize))
                                                    {
                                                        found = true;
                                                        break;
                                                    }
                                                }

                                                if (!found)
                                                {
                                                    dist++;
                                                    wDist += splitLengths[j][l];
                                                }
                                            }
                                            else if (wRFDistances != null)
                                            {
                                                bool found = false;

                                                for (int k = 0; k < splitCounts[i]; k++)
                                                {
                                                    if (CompareSplits(splits + (splitOffsets[i] + k) * splitSize, splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize))
                                                    {
                                                        found = true;
                                                        break;
                                                    }
                                                }

                                                if (!found)
                                                {
                                                    wDist += splitLengths[j][l];
                                                }
                                            }
                                        }
                                        else if (wRFDistances != null)
                                        {
                                            bool found = false;

                                            for (int k = 0; k < splitCounts[i]; k++)
                                            {
                                                if (CompareSplits(splits + (splitOffsets[i] + k) * splitSize, splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize))
                                                {
                                                    found = true;
                                                    break;
                                                }
                                            }

                                            if (!found)
                                            {
                                                wDist += splitLengths[j][l];
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (ELDistances != null)
                        {
                            splitLengths1[threadIndex].Clear();
                            splitLengths2[threadIndex].Clear();

                            for (int k = 0; k < splitCounts[i]; k++)
                            {
                                SplitPointer split = new SplitPointer(splits + (splitOffsets[i] + k) * splitSize, currMask, splitSize);

                                if (splitLengths1[threadIndex].TryGetValue(split, out double val))
                                {
                                    splitLengths1[threadIndex][split] = val + splitLengths[i][k];
                                }
                                else
                                {
                                    splitLengths1[threadIndex][split] = splitLengths[i][k];
                                }
                            }

                            for (int l = 0; l < splitCounts[j]; l++)
                            {
                                SplitPointer split = new SplitPointer(splits + (splitOffsets[j] + l) * splitSize, currMask, splitSize);

                                if (splitLengths2[threadIndex].TryGetValue(split, out double val))
                                {
                                    splitLengths2[threadIndex][split] = val + splitLengths[j][l];
                                }
                                else
                                {
                                    splitLengths2[threadIndex][split] = splitLengths[j][l];
                                }
                            }

                            for (int k = 0; k < leaves.Count; k++)
                            {
                                if ((currMask[k / 8] & (byte)(0b1 << (k % 8))) != 0 && !double.IsNaN(leafLengths[i][k]) && !double.IsNaN(leafLengths[j][k]))
                                {
                                    SplitPointer split = new SplitPointer(leafSplits + k * splitSize, currMask, splitSize);

                                    if (splitLengths1[threadIndex].TryGetValue(split, out double val))
                                    {
                                        splitLengths1[threadIndex][split] = val + leafLengths[i][k];
                                    }
                                    else
                                    {
                                        splitLengths1[threadIndex][split] = leafLengths[i][k];
                                    }

                                    if (splitLengths2[threadIndex].TryGetValue(split, out double val2))
                                    {
                                        splitLengths2[threadIndex][split] = val2 + leafLengths[j][k];
                                    }
                                    else
                                    {
                                        splitLengths2[threadIndex][split] = leafLengths[j][k];
                                    }
                                }
                            }

                            double totDist = 0;

                            foreach (KeyValuePair<SplitPointer, double> kvp in splitLengths1[threadIndex])
                            {
                                if (!double.IsNaN(kvp.Value) && splitLengths2[threadIndex].TryGetValue(kvp.Key, out double val) && !double.IsNaN(val))
                                {
                                    totDist += (kvp.Value - val) * (kvp.Value - val);
                                }
                            }

                            ELDistances[i, j] = Math.Sqrt(totDist);
                            ELDistances[j, i] = ELDistances[i, j];
                        }

                        if (RFDistances != null)
                        {
                            RFDistances[i, j] = dist;
                            RFDistances[j, i] = dist;
                        }

                        if (wRFDistances != null)
                        {
                            wRFDistances[i, j] = wDist;
                            wRFDistances[j, i] = wDist;
                        }

                        threadIndexer.ReturnIndex(threadIndex);

                        if (progress != null)
                        {
                            lock (progressLock)
                            {
                                progressCount++;

                                double currProg = (double)progressCount / comparisonCounts;

                                _ = Task.Run(() => progress?.Report(currProg));
                            }
                        }
                    });
                }
            }
            finally
            {
                Marshal.FreeHGlobal(memoryIntPtr);
            }
        }

        /// <summary>
        /// Compares two splits to determine whether they are actually the same split. This is not the same as the splits being compatible.
        /// </summary>
        /// <param name="split1">A pointer to the first byte of the first split.</param>
        /// <param name="split2">A pointer to the first byte of the first split.</param>
        /// <param name="mask">A pointer to the first byte of the mask.</param>
        /// <param name="splitSize">The size in bytes of the splits and mask.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe static bool CompareSplits(byte* split1, byte* split2, byte* mask, int splitSize)
        {
            bool simpleEqual = true;
            bool xorEqual = true;

            for (int i = 0; i < splitSize; i++)
            {
                byte split1El = (byte)(split1[i] & mask[i]);
                byte split2El = (byte)(split2[i] & mask[i]);

                if (split1El != split2El)
                {
                    simpleEqual = false;
                }

                if ((split1El ^ split2El) != mask[i])
                {
                    xorEqual = false;
                }

                if (!simpleEqual && !xorEqual)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Checks whether a <paramref name="split"/> contains more than one taxa on each side, when masked with the specified <paramref name="mask"/>.
        /// </summary>
        /// <param name="split">A pointer to the first byte in the split.</param>
        /// <param name="mask">A pointer to the first byte in the mask.</param>
        /// <param name="splitSize">The size of the split and mask in bytes.</param>
        /// <returns><see langword="true" /> if the <paramref name="split"/> contains at least two taxa on each side.</returns>
        private unsafe static bool CheckIfMoreThanOne(byte* split, byte* mask, int splitSize)
        {
            bool step1 = false;

            {
                bool oneFound = false;

                for (int i = 0; i < splitSize; i++)
                {
                    byte b = (byte)(split[i] & mask[i]);

                    if (b != 0)
                    {
                        if ((b & (b - 1)) == 0)
                        {
                            if (oneFound)
                            {
                                step1 = true;
                                break;
                            }
                            else
                            {
                                oneFound = true;
                            }
                        }
                        else
                        {
                            step1 = true;
                            break;
                        }
                    }
                }
            }

            if (!step1)
            {
                return false;
            }
            else
            {
                bool step2 = false;

                bool oneFound = false;

                for (int i = 0; i < splitSize; i++)
                {
                    byte b = (byte)(~split[i] & mask[i]);

                    if (b != 0)
                    {
                        if ((b & (b - 1)) == 0)
                        {
                            if (oneFound)
                            {
                                step2 = true;
                                break;
                            }
                            else
                            {
                                oneFound = true;
                            }
                        }
                        else
                        {
                            step2 = true;
                            break;
                        }
                    }
                }

                return step2;
            }
        }

        /// <summary>
        /// A wrapper for a pointer to a split in unmanaged memory, for use in <see cref="HashSet{T}"/>s.
        /// </summary>
        internal unsafe struct SplitPointer : IEquatable<SplitPointer>
        {
            public byte* Split;
            public byte* Mask;
            public int Length;
            private int hashCode;

            public SplitPointer(byte* split, byte* mask, int length)
            {
                this.Split = split;
                this.Mask = mask;
                this.Length = length;

                hashCode = 0;
                int hashCode2 = 0;

                for (int i = 0; i < length; i++)
                {
                    hashCode ^= (split[i] & mask[i]) << ((i % 9) * 3);

                    hashCode2 ^= ((~split[i]) & mask[i]) << ((i % 9) * 3);
                }

                hashCode |= hashCode2;
            }

            public bool Equals(SplitPointer other)
            {
#if DEBUG
                if (this.Mask != other.Mask || this.Length != other.Length)
                {
                    throw new Exception("Invalid split comparison: mask or length are different!");
                }
#endif

                if (this.Split == other.Split)
                {
                    return true;
                }

                return CompareSplits(this.Split, other.Split, this.Mask, this.Length);
            }

            public bool NotEquals(SplitPointer other)
            {
#if DEBUG
                if (this.Mask != other.Mask || this.Length != other.Length)
                {
                    throw new Exception("Invalid split comparison: mask or length are different!");
                }
#endif

                if (this.Split == other.Split)
                {
                    return false;
                }

                return !CompareSplits(this.Split, other.Split, this.Mask, this.Length);
            }

            public static bool operator ==(SplitPointer left, SplitPointer right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(SplitPointer left, SplitPointer right)
            {
                return left.NotEquals(right);
            }

            public override bool Equals([NotNullWhen(true)] object obj)
            {
                if (obj is SplitPointer spt)
                {
                    return this.Equals(spt);
                }
                else
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                return this.hashCode;
            }
        }

        /// <summary>
        /// Assigns a unique index to each thread.
        /// </summary>
        internal class ThreadIndexer
        {
            /// <summary>
            /// The maximum number of concurrent threads.
            /// </summary>
            public int MaxThreads { get; }

            /// <summary>
            /// Contains the available indices.
            /// </summary>
            private ConcurrentBag<int> threadIndices;

            /// <summary>
            /// Ensures that no more than the maximum number of indices are given out.
            /// </summary>
            private SemaphoreSlim semaphore;

            /// <summary>
            /// Creates a new <see cref="ThreadIndexer"/> instance.
            /// </summary>
            /// <param name="maxThreads">The maximum number of threads.</param>
            public ThreadIndexer(int maxThreads)
            {
                this.MaxThreads = maxThreads;

                threadIndices = new ConcurrentBag<int>(Enumerable.Range(0, maxThreads));
                semaphore = new SemaphoreSlim(maxThreads, maxThreads);
            }

            /// <summary>
            /// Gets one of the available indices. No guarantee about the order.
            /// </summary>
            /// <returns>A unique index that is not in use by any other thread.</returns>
            /// <exception cref="Exception">Thrown if an error occurs while retrieving the thread index. This should never happen.</exception>
            public int GetIndex()
            {
                semaphore.Wait();
                if (threadIndices.TryTake(out int tbr))
                {
                    return tbr;
                }
                else
                {
                    throw new Exception("An error occurred while trying to access the thread index!");
                }
            }

            /// <summary>
            /// Gives back an index so that it can be used by other threads.
            /// </summary>
            /// <param name="index">The index that will become available again.</param>
            public void ReturnIndex(int index)
            {
                threadIndices.Add(index);
                semaphore.Release();
            }
        }


    }
}
