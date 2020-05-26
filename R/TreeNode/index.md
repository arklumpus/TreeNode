# TreeNode R package

<div style="float:right"><img src="../Logo.svg" width="256"></div>

This is the documentation website for the __TreeNode R package__.

__TreeNode__ is a library for reading, writing and manipulating phylogenetic trees in C# and R. It can open and create files in the most common phylogenetic formats (i.e. Newick/New Hampshire and NEXUS) and adds support for two new formats, the [Newick-with-Attributes](https://github.com/arklumpus/TreeNode/blob/master/NWKA.md) and [Binary format](https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md).

The __TreeNode R package__ can be used to read and write files in these two formats. The tree produced by this package have the same structure (and, thus, are compatible with) the popular phylogenetics package [ape](https://cran.r-project.org/web/packages/ape/index.html).

TreeNode is released under the [GPLv3](https://www.gnu.org/licenses/gpl-3.0.html) licence.

## Getting started

You can install the TreeNode R package from GitHub using the `install_github` function provided by the package `devtools`.

First, install devtools (if you have not done so already):

```R
install.packages("devtools")
```

Then, you can install TreeNode by using the following command:

```R
devtools::install_github("arklumpus/TreeNode/R")
```

## Usage

The TreeNode R package provides functions to read and write phylogenetic trees in the NWKA and binary formats.

The [`read_nwka_tree()`](https://arklumpus.github.io/TreeNode/R/reference/read_nwka_tree.html) function can be used to read a file (or a string) containing tree(s) in NWKA format. The [`read_nwka_nexus()`](https://arklumpus.github.io/TreeNode/R/reference/read_nwka_nexus.html) function can be used to read files in NEXUS format, interpreting the trees that they contain as NWKA trees.

The [`read_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/read_binary_trees.html) function can be used to read a file in binary format. The [`read_one_binary_tree()`](https://arklumpus.github.io/TreeNode/R/reference/read_one_binary_tree.html) function can be used to read just one tree from the file.

The [`write_nwka_tree()`](https://arklumpus.github.io/TreeNode/R/reference/write_nwka_tree.html) function can be used to create a NWKA or Newick file (or string) starting from a `phylo` or `multiPhylo` object. The [`write_nwka_nexus()`](https://arklumpus.github.io/TreeNode/R/reference/write_nwka_nexus.html) function can be used to create a NEXUS file in which the trees are stored in NWKA format.

The [`write_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/write_binary_trees.html) function can be used to create a tree file in binary format starting from a `phylo` or `multiPhylo` object. The [`begin_writing_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/begin_writing_binary_trees.html), [`keep_writing_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/keep_writing_binary_trees.html) and [`finish_writing_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/finish_writing_binary_trees.html) functions can be used to write one tree at a time to the file.

Detailed documentation and examples for this package are available in the R online help (e.g., by typing `?read_nwka_tree` at the R prompt) or in the [function description pages](reference).

