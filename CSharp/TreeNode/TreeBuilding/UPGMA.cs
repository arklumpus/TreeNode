using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace PhyloTree.TreeBuilding
{
    /// <summary>
    /// Contains methods to compute UPGMA trees.
    /// </summary>
    public static class UPGMA
    {
        /// <summary>
        /// Builds a UPGMA tree using data from a sequence alignment. This method first computes a distance matrix from the sequence alignment, and then uses the distance matrix to compute the tree.
        /// </summary>
        /// <param name="alignment">The sequence alignment.</param>
        /// <param name="evolutionModel">The evolutionary model to use when computing the distance matrix.</param>
        /// <param name="bootstrapReplicates">The number of bootstrap replicates to perform.</param>
        /// <param name="alignmentType">The type of sequence alignment (DNA, protein, or autodetect).</param>
        /// <param name="constraint">An optional tree to constrain the search. The tree produced by this method will be compatible with this tree. The constraint tree can be multifurcating.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>The UPGMA tree built from the supplied <paramref name="alignment"/>.</returns>
        public static TreeNode BuildTree(Dictionary<string, string> alignment, EvolutionModel evolutionModel = EvolutionModel.Kimura, int bootstrapReplicates = 0, AlignmentType alignmentType = AlignmentType.Autodetect, TreeNode constraint = null, int numCores = -1, Action<double> progressCallback = null)
        {
            List<string> sequenceNames = alignment.Keys.ToList();
            List<string> sequences = alignment.Values.ToList();

            if (bootstrapReplicates == 0)
            {
                Action<double> distMatProgress = null;
                Action<double> treeProgress = null;

                if (progressCallback != null)
                {
                    distMatProgress = x => progressCallback(x * 0.5);
                    treeProgress = x => progressCallback(0.5 + x * 0.5);
                }

                float[][] distMat = DistanceMatrix.BuildFromAlignment(sequences, alignmentType, evolutionModel, numCores, distMatProgress);

                return BuildTree(distMat, sequenceNames, constraint, copyMatrix: false, numCores: numCores, progressCallback: treeProgress);
            }
            else
            {
                float[][] distMat = DistanceMatrix.BuildFromAlignment(sequences, alignmentType, evolutionModel, numCores);

                TreeNode initialTree = BuildTree(distMat, sequenceNames, constraint, copyMatrix: false, numCores: numCores);

                progressCallback?.Invoke(1.0 / (bootstrapReplicates + 1));

                Dictionary<string, int> sequenceIndices = new Dictionary<string, int>(sequenceNames.Count);
                for (int i = 0; i < sequenceNames.Count; i++)
                {
                    sequenceIndices[sequenceNames[i]] = i;
                }

                List<int[][]> splits = NeighborJoining.GetSplits(initialTree, sequenceIndices);
                List<TreeNode> nodes = initialTree.GetChildrenRecursive();
                int[] supports = new int[splits.Count];
                object supportLock = new object();

                (HashSet<int>, HashSet<int>)[] setSplits = new (HashSet<int>, HashSet<int>)[splits.Count];

                for (int i = 0; i < splits.Count; i++)
                {
                    setSplits[i] = (new HashSet<int>(splits[i][0]), new HashSet<int>(splits[i][1]));
                }

                int completed = 0;
                object progressLock = new object();

                ParallelOptions opt = new ParallelOptions() { MaxDegreeOfParallelism = numCores };

                Parallel.For(0, bootstrapReplicates, opt, i =>
                {
                    float[][] currDistMat = DistanceMatrix.BootstrapReplicateFromAlignment(sequences, alignmentType, evolutionModel);

                    TreeNode tree = BuildTree(currDistMat, sequenceNames, constraint, copyMatrix: false, numCores: 1);

                    currDistMat = null;

                    List<int[][]> newSplits = NeighborJoining.GetSplits(tree, sequenceIndices);

                    List<int> supported = new List<int>(nodes.Count);

                    Parallel.For(0, splits.Count, opt, j =>
                    {
                        if (nodes[j].Children.Count == 0 || NeighborJoining.IsCompatible(setSplits[j].Item1, setSplits[j].Item2, newSplits))
                        {
                            Interlocked.Increment(ref supports[j]);
                        }
                    });

                    if (progressCallback != null)
                    {
                        lock (progressLock)
                        {
                            completed++;
                            progressCallback?.Invoke((double)completed / (bootstrapReplicates + 1));
                        }
                    }
                });

                for (int i = 0; i < nodes.Count; i++)
                {
                    nodes[i].Support = (double)supports[i] / bootstrapReplicates;
                }

                return initialTree;
            }
        }

        /// <summary>
        /// Builds a UPGMA tree using data from a distance matrix.
        /// </summary>
        /// <param name="distanceMatrix">The distance matrix containg distances between the taxa. This can be a lower triangular matrix or a full matrix; values above the diagonal will not be used.</param>
        /// <param name="sequenceNames">The names of the taxa. The indices of this list should correspond to the rows and columns of the <paramref name="distanceMatrix"/>.</param>
        /// <param name="constraint">An optional tree to constrain the search. The tree produced by this method will be compatible with this tree. The constraint tree can be multifurcating.</param>
        /// <param name="copyMatrix">If this is <see langword="true"/>, the matrix is copied before using it to compute the tree. If this is <see langword="false"/>, the matrix is not copied. Copying the matrix
        /// increases the memory used by the method, but note that if the matrix is not copied, it will be modified in-place!</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>The UPGMA tree built from the supplied <paramref name="distanceMatrix"/>.</returns>
        public static TreeNode BuildTree(float[][] distanceMatrix, IReadOnlyList<string> sequenceNames, TreeNode constraint = null, bool copyMatrix = true, int numCores = -1, Action<double> progressCallback = null)
        {
            if (copyMatrix)
            {
                float[][] newDistMat = new float[distanceMatrix.Length][];

                for (int i = 0; i < distanceMatrix.Length; i++)
                {
                    newDistMat[i] = new float[i];

                    for (int j = 0; j < i; j++)
                    {
                        newDistMat[i][j] = distanceMatrix[i][j];
                    }
                }

                distanceMatrix = newDistMat;
            }

            unsafe
            {
                IntPtr[] rows = new IntPtr[distanceMatrix.Length];

                GCHandle[] handles = new GCHandle[distanceMatrix.Length];

                for (int i = 0; i < distanceMatrix.Length; i++)
                {
                    handles[i] = GCHandle.Alloc(distanceMatrix[i], GCHandleType.Pinned);
                    rows[i] = handles[i].AddrOfPinnedObject();
                }

                fixed (IntPtr* rowsPointer = rows)
                {
                    float** rowsFloatPointer = (float**)rowsPointer;

                    TreeNode tree;

                    if (constraint == null)
                    {
                        tree = BuildTree(rowsFloatPointer, sequenceNames, numCores, progressCallback);
                    }
                    else
                    {
                        tree = BuildTreeWithConstraint(rowsFloatPointer, sequenceNames, constraint, numCores, progressCallback);
                    }

                    for (int i = 0; i < distanceMatrix.Length; i++)
                    {
                        handles[i].Free();
                    }

                    return tree;
                }
            }
        }


        /// <summary>
        /// Builds a UPGMA tree using data from a distance matrix.
        /// </summary>
        /// <param name="distanceMatrix">The distance matrix containg distances between the taxa. This can be a lower triangular matrix or a full matrix; values above the diagonal will not be used.</param>
        /// <param name="sequenceNames">The names of the taxa. The indices of this list should correspond to the rows and columns of the <paramref name="distanceMatrix"/>.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>The UPGMA tree built from the supplied <paramref name="distanceMatrix"/>.</returns>
        private static unsafe TreeNode BuildTree(float** distanceMatrix, IReadOnlyList<string> sequenceNames, int numCores, Action<double> progressCallback)
        {
            List<TreeNode> currentLeaves = new List<TreeNode>(sequenceNames.Count);
            List<int> correspondences = new List<int>(sequenceNames.Count);
            List<int> weights = new List<int>(sequenceNames.Count);
            List<double> downstreamLengths = new List<double>(sequenceNames.Count);

            for (int i = 0; i < sequenceNames.Count; i++)
            {
                currentLeaves.Add(new TreeNode(null) { Name = sequenceNames[i] });
                correspondences.Add(i);
                downstreamLengths.Add(0);
                weights.Add(1);
            }

            double totalToProcess = ((double)sequenceNames.Count * (sequenceNames.Count + 1) * (2 * sequenceNames.Count + 1)) / 12 - 0.25 * sequenceNames.Count * (sequenceNames.Count + 1);
            long processed = 0;

            ParallelOptions opt = new ParallelOptions() { MaxDegreeOfParallelism = numCores };

            while (currentLeaves.Count > 1)
            {
                long newStep = (long)currentLeaves.Count * (currentLeaves.Count - 1) / 2;
                progressCallback?.Invoke(processed / totalToProcess);

                int minI = -1;
                int minJ = -1;

                double minDist = double.MaxValue;

                double[] minDists = new double[currentLeaves.Count];
                int[] minJs = new int[currentLeaves.Count];

                Parallel.For(0, currentLeaves.Count, opt, i =>
                {
                    double myMinDist = double.MaxValue;
                    int myMinJ = -1;

                    for (int j = 0; j < i; j++)
                    {
                        int realI = correspondences[i];
                        int realJ = correspondences[j];

                        double dist = distanceMatrix[realI][realJ];

                        if (dist < myMinDist)
                        {
                            myMinDist = dist;
                            myMinJ = j;
                        }
                    }

                    minDists[i] = myMinDist;
                    minJs[i] = myMinJ;
                });

                for (int i = 0; i < currentLeaves.Count; i++)
                {
                    if (minDists[i] < minDist)
                    {
                        minDist = minDists[i];
                        minI = i;
                        minJ = minJs[i];
                    }
                }

                minDist /= 2;

                TreeNode newNode = new TreeNode(null);
                newNode.Children.Add(currentLeaves[minI]);
                newNode.Children.Add(currentLeaves[minJ]);
                currentLeaves[minI].Parent = newNode;
                currentLeaves[minJ].Parent = newNode;
                currentLeaves[minI].Length = minDist - downstreamLengths[minI];
                currentLeaves[minJ].Length = minDist - downstreamLengths[minJ];

                int correspI = correspondences[minI];
                int correspJ = correspondences[minJ];

                int weightI = weights[minI];
                int weightJ = weights[minJ];
                int newWeight = weightI + weightJ;

                currentLeaves.RemoveAt(Math.Min(minI, minJ));
                correspondences.RemoveAt(Math.Min(minI, minJ));
                downstreamLengths.RemoveAt(Math.Min(minI, minJ));
                weights.RemoveAt(Math.Min(minI, minJ));

                currentLeaves[Math.Max(minI, minJ) - 1] = newNode;
                correspondences[Math.Max(minI, minJ) - 1] = correspI;
                downstreamLengths[Math.Max(minI, minJ) - 1] = minDist;
                weights[Math.Max(minI, minJ) - 1] = newWeight;

                for (int i = 0; i < currentLeaves.Count; i++)
                {
                    distanceMatrix[Math.Max(correspI, correspondences[i])][Math.Min(correspI, correspondences[i])] = (distanceMatrix[Math.Max(correspI, correspondences[i])][Math.Min(correspI, correspondences[i])] * weightI + distanceMatrix[Math.Max(correspJ, correspondences[i])][Math.Min(correspJ, correspondences[i])] * weightJ) / newWeight;
                }

                processed += newStep;
            }

            progressCallback?.Invoke(1);

            return currentLeaves[0];
        }

        /// <summary>
        /// Builds a UPGMA tree using data from a distance matrix, applying the specified constraint.
        /// </summary>
        /// <param name="distanceMatrix">The distance matrix containg distances between the taxa. This can be a lower triangular matrix or a full matrix; values above the diagonal will not be used.</param>
        /// <param name="sequenceNames">The names of the taxa. The indices of this list should correspond to the rows and columns of the <paramref name="distanceMatrix"/>.</param>
        /// <param name="constraint">A tree to constrain the search. The tree produced by this method will be compatible with this tree. The constraint tree can be multifurcating.</param>
        /// <param name="numCores">Maximum number of threads to use, or -1 to let the runtime decide.</param>
        /// <param name="progressCallback">A method used to report progress.</param>
        /// <returns>The UPGMA tree built from the supplied <paramref name="distanceMatrix"/>.</returns>
        private static unsafe TreeNode BuildTreeWithConstraint(float** distanceMatrix, IReadOnlyList<string> sequenceNames, TreeNode constraint, int numCores, Action<double> progressCallback)
        {
            Dictionary<string, int> sequenceIndices = new Dictionary<string, int>(sequenceNames.Count);
            for (int i = 0; i < sequenceNames.Count; i++)
            {
                sequenceIndices[sequenceNames[i]] = i;
            }

            constraint = constraint.GetUnrootedTree();

            List<int[][]> splits = new List<int[][]>();

            foreach (var v in constraint.GetSplits())
            {
                if (v.side1.Count > 1 && v.side2.Count > 1)
                {
                    int[] leftSide = new int[v.side1.Count];
                    int[] rightSide = new int[v.side2.Count];

                    for (int i = 0; i < v.side1.Count; i++)
                    {
                        leftSide[i] = sequenceIndices[v.side1[i].Name];
                    }

                    for (int i = 0; i < v.side2.Count; i++)
                    {
                        rightSide[i] = sequenceIndices[v.side2[i].Name];
                    }

                    splits.Add(new int[][] { leftSide, rightSide });
                }
            }

            List<TreeNode> currentLeaves = new List<TreeNode>(sequenceNames.Count);
            List<int> correspondences = new List<int>(sequenceNames.Count);
            List<int> weights = new List<int>(sequenceNames.Count);
            List<double> downstreamLengths = new List<double>(sequenceNames.Count);
            List<List<int>> underlyingLeaves = new List<List<int>>(sequenceNames.Count);

            for (int i = 0; i < sequenceNames.Count; i++)
            {
                currentLeaves.Add(new TreeNode(null) { Name = sequenceNames[i] });
                correspondences.Add(i);
                downstreamLengths.Add(0);
                weights.Add(1);
                underlyingLeaves.Add(new List<int>() { i });
            }

            double totalToProcess = ((double)sequenceNames.Count * (sequenceNames.Count + 1) * (2 * sequenceNames.Count + 1)) / 12 - 0.25 * sequenceNames.Count * (sequenceNames.Count + 1);
            long processed = 0;

            ParallelOptions opt = new ParallelOptions() { MaxDegreeOfParallelism = numCores };

            while (currentLeaves.Count > 1)
            {
                long newStep = (long)currentLeaves.Count * (currentLeaves.Count - 1) / 2;
                progressCallback?.Invoke(processed / totalToProcess);

                int minI = -1;
                int minJ = -1;

                double minDist = double.MaxValue;

                double[] minDists = new double[currentLeaves.Count];
                int[] minJs = new int[currentLeaves.Count];

                Parallel.For(0, currentLeaves.Count, opt, i =>
                {
                    double myMinDist = double.MaxValue;
                    int myMinJ = -1;

                    for (int j = 0; j < i; j++)
                    {
                        int realI = correspondences[i];
                        int realJ = correspondences[j];

                        double dist = distanceMatrix[realI][realJ];

                        if (dist < myMinDist)
                        {
                            HashSet<int> leftSide = new HashSet<int>(underlyingLeaves[i].Count + underlyingLeaves[j].Count);
                            for (int k = 0; k < underlyingLeaves[i].Count; k++)
                            {
                                leftSide.Add(underlyingLeaves[i][k]);
                            }
                            for (int k = 0; k < underlyingLeaves[j].Count; k++)
                            {
                                leftSide.Add(underlyingLeaves[j][k]);
                            }

                            HashSet<int> rightSide = new HashSet<int>(sequenceNames.Count);

                            for (int k = 0; k < sequenceNames.Count; k++)
                            {
                                if (!leftSide.Contains(k))
                                {
                                    rightSide.Add(k);
                                }
                            }

                            if (NeighborJoining.IsCompatible(leftSide, rightSide, splits))
                            {
                                myMinDist = dist;
                                myMinJ = j;
                            }
                        }
                    }

                    minDists[i] = myMinDist;
                    minJs[i] = myMinJ;
                });

                for (int i = 0; i < currentLeaves.Count; i++)
                {
                    if (minDists[i] < minDist)
                    {
                        minDist = minDists[i];
                        minI = i;
                        minJ = minJs[i];
                    }
                }

                minDist /= 2;

                TreeNode newNode = new TreeNode(null);
                newNode.Children.Add(currentLeaves[minI]);
                newNode.Children.Add(currentLeaves[minJ]);
                currentLeaves[minI].Parent = newNode;
                currentLeaves[minJ].Parent = newNode;

                // Prevent negative branch lengths.
                if (downstreamLengths[minI] > minDist && downstreamLengths[minJ] <= minDist)
                {
                    minDist += 2 * (downstreamLengths[minI] - minDist);
                }
                else if (downstreamLengths[minJ] > minDist && downstreamLengths[minI] <= minDist)
                {
                    minDist += 2 * (downstreamLengths[minJ] - minDist);
                }
                else if(downstreamLengths[minI] > minDist && downstreamLengths[minJ] > minDist)
                {
                    minDist += downstreamLengths[minI] + downstreamLengths[minJ] - 2 * minDist;
                }

                currentLeaves[minI].Length = minDist - downstreamLengths[minI];
                currentLeaves[minJ].Length = minDist - downstreamLengths[minJ];

                int correspI = correspondences[minI];
                int correspJ = correspondences[minJ];

                int weightI = weights[minI];
                int weightJ = weights[minJ];
                int newWeight = weightI + weightJ;

                underlyingLeaves[Math.Max(minI, minJ)].AddRange(underlyingLeaves[Math.Min(minI, minJ)]);
                underlyingLeaves.RemoveAt(Math.Min(minI, minJ));

                currentLeaves.RemoveAt(Math.Min(minI, minJ));
                correspondences.RemoveAt(Math.Min(minI, minJ));
                downstreamLengths.RemoveAt(Math.Min(minI, minJ));
                weights.RemoveAt(Math.Min(minI, minJ));

                currentLeaves[Math.Max(minI, minJ) - 1] = newNode;
                correspondences[Math.Max(minI, minJ) - 1] = correspI;
                downstreamLengths[Math.Max(minI, minJ) - 1] = minDist;
                weights[Math.Max(minI, minJ) - 1] = newWeight;

                for (int i = 0; i < currentLeaves.Count; i++)
                {
                    distanceMatrix[Math.Max(correspI, correspondences[i])][Math.Min(correspI, correspondences[i])] = (distanceMatrix[Math.Max(correspI, correspondences[i])][Math.Min(correspI, correspondences[i])] * weightI + distanceMatrix[Math.Max(correspJ, correspondences[i])][Math.Min(correspJ, correspondences[i])] * weightJ) / newWeight;
                }

                processed += newStep;
            }

            progressCallback?.Invoke(1);

            return currentLeaves[0];
        }
    }
}
