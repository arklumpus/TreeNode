using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace PhyloTree.Formats
{
    /// <summary>
    /// Contains methods to read and write trees in the NCBI ASN.1 binary format.<br/>
    /// <b>Note</b>: this is a hackish reverse-engineering of the NCBI binary ASN format. A lot of this is derived by assumptions and observations.
    /// </summary>
    public static class NcbiAsnBer
    {
        /// <summary>
        /// Tags indicating object types.
        /// </summary>
        internal enum ByteTags
        {
            /// <summary>
            /// The start of a generic object. The object must be closed by two <see cref="EndOfContext"/> bytes.
            /// </summary>
            ObjectStart = 0x30,

            /// <summary>
            /// The start of an array. The array must be closed by two <see cref="EndOfContext"/> bytes.
            /// </summary>
            ArrayStart = 0x31,

            /// <summary>
            /// A length value indicating that the object has an unspecified length.
            /// </summary>
            UndefinedLength = 0x80,

            /// <summary>
            /// Tag used to close objects with unspecified length. Two of these are required to close each object.
            /// </summary>
            EndOfContext = 0x00,

            /// <summary>
            /// Indicates that the object is a string (UTF8-encoded, probably).
            /// </summary>
            String = 0x1A,

            /// <summary>
            /// Indicates that the object is an integer.
            /// </summary>
            Int = 0x02,

            /// <summary>
            /// Specifies the <c>treetype</c> property defined in the NCBI ASN.1 tree format.
            /// </summary>
            TreeType = 0xA0,

            /// <summary>
            /// Specifies the <c>fdict</c> property (feature dictionary) defined in the NCBI ASN.1 tree format.
            /// </summary>
            FDict = 0xA1,

            /// <summary>
            /// Specifies the <c>nodes</c> property (list of nodes) defined in the NCBI ASN.1 tree format.
            /// </summary>
            Nodes = 0xA2,

            /// <summary>
            /// Specifies the <c>label</c> property defined in the NCBI ASN.1 tree format.
            /// </summary>
            Label = 0xA3,

            /// <summary>
            /// Specifies the ID of a feature.
            /// </summary>
            FeatureId = 0xA0,

            /// <summary>
            /// Specifies the name of a feature.
            /// </summary>
            FeatureName = 0xA1,

            /// <summary>
            /// Specifies the ID of a node.
            /// </summary>
            NodeId = 0xA0,

            /// <summary>
            /// Specifies the parent of a node.
            /// </summary>
            NodeParent = 0xA1,

            /// <summary>
            /// Specifies the features of a node.
            /// </summary>
            NodeFeatures = 0xA2,

            /// <summary>
            /// Specifies the ID of a feature of a node.
            /// </summary>
            NodeFeatureId = 0xA0,

            /// <summary>
            /// Specifies the value of a fetuare of a node.
            /// </summary>
            NodeFeatureValue = 0xA1
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 binary format file. Note that the tree can only contain a single file, and this method will always return a collection with a single element.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <returns>A <see cref="IEnumerable{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static IEnumerable<TreeNode> ParseTrees(string inputFile)
        {
            yield return ParseAllTrees(inputFile)[0];
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 binary format file. Note that the tree can only contain a single file, and this method will always return a collection with a single element.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <returns>A <see cref="IEnumerable{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static IEnumerable<TreeNode> ParseTrees(Stream inputStream, bool keepOpen = false)
        {
            yield return ParseAllTrees(inputStream, keepOpen)[0];
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 binary format file. Note that the tree can only contain a single file, and this method will always return a list with a single element.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <returns>A <see cref="List{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static List<TreeNode> ParseAllTrees(string inputFile)
        {
            using FileStream stream = File.OpenRead(inputFile);
            using BinaryReader reader = new BinaryReader(stream);
            return new List<TreeNode>() { ParseTree(reader) };
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 binary format file. Note that the tree can only contain a single file, and this method will always return a list with a single element.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <returns>A <see cref="List{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static List<TreeNode> ParseAllTrees(Stream inputStream, bool keepOpen = false)
        {
            using BinaryReader reader = new BinaryReader(inputStream, Encoding.UTF8, keepOpen);
            return new List<TreeNode>() { ParseTree(reader) };
        }

        /// <summary>
        /// Parses a tree from a <see cref="BinaryReader"/> reading a stream in NCBI ASN.1 binary format into a <see cref="TreeNode"/> object.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> that reads a stream in NCBI ASN.1 binary format.</param>
        /// <returns>The parsed <see cref="TreeNode"/> object.</returns>
        public static TreeNode ParseTree(BinaryReader reader)
        {
            Contract.Requires(reader != null);

            byte currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.ObjectStart);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.UndefinedLength);

            currByte = reader.ReadByte();

            string treetype = null;

            if (currByte == (byte)ByteTags.TreeType)
            {
                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.UndefinedLength);

                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.String);

                int length = ReadLength(reader);

                treetype = ReadString(reader, length);

                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.EndOfContext);
                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.EndOfContext);

                currByte = reader.ReadByte();
            }

            AssertByte(currByte, ByteTags.FDict);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.UndefinedLength);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.ArrayStart);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.UndefinedLength);

            Dictionary<int, string> features = new Dictionary<int, string>();

            bool finishedFeatures = false;

            while (!finishedFeatures)
            {
                currByte = reader.ReadByte();

                if (currByte == (byte)ByteTags.ObjectStart)
                {
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.UndefinedLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.FeatureId);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.UndefinedLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.Int);

                    int idLength = ReadLength(reader);
                    int id = ReadInt(reader, idLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.FeatureName);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.UndefinedLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.String);

                    int nameLength = ReadLength(reader);
                    string name = ReadString(reader, nameLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);

                    features[id] = name;
                }
                else
                {
                    AssertByte(currByte, ByteTags.EndOfContext);
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);

                    finishedFeatures = true;
                }
            }

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.EndOfContext);
            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.EndOfContext);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.Nodes);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.UndefinedLength);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.ArrayStart);

            currByte = reader.ReadByte();
            AssertByte(currByte, ByteTags.UndefinedLength);

            bool finishedNodes = false;
            Dictionary<int, (TreeNode node, int? parent)> nodes = new Dictionary<int, (TreeNode node, int? parent)>();

            while (!finishedNodes)
            {
                currByte = reader.ReadByte();

                if (currByte == (byte)ByteTags.ObjectStart)
                {
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.UndefinedLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.NodeId);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.UndefinedLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.Int);

                    int idLength = ReadLength(reader);
                    int id = ReadInt(reader, idLength);

                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);

                    currByte = reader.ReadByte();

                    int? parent = null;

                    TreeNode node = new TreeNode(null);

                    if (currByte == (byte)ByteTags.NodeParent)
                    {
                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.UndefinedLength);

                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.Int);

                        int parentLength = ReadLength(reader);
                        parent = ReadInt(reader, parentLength);

                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.EndOfContext);
                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.EndOfContext);

                        currByte = reader.ReadByte();
                    }

                    if (currByte == (byte)ByteTags.NodeFeatures)
                    {
                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.UndefinedLength);

                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.ArrayStart);

                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.UndefinedLength);

                        bool finishedNodeFeatures = false;

                        while (!finishedNodeFeatures)
                        {
                            currByte = reader.ReadByte();

                            if (currByte == (byte)ByteTags.ObjectStart)
                            {
                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.UndefinedLength);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.NodeFeatureId);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.UndefinedLength);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.Int);

                                int featureIdLength = ReadLength(reader);
                                int featureId = ReadInt(reader, featureIdLength);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.EndOfContext);
                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.EndOfContext);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.NodeFeatureValue);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.UndefinedLength);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.String);

                                int featureValueLength = ReadLength(reader);
                                string featureValue = ReadString(reader, featureValueLength);

                                object valueObject;

                                if (!features[featureId].Equals("label", StringComparison.OrdinalIgnoreCase) && double.TryParse(featureValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double doubleValue))
                                {
                                    valueObject = doubleValue;
                                }
                                else
                                {
                                    valueObject = featureValue;
                                }

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.EndOfContext);
                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.EndOfContext);

                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.EndOfContext);
                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.EndOfContext);

                                node.Attributes[features[featureId]] = valueObject;
                            }
                            else
                            {
                                AssertByte(currByte, ByteTags.EndOfContext);
                                currByte = reader.ReadByte();
                                AssertByte(currByte, ByteTags.EndOfContext);

                                finishedNodeFeatures = true;
                            }
                        }

                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.EndOfContext);
                        currByte = reader.ReadByte();
                        AssertByte(currByte, ByteTags.EndOfContext);

                        currByte = reader.ReadByte();
                    }

                    AssertByte(currByte, ByteTags.EndOfContext);
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);

                    nodes[id] = (node, parent);
                }
                else
                {
                    AssertByte(currByte, ByteTags.EndOfContext);
                    currByte = reader.ReadByte();
                    AssertByte(currByte, ByteTags.EndOfContext);

                    finishedNodes = true;
                }
            }

            currByte = reader.ReadByte();

            string label = null;

            if (currByte == (byte)ByteTags.Label)
            {
                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.UndefinedLength);

                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.String);

                int length = ReadLength(reader);

                label = ReadString(reader, length);

                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.EndOfContext);
                currByte = reader.ReadByte();
                AssertByte(currByte, ByteTags.EndOfContext);
            }

            TreeNode tree = null;

            foreach (KeyValuePair<int, (TreeNode node, int? parent)> kvp in nodes)
            {
                if (kvp.Value.parent != null)
                {
                    int parent = kvp.Value.parent.Value;

                    nodes[parent].node.Children.Add(kvp.Value.node);
                    kvp.Value.node.Parent = nodes[parent].node;
                }
                else
                {
                    tree = kvp.Value.node;
                }

                if (kvp.Value.node.Attributes.TryGetValue("dist", out object distValue) && distValue is double branchLength)
                {
                    kvp.Value.node.Length = branchLength;
                }
            }

            foreach (KeyValuePair<int, (TreeNode node, int? parent)> kvp in nodes)
            {
                if (kvp.Value.node.Children.Count == 0)
                {
                    if (kvp.Value.node.Attributes.TryGetValue("label", out object labelValue) && labelValue is string nodeLabel)
                    {
                        kvp.Value.node.Name = nodeLabel;
                    }
                }
            }

            if (tree != null)
            {
                if (!string.IsNullOrEmpty(treetype))
                {
                    tree.Attributes["Tree-treetype"] = treetype;
                }

                if (!string.IsNullOrEmpty(label))
                {
                    tree.Attributes["TreeName"] = label;
                }
            }

            return tree;
        }

        /// <summary>
        /// Writes a <see cref="TreeNode"/> to a file in NCBI ASN.1 binary format.
        /// </summary>
        /// <param name="tree">The tree to write.</param>
        /// <param name="outputFile">The path to the output file.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteTree(TreeNode tree, string outputFile, string treeType = null, string label = null)
        {
            using FileStream stream = File.Create(outputFile);
            WriteTree(tree, stream, false, treeType, label);
        }

        /// <summary>
        /// Writes a <see cref="TreeNode"/> to a file in NCBI ASN.1 binary format.
        /// </summary>
        /// <param name="tree">The tree to write.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the tree should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteTree(TreeNode tree, Stream outputStream, bool keepOpen = false, string treeType = null, string label = null)
        {
            using BinaryWriter sw = new BinaryWriter(outputStream, Encoding.UTF8, keepOpen);
            WriteTree(tree, sw, treeType, label);
        }

        /// <summary>
        /// Writes a collection of <see cref="TreeNode"/>s to a file in NCBI ASN.1 binary format. Note that only one tree can be saved in each file; if the collection contains more than one tree an exception will be thrown.
        /// </summary>
        /// <param name="trees">The collection of trees to write. If this contains more than one tree, an exception will be thrown.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the tree should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, Stream outputStream, bool keepOpen = false, string treeType = null, string label = null)
        {
            Contract.Requires(trees != null);

            bool firstTree = true;

            foreach (TreeNode tree in trees)
            {
                if (firstTree)
                {
                    WriteTree(tree, outputStream, keepOpen, treeType, label);
                    firstTree = false;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(trees), "Only one tree can be saved in an NCBI ASN.1 file!");
                }
            }
        }

        /// <summary>
        /// Writes a collection of <see cref="TreeNode"/>s to a file in NCBI ASN.1 binary format. Note that only one tree can be saved in each file; if the collection contains more than one tree an exception will be thrown.
        /// </summary>
        /// <param name="trees">The collection of trees to write. If this contains more than one tree, an exception will be thrown.</param>
        /// <param name="outputFile">The path to the output file.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, string outputFile, string treeType = null, string label = null)
        {
            Contract.Requires(trees != null);

            bool firstTree = true;

            foreach (TreeNode tree in trees)
            {
                if (firstTree)
                {
                    WriteTree(tree, outputFile, treeType, label);
                    firstTree = false;
                }
                else
                {
                    throw new ArgumentOutOfRangeException(nameof(trees), "Only one tree can be saved in an NCBI ASN.1 file!");
                }
            }
        }

        /// <summary>
        /// Writes a list of <see cref="TreeNode"/>s to a file in NCBI ASN.1 binary format. Note that only one tree can be saved in each file; if the tree contains more than one tree an exception will be thrown.
        /// </summary>
        /// <param name="trees">The list of trees to write. If this contains more than one tree, an exception will be thrown.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the tree should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteAllTrees(List<TreeNode> trees, Stream outputStream, bool keepOpen = false, string treeType = null, string label = null)
        {
            Contract.Requires(trees != null);

            if (trees.Count > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(trees), "Only one tree can be saved in an NCBI ASN.1 file!");
            }

            WriteTree(trees[0], outputStream, keepOpen, treeType, label);
        }

        /// <summary>
        /// Writes a list of <see cref="TreeNode"/>s to a file in NCBI ASN.1 binary format. Note that only one tree can be saved in each file; if the list contains more than one tree an exception will be thrown.
        /// </summary>
        /// <param name="trees">The list of trees to write. If this contains more than one tree, an exception will be thrown.</param>
        /// <param name="outputFile">The path to the output file.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteAllTrees(List<TreeNode> trees, string outputFile, string treeType = null, string label = null)
        {
            Contract.Requires(trees != null);

            if (trees.Count > 1)
            {
                throw new ArgumentOutOfRangeException(nameof(trees), "Only one tree can be saved in an NCBI ASN.1 file!");
            }

            WriteTree(trees[0], outputFile, treeType, label);
        }

        /// <summary>
        /// Writes a <see cref="TreeNode"/> to a <see cref="BinaryWriter"/> in NCBI ASN.1 binary format.
        /// </summary>
        /// <param name="tree">The tree to write.</param>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which the tree will be written..</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteTree(TreeNode tree, BinaryWriter writer, string treeType = null, string label = null)
        {
            Contract.Requires(writer != null);

            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            writer.Write((byte)ByteTags.ObjectStart);
            writer.Write((byte)ByteTags.UndefinedLength);

            if (!string.IsNullOrEmpty(treeType))
            {
                writer.Write((byte)ByteTags.TreeType);
                writer.Write((byte)ByteTags.UndefinedLength);

                WriteString(writer, treeType);

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);
            }

            writer.Write((byte)ByteTags.FDict);
            writer.Write((byte)ByteTags.UndefinedLength);

            writer.Write((byte)ByteTags.ArrayStart);
            writer.Write((byte)ByteTags.UndefinedLength);

            HashSet<string> attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "label", "dist" };

            foreach (TreeNode node in tree.GetChildrenRecursiveLazy())
            {
                foreach (string attribute in node.Attributes.Keys)
                {
                    attributes.Add(attribute);
                }
            }

            Dictionary<string, int> featureIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int currInd = 0;

            foreach (string attribute in attributes)
            {
                featureIndex.Add(attribute, currInd);

                writer.Write((byte)ByteTags.ObjectStart);
                writer.Write((byte)ByteTags.UndefinedLength);

                writer.Write((byte)ByteTags.FeatureId);
                writer.Write((byte)ByteTags.UndefinedLength);

                WriteInt(writer, currInd);

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);

                writer.Write((byte)ByteTags.FeatureName);
                writer.Write((byte)ByteTags.UndefinedLength);

                WriteString(writer, attribute);

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);

                currInd++;
            }

            writer.Write((byte)ByteTags.EndOfContext);
            writer.Write((byte)ByteTags.EndOfContext);

            writer.Write((byte)ByteTags.EndOfContext);
            writer.Write((byte)ByteTags.EndOfContext);

            writer.Write((byte)ByteTags.Nodes);
            writer.Write((byte)ByteTags.UndefinedLength);

            writer.Write((byte)ByteTags.ArrayStart);
            writer.Write((byte)ByteTags.UndefinedLength);

            Dictionary<TreeNode, int> nodeIndex = new Dictionary<TreeNode, int>();
            currInd = 0;

            foreach (TreeNode node in tree.GetChildrenRecursiveLazy())
            {
                nodeIndex.Add(node, currInd);

                writer.Write((byte)ByteTags.ObjectStart);
                writer.Write((byte)ByteTags.UndefinedLength);

                writer.Write((byte)ByteTags.NodeId);
                writer.Write((byte)ByteTags.UndefinedLength);

                WriteInt(writer, currInd);

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);

                if (node.Parent != null)
                {
                    writer.Write((byte)ByteTags.NodeParent);
                    writer.Write((byte)ByteTags.UndefinedLength);

                    WriteInt(writer, nodeIndex[node.Parent]);

                    writer.Write((byte)ByteTags.EndOfContext);
                    writer.Write((byte)ByteTags.EndOfContext);
                }

                List<(int, string)> nodeFeatures = new List<(int, string)>();

                bool hasLabel = false;
                bool hasDist = false;

                foreach (KeyValuePair<string, object> attribute in node.Attributes)
                {
                    if (attribute.Value != null)
                    {
                        int attributeIndex = featureIndex[attribute.Key];

                        if (attribute.Value is string stringValue && !string.IsNullOrEmpty(stringValue))
                        {
                            nodeFeatures.Add((attributeIndex, stringValue));

                            if (attribute.Key.Equals("label", StringComparison.OrdinalIgnoreCase))
                            {
                                hasLabel = true;
                            }

                            if (attribute.Key.Equals("dist", StringComparison.OrdinalIgnoreCase))
                            {
                                hasDist = true;
                            }
                        }
                        else if (attribute.Value is double doubleValue && !double.IsNaN(doubleValue))
                        {
                            nodeFeatures.Add((attributeIndex, doubleValue.ToString(System.Globalization.CultureInfo.InvariantCulture)));

                            if (attribute.Key.Equals("label", StringComparison.OrdinalIgnoreCase))
                            {
                                hasLabel = true;
                            }

                            if (attribute.Key.Equals("dist", StringComparison.OrdinalIgnoreCase))
                            {
                                hasDist = true;
                            }
                        }
                    }
                }

                if (!hasLabel && node.Name != null)
                {
                    nodeFeatures.Add((featureIndex["label"], node.Name));
                }

                if (!hasDist && !double.IsNaN(node.Length))
                {
                    nodeFeatures.Add((featureIndex["dist"], node.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                }

                if (nodeFeatures.Count > 0)
                {
                    writer.Write((byte)ByteTags.NodeFeatures);
                    writer.Write((byte)ByteTags.UndefinedLength);

                    writer.Write((byte)ByteTags.ArrayStart);
                    writer.Write((byte)ByteTags.UndefinedLength);

                    for (int i = 0; i < nodeFeatures.Count; i++)
                    {
                        writer.Write((byte)ByteTags.ObjectStart);
                        writer.Write((byte)ByteTags.UndefinedLength);

                        writer.Write((byte)ByteTags.NodeFeatureId);
                        writer.Write((byte)ByteTags.UndefinedLength);

                        WriteInt(writer, nodeFeatures[i].Item1);

                        writer.Write((byte)ByteTags.EndOfContext);
                        writer.Write((byte)ByteTags.EndOfContext);

                        writer.Write((byte)ByteTags.NodeFeatureValue);
                        writer.Write((byte)ByteTags.UndefinedLength);

                        WriteString(writer, nodeFeatures[i].Item2);

                        writer.Write((byte)ByteTags.EndOfContext);
                        writer.Write((byte)ByteTags.EndOfContext);

                        writer.Write((byte)ByteTags.EndOfContext);
                        writer.Write((byte)ByteTags.EndOfContext);
                    }

                    writer.Write((byte)ByteTags.EndOfContext);
                    writer.Write((byte)ByteTags.EndOfContext);

                    writer.Write((byte)ByteTags.EndOfContext);
                    writer.Write((byte)ByteTags.EndOfContext);
                }

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);

                currInd++;
            }

            writer.Write((byte)ByteTags.EndOfContext);
            writer.Write((byte)ByteTags.EndOfContext);

            writer.Write((byte)ByteTags.EndOfContext);
            writer.Write((byte)ByteTags.EndOfContext);

            if (!string.IsNullOrEmpty(label))
            {
                writer.Write((byte)ByteTags.Label);
                writer.Write((byte)ByteTags.UndefinedLength);

                WriteString(writer, label);

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);
            }
            else if (tree.Attributes.TryGetValue("TreeName", out object treeNameValue) && treeNameValue != null && treeNameValue is string treeName && !string.IsNullOrEmpty(treeName))
            {
                writer.Write((byte)ByteTags.Label);
                writer.Write((byte)ByteTags.UndefinedLength);

                WriteString(writer, treeName);

                writer.Write((byte)ByteTags.EndOfContext);
                writer.Write((byte)ByteTags.EndOfContext);
            }

            writer.Write((byte)ByteTags.EndOfContext);
            writer.Write((byte)ByteTags.EndOfContext);
        }

        /// <summary>
        /// Throws an exception if the byte that has been read does not correspond to the tag that was expected.
        /// </summary>
        /// <param name="observed">The byte that has been read.</param>
        /// <param name="expected">The tag that was expected.</param>
        private static void AssertByte(byte observed, ByteTags expected)
        {
            if (observed != (byte)expected)
            {
                throw new Exception("Unexpected byte: 0x" + observed.ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + "! Was expecting: " + expected.ToString() + "(0x" + ((byte)expected).ToString("X2", System.Globalization.CultureInfo.InvariantCulture) + ").");
            }
        }

        /// <summary>
        /// Reads an UTF8-encoded <see cref="string"/> with the specified length from a <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> from which the string will be read.</param>
        /// <param name="length">The length of the string to read.</param>
        /// <returns>The string that has been read.</returns>
        private static string ReadString(BinaryReader reader, int length)
        {
            byte[] buffer = new byte[length];

            reader.Read(buffer, 0, length);

            // Wishful thinking. 
            return System.Text.Encoding.UTF8.GetString(buffer);
        }

        /// <summary>
        /// Writes an UTF8-encoded <see cref="string"/> to a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which the string will be written.</param>
        /// <param name="str">The <see cref="string"/> to write.</param>
        private static void WriteString(BinaryWriter writer, string str)
        {
            writer.Write((byte)ByteTags.String);

            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);

            WriteLength(writer, bytes.Length);

            writer.Write(bytes);
        }

        /// <summary>
        /// Writes an <see cref="int"/> to a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which the <see cref="int"/> will be written.</param>
        /// <param name="value">The <see cref="int"/> to write.</param>
        private static void WriteInt(BinaryWriter writer, int value)
        {
            writer.Write((byte)ByteTags.Int);

            WriteLength(writer, 4);

            // Not the optimal way to store this, but who cares.
            writer.Write((byte)((value >> 24) & 0b11111111));
            writer.Write((byte)((value >> 16) & 0b11111111));
            writer.Write((byte)((value >> 8) & 0b11111111));
            writer.Write((byte)(value & 0b11111111));
        }

        /// <summary>
        /// Writes a length to a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="writer">The <see cref="BinaryWriter"/> on which the <paramref name="length"/> will be written.</param>
        /// <param name="length">The length to write.</param>
        private static void WriteLength(BinaryWriter writer, int length)
        {
            if (length < 128)
            {
                writer.Write((byte)length);
            }
            else
            {
                int lengthLength = 1;
                int shiftedLength = length >> 8;

                while (shiftedLength != 0)
                {
                    shiftedLength >>= 8;
                    lengthLength++;
                }

                byte lengthByte = 0b10000000;
                lengthByte |= (byte)lengthLength;

                writer.Write(lengthByte);

                for (int i = 0; i < lengthLength; i++)
                {
                    byte currByte = (byte)((length >> (8 * (lengthLength - 1 - i))) & 0b11111111);
                    writer.Write(currByte);
                }
            }
        }

        /// <summary>
        /// Reads a length from a <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> from which the length will be read.</param>
        /// <returns>The length that has been read.</returns>
        private static int ReadLength(BinaryReader reader)
        {
            byte currByte = reader.ReadByte();

            if ((currByte & 0b10000000) == 0)
            {
                return currByte;
            }
            else
            {
                int additionalBytes = currByte & 0b01111111;

                if (additionalBytes > 4)
                {
                    // We could use a long or something even bigger, but most of the things we will want to use the length for have int indexers, thus it is better to fail directly here.
                    throw new OverflowException("The length specified in the ASN stream exceeds the capability of the Int32 type!");
                }

                int length = 0;

                for (int i = 0; i < additionalBytes; i++)
                {
                    byte digit = reader.ReadByte();

                    if (additionalBytes == 4 && i == 0 && digit > 127)
                    {
                        throw new OverflowException("The length specified in the ASN stream exceeds the capability of the Int32 type!");
                    }

                    length |= (digit << ((additionalBytes - 1 - i) * 8));
                }

                return length;
            }
        }

        /// <summary>
        /// Reads an <see cref="int"/> from a <see cref="BinaryReader"/>.
        /// </summary>
        /// <param name="reader">The <see cref="BinaryReader"/> from which the <see cref="int"/> will be read.</param>
        /// <param name="length">The length (in bytes) of the <see cref="int"/> to read.</param>
        /// <returns>The <see cref="int"/> that has been read.</returns>
        private static int ReadInt(BinaryReader reader, int length)
        {
            if (length > 4)
            {
                // See comment for the length above.
                throw new OverflowException("The integer specified in the ASN stream exceeds the capability of the Int32 type!");
            }

            byte[] buffer = new byte[length];

            reader.Read(buffer, 0, length);

            bool needComplement = false;

            if (length < 4 && ((buffer[0] & 0b10000000) != 0))
            {
                needComplement = true;
            }

            int value = 0;

            for (int i = 0; i < length; i++)
            {
                value |= (buffer[i] << ((length - 1 - i) * 8));
            }

            // The problem here is that we need the 2's complement based on the number of bytes that are actually used to store the data.
            // Maybe I could get away with just setting the bits from the unused bytes to 1.
            if (needComplement)
            {
                // Mask to the number of bytes used.
                int maskPattern = 0;

                for (int i = 0; i < length; i++)
                {
                    maskPattern |= (0b11111111 << ((length - 1 - i) * 8));
                }

                // Perform the 2's complement and mask the unused bytes back to 0 to get the absolute value of the number.
                value = ((~value) + 1) & maskPattern;

                // Negate it.
                value = -value;
            }

            return value;
        }

    }
}
