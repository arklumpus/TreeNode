# TreeNode: a library to read, write and manipulate phylogenetic trees

<img src="Logo.svg" width="256" align="right">

__TreeNode__ is a library for reading, writing and manipulating phylogenetic trees in C# and R. It can open and create files in the most common phylogenetic formats (i.e. Newick/New Hampshire and NEXUS) and adds support for two new formats, the Newick-with-Attributes and Binary format.

The __Newick-with-Attributes (NWKA) format__ is an extension of the Newick format which makes it possible to specify any number of arbitrary attributes to associate to each node in the tree. The NWKA format is backwards compatible with the Newick format (all Newick trees are valid NWKA trees, and NWKA trees produced by TreeNode are valid Newick trees). A full description of this format is available [in this repository](NWKA.md).

The __Binary format__ makes it possible to read just one tree from a file, without having to parse the whole file. Note that the name refers to the way the data are stored in the file (as opposed to text-based formats) - multifurcating trees can be represented in this format without any issue. A full description of this format is also available [in this repository](BinaryTree.md).

The __TreeNode R package__ can be used to read and write files in these two formats. The tree produced by this package have the same structure (and, thus, are compatible with) the popular phylogenetics package [ape](https://cran.r-project.org/web/packages/ape/index.html).

The __TreeNode C# library__, in addition to providing methods to read and write phylogenetic tree files, also includes methods to manipulate the resulting trees (e.g. to reroot the tree, compute a consensus tree, find the last common ancestor of a group, etc.).

TreeNode is released under the [GPLv3](https://www.gnu.org/licenses/gpl-3.0.html) licence.

## Getting started

### R

You can install the TreeNode R package from GitHub using the `install_github` function provided by the package `devtools`.

First, install devtools (if you have not done so already):

```R
install.packages("devtools")
```

Then, you can install TreeNode by using the following command:

```R
devtools::install_github("arklumpus/TreeNode", subdir="R/TreeNode")
```

The TreeNode R package is actually a very thin wrapper over some C++ functions, thus you may need some additional tools to install it (e.g. [Rtools](https://cran.r-project.org/bin/windows/Rtools/) on Windows and [Xcode](https://developer.apple.com/xcode/) on macOS).

### C#

The TreeNode C# library targets .NET Standard 2.1, thus it can be used in projects that target .NET Standard 2.1+ and .NET Core 3.0+, as well as Mono and Xamarin.

To use the library in your project, you should install the [TreeNode NuGet package](https://www.nuget.org/packages/TreeNode/).

## Usage

### Documentation

Interactive documentation for the TreeNode library can be accessed from the [documentation website](https://arklumpus.github.io/TreeNode/). PDF reference manuals are also available for the [TreeNode R package](https://arklumpus.github.io/TreeNode/TreeNode-R.pdf) and the [TreeNode C# library](https://arklumpus.github.io/TreeNode/TreeNode-Csharp.pdf).

### R

The R package provides functions to read and write phylogenetic trees in the NWKA and binary formats.

The [`read_nwka_tree()`](https://arklumpus.github.io/TreeNode/R/reference/read_nwka_tree.html) function can be used to read a file (or a string) containing tree(s) in NWKA format. The [`read_nwka_nexus()`](https://arklumpus.github.io/TreeNode/R/reference/read_nwka_nexus.html) function can be used to read files in NEXUS format, interpreting the trees that they contain as NWKA trees.

The [`read_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/read_binary_trees.html) function can be used to read a file in binary format. The [`read_one_binary_tree()`](https://arklumpus.github.io/TreeNode/R/reference/read_one_binary_tree.html) function can be used to read just one tree from the file.

The [`write_nwka_tree()`](https://arklumpus.github.io/TreeNode/R/reference/write_nwka_tree.html) function can be used to create a NWKA or Newick file (or string) starting from a `phylo` or `multiPhylo` object. The [`write_nwka_nexus()`](https://arklumpus.github.io/TreeNode/R/reference/write_nwka_nexus.html) function can be used to create a NEXUS file in which the trees are stored in NWKA format.

The [`write_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/write_binary_trees.html) function can be used to create a tree file in binary format starting from a `phylo` or `multiPhylo` object. The [`begin_writing_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/begin_writing_binary_trees.html), [`keep_writing_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/keep_writing_binary_trees.html) and [`finish_writing_binary_trees()`](https://arklumpus.github.io/TreeNode/R/reference/finish_writing_binary_trees.html) functions can be used to write one tree at a time to the file.

Detailed documentation and examples for this package is available in the R online help (e.g., by typing `?read_nwka_tree` at the R prompt) or in the [documentation website](https://arklumpus.github.io/TreeNode/R/).

### C#

The full documentation for the C# library is available at the [documentation website](https://arklumpus.github.io/TreeNode/Csharp/). The [Examples](CSharp/Examples) project in this repository contains an example C# .NET Core console program showing some of the capabilities of the library.

The `PhyloTree` namespace contains the `TreeNode` class, which is used to represent nodes in a tree. `TreeNode` does not distinguish between internal nodes, tips or even whole trees (except when looking at some specific properties - e.g. a tip will not have any `Children`, and the root node of the tree will not have any `Parent`). This makes it possible to navigate the tree in an intuitive manner: for example, the ancestor of a `TreeNode` can be accessed using its `Parent` property (which is itself a `TreeNode`) and the descendants of a node can be found as the node's `Children` (which is a `List<TreeNode>`).

A full list of the information that can be extracted and the manipulations that can be performed on `TreeNode` objects can be obtained by looking at the methods and properties of the [`TreeNode` class in the documentation website](https://arklumpus.github.io/TreeNode/Csharp/class_phylo_tree_1_1_tree_node.html).

In addition to this, the `PhyloTree` namespace contains the `Phylotree.Formats` namespace. The three classes in this namespace (`NWKA`, `NEXUS` and `BinaryTree`) contain methods that can be used to read and write `TreeNode` objects to files in the respective format.

Each of these classes offers (at least) the following methods (with additional optional arguments):

```Csharp
    //Methods to read trees
    IEnumerable<TreeNode> ParseTrees(string inputFile);
    IEnumerable<TreeNode> ParseTrees(Stream inputStream);

    List<TreeNode> ParseAllTrees(string inputFile);
    List<TreeNode> ParseAllTrees(Stream inputStream);


    //Methods to write trees
    void WriteTree(TreeNode tree, string outputFile);
    void WriteTree(TreeNode tree, Stream outputStream);

    void WriteAllTrees(IEnumerable<TreeNode> trees, string outputFile);
    void WriteAllTrees(IEnumerable<TreeNode> trees, Stream outputStream);

    void WriteAllTrees(List<TreeNode> trees, string outputFile);
    void WriteAllTrees(List<TreeNode> trees, Stream outputStream);
```

The `ParseTrees` methods can be used to read trees off a file or a `Stream`, without having to load them completely into memory. This can be useful if each tree only needs to be processed briefly. The `ParseAllTrees` methods instead load all the trees from the file into memory.

The `WriteTree` methods are used to write a single tree to a file or a stream, while the `WriteAllTrees` methods write a collection of trees.

In addition to this, the library also provides the `TreeCollection` class, which represents a collection of trees, much like a `List<TreeNode>`. However, a `TreeCollection` can also be created by passing a stream of trees in binary format to it: in this case, the `TreeCollection` will only parse trees from the stream when necessary, thus reducing the amount of memory that is necessary to store them.

The key feature of `TreeCollection` is that this is done _transparently_: accessing an element of the collection, e.g. by using `treeCollection[i]`, will automatically perform all the reading and parsing operations from the stream to produce the `TreeNode` that is returned. This makes it possible to have an "agnostic" interface that behaves in the same way whether the trees in the collection have been completely loaded into memory or not.

## Source code

The source code for the TreeNode [R package](R/TreeNode) and [C# library](CSharp) is accessible from the respective folders in this repository and can be downloaded from the [Releases](https://github.com/arklumpus/TreeNode/releases) page.
