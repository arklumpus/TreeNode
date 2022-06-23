using PhyloTree.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

            List<(string[], string[], double)> splits1 = (from el in tree1.GetSplits()
                                                  select (
                                                  (from el1 in el.side1 where el1 == null || !string.IsNullOrEmpty(el1.Name) select el1 == null ? "@Root" : el1.Name).ToArray(),
                                                  (from el2 in el.side2 where el2 == null || !string.IsNullOrEmpty(el2.Name) select el2 == null ? "@Root" : el2.Name).ToArray(),
                                                  el.branchLength
                                                  )).ToList();

            List<(string[], string[], double)> splits2 = (from el in tree2.GetSplits()
                                                  select (
                                                  (from el1 in el.side1 where el1 == null || !string.IsNullOrEmpty(el1.Name) select el1 == null ? "@Root" : el1.Name).ToArray(),
                                                  (from el2 in el.side2 where el2 == null || !string.IsNullOrEmpty(el2.Name) select el2 == null ? "@Root" : el2.Name).ToArray(),
                                                  el.branchLength
                                                  )).ToList();

            static bool AreSameSplit((string[], string[], double) split1, (string[], string[], double) split2)
            {
                if (split1.Item1.Length == split1.Item2.Length || split2.Item1.Length == split2.Item2.Length)
                {
                    if (split1.Item1.Length == split1.Item2.Length && split2.Item1.Length == split2.Item2.Length)
                    {
                        return AreSameSplit2(split1.Item1, split1.Item2, split2.Item1, split2.Item2) || AreSameSplit2(split1.Item1, split1.Item2, split2.Item2, split2.Item1);
                    }
                    else
                    {
                        return false;
                    }
                }

                string[] split11, split12;

                if (split1.Item1.Length > split1.Item2.Length)
                {
                    split11 = split1.Item1;
                    split12 = split1.Item2;
                }
                else
                {
                    split11 = split1.Item2;
                    split12 = split1.Item1;
                }

                string[] split21, split22;

                if (split2.Item1.Length > split2.Item2.Length)
                {
                    split21 = split2.Item1;
                    split22 = split2.Item2;
                }
                else
                {
                    split21 = split2.Item2;
                    split22 = split2.Item1;
                }

                return AreSameSplit2(split11, split12, split21, split22);
            }


            static bool AreSameSplit2(string[] split11, string[] split12, string[] split21, string[] split22)
            {
                if (split11.Length != split21.Length || split12.Length != split22.Length)
                {
                    return false;
                }

                HashSet<string> union2 = new HashSet<string>(split12.Length);

                for (int i = 0; i < split12.Length; i++)
                {
                    union2.Add(split12[i]);
                    union2.Add(split22[i]);
                }

                if (union2.Count != split12.Length)
                {
                    return false;
                }

                HashSet<string> union1 = new HashSet<string>();

                for (int i = 0; i < split11.Length; i++)
                {
                    union1.Add(split11[i]);
                    union1.Add(split21[i]);
                }

                return union1.Count == split11.Length;
            }


            bool?[] matched1 = new bool?[splits1.Count];
            bool?[] matched2 = new bool?[splits2.Count];

            for (int i = 0; i < splits1.Count; i++)
            {
                matched1[i] = false;

                for (int j = 0; j < splits2.Count; j++)
                {
                    if (AreSameSplit(splits1[i], splits2[j]))
                    {
                        matched1[i] = true;
                        matched2[j] = true;
                        break;
                    }
                }
            }

            for (int j = 0; j < splits2.Count; j++)
            {
                if (matched2[j] == null)
                {
                    matched2[j] = false;

                    for (int i = 0; i < splits1.Count; i++)
                    {
                        if (AreSameSplit(splits1[i], splits2[j]))
                        {
                            matched2[j] = true;
                            break;
                        }
                    }
                }
            }

            if (!weighted)
            {
                return matched1.Count(x => x == false) + matched2.Count(x => x == false);
            }
            else
            {
                double tbr = 0;

                for (int i = 0; i < matched1.Length; i++)
                {
                    if (matched1[i] == false)
                    {
                        tbr += splits1[i].Item3;
                    }
                }

                for (int i = 0; i < matched2.Length; i++)
                {
                    if (matched2[i] == false)
                    {
                        tbr += splits2[i].Item3;
                    }
                }

                return tbr;
            }
        }
    }
}
