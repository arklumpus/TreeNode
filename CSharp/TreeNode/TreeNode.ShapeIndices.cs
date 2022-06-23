using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PhyloTree
{
    public partial class TreeNode
    {
        /// <summary>
        /// Null hypothesis for normalising tree shape indices.
        /// </summary>
        public enum NullHypothesis
        {
            /// <summary>
            /// Yule-Harding-Kingman model (also known as Yule model or Equal-rates Markov model). At each step in growing the tree, a new leaf is added as a sibling to an existing leaf.
            /// </summary>
            YHK,
            
            /// <summary>
            /// Proportional to distinguished arrangements model (also known as uniform model). At each step in growing the tree, a new leaf is added as a sibling to an existing (possibly internal) node.
            /// </summary>
            PDA,
            
            /// <summary>
            /// Do not perform any normalisation.
            /// </summary>
            None
        }

        /// <summary>
        /// Compute the depth of the node (number of branches from this node until the root node).
        /// </summary>
        /// <returns>The depth of the node.</returns>
        public int GetDepth()
        {
            return this.GetDepth(0);
        }

        private int GetDepth(int currentDepth = 0)
        {
            if (this.Parent == null)
            {
                return currentDepth;
            }

            return this.Parent.GetDepth(currentDepth + 1);
        }

        /// <summary>
        /// Computes the Sackin index of the tree (sum of the leaf depths).
        /// </summary>
        /// <param name="model">If this is <see cref="NullHypothesis.None"/>, the raw Sackin index is returned. If this is <see cref="NullHypothesis.YHK"/> or <see cref="NullHypothesis.PDA"/>, the Sackin
        /// index is normalised with respect to the corresponding null tree model (which makes scores comparable across trees of different sizes).</param>
        /// <returns>The Sackin index of the tree, either as a raw value, or normalised according to the selected null tree model.</returns>
        public double SackinIndex(NullHypothesis model = NullHypothesis.None)
        {
            List<double> leafDepths = new List<double>();

            List<TreeNode> leaves = this.GetLeaves();

            foreach (TreeNode leaf in leaves)
            {
                leafDepths.Add(leaf.GetDepth());
            }

            double averageLeafDepth = leafDepths.Average();

            int sackinIndex = (int)leafDepths.Sum();

            switch (model)
            {
                case NullHypothesis.None:
                    return sackinIndex;
                case NullHypothesis.YHK:
                    return (sackinIndex - 2 * leaves.Count * (from el in Enumerable.Range(2, leaves.Count - 1) select 1.0 / el).Sum()) / leaves.Count;
                case NullHypothesis.PDA:
                    return sackinIndex / Math.Pow(leaves.Count, 1.5);
            }

            return double.NaN;
        }


        private (int score, int leaves) ComputeCollessInner()
        {
            if (this.Children.Count > 0)
            {
                (int score1, int leaves1) = this.Children[0].ComputeCollessInner();
                (int score2, int leaves2) = this.Children[1].ComputeCollessInner();

                return (score1 + score2 + Math.Abs(leaves1 - leaves2), leaves1 + leaves2);
            }
            else
            {
                return (0, 1);
            }
        }

        /// <summary>
        /// Computes the expected value of the Colless index under the YHK model.
        /// </summary>
        /// <param name="numberOfLeaves">The number of leaves in the tree.</param>
        /// <returns>The expected value of the Colless index for a tree with the specified <paramref name="numberOfLeaves"/>.</returns>
        /// <remarks>Proof in DOI: 10.1214/105051606000000547</remarks>
        public static double GetCollessExpectationYHK(int numberOfLeaves)
        {
            static double tN(int n)
            {
                if (n % 2 == 0)
                {
                    return (n - 2) / 4.0;
                }
                else
                {
                    return (n - 1) * (n - 1) / (4.0 * n);
                }
            }

            double sum = 0;

            for (int k = 1; k < numberOfLeaves; k++)
            {
                sum += (k - 1 - 2 * tN(k)) / ((k + 1) * (k + 2));
            }

            return numberOfLeaves - 1 - 2 * tN(numberOfLeaves) + 2 * (numberOfLeaves + 1) * sum;
        }

        /// <summary>
        /// Compute the Colless index of the tree.
        /// </summary>
        /// <param name="model">If this is <see cref="NullHypothesis.None"/>, the raw Colless index is returned. If this is <see cref="NullHypothesis.YHK"/> or <see cref="NullHypothesis.PDA"/>, the Colless
        /// index is normalised with respect to the corresponding null tree model (which makes scores comparable across trees of different sizes).</param>
        /// <param name="yhkExpectation">If <paramref name="model"/> is <see cref="NullHypothesis.YHK"/>, you can optionally use this parameter to provide a pre-computed value for the expected value of the
        /// Colless index under the YHK model. This is useful to save time if you need to compute the Colless index of many trees with the same number of leaves. If this is <see cref="double.NaN"/>, the
        /// expected value under the YHK model is computed by this method.</param>
        /// <returns>The Colless index of the tree.</returns>
        public double CollessIndex(NullHypothesis model = NullHypothesis.None, double yhkExpectation = double.NaN)
        {
            (int score, int leaves) = this.ComputeCollessInner();

            switch (model)
            {
                case NullHypothesis.None:
                    return score;
                case NullHypothesis.YHK:
                    if (double.IsNaN(yhkExpectation))
                    {
                        yhkExpectation = GetCollessExpectationYHK(leaves);
                    }
                    return (score - yhkExpectation) / leaves;
                case NullHypothesis.PDA:
                    return score / Math.Pow(leaves, 1.5);
            }

            return double.NaN;
        }

        /// <summary>
        /// Computes the number of cherries in the tree.
        /// </summary>
        /// <param name="model">If this is <see cref="NullHypothesis.None"/>, the raw number of cherries is returned. If this is <see cref="NullHypothesis.YHK"/> or <see cref="NullHypothesis.PDA"/>, the number
        /// of cherries is normalised with respect to the corresponding null tree model (which makes scores comparable across trees of different sizes).</param>
        /// <returns>The number of cherries in the tree.</returns>
        /// <remarks>Proofs in DOI: 10.1016/S0025-5564(99)00060-7</remarks>
        public double NumberOfCherries(NullHypothesis model = NullHypothesis.None)
        {
            List<TreeNode> leaves = this.GetLeaves();

            int numberOfCherries = 0;

            for (int i = 0; i < leaves.Count; i++)
            {
                if (leaves[i].Parent.Children.Count == 2 && leaves[i].Parent.Children[0].Children.Count == 0 && leaves[i].Parent.Children[1].Children.Count == 0)
                {
                    numberOfCherries++;
                }
            }

            numberOfCherries /= 2;

            switch (model)
            {
                case NullHypothesis.None:
                    return numberOfCherries;

                case NullHypothesis.YHK:
                    return (numberOfCherries - leaves.Count / 3.0) / Math.Sqrt(2.0 * leaves.Count / 45.0);

                case NullHypothesis.PDA:
                    double mu = (double)leaves.Count * (leaves.Count - 1) / (2.0 * (2 * leaves.Count - 5));
                    double sigmaSq = (double)leaves.Count * (leaves.Count - 1) * (leaves.Count - 4) * (leaves.Count - 5) / (2.0 * (2 * leaves.Count - 5) * (2 * leaves.Count - 5) * (2 * leaves.Count - 7));
                    return (numberOfCherries - mu) / Math.Sqrt(sigmaSq);
            }

            return double.NaN;
        }
    }
}
