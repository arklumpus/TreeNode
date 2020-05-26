# TreeNode C# library

<div style="float:right"><img src="Logo.svg" width="256"></div>

This is the documentation website for the __TreeNode C# library__.

__TreeNode__ is a library for reading, writing and manipulating phylogenetic trees in C# and R. It can open and create files in the most common phylogenetic formats (i.e. Newick/New Hampshire and NEXUS) and adds support for two new formats, the [Newick-with-Attributes](https://github.com/arklumpus/TreeNode/blob/master/NWKA.md) and [Binary format](https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md).

The __TreeNode C# library__, in addition to providing methods to read and write phylogenetic tree files, also includes methods to manipulate the resulting trees (e.g. to reroot the tree, compute a consensus tree, find the last common ancestor of a group, etc.).

TreeNode is released under the [GPLv3](https://www.gnu.org/licenses/gpl-3.0.html) licence.

## Getting started

The TreeNode C# library targets .NET Standard 2.1, thus it can be used in projects that target .NET Standard 2.1+ and .NET Core 3.0+, as well as Mono and Xamarin.

To use the library in your project, you should install the [TreeNode NuGet package](https://www.nuget.org/packages/TreeNode/).

## Usage

The [Examples](https://github.com/arklumpus/TreeNode/tree/master/CSharp/Examples) project in the [TreeNode GitHub repository](https://github.com/arklumpus/TreeNode) contains an example C# .NET Core console program showing some of the capabilities of the library.

The `PhyloTree` namespace contains the `TreeNode` class, which is used to represent nodes in a tree. `TreeNode` does not distinguish between internal nodes, tips or even whole trees (except when looking at some specific properties - e.g. a tip will not have any `Children`, and the root node of the tree will not have any `Parent`). This makes it possible to navigate the tree in an intuitive manner: for example, the ancestor of a `TreeNode` can be accessed using its `Parent` property (which is itself a `TreeNode`) and the descendants of a node can be found as the node's `Children` (which is a `List<TreeNode>`).

A full list of the information that can be extracted and the manipulations that can be performed on `TreeNode` objects can be obtained by looking at the methods and properties of the `TreeNode` class in this website.

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