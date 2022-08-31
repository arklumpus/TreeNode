using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhyloTree.TreeBuilding
{
    /// <summary>
    /// Contains methods to simulate coalescent trees.
    /// </summary>
    public static class CoalescentTree
    {
        /// <summary>
        /// Simulate an unlabelled coalescent tree.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the tree.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the unlabelled coalescent tree.</returns>
        public static TreeNode UnlabelledTree(int leafCount)
        {
            string[] leafNames = new string[leafCount];

            for (int i = 0; i < leafNames.Length; i++)
            {
                leafNames[i] = "";
            }

            return LabelledTree(leafNames);
        }

        /// <summary>
        /// Simulate a labelled coalescent tree with the supplied tip labels.
        /// </summary>
        /// <param name="leafNames">The labels for the terminal nodes of the tree.</param>
        /// <param name="constraint">A tree to constrain the sampling. The tree produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the tree, this will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the labelled coalescent tree.</returns>
        public static TreeNode LabelledTree(IReadOnlyList<string> leafNames, TreeNode constraint = null)
        {
            if (constraint == null)
            {
                int leafCount = leafNames.Count;

                double[] coalescenceTimes = new double[leafCount - 1];

                for (int k = leafCount; k > 1; k--)
                {
                    coalescenceTimes[leafCount - k] = Exponential.Sample(k * (k - 1) * 0.25);
                }

                List<TreeNode> leaves = new List<TreeNode>(leafCount);

                for (int i = 0; i < leafCount; i++)
                {
                    leaves.Add(new TreeNode(null) { Length = 0, Name = leafNames[i] });
                }

                for (int i = 0; i < coalescenceTimes.Length; i++)
                {
                    foreach (TreeNode leaf in leaves)
                    {
                        leaf.Length += coalescenceTimes[i];
                    }

                    int index1 = RandomTree.RandomNumberGenerator.Next(0, leaves.Count);
                    TreeNode leaf1 = leaves[index1];
                    leaves.RemoveAt(index1);

                    int index2 = RandomTree.RandomNumberGenerator.Next(0, leaves.Count);
                    TreeNode leaf2 = leaves[index2];
                    leaves.RemoveAt(index2);

                    TreeNode newNode = new TreeNode(null) { Length = 0 };
                    newNode.Children.Add(leaf1);
                    newNode.Children.Add(leaf2);
                    leaf1.Parent = newNode;
                    leaf2.Parent = newNode;

                    leaves.Add(newNode);
                }

                leaves[0].Length = double.NaN;

                return leaves[0];
            }
            else
            {
                constraint = constraint.Clone();

                List<string> availableNames = leafNames.ToList();

                Dictionary<string, int> sequenceIndices = new Dictionary<string, int>();
                List<TreeNode> toBePruned = new List<TreeNode>();

                foreach (TreeNode leaf in constraint.GetLeaves())
                {
                    if (!string.IsNullOrEmpty(leaf.Name))
                    {
                        int index = availableNames.IndexOf(leaf.Name);
                        if (index >= 0)
                        {
                            sequenceIndices[leaf.Name] = index;
                        }
                        else
                        {
                            toBePruned.Add(leaf);
                        }
                    }
                    else
                    {
                        toBePruned.Add(leaf);
                    }
                }

                for (int i = 0; i < toBePruned.Count; i++)
                {
                    constraint = constraint.Prune(toBePruned[i], false);
                }

                if (constraint == null || constraint.GetLeaves().Count == 1)
                {
                    return LabelledTree(leafNames, null);
                }

                List<int[][]> splits = NeighborJoining.GetSplits(constraint, sequenceIndices);

                List<TreeNode> leaves = new List<TreeNode>(leafNames.Count);
                List<HashSet<int>> underlyingLeaves = new List<HashSet<int>>();
                HashSet<int> allLeaves = new HashSet<int>();

                for (int i = 0; i < leafNames.Count; i++)
                {
                    leaves.Add(new TreeNode(null) { Length = 0, Name = leafNames[i] });

                    if (sequenceIndices.TryGetValue(leafNames[i], out int index))
                    {
                        underlyingLeaves.Add(new HashSet<int>() { index });
                        allLeaves.Add(index);
                    }
                    else
                    {
                        underlyingLeaves.Add(new HashSet<int>());
                    }
                }

                allLeaves.Add(-1);

                int leafCount = leafNames.Count;

                double[] coalescenceTimes = new double[leafCount - 1];

                for (int k = leafCount; k > 1; k--)
                {
                    coalescenceTimes[leafCount - k] = Exponential.Sample(k * (k - 1) * 0.25);
                }

                for (int i = 0; i < coalescenceTimes.Length; i++)
                {
                    foreach (TreeNode leaf in leaves)
                    {
                        leaf.Length += coalescenceTimes[i];
                    }


                    int index1, index2;
                    HashSet<int> potentialSplitLeft, potentialSplitRight;

                    List<(int, int)> availablePairs = (from el in Enumerable.Range(0, leaves.Count) select (from el2 in Enumerable.Range(0, el) select (el, el2))).Aggregate(new List<(int, int)>(), (a, b) => { a.AddRange(b); return a; });

                    do
                    {
                        (index1, index2) = availablePairs.Sample();

                        potentialSplitRight = new HashSet<int>(underlyingLeaves[index1]);
                        potentialSplitRight.UnionWith(underlyingLeaves[index2]);

                        potentialSplitLeft = new HashSet<int>(allLeaves);
                        potentialSplitLeft.ExceptWith(potentialSplitRight);

                    } while (!NeighborJoining.IsCompatible(potentialSplitLeft, potentialSplitRight, splits));


                    TreeNode leaf1 = leaves[index1];
                    TreeNode leaf2 = leaves[index2];

                    underlyingLeaves[Math.Min(index1, index2)].UnionWith(underlyingLeaves[Math.Max(index1, index2)]);
                    underlyingLeaves.RemoveAt(Math.Max(index1, index2));

                    TreeNode newNode = new TreeNode(null) { Length = 0 };
                    newNode.Children.Add(leaf1);
                    newNode.Children.Add(leaf2);
                    leaf1.Parent = newNode;
                    leaf2.Parent = newNode;

                    leaves.RemoveAt(Math.Max(index1, index2));
                    leaves[Math.Min(index1, index2)] = newNode;
                }

                leaves[0].Length = double.NaN;

                return leaves[0];
            }
        }

        /// <summary>
        /// Simulate a labelled coalescent tree with the specified number of terminal nodes.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the tree. Their names will be in the form <c>t1, t2, ..., tN</c>, where <c>N</c> is <paramref name="leafCount"/>.</param>
        /// <param name="constraint">A tree to constrain the sampling. The tree produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the tree, this will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the labelled coalescent tree.</returns>
        public static TreeNode LabelledTree(int leafCount, TreeNode constraint = null)
        {
            string[] leafNames = new string[leafCount];

            for (int i = 0; i < leafNames.Length; i++)
            {
                leafNames[i] = "t" + (i + 1).ToString();
            }

            return LabelledTree(leafNames, constraint);
        }
    }
}
