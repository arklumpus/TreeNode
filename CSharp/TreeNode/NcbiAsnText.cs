using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

namespace PhyloTree.Formats
{
    /// <summary>
    /// Contains methods to read and write trees in the NCBI ASN.1 text format.
    /// </summary>
    public static class NcbiAsnText
    {
        /// <summary>
        /// Parses a tree from an NCBI ASN.1 text format file. Note that the tree can only contain a single file, and this method will always return a collection with a single element.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <returns>A <see cref="IEnumerable{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static IEnumerable<TreeNode> ParseTrees(string inputFile)
        {
            yield return ParseAllTrees(inputFile)[0];
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 text format file. Note that the tree can only contain a single file, and this method will always return a collection with a single element.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <returns>A <see cref="IEnumerable{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static IEnumerable<TreeNode> ParseTrees(Stream inputStream, bool keepOpen = false)
        {
            yield return ParseAllTrees(inputStream, keepOpen)[0];
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 text format file. Note that the tree can only contain a single file, and this method will always return a list with a single element.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <returns>A <see cref="List{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static List<TreeNode> ParseAllTrees(string inputFile)
        {
            using StreamReader reader = new StreamReader(inputFile);
            return new List<TreeNode>() { ParseTree(reader) };
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 text format file. Note that the tree can only contain a single file, and this method will always return a list with a single element.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <returns>A <see cref="List{T}"/> containing the tree defined in the file. This will always consist of a single element.</returns>
        public static List<TreeNode> ParseAllTrees(Stream inputStream, bool keepOpen = false)
        {
            using StreamReader reader = new StreamReader(inputStream, Encoding.UTF8, true, 1024, keepOpen);
            return new List<TreeNode>() { ParseTree(reader) };
        }

        /// <summary>
        /// Parses a tree from an NCBI ASN.1 format string into a <see cref="TreeNode"/> object.
        /// </summary>
        /// <param name="source">The NCBI ASN.1 format tree string.</param>
        /// <returns>The parsed <see cref="TreeNode"/> object.</returns>
        public static TreeNode ParseTree(string source)
        {
            using StringReader reader = new StringReader(source);
            return ParseTree(reader);
        }

        /// <summary>
        /// Parses a tree from a <see cref="TextReader"/> that reads an NCBI ASN.1 format string into a <see cref="TreeNode"/> object.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> that reads the NCBI ASN.1 format string.</param>
        /// <returns>The parsed <see cref="TreeNode"/> object.</returns>
        public static TreeNode ParseTree(TextReader reader)
        {
            Contract.Requires(reader != null);

            bool eof = false;

            string currToken = ReadToken(reader, ref eof);
            AssertToken(currToken, "BioTreeContainer");

            currToken = ReadToken(reader, ref eof);
            AssertToken(currToken, "::=");

            currToken = ReadToken(reader, ref eof);
            AssertToken(currToken, "{");

            currToken = ReadToken(reader, ref eof);


            string treetype = null;

            if (currToken.Equals("treetype", StringComparison.OrdinalIgnoreCase))
            {
                currToken = ReadToken(reader, ref eof);
                treetype = currToken[1..^1];

                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, ",");

                currToken = ReadToken(reader, ref eof);
            }

            AssertToken(currToken, "fdict");

            currToken = ReadToken(reader, ref eof);
            AssertToken(currToken, "{");

            Dictionary<int, string> features = new Dictionary<int, string>();

            bool finishedFeatures = false;

            while (!finishedFeatures)
            {
                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, "{");

                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, "id");

                currToken = ReadToken(reader, ref eof);
                int id = int.Parse(currToken, System.Globalization.CultureInfo.InvariantCulture);

                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, ",");

                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, "name");

                string name = ReadToken(reader, ref eof)[1..^1];

                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, "}");

                features[id] = name;

                currToken = ReadToken(reader, ref eof);

                if (currToken == "}")
                {
                    finishedFeatures = true;
                }
                else
                {
                    AssertToken(currToken, ",");
                }
            }

            currToken = ReadToken(reader, ref eof);
            AssertToken(currToken, ",");

            currToken = ReadToken(reader, ref eof);
            AssertToken(currToken, "nodes");

            currToken = ReadToken(reader, ref eof);
            AssertToken(currToken, "{");

            bool finishedNodes = false;

            Dictionary<int, (TreeNode node, int? parent)> nodes = new Dictionary<int, (TreeNode node, int? parent)>();

            while (!finishedNodes)
            {
                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, "{");

                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, "id");

                currToken = ReadToken(reader, ref eof);
                int id = int.Parse(currToken, System.Globalization.CultureInfo.InvariantCulture);

                currToken = ReadToken(reader, ref eof);
                AssertToken(currToken, ",");

                currToken = ReadToken(reader, ref eof);

                int? parent = null;
                if (currToken.Equals("parent", StringComparison.OrdinalIgnoreCase))
                {
                    currToken = ReadToken(reader, ref eof);
                    parent = int.Parse(currToken, System.Globalization.CultureInfo.InvariantCulture);

                    currToken = ReadToken(reader, ref eof);
                    AssertToken(currToken, ",");

                    currToken = ReadToken(reader, ref eof);
                }

                TreeNode node = new TreeNode(null);

                if (currToken.Equals("features", StringComparison.OrdinalIgnoreCase))
                {
                    currToken = ReadToken(reader, ref eof);
                    AssertToken(currToken, "{");

                    bool finishedNodeFeatures = false;

                    while (!finishedNodeFeatures)
                    {
                        currToken = ReadToken(reader, ref eof);
                        AssertToken(currToken, "{");

                        currToken = ReadToken(reader, ref eof);
                        AssertToken(currToken, "featureid");

                        currToken = ReadToken(reader, ref eof);
                        int featureId = int.Parse(currToken, System.Globalization.CultureInfo.InvariantCulture);

                        currToken = ReadToken(reader, ref eof);
                        AssertToken(currToken, ",");

                        currToken = ReadToken(reader, ref eof);
                        AssertToken(currToken, "value");

                        string value = ReadToken(reader, ref eof)[1..^1];

                        object valueObject;

                        if (!features[featureId].Equals("label", StringComparison.OrdinalIgnoreCase) && double.TryParse(value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double doubleValue))
                        {
                            valueObject = doubleValue;
                        }
                        else
                        {
                            valueObject = value;
                        }

                        currToken = ReadToken(reader, ref eof);
                        AssertToken(currToken, "}");

                        node.Attributes[features[featureId]] = valueObject;

                        currToken = ReadToken(reader, ref eof);

                        if (currToken == "}")
                        {
                            finishedNodeFeatures = true;
                        }
                        else
                        {
                            AssertToken(currToken, ",");
                        }
                    }

                    currToken = ReadToken(reader, ref eof);
                }

                AssertToken(currToken, "}");

                nodes[id] = (node, parent);

                currToken = ReadToken(reader, ref eof);

                if (currToken == "}")
                {
                    finishedNodes = true;
                }
                else
                {
                    AssertToken(currToken, ",");
                }
            }

            currToken = ReadToken(reader, ref eof);

            string label = null;

            if (currToken.Equals("label", StringComparison.OrdinalIgnoreCase))
            {
                label = ReadToken(reader, ref eof)[1..^1];
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
        /// Writes a <see cref="TreeNode"/> to a file in NCBI ASN.1 text format.
        /// </summary>
        /// <param name="tree">The tree to write.</param>
        /// <param name="outputFile">The path to the output file.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteTree(TreeNode tree, string outputFile, string treeType = null, string label = null)
        {
            File.WriteAllText(outputFile, WriteTree(tree, treeType, label));
        }

        /// <summary>
        /// Writes a <see cref="TreeNode"/> to a file in NCBI ASN.1 text format.
        /// </summary>
        /// <param name="tree">The tree to write.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the tree should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        public static void WriteTree(TreeNode tree, Stream outputStream, bool keepOpen = false, string treeType = null, string label = null)
        {
            using StreamWriter sw = new StreamWriter(outputStream, Encoding.UTF8, 1024, keepOpen);
            sw.Write(WriteTree(tree, treeType, label));
        }

        /// <summary>
        /// Writes a collection of <see cref="TreeNode"/>s to a file in NCBI ASN.1 text format. Note that only one tree can be saved in each file; if the collection contains more than one tree an exception will be thrown.
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
        /// Writes a collection of <see cref="TreeNode"/>s to a file in NCBI ASN.1 text format. Note that only one tree can be saved in each file; if the collection contains more than one tree an exception will be thrown.
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
        /// Writes a list of <see cref="TreeNode"/>s to a file in NCBI ASN.1 text format. Note that only one tree can be saved in each file; if the tree contains more than one tree an exception will be thrown.
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
        /// Writes a list of <see cref="TreeNode"/>s to a file in NCBI ASN.1 text format. Note that only one tree can be saved in each file; if the list contains more than one tree an exception will be thrown.
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
        /// Writes a <see cref="TreeNode"/> to a <see cref="string"/> in NCBI ASN.1 text format.
        /// </summary>
        /// <param name="tree">The tree to write.</param>
        /// <param name="treeType">An optional value for the <c>treetype</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <param name="label">An optional value for the <c>label</c> property defined in the NCBI ASN.1 tree format.</param>
        /// <returns>A <see cref="string"/> containing the NCBI ASN.1 representation of the <see cref="TreeNode"/>.</returns>
        public static string WriteTree(TreeNode tree, string treeType = null, string label = null)
        {
            if (tree == null)
            {
                throw new ArgumentNullException(nameof(tree));
            }

            StringBuilder builder = new StringBuilder();

            builder.Append("BioTreeContainer ::= {\n");

            if (!string.IsNullOrEmpty(treeType))
            {
                builder.Append("  treetype \"" + treeType.Replace("\"", "\"\"", StringComparison.OrdinalIgnoreCase) + "\",\n");
            }

            builder.Append("  fdict {\n");

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

                if (currInd > 0)
                {
                    builder.Append(",\n");
                }

                builder.Append("    {\n");
                builder.Append("      id " + currInd.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\n");
                builder.Append("      name \"" + attribute.Replace("\"", "\"\"", StringComparison.OrdinalIgnoreCase) + "\"\n");

                builder.Append("    }");

                currInd++;
            }

            builder.Append("\n  },\n");

            builder.Append("  nodes {\n");

            Dictionary<TreeNode, int> nodeIndex = new Dictionary<TreeNode, int>();
            currInd = 0;

            foreach (TreeNode node in tree.GetChildrenRecursiveLazy())
            {
                nodeIndex.Add(node, currInd);

                if (currInd > 0)
                {
                    builder.Append(",\n");
                }

                builder.Append("    {\n");
                builder.Append("      id " + currInd.ToString(System.Globalization.CultureInfo.InvariantCulture));

                if (node.Parent != null)
                {
                    builder.Append(",\n      parent " + nodeIndex[node.Parent].ToString(System.Globalization.CultureInfo.InvariantCulture));
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
                    builder.Append(",\n      features {\n");

                    for (int i = 0; i < nodeFeatures.Count; i++)
                    {
                        builder.Append("        {\n");
                        builder.Append("          featureid " + nodeFeatures[i].Item1.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\n");
                        builder.Append("          value \"" + nodeFeatures[i].Item2.Replace("\"", "\"\"", StringComparison.OrdinalIgnoreCase) + "\"\n");
                        builder.Append("        }");

                        if (i < nodeFeatures.Count - 1)
                        {
                            builder.Append(",\n");
                        }
                        else
                        {
                            builder.Append("\n");
                        }
                    }

                    builder.Append("      }");
                }

                builder.Append("\n    }");

                currInd++;
            }

            builder.Append("\n  }");

            if (!string.IsNullOrEmpty(label))
            {
                builder.Append(",\n  label \"" + label.Replace("\"", "\"\"", StringComparison.OrdinalIgnoreCase) + "\"");
            }
            else if (tree.Attributes.TryGetValue("TreeName", out object treeNameValue) && treeNameValue != null && treeNameValue is string treeName && !string.IsNullOrEmpty(treeName))
            {
                builder.Append(",\n  label \"" + treeName.Replace("\"", "\"\"", StringComparison.OrdinalIgnoreCase) + "\"");
            }

            builder.Append("\n}\n");

            return builder.ToString();
        }

        /// <summary>
        /// Throws an exception if the token that has been read is different than what was expected.
        /// </summary>
        /// <param name="token">The token that has been read.</param>
        /// <param name="expected">The token that was expected.</param>
        private static void AssertToken(string token, string expected)
        {
            if (!token.Equals(expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Unexpected token: \"" + token + "\"! Was expecting: \"" + expected + "\".");
            }
        }

        /// <summary>
        /// Reads a token from the <see cref="TextReader"/>. A token is usually a word, a curly bracket or a comma.
        /// </summary>
        /// <param name="reader">The <see cref="TextReader"/> from which the token will be read.</param>
        /// <param name="eof">This parameter will be set to <see langword="true" /> if the reader reaches the end of the file. If this is already <see langword="true" /> when the method starts, an exception is thrown.</param>
        /// <returns>The token that has been read.</returns>
        private static string ReadToken(TextReader reader, ref bool eof)
        {
            if (eof)
            {
                throw new IndexOutOfRangeException("Trying to read beyond the end of the string!");
            }

            StringBuilder tokenBuilder = new StringBuilder();

            int charInt = reader.Peek();

            while (charInt >= 0 && char.IsWhiteSpace((char)charInt))
            {
                reader.Read();
                charInt = reader.Peek();
            }

            if (charInt >= 0)
            {
                bool firstChar = true;
                bool quotesOpen = false;

                while (!IsBreakCharacter(charInt, firstChar, quotesOpen))
                {
                    charInt = reader.Read();

                    if ((!quotesOpen || (char)charInt != '\n') && (char)charInt != '\r')
                    {
                        tokenBuilder.Append((char)charInt);
                    }

                    if ((char)charInt == '\"')
                    {
                        quotesOpen = !quotesOpen;
                    }
                    charInt = reader.Peek();
                    firstChar = false;
                }

                if (charInt < 0)
                {
                    eof = true;
                }
                else
                {
                    eof = false;
                }

                return tokenBuilder.ToString();
            }
            else
            {
                eof = true;
                return tokenBuilder.ToString();
            }
        }

        /// <summary>
        /// Determines whether a character breaks the current token.
        /// </summary>
        /// <param name="charInt">The character that was read.</param>
        /// <param name="firstChar">A <see langword="bool" /> specifying whether this character is the first character in the token.</param>
        /// <param name="quotesOpen">A <see langword="bool" /> specifying whether the character being read is currently within a double-quoted string.</param>
        /// <returns><see langword="true"/> if the character breaks the current token; otherwise, <see langword="false"/>. </returns>
        private static bool IsBreakCharacter(int charInt, bool firstChar, bool quotesOpen)
        {
            if (charInt < 0)
            {
                return true;
            }
            else if (firstChar)
            {
                return char.IsWhiteSpace((char)charInt);
            }
            else if (quotesOpen)
            {
                return false;
            }
            else
            {
                char c = (char)charInt;

                return char.IsWhiteSpace(c) || c == ',' || c == '{' || c == '}';
            }
        }
    }
}
