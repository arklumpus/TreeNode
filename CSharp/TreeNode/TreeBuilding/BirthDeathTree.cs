using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace PhyloTree.TreeBuilding
{
    /// <summary>
    /// Contains methods to simulate birth-death trees.
    /// </summary>
    public static class BirthDeathTree
    {
        /// <summary>
        /// Simulate an unlabelled birth-death tree, stopping when the age of the tree reaches a certain value.
        /// </summary>
        /// <param name="treeAge">The final age of the tree. Note that the actual age of the tree may be smaller than this;
        /// this can happen either if <paramref name="keepDeadLineages"/> is <see langword="false"/> and one of the two clades
        /// descending from the root node goes exinct, or if <paramref name="keepDeadLineages"/> is <see langword="true"/> and
        /// all the clades go extinct. If all the clades go extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>,
        /// this method will return <see langword="null"/>.</param>
        /// <param name="birthRate">The birth rate of the tree.</param>
        /// <param name="deathRate">The death rate of the tree.</param>
        /// <param name="keepDeadLineages">If this is <see langword="true"/>, dead lineages are kept in the tree. If this is
        /// <see langword="false"/>, they are pruned from the tree.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the simulation if it takes too long.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the unlabelled birth-death tree, or <see langword="null"/> if all
        /// the lineages went extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>.</returns>
        public static TreeNode UnlabelledTree(double treeAge, double birthRate, double deathRate = 0, bool keepDeadLineages = false, CancellationToken cancellationToken = default)
        {
            List<TreeNode> aliveLineages = new List<TreeNode>();

            TreeNode root = new TreeNode(null);
            TreeNode child1 = new TreeNode(root) { Length = 0 };
            TreeNode child2 = new TreeNode(root) { Length = 0 };
            root.Children.Add(child1);
            root.Children.Add(child2);

            aliveLineages.Add(child1);
            aliveLineages.Add(child2);

            double time = 0;

            while (time < treeAge)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double timeToNextEvent = Exponential.Sample(RandomTree.RandomNumberGenerator, aliveLineages.Count * (birthRate + deathRate));

                if (timeToNextEvent < treeAge - time)
                {
                    foreach (TreeNode lineage in aliveLineages)
                    {
                        lineage.Length += timeToNextEvent;
                    }

                    bool isBirth = ContinuousUniform.Sample(RandomTree.RandomNumberGenerator, 0, 1) < birthRate / (birthRate + deathRate);

                    if (isBirth)
                    {
                        int parent = DiscreteUniform.Sample(RandomTree.RandomNumberGenerator, 0, aliveLineages.Count - 1);

                        TreeNode newChild1 = new TreeNode(aliveLineages[parent]) { Length = 0 };
                        TreeNode newChild2 = new TreeNode(aliveLineages[parent]) { Length = 0 };

                        aliveLineages[parent].Children.Add(newChild1);
                        aliveLineages[parent].Children.Add(newChild2);

                        aliveLineages.RemoveAt(parent);
                        aliveLineages.Add(newChild1);
                        aliveLineages.Add(newChild2);
                    }
                    else
                    {
                        int dead = DiscreteUniform.Sample(RandomTree.RandomNumberGenerator, 0, aliveLineages.Count - 1);

                        if (!keepDeadLineages)
                        {
                            if (aliveLineages.Count == 1)
                            {
                                return null;
                            }
                            else
                            {
                                root = root.Prune(aliveLineages[dead], false);
                            }
                        }

                        aliveLineages.RemoveAt(dead);

                        if (aliveLineages.Count == 0)
                        {
                            return root;
                        }
                    }
                }
                else
                {
                    foreach (TreeNode lineage in aliveLineages)
                    {
                        lineage.Length += treeAge - time;
                    }
                }

                time += timeToNextEvent;
            }

            return root;
        }

        /// <summary>
        /// Simulate a labelled birth-death tree, stopping when the age of the tree reaches a certain value.
        /// </summary>
        /// <param name="treeAge">The final age of the tree. Note that the actual age of the tree may be smaller than this;
        /// this can happen either if <paramref name="keepDeadLineages"/> is <see langword="false"/> and one of the two clades
        /// descending from the root node goes exinct, or if <paramref name="keepDeadLineages"/> is <see langword="true"/> and
        /// all the clades go extinct. If all the clades go extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>,
        /// this method will return <see langword="null"/>.</param>
        /// <param name="birthRate">The birth rate of the tree.</param>
        /// <param name="deathRate">The death rate of the tree.</param>
        /// <param name="keepDeadLineages">If this is <see langword="true"/>, dead lineages are kept in the tree. If this is
        /// <see langword="false"/>, they are pruned from the tree.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the simulation if it takes too long.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the labelled birth-death tree, or <see langword="null"/> if all
        /// the lineages went extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>.</returns>
        public static TreeNode LabelledTree(double treeAge, double birthRate, double deathRate = 0, bool keepDeadLineages = false, CancellationToken cancellationToken = default)
        {
            TreeNode unlabelledTree = UnlabelledTree(treeAge, birthRate, deathRate, keepDeadLineages, cancellationToken);

            if (unlabelledTree == null)
            {
                return null;
            }

            List<TreeNode> leaves = unlabelledTree.GetLeaves();

            List<int> labels = Enumerable.Range(1, leaves.Count).ToList();

            for (int i = 0; i < leaves.Count; i++)
            {
                int label = RandomTree.RandomNumberGenerator.Next(0, labels.Count);

                leaves[i].Name = "t" + labels[label].ToString();
                labels.RemoveAt(label);
            }

            return unlabelledTree;
        }

        /// <summary>
        /// Simulate an unlabelled birth-death tree, stopping when the number of lineages that are alive in the tree reaches a certain value.
        /// </summary>
        /// <param name="leafCount">The final number of lineages that are alive in the tree. Note that, if <paramref name="keepDeadLineages"/>
        /// is <see langword="true"/>, the actual number of leaves in the tree may be larger than this, because the leaves corresponding
        /// to dead lineages are kept. If all the lineages go extinct before the target number of lineages is reaced, the method will return
        /// a smaller tree if <paramref name="keepDeadLineages"/> is <see langword="true"/>, or <see langword="null"/> if it is <see langword="false"/>.</param>
        /// <param name="birthRate">The birth rate of the tree.</param>
        /// <param name="deathRate">The death rate of the tree.</param>
        /// <param name="keepDeadLineages">If this is <see langword="true"/>, dead lineages are kept in the tree. If this is
        /// <see langword="false"/>, they are pruned from the tree.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the simulation if it takes too long.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the unlabelled birth-death tree, or <see langword="null"/> if all
        /// the lineages went extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>.</returns>
        public static TreeNode UnlabelledTree(int leafCount, double birthRate, double deathRate = 0, bool keepDeadLineages = false, CancellationToken cancellationToken = default)
        {
            return UnlabelledTree(leafCount, birthRate, deathRate, keepDeadLineages, out _, cancellationToken);
        }

        /// <summary>
        /// Simulate an unlabelled birth-death tree, stopping when the number of lineages that are alive in the tree reaches a certain value.
        /// </summary>
        /// <param name="leafCount">The final number of lineages that are alive in the tree. Note that, if <paramref name="keepDeadLineages"/>
        /// is <see langword="true"/>, the actual number of leaves in the tree may be larger than this, because the leaves corresponding
        /// to dead lineages are kept. If all the lineages go extinct before the target number of lineages is reaced, the method will return
        /// a smaller tree if <paramref name="keepDeadLineages"/> is <see langword="true"/>, or <see langword="null"/> if it is <see langword="false"/>.</param>
        /// <param name="birthRate">The birth rate of the tree.</param>
        /// <param name="deathRate">The death rate of the tree.</param>
        /// <param name="keepDeadLineages">If this is <see langword="true"/>, dead lineages are kept in the tree. If this is
        /// <see langword="false"/>, they are pruned from the tree.</param>
        /// <param name="aliveLineages">The lineages that are alive at the end of the tree.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the simulation if it takes too long.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the unlabelled birth-death tree, or <see langword="null"/> if all
        /// the lineages went extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>.</returns>
        private static TreeNode UnlabelledTree(int leafCount, double birthRate, double deathRate, bool keepDeadLineages, out List<TreeNode> aliveLineages, CancellationToken cancellationToken)
        {
            List<TreeNode> leaves = new List<TreeNode>(leafCount);

            for (int i = 0; i < leafCount; i++)
            {
                leaves.Add(new TreeNode(null) { Length = 0 });
            }

            aliveLineages = new List<TreeNode>(leaves);

            List<TreeNode> deadLeaves = new List<TreeNode>();

            while (leaves.Count > 1)
            {
                cancellationToken.ThrowIfCancellationRequested();

                double timeToNextEvent = Exponential.Sample(RandomTree.RandomNumberGenerator, leaves.Count * (birthRate + deathRate));

                foreach (TreeNode leaf in leaves)
                {
                    leaf.Length += timeToNextEvent;
                }

                bool isBirth = ContinuousUniform.Sample(RandomTree.RandomNumberGenerator, 0, 1) < birthRate / (birthRate + deathRate);

                if (isBirth)
                {

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
                else
                {
                    TreeNode newLeaf = new TreeNode(null) { Length = 0 };
                    leaves.Add(newLeaf);
                    deadLeaves.Add(newLeaf);
                }
            }

            leaves[0].Length = double.NaN;

            if (!keepDeadLineages)
            {
                foreach (TreeNode leaf in deadLeaves)
                {
                    leaves[0] = leaves[0].Prune(leaf, false);
                }
            }

            return leaves[0];
        }

        /// <summary>
        /// Simulate a labelled birth-death tree, stopping when the number of lineages that are alive in the tree reaches a certain value.
        /// </summary>
        /// <param name="leafNames">The names for the terminal nodes of the tree. Note that, if <paramref name="keepDeadLineages"/>
        /// is <see langword="true"/>, the actual number of leaves in the tree may be larger than this, because the leaves corresponding
        /// to dead lineages are kept (their names will be empty). If all the lineages go extinct before the target number of lineages
        /// is reaced, the method will return a smaller tree (without any leaf names) if <paramref name="keepDeadLineages"/> is <see langword="true"/>,
        /// or <see langword="null"/> if it is <see langword="false"/>.</param>
        /// <param name="birthRate">The birth rate of the tree.</param>
        /// <param name="deathRate">The death rate of the tree.</param>
        /// <param name="keepDeadLineages">If this is <see langword="true"/>, dead lineages are kept in the tree. If this is
        /// <see langword="false"/>, they are pruned from the tree.</param>
        /// <param name="constraint">A tree to constrain the sampling. The tree produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the tree, this will bias the sampled topology distribution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the simulation if it takes too long.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the unlabelled birth-death tree, or <see langword="null"/> if all
        /// the lineages went extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>.</returns>
        public static TreeNode LabelledTree(IReadOnlyList<string> leafNames, double birthRate, double deathRate = 0, bool keepDeadLineages = false, TreeNode constraint = null, CancellationToken cancellationToken = default)
        {
            if (constraint == null)
            {
                int leafCount = leafNames.Count;

                List<TreeNode> leaves = new List<TreeNode>(leafCount);

                for (int i = 0; i < leafCount; i++)
                {
                    leaves.Add(new TreeNode(null) { Length = 0, Name = leafNames[i] });
                }
                List<TreeNode> deadLeaves = new List<TreeNode>();

                while (leaves.Count > 1)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double timeToNextEvent = Exponential.Sample(RandomTree.RandomNumberGenerator, leaves.Count * (birthRate + deathRate));

                    foreach (TreeNode leaf in leaves)
                    {
                        leaf.Length += timeToNextEvent;
                    }

                    bool isBirth = ContinuousUniform.Sample(RandomTree.RandomNumberGenerator, 0, 1) < birthRate / (birthRate + deathRate);

                    if (isBirth)
                    {

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
                    else
                    {
                        TreeNode newLeaf = new TreeNode(null) { Length = 0 };
                        leaves.Add(newLeaf);
                        deadLeaves.Add(newLeaf);
                    }
                }

                leaves[0].Length = double.NaN;

                if (!keepDeadLineages)
                {
                    foreach (TreeNode leaf in deadLeaves)
                    {
                        leaves[0] = leaves[0].Prune(leaf, false);
                    }
                }

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
                    return LabelledTree(leafNames, birthRate, deathRate, keepDeadLineages, null, cancellationToken);
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

                List<TreeNode> deadLeaves = new List<TreeNode>();

                while (leaves.Count > 1)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    double timeToNextEvent = Exponential.Sample(RandomTree.RandomNumberGenerator, leaves.Count * (birthRate + deathRate));

                    foreach (TreeNode leaf in leaves)
                    {
                        leaf.Length += timeToNextEvent;
                    }

                    bool isBirth = ContinuousUniform.Sample(RandomTree.RandomNumberGenerator, 0, 1) < birthRate / (birthRate + deathRate);

                    if (isBirth)
                    {
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
                    else
                    {
                        TreeNode newLeaf = new TreeNode(null) { Length = 0 };
                        leaves.Add(newLeaf);
                        deadLeaves.Add(newLeaf);
                        underlyingLeaves.Add(new HashSet<int>());
                    }
                }

                leaves[0].Length = double.NaN;

                if (!keepDeadLineages)
                {
                    foreach (TreeNode leaf in deadLeaves)
                    {
                        leaves[0] = leaves[0].Prune(leaf, false);
                    }
                }

                return leaves[0];
            }
        }

        /// <summary>
        /// Simulate a labelled birth-death tree, stopping when the number of lineages that are alive in the tree reaches a certain value.
        /// </summary>
        /// <param name="leafCount">The final number of lineages that are alive in the tree (their names will be in the form <c>t1, t2, ..., tN</c>,
        /// where <c>N</c> is <paramref name="leafCount"/>). Note that, if <paramref name="keepDeadLineages"/>
        /// is <see langword="true"/>, the actual number of leaves in the tree may be larger than this, because the leaves corresponding
        /// to dead lineages are kept (their names will be empty). If all the lineages go extinct before the target number of lineages
        /// is reaced, the method will return a smaller tree (without any leaf names) if <paramref name="keepDeadLineages"/> is <see langword="true"/>,
        /// or <see langword="null"/> if it is <see langword="false"/>.</param>
        /// <param name="birthRate">The birth rate of the tree.</param>
        /// <param name="deathRate">The death rate of the tree.</param>
        /// <param name="keepDeadLineages">If this is <see langword="true"/>, dead lineages are kept in the tree. If this is
        /// <see langword="false"/>, they are pruned from the tree.</param>
        /// <param name="constraint">A tree to constrain the sampling. The tree produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the tree, this will bias the sampled topology distribution.</param>
        /// <param name="cancellationToken">A <see cref="CancellationToken"/> used to cancel the simulation if it takes too long.</param>
        /// <returns>A <see cref="TreeNode"/> object containing the unlabelled birth-death tree, or <see langword="null"/> if all
        /// the lineages went extinct and <paramref name="keepDeadLineages"/> is <see langword="false"/>.</returns>

        public static TreeNode LabelledTree(int leafCount, double birthRate, double deathRate = 0, bool keepDeadLineages = false, TreeNode constraint = null, CancellationToken cancellationToken = default)
        {
            string[] leafNames = new string[leafCount];

            for (int i = 0; i < leafCount; i++)
            {
                leafNames[i] = "t" + (i + 1).ToString();
            }

            return LabelledTree(leafNames, birthRate, deathRate, keepDeadLineages, constraint, cancellationToken);
        }
    }
}
