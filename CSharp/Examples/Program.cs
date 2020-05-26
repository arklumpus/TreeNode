using PhyloTree;
using PhyloTree.Extensions;
using PhyloTree.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Examples
{
    class Program
    {
        static void Main()
        {
            //Parse a tree in NWKA format, printing debug information
            TreeNode tree = NWKA.ParseTree(@"(((('Homo sapiens'[rank=species])'Homo'[rank=genus])
'Hominina'[rank=subtribe],(('Pan paniscus'[rank=species],'Pan troglodytes'
[rank=species])'Pan'[rank=genus])'Panina'[rank=subtribe])'Hominini'[rank=tribe],
(('Gorilla gorilla'[rank=species],'Gorilla beringei'[rank=species])'Gorilla'
[rank=genus])'Gorillini'[rank=tribe])'Homininae'[rank=subfamily];", debug: true);


            Console.WriteLine();
            Console.WriteLine();

            //Leaf (tip) nodes in the tree
            List<string> leafNames = tree.GetLeafNames();
            Console.WriteLine("The tree has {0} leaves:", leafNames.Count);
            foreach (string name in leafNames)
            {
                Console.WriteLine(" - {0}", name);
            }

            Console.WriteLine();

            //Last common ancestor
            string taxon1 = "Homo sapiens";
            string taxon2 = "Pan paniscus";
            TreeNode LCA = tree.GetLastCommonAncestor(taxon1, taxon2);
            Console.WriteLine("The last common ancestor of {0} and {1} is {2}.", taxon1, taxon2, LCA.Name);

            Console.WriteLine();

            //Attributes
            Console.WriteLine("The root node has the following attributes:");
            foreach (KeyValuePair<string, object> attribute in tree.Attributes)
            {
                Console.WriteLine(" - {0}: {1}", attribute.Key, attribute.Value);
            }

            //Load some trees
            List<TreeNode> trees = BinaryTree.ParseAllTrees(@"manyTrees.tbi");

            //Majority-rule consensus tree
            TreeNode consensus = trees.GetConsensus(false, false, 0.5, true);

            //Write the tree to a NEXUS file
            NEXUS.WriteTree(consensus, "outputFile.nex");
        }
    }
}
