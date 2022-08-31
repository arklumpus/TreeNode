using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MathNet.Numerics.Distributions;

namespace PhyloTree.TreeBuilding
{
    /// <summary>
    /// Contains methods to generate random trees.
    /// </summary>
    public static class RandomTree
    {
        /// <summary>
        /// Random number generator used for sampling.
        /// </summary>
        public static Random RandomNumberGenerator = new ThreadSafeRandom();

        /// <summary>
        /// Samples a random unlabelled topology according to the specified model.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the topology.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random unlabelled topology (branch lengths will all be set to <see cref="double.NaN"/>).</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="model"/> is neither <see cref="TreeNode.NullHypothesis.PDA"/> nor <see cref="TreeNode.NullHypothesis.YHK"/>.</exception>
        public static TreeNode UnlabelledTopology(int leafCount, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false)
        {
            if (model == TreeNode.NullHypothesis.YHK)
            {
                TreeNode initialTree = new TreeNode(null);
                initialTree.Children.Add(new TreeNode(initialTree));
                initialTree.Children.Add(new TreeNode(initialTree));

                if (!rooted)
                {
                    initialTree.Children.Add(new TreeNode(initialTree));
                }

                List<TreeNode> leaves = new List<TreeNode>();

                leaves.AddRange(initialTree.Children);

                while (leaves.Count < leafCount)
                {
                    int index = RandomNumberGenerator.Next(0, leaves.Count);

                    TreeNode selectedLeaf = leaves[index];

                    leaves.RemoveAt(index);

                    selectedLeaf.Children.Add(new TreeNode(selectedLeaf));
                    selectedLeaf.Children.Add(new TreeNode(selectedLeaf));
                    leaves.AddRange(selectedLeaf.Children);
                }

                return initialTree;
            }
            else if (model == TreeNode.NullHypothesis.PDA)
            {
                TreeNode initialTree = new TreeNode(null);
                initialTree.Children.Add(new TreeNode(initialTree));
                initialTree.Children.Add(new TreeNode(initialTree));

                if (!rooted)
                {
                    initialTree.Children.Add(new TreeNode(initialTree));
                }

                List<TreeNode> leaves = new List<TreeNode>(initialTree.Children);
                List<TreeNode> nodes;

                if (rooted)
                {
                    nodes = initialTree.GetChildrenRecursive();
                }
                else
                {
                    nodes = new List<TreeNode>(initialTree.Children);
                }

                while (leaves.Count < leafCount)
                {
                    int index = RandomNumberGenerator.Next(0, nodes.Count);

                    TreeNode selectedNode = nodes[index];

                    if (selectedNode.Children.Count == 0)
                    {
                        leaves.Remove(selectedNode);

                        selectedNode.Children.Add(new TreeNode(selectedNode));
                        selectedNode.Children.Add(new TreeNode(selectedNode));
                        leaves.AddRange(selectedNode.Children);
                        nodes.AddRange(selectedNode.Children);
                    }
                    else
                    {
                        if (selectedNode.Parent != null)
                        {
                            TreeNode newNode = new TreeNode(selectedNode.Parent);
                            selectedNode.Parent.Children.Add(newNode);

                            TreeNode newLeaf = new TreeNode(newNode);
                            newNode.Children.Add(newLeaf);

                            selectedNode.Parent.Children.Remove(selectedNode);
                            selectedNode.Parent = newNode;
                            newNode.Children.Add(selectedNode);

                            nodes.Add(newNode);
                            nodes.Add(newLeaf);
                            leaves.Add(newLeaf);
                        }
                        else
                        {
                            TreeNode newNode = new TreeNode(null);
                            TreeNode newLeaf = new TreeNode(newNode);
                            newNode.Children.Add(newLeaf);

                            selectedNode.Parent = newNode;
                            newNode.Children.Add(selectedNode);

                            nodes.Add(newNode);
                            nodes.Add(newLeaf);
                            leaves.Add(newLeaf);

                            initialTree = newNode;
                        }

                    }
                }

                return initialTree;
            }
            else
            {
                throw new ArgumentException("Invalid tree model");
            }
        }

        /// <summary>
        /// Samples a random unlabelled tree according to the specified model, using branch lengths drawn from the supplied distribution.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the topology.</param>
        /// <param name="branchLengthDistribution">The continuous univariate distribution from which the branch lengths will be drawn.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random unlabelled tree, with branch lengths.</returns>
        public static TreeNode UnlabelledTree(int leafCount, IContinuousDistribution branchLengthDistribution, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false)
        {
            TreeNode topology = UnlabelledTopology(leafCount, model, rooted);

            List<TreeNode> nodes = topology.GetChildrenRecursive();

            double[] branchLengths = new double[nodes.Count - 1];

            branchLengthDistribution.Samples(branchLengths);

            for (int i = 1; i < nodes.Count; i++)
            {
                nodes[i].Length = branchLengths[i - 1];
            }

            return topology;
        }

        /// <summary>
        /// Samples a random unlabelled tree according to the specified model, using branch lengths drawn from a Uniform(0, 1) distribution.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the topology.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random unlabelled tree, with branch lengths.</returns>
        public static TreeNode UnlabelledTree(int leafCount, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false)
        {
            return UnlabelledTree(leafCount, new ContinuousUniform(0, 1, RandomNumberGenerator), model, rooted);
        }

        /// <summary>
        /// Adds leaf names to the supplied tree, in a random order.
        /// </summary>
        /// <param name="tree">The tree on which the leaf names will be added.</param>
        /// <param name="leafNames">The leaf names to add.</param>
        private static void AddLeafNames(TreeNode tree, IReadOnlyList<string> leafNames)
        {
            List<TreeNode> leaves = tree.GetLeaves();
            List<string> leafNamesList = leafNames.ToList();

            for (int i = 0; i < leaves.Count; i++)
            {
                int index = RandomNumberGenerator.Next(0, leafNamesList.Count);
                leaves[i].Name = leafNamesList[index];
                leafNamesList.RemoveAt(index);
            }
        }

        /// <summary>
        /// Takes a random element from a list and returns it, removing it from the list.
        /// </summary>
        /// <param name="elements">The list from which the element is sampled.</param>
        /// <returns>A random element selected from the list.</returns>
        internal static T Sample<T>(this IList<T> elements)
        {
            int index = RandomNumberGenerator.Next(0, elements.Count);
            T name = elements[index];
            elements.RemoveAt(index);
            return name;
        }

        /// <summary>
        /// Randomly resolve all the polytomies in a tree.
        /// </summary>
        /// <param name="tree">The tree containing the polytomies to be resolved.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree is supposed to be rooted or not. If this is <see langword="true"/>, a trichotomy at the root node will be resolved.</param>
        /// <returns>The tree, where all the polytomies have been randomly resolved. This is performed in-place, but the return value of this method should be used in case the root node has changed.</returns>
        public static TreeNode ResolvePolytomies(TreeNode tree, bool rooted = false)
        {
            bool found = true;

            while (found)
            {
                List<TreeNode> nodes = tree.GetChildrenRecursive();
                found = false;

                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].Children.Count > 2 && !(i == 0 && !rooted && nodes[i].Children.Count == 3))
                    {
                        TreeNode nodeToRemove = nodes[i].Children[RandomNumberGenerator.Next(0, nodes[i].Children.Count)];
                        nodes[i].Children.Remove(nodeToRemove);

                        int newSiblingIndex = RandomNumberGenerator.Next(0, nodes[i].Children.Count + 1);

                        if (newSiblingIndex < nodes[i].Children.Count)
                        {
                            TreeNode newNode = new TreeNode(nodes[i]);
                            nodes[i].Children[newSiblingIndex].Parent = newNode;
                            newNode.Children.Add(nodes[i].Children[newSiblingIndex]);
                            nodeToRemove.Parent = newNode;
                            newNode.Children.Add(nodeToRemove);
                            nodes[i].Children[newSiblingIndex] = newNode;
                        }
                        else
                        {
                            if (nodes[i].Parent != null)
                            {
                                TreeNode newNode = new TreeNode(nodes[i].Parent);
                                newNode.Parent.Children.Add(newNode);

                                nodes[i].Parent.Children.Remove(nodes[i]);
                                nodes[i].Parent = newNode;
                                newNode.Children.Add(nodes[i]);


                                nodeToRemove.Parent = newNode;
                                newNode.Children.Add(nodeToRemove);
                            }
                            else
                            {
                                TreeNode newNode = new TreeNode(null);

                                nodes[i].Parent = newNode;
                                newNode.Children.Add(nodes[i]);

                                nodeToRemove.Parent = newNode;
                                newNode.Children.Add(nodeToRemove);

                                tree = newNode;
                            }
                        }

                        found = true;
                        break;
                    }
                }
            }

            return tree;
        }

        /// <summary>
        /// Samples a random labelled topology according to the specified model.
        /// </summary>
        /// <param name="leafNames">The labels for the terminal nodes of the topology.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <param name="constraint">A tree to constrain the sampling. The topology produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the topology, using a constraint with <see cref="TreeNode.NullHypothesis.YHK"/> will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random labelled topology (branch lengths will all be set to <see cref="double.NaN"/>).</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="model"/> is neither <see cref="TreeNode.NullHypothesis.PDA"/> nor <see cref="TreeNode.NullHypothesis.YHK"/>.</exception>
        public static TreeNode LabelledTopology(IReadOnlyList<string> leafNames, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false, TreeNode constraint = null)
        {
            if (constraint == null)
            {
                TreeNode topology = UnlabelledTopology(leafNames.Count, model, rooted);

                AddLeafNames(topology, leafNames);

                return topology;
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
                    return LabelledTopology(leafNames, model, rooted, null);
                }

                if (model == TreeNode.NullHypothesis.YHK)
                {
                    List<int[][]> splits = NeighborJoining.GetSplits(constraint, sequenceIndices);

                    List<TreeNode> leaves = new List<TreeNode>(leafNames.Count);
                    List<HashSet<int>> underlyingLeaves = new List<HashSet<int>>();
                    HashSet<int> allLeaves = new HashSet<int>();

                    for (int i = 0; i < leafNames.Count; i++)
                    {
                        leaves.Add(new TreeNode(null) { Name = leafNames[i] });

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

                    if (rooted)
                    {
                        allLeaves.Add(-1);
                    }

                    int target = rooted ? 1 : 3;

                    while (leaves.Count > target)
                    {
                        int leaf1Index, leaf2Index;
                        HashSet<int> potentialSplitLeft, potentialSplitRight;

                        List<(int, int)> availablePairs = (from el in Enumerable.Range(0, leaves.Count) select (from el2 in Enumerable.Range(0, el) select (el, el2))).Aggregate(new List<(int, int)>(), (a, b) => { a.AddRange(b); return a; });

                        do
                        {
                            (leaf1Index, leaf2Index) = Sample(availablePairs);

                            potentialSplitRight = new HashSet<int>(underlyingLeaves[leaf1Index]);
                            potentialSplitRight.UnionWith(underlyingLeaves[leaf2Index]);

                            potentialSplitLeft = new HashSet<int>(allLeaves);
                            potentialSplitLeft.ExceptWith(potentialSplitRight);

                        } while (!NeighborJoining.IsCompatible(potentialSplitLeft, potentialSplitRight, splits));

                        TreeNode leaf1 = leaves[leaf1Index];
                        TreeNode leaf2 = leaves[leaf2Index];

                        TreeNode newNode = new TreeNode(null);
                        newNode.Children.Add(leaf1);
                        newNode.Children.Add(leaf2);
                        leaf1.Parent = newNode;
                        leaf2.Parent = newNode;

                        underlyingLeaves[Math.Min(leaf1Index, leaf2Index)].UnionWith(underlyingLeaves[Math.Max(leaf1Index, leaf2Index)]);
                        underlyingLeaves.RemoveAt(Math.Max(leaf1Index, leaf2Index));

                        leaves.RemoveAt(Math.Max(leaf1Index, leaf2Index));
                        leaves[Math.Min(leaf1Index, leaf2Index)] = newNode;
                    }

                    if (!rooted)
                    {
                        TreeNode root = new TreeNode(null);

                        leaves[0].Parent = root;
                        leaves[1].Parent = root;
                        leaves[2].Parent = root;

                        root.Children.Add(leaves[0]);
                        root.Children.Add(leaves[1]);
                        root.Children.Add(leaves[2]);

                        return root;
                    }
                    else
                    {
                        return leaves[0];
                    }
                }
                else if (model == TreeNode.NullHypothesis.PDA)
                {
                    TreeNode initialTree = constraint.Clone();

                    initialTree = ResolvePolytomies(initialTree, rooted);

                    List<TreeNode> leaves = initialTree.GetLeaves();

                    for (int i = 0; i < leaves.Count; i++)
                    {
                        availableNames.Remove(leaves[i].Name);
                    }

                    List<TreeNode> nodes;

                    if (rooted)
                    {
                        if (!initialTree.IsRooted())
                        {
                            initialTree = initialTree.GetRootedTree(initialTree.Children[0]);
                        }

                        nodes = initialTree.GetChildrenRecursive();
                    }
                    else
                    {
                        if (initialTree.IsRooted())
                        {
                            initialTree = initialTree.GetUnrootedTree();
                        }

                        nodes = initialTree.GetChildrenRecursive();
                        nodes.RemoveAt(0);
                    }


                    while (availableNames.Count > 0)
                    {
                        string name = Sample(availableNames);

                        int index = RandomNumberGenerator.Next(0, nodes.Count);

                        TreeNode selectedNode = nodes[index];

                        if (selectedNode.Children.Count == 0)
                        {
                            leaves.Remove(selectedNode);

                            selectedNode.Children.Add(new TreeNode(selectedNode) { Name = selectedNode.Name });
                            selectedNode.Children.Add(new TreeNode(selectedNode) { Name = name });
                            selectedNode.Name = "";
                            leaves.AddRange(selectedNode.Children);
                            nodes.AddRange(selectedNode.Children);
                        }
                        else
                        {
                            if (selectedNode.Parent != null)
                            {
                                TreeNode newNode = new TreeNode(selectedNode.Parent);
                                selectedNode.Parent.Children.Add(newNode);

                                TreeNode newLeaf = new TreeNode(newNode) { Name = name };
                                newNode.Children.Add(newLeaf);

                                selectedNode.Parent.Children.Remove(selectedNode);
                                selectedNode.Parent = newNode;
                                newNode.Children.Add(selectedNode);

                                nodes.Add(newNode);
                                nodes.Add(newLeaf);
                                leaves.Add(newLeaf);
                            }
                            else
                            {
                                TreeNode newNode = new TreeNode(null);
                                TreeNode newLeaf = new TreeNode(newNode) { Name = name };
                                newNode.Children.Add(newLeaf);

                                selectedNode.Parent = newNode;
                                newNode.Children.Add(selectedNode);

                                nodes.Add(newNode);
                                nodes.Add(newLeaf);
                                leaves.Add(newLeaf);

                                initialTree = newNode;
                            }

                        }
                    }

                    return initialTree;
                }
                else
                {
                    throw new ArgumentException("Invalid tree model");
                }
            }
        }

        /// <summary>
        /// Samples a random labelled topology according to the specified model.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the topology. Their names will be in the form <c>t1, t2, ..., tN</c>, where <c>N</c> is <paramref name="leafCount"/>.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <param name="constraint">A tree to constrain the sampling. The topology produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the topology, using a constraint with <see cref="TreeNode.NullHypothesis.YHK"/> will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random labelled topology (branch lengths will all be set to <see cref="double.NaN"/>).</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="model"/> is neither <see cref="TreeNode.NullHypothesis.PDA"/> nor <see cref="TreeNode.NullHypothesis.YHK"/>.</exception>
        public static TreeNode LabelledTopology(int leafCount, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false, TreeNode constraint = null)
        {
            string[] leafNames = new string[leafCount];

            for (int i = 0; i < leafCount; i++)
            {
                leafNames[i] = "t" + (i + 1).ToString();
            }

            return LabelledTopology(leafNames, model, rooted, constraint);
        }

        /// <summary>
        /// Samples a random labelled tree according to the specified model, using branch lengths drawn from the supplied distribution.
        /// </summary>
        /// <param name="leafNames">The labels for the terminal nodes of the topology.</param>
        /// <param name="branchLengthDistribution">The continuous univariate distribution from which the branch lengths will be drawn.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <param name="constraint">A tree to constrain the sampling. The topology produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the topology, using a constraint with <see cref="TreeNode.NullHypothesis.YHK"/> will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random unlabelled tree, with branch lengths.</returns>
        public static TreeNode LabelledTree(IReadOnlyList<string> leafNames, IContinuousDistribution branchLengthDistribution, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false, TreeNode constraint = null)
        {
            TreeNode topology = LabelledTopology(leafNames, model, rooted, constraint);

            List<TreeNode> nodes = topology.GetChildrenRecursive();

            double[] branchLengths = new double[nodes.Count - 1];

            branchLengthDistribution.Samples(branchLengths);

            for (int i = 1; i < nodes.Count; i++)
            {
                nodes[i].Length = branchLengths[i - 1];
            }

            return topology;
        }

        /// <summary>
        /// Samples a random labelled tree according to the specified model, using branch lengths drawn from the supplied distribution.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the topology. Their names will be in the form <c>t1, t2, ..., tN</c>, where <c>N</c> is <paramref name="leafCount"/>.</param>
        /// <param name="branchLengthDistribution">The continuous univariate distribution from which the branch lengths will be drawn.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <param name="constraint">A tree to constrain the sampling. The topology produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the topology, using a constraint with <see cref="TreeNode.NullHypothesis.YHK"/> will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random unlabelled tree, with branch lengths.</returns>
        public static TreeNode LabelledTree(int leafCount, IContinuousDistribution branchLengthDistribution, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false, TreeNode constraint = null)
        {
            string[] leafNames = new string[leafCount];

            for (int i = 0; i < leafCount; i++)
            {
                leafNames[i] = "t" + (i + 1).ToString();
            }

            return LabelledTree(leafNames, branchLengthDistribution, model, rooted, constraint);
        }

        /// <summary>
        /// Samples a random labelled tree according to the specified model, using branch lengths drawn from a Uniform(0, 1) distribution.
        /// </summary>
        /// <param name="leafNames">The labels for the terminal nodes of the topology.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <param name="constraint">A tree to constrain the sampling. The topology produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the topology, using a constraint with <see cref="TreeNode.NullHypothesis.YHK"/> will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random unlabelled tree, with branch lengths.</returns>
        public static TreeNode LabelledTree(IReadOnlyList<string> leafNames, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false, TreeNode constraint = null)
        {
            return LabelledTree(leafNames, new ContinuousUniform(0, 1, RandomNumberGenerator), model, rooted, constraint);
        }

        /// <summary>
        /// Samples a random labelled tree according to the specified model, using branch lengths drawn from a Uniform(0, 1) distribution.
        /// </summary>
        /// <param name="leafCount">The number of terminal nodes in the topology. Their names will be in the form <c>t1, t2, ..., tN</c>, where <c>N</c> is <paramref name="leafCount"/>.</param>
        /// <param name="model">The model to use for growing the tree.</param>
        /// <param name="rooted">A <see cref="bool"/> indicating whether the tree should be rooted or not.</param>
        /// <param name="constraint">A tree to constrain the sampling. The topology produced by this method will be compatible with this tree. The constraint tree can be multifurcating.
        /// Please note that, as the constraint is applied at every step while growing the topology, using a constraint with <see cref="TreeNode.NullHypothesis.YHK"/> will bias the sampled topology distribution.</param>
        /// <returns>A <see cref="TreeNode"/> object containing a random unlabelled tree, with branch lengths.</returns>
        public static TreeNode LabelledTree(int leafCount, TreeNode.NullHypothesis model = TreeNode.NullHypothesis.PDA, bool rooted = false, TreeNode constraint = null)
        {
            return LabelledTree(leafCount, new ContinuousUniform(0, 1, RandomNumberGenerator), model, rooted, constraint);
        }
    }
}
