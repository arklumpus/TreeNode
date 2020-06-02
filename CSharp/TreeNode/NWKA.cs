using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PhyloTree.Extensions;

namespace PhyloTree.Formats
{
    /// <summary>
    /// Contains methods to read and write trees in Newick and Newick-with-Attributes (NWKA) format.
    /// </summary>
    public static class NWKA
    {
        /// <summary>
        /// Parse a Newick-with-Attributes string into a TreeNode object.
        /// </summary>
        /// <param name="source">The Newick-with-Attributes string. This string must specify only a single tree.</param>
        /// <param name="parent">The parent node of this node. If parsing a whole tree, this parameter should be left equal to <c>null</c>.</param>
        /// <param name="debug">When this is <c>true</c>, debug information is printed to the standard output during the parsing.</param>
        /// <returns>The parsed <see cref="TreeNode"/> object.</returns>
        public static TreeNode ParseTree(string source, bool debug = false, TreeNode parent = null)
        {
            Contract.Requires(source != null);

            source = source.Trim();
            if (source.EndsWith(";", StringComparison.OrdinalIgnoreCase))
            {
                source = source[0..^1];
            }

            if (debug)
            {
                Console.WriteLine("Parsing: " + source);
            }

            if (source.StartsWith("(", StringComparison.OrdinalIgnoreCase))
            {
                using StringReader sr = new StringReader(source);

                StringBuilder childrenBuilder = new StringBuilder();

                sr.Read();

                bool closed = false;
                int openCount = 0;
                int openSquareCount = 0;
                int openCurlyCount = 0;

                bool escaping = false;
                bool openQuotes = false;
                bool openApostrophe = false;
                bool eof = false;

                List<int> commas = new List<int>();
                int position = 0;

                while (!closed && !eof)
                {
                    char c = sr.NextToken(ref escaping, out bool escaped, ref openQuotes, ref openApostrophe, out eof);

                    if (!escaped)
                    {
                        if (!openQuotes && !openApostrophe)
                        {
                            switch (c)
                            {
                                case '(':
                                    openCount++;
                                    break;
                                case ')':
                                    if (openCount > 0)
                                    {
                                        openCount--;
                                    }
                                    else
                                    {
                                        closed = true;
                                    }
                                    break;
                                case '[':
                                    openSquareCount++;
                                    break;
                                case ']':
                                    openSquareCount--;
                                    break;
                                case '{':
                                    openCurlyCount++;
                                    break;
                                case '}':
                                    openCurlyCount--;
                                    break;
                                case ',':
                                    if (openCount == 0 && openSquareCount == 0 && openCurlyCount == 0)
                                    {
                                        commas.Add(position);
                                    }
                                    break;
                            }
                        }
                    }

                    if (!closed && !eof)
                    {
                        childrenBuilder.Append(c);
                        position++;
                    }
                }

                List<string> children = new List<string>();

                if (commas.Count > 0)
                {
                    for (int i = 0; i < commas.Count; i++)
                    {
                        children.Add(childrenBuilder.ToString(i > 0 ? commas[i - 1] + 1 : 0, commas[i] - (i > 0 ? commas[i - 1] + 1 : 0)));
                    }
                    children.Add(childrenBuilder.ToString(commas.Last() + 1, childrenBuilder.Length - commas.Last() - 1));
                }
                else
                {
                    children.Add(childrenBuilder.ToString());
                }

                if (debug)
                {
                    Console.WriteLine();
                    Console.WriteLine("Children: ");
                    for (int i = 0; i < children.Count; i++)
                    {
                        Console.WriteLine(" - " + children[i]);
                    }

                    Console.WriteLine();
                }

                TreeNode tbr = new TreeNode(parent);

                ParseAttributes(sr, ref eof, tbr, children.Count);

                if (debug)
                {
                    Console.WriteLine("Attributes:");

                    foreach (KeyValuePair<string, object> kvp in tbr.Attributes)
                    {
                        Console.WriteLine(" - " + kvp.Key + " = " + kvp.Value.ToString());
                    }

                    Console.WriteLine();
                    Console.WriteLine();
                }

                for (int i = 0; i < children.Count; i++)
                {
                    tbr.Children.Add(ParseTree(children[i], debug, tbr));
                }

                return tbr;
            }
            else
            {
                using StringReader sr = new StringReader(source);

                bool eof = false;

                TreeNode tbr = new TreeNode(parent);

                ParseAttributes(sr, ref eof, tbr, 0);

                if (debug)
                {
                    Console.WriteLine();
                    Console.WriteLine("Attributes:");

                    foreach (KeyValuePair<string, object> kvp in tbr.Attributes)
                    {
                        Console.WriteLine(" - " + kvp.Key + " = " + kvp.Value.ToString());
                    }

                    Console.WriteLine();
                }

                return tbr;
            }
        }

        /// <summary>
        /// Lazily parses trees from a string in Newick-with-Attributes (NWKA) format. Each tree in the string is not read and parsed until it is requested.
        /// </summary>
        /// <param name="source">The <see cref="string"/> from which the trees should be read.</param>
        /// <param name="debug">When this is <c>true</c>, debug information is printed to the standard output during the parsing.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the string.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031")]
        public static IEnumerable<TreeNode> ParseTreesFromSource(string source, bool debug = false)
        {
            bool escaping = false;
            bool openQuotes = false;
            bool openApostrophe = false;
            bool eof = false;

            while (!eof)
            {
                using StringReader sr = new StringReader(source);

                StringBuilder sb = new StringBuilder();

                char c = sr.NextToken(ref escaping, out bool escaped, ref openQuotes, ref openApostrophe, out eof);

                while (!eof && !(c == ';' && !escaped && !openQuotes && !openApostrophe))
                {
                    sb.Append(c);
                    c = sr.NextToken(ref escaping, out escaped, ref openQuotes, ref openApostrophe, out eof);
                }

                string treeString = sb.ToString().Trim();

                int index = treeString.IndexOf("(", StringComparison.OrdinalIgnoreCase);

                string treeName = "";

                if (index > 0)
                {
                    treeName = treeString.Substring(0, index);
                    treeString = treeString.Substring(index);
                }

                if (treeString.Length > 0)
                {
                    TreeNode tbr = null;

                    try
                    {
                        tbr = ParseTree(treeString, debug, null);
                        if (!tbr.Attributes.ContainsKey("TreeName") && !string.IsNullOrWhiteSpace(treeName))
                        {
                            tbr.Attributes["TreeName"] = treeName;
                        }
                    }
                    catch
                    {
                        yield break;
                    }

                    yield return tbr;   
                }
            }
        }

        /// <summary>
        /// Parses trees from a string in Newick-with-Attributes (NWKA) format and completely loads them in memory.
        /// </summary>
        /// <param name="source">The <see cref="string"/> from which the trees should be read.</param>
        /// <param name="debug">When this is <c>true</c>, debug information is printed to the standard output during the parsing.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the string.</returns>
        public static List<TreeNode> ParseAllTreesFromSource(string source, bool debug = false)
        {
            return ParseTreesFromSource(source, debug).ToList();
        }

        /// <summary>
        /// Lazily parses trees from a file in Newick-with-Attributes (NWKA) format. Each tree in the file is not read and parsed until it is requested.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <param name="progressAction">An <see cref="Action" /> that will be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <param name="debug">When this is <c>true</c>, debug information is printed to the standard output during the parsing.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the file.</returns>
        public static IEnumerable<TreeNode> ParseTrees(string inputFile, Action<double> progressAction = null, bool debug = false)
        {
            FileStream inputStream = File.OpenRead(inputFile);
            return ParseTrees(inputStream, false, progressAction, debug);
        }

        /// <summary>
        /// Lazily parses trees from a file in Newick-with-Attributes (NWKA) format. Each tree in the file is not read and parsed until it is requested.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that will be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <param name="debug">When this is <c>true</c>, debug information is printed to the standard output during the parsing.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the file.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1031")]
        public static IEnumerable<TreeNode> ParseTrees(Stream inputStream, bool keepOpen = false, Action<double> progressAction = null, bool debug = false)
        {
            bool escaping = false;
            bool openQuotes = false;
            bool openApostrophe = false;
            bool eof = false;

            using StreamReader sr = new StreamReader(inputStream, Encoding.UTF8, true, 1024, keepOpen);

            while (!eof)
            {
                StringBuilder sb = new StringBuilder();

                char c = sr.NextToken(ref escaping, out bool escaped, ref openQuotes, ref openApostrophe, out eof);

                while (!eof && !(c == ';' && !escaped && !openQuotes && !openApostrophe))
                {
                    sb.Append(c);
                    c = sr.NextToken(ref escaping, out escaped, ref openQuotes, ref openApostrophe, out eof);
                }

                string treeString = sb.ToString().Trim();

                int index = treeString.IndexOf("(", StringComparison.OrdinalIgnoreCase);

                string treeName = "";

                if (index > 0)
                {
                    treeName = treeString.Substring(0, index);
                    treeString = treeString.Substring(index);
                }

                if (treeString.Length > 0)
                {
                    TreeNode tbr = null;

                    try
                    {
                        tbr = ParseTree(treeString, debug, null);
                        if (!tbr.Attributes.ContainsKey("TreeName") && !string.IsNullOrWhiteSpace(treeName))
                        {
                            tbr.Attributes["TreeName"] = treeName;
                        }
                        progressAction?.Invoke((double)inputStream.Position / inputStream.Length);
                    }
                    catch
                    {
                        yield break;
                    }

                    yield return tbr;
                }
            }
        }

        /// <summary>
        /// Parses trees from a file in Newick-with-Attributes (NWKA) format and completely loads them in memory.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <param name="progressAction">An <see cref="Action" /> that will be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <param name="debug">When this is <c>true</c>, debug information is printed to the standard output during the parsing.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the file.</returns>
        public static List<TreeNode> ParseAllTrees(string inputFile, Action<double> progressAction = null, bool debug = false)
        {
            using FileStream inputStream = File.OpenRead(inputFile);
            return ParseAllTrees(inputStream, false, progressAction, debug);
        }

        /// <summary>
        /// Parses trees from a file in Newick-with-Attributes (NWKA) format and completely loads them in memory.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that will be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <param name="debug">When this is <c>true</c>, debug information is printed to the standard output during the parsing.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the file.</returns>
        public static List<TreeNode> ParseAllTrees(Stream inputStream, bool keepOpen = false, Action<double> progressAction = null, bool debug = false)
        {
            return ParseTrees(inputStream, keepOpen, progressAction, debug).ToList();
        }

        /// <summary>
        /// Parse the attributes of a node in the tree.
        /// </summary>
        /// <param name="sr">The <see cref="TextReader"/> from which the attributes should be read.</param>
        /// <param name="eof">A <see cref="bool"/> indicating whether we have reach the end of the stream.</param>
        /// <param name="node">The <see cref="TreeNode"/> whose attributes we are parsing.</param>
        /// <param name="childCount">The number of children of <paramref name="node"/>.</param>
        internal static void ParseAttributes(TextReader sr, ref bool eof, TreeNode node, int childCount)
        {
            StringBuilder attributeValue = new StringBuilder();
            StringBuilder attributeName = new StringBuilder();

            int openSquareCount = 0;
            int openCurlyCount = 0;


            bool escaping = false;
            bool escaped = false;
            bool openQuotes = false;
            bool openApostrophe = false;

            bool nameFinished = false;
            char lastSeparator = ',';

            bool start = true;
            bool closedOuterBrackets = false;

            bool withinBrackets = false;

            char expectedClosingBrackets = '\0';

            while (!eof)
            {
                char c2;

                if (!closedOuterBrackets)
                {
                    c2 = sr.NextToken(ref escaping, out escaped, ref openQuotes, ref openApostrophe, out eof);
                }
                else
                {
                    c2 = ',';
                }

                if (start)
                {
                    if (c2 == '[')
                    {
                        expectedClosingBrackets = ']';
                        c2 = ',';
                        start = false;
                    }
                }


                if (c2 == '=' && !escaped && !openQuotes && !openApostrophe)
                {
                    nameFinished = true;

                    if (closedOuterBrackets)
                    {
                        closedOuterBrackets = false;
                        expectedClosingBrackets = '\0';
                        start = true;
                        withinBrackets = false;
                    }

                    if (expectedClosingBrackets != '\0')
                    {
                        withinBrackets = true;
                    }
                }
                else if ((eof || ((c2 == ':' || c2 == '/' || c2 == ',') && openSquareCount == 0 && openCurlyCount == 0)) && !escaped && !openQuotes && !openApostrophe)
                {
                    if (attributeValue.Length > 0)
                    {
                        string name = attributeName.ToString();

                        if (name.StartsWith("&", StringComparison.OrdinalIgnoreCase))
                        {
                            name = name.Substring(1);
                        }

                        if (name.StartsWith("!", StringComparison.OrdinalIgnoreCase))
                        {
                            name = name.Substring(1);
                        }

                        if (name.Equals("Name", StringComparison.OrdinalIgnoreCase))
                        {
                            string value = attributeValue.ToString();

                            if ((value.StartsWith("\"", StringComparison.OrdinalIgnoreCase) && value.EndsWith("\"", StringComparison.OrdinalIgnoreCase)) || (value.StartsWith("'", StringComparison.OrdinalIgnoreCase) && value.EndsWith("'", StringComparison.OrdinalIgnoreCase)))
                            {
                                value = value[1..^1];
                            }

                            node.Name = value;
                        }
                        else if (name.Equals("Support", StringComparison.OrdinalIgnoreCase))
                        {
                            node.Support = double.Parse(attributeValue.ToString(), CultureInfo.InvariantCulture);
                        }
                        else if (name.Equals("Length", StringComparison.OrdinalIgnoreCase))
                        {
                            node.Length = double.Parse(attributeValue.ToString(), CultureInfo.InvariantCulture);
                        }
                        else
                        {
                            string value = attributeValue.ToString();
                            if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                            {
                                node.Attributes.Add(name, result);
                            }
                            else
                            {
                                if ((value.StartsWith("\"", StringComparison.OrdinalIgnoreCase) && value.EndsWith("\"", StringComparison.OrdinalIgnoreCase)) || (value.StartsWith("'", StringComparison.OrdinalIgnoreCase) && value.EndsWith("'", StringComparison.OrdinalIgnoreCase)))
                                {
                                    value = value[1..^1];
                                }
                                node.Attributes.Add(name, value);
                            }
                        }
                    }
                    else if (attributeName.Length > 0)
                    {
                        switch (lastSeparator)
                        {
                            case ':':
                                if (double.TryParse(attributeName.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double result))
                                {
                                    node.Length = result;
                                }
                                else
                                {
                                    string name = "Unknown";

                                    if (node.Attributes.ContainsKey(name))
                                    {
                                        int ind = 2;
                                        string newName = name + ind.ToString(System.Globalization.CultureInfo.InvariantCulture);

                                        while (node.Attributes.ContainsKey(newName))
                                        {
                                            ind++;
                                            newName = name + ind.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                        }

                                        name = newName;
                                    }

                                    node.Attributes.Add(name, attributeName.ToString());
                                }
                                break;
                            case '/':
                                if (double.TryParse(attributeName.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double result2))
                                {
                                    node.Support = result2;
                                }
                                else
                                {
                                    string name = "Unknown";

                                    if (node.Attributes.ContainsKey(name))
                                    {
                                        int ind = 2;
                                        string newName = name + ind.ToString(System.Globalization.CultureInfo.InvariantCulture);

                                        while (node.Attributes.ContainsKey(newName))
                                        {
                                            ind++;
                                            newName = name + ind.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                        }

                                        name = newName;
                                    }

                                    node.Attributes.Add(name, attributeName.ToString());
                                }
                                break;
                            case ',':
                                bool isName = false;

                                string value = attributeName.ToString();

                                if ((value.StartsWith("\"", StringComparison.OrdinalIgnoreCase) && value.EndsWith("\"", StringComparison.OrdinalIgnoreCase)) || (value.StartsWith("'", StringComparison.OrdinalIgnoreCase) && value.EndsWith("'", StringComparison.OrdinalIgnoreCase)))
                                {
                                    value = value[1..^1];
                                    isName = true;
                                }

                                if (childCount == 0 && node.Attributes.Count == 3 && string.IsNullOrEmpty(node.Name) && double.IsNaN(node.Length) && double.IsNaN(node.Support))
                                {
                                    isName = true;
                                }

                                if (string.IsNullOrEmpty(node.Name) && !withinBrackets && !closedOuterBrackets && (isName || !int.TryParse(value.Substring(0, 1), out _)))
                                {
                                    node.Name = value;
                                }
                                else
                                {
                                    if (double.IsNaN(node.Support) && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double result3))
                                    {
                                        node.Support = result3;
                                    }
                                    else
                                    {

                                        string name = "Unknown";

                                        if (node.Attributes.ContainsKey(name))
                                        {
                                            int ind = 2;
                                            string newName = name + ind.ToString(System.Globalization.CultureInfo.InvariantCulture);

                                            while (node.Attributes.ContainsKey(newName))
                                            {
                                                ind++;
                                                newName = name + ind.ToString(System.Globalization.CultureInfo.InvariantCulture);
                                            }

                                            name = newName;
                                        }

                                        node.Attributes.Add(name, value);
                                    }
                                }
                                break;
                        }
                    }

                    lastSeparator = c2;
                    nameFinished = false;
                    attributeName.Clear();
                    attributeValue.Clear();

                    if (closedOuterBrackets)
                    {
                        closedOuterBrackets = false;
                        expectedClosingBrackets = '\0';
                        start = true;
                        withinBrackets = false;
                    }

                    if (expectedClosingBrackets != '\0')
                    {
                        withinBrackets = true;
                    }

                }
                else
                {
                    if (closedOuterBrackets)
                    {
                        closedOuterBrackets = false;
                        expectedClosingBrackets = '\0';
                        start = true;
                        withinBrackets = false;
                    }

                    if (expectedClosingBrackets != '\0')
                    {
                        withinBrackets = true;
                    }

                    if (c2 == '[' && !escaped && !openQuotes && !openApostrophe)
                    {
                        openSquareCount++;
                    }
                    else if (c2 == ']' && !escaped && !openQuotes && !openApostrophe)
                    {
                        if (openSquareCount > 0)
                        {
                            openSquareCount--;
                        }
                        else if (expectedClosingBrackets == c2)
                        {
                            closedOuterBrackets = true;
                        }
                    }
                    else if (c2 == '{' && !escaped && !openQuotes && !openApostrophe)
                    {
                        openCurlyCount++;
                    }
                    else if (c2 == '}' && !escaped && !openQuotes && !openApostrophe)
                    {
                        if (openCurlyCount > 0)
                        {
                            openCurlyCount--;
                        }
                    }


                    if (!closedOuterBrackets && !escaping)
                    {
                        if (!nameFinished)
                        {
                            attributeName.Append(c2);
                        }
                        else
                        {
                            attributeValue.Append(c2);
                        }

                    }
                }
            }

            if (double.IsNaN(node.Support) && node.Attributes.ContainsKey("prob"))
            {
                node.Support = Convert.ToDouble(node.Attributes["prob"], System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Writes a <see cref="TreeNode"/> to a <see cref="string"/>.
        /// </summary>
        /// <param name="tree">The tree to write.</param>
        /// <param name="nwka">If this is false, a Newick-compliant string is produced, only including the <see cref="TreeNode.Name"/>, <see cref="TreeNode.Length"/> and <see cref="TreeNode.Support"/> attributes of each branch.
        /// Otherwise, a Newick-with-Attributes string is produced, including all attributes.</param>
        /// <param name="singleQuoted">If <paramref name="nwka"/> is false, this determines whether the names of the nodes are placed between single quotes.</param>
        /// <returns>A <see cref="string"/> containing the Newick or NWKA representation of the <see cref="TreeNode"/>.</returns>
        public static string WriteTree(TreeNode tree, bool nwka, bool singleQuoted = false)
        {
            Contract.Requires(tree != null);

            if (!nwka)
            {
                if (tree.Children.Count == 0)
                {
                    StringBuilder tbr = new StringBuilder();
                    if (singleQuoted)
                    {
                        tbr.Append("'");
                        tbr.Append(tree.Name.Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                        tbr.Append("'");
                    }
                    else
                    {
                        tbr.Append(tree.Name);
                    }
                    if (!double.IsNaN(tree.Length))
                    {
                        tbr.Append(":");
                        tbr.Append(tree.Length.ToString(CultureInfo.InvariantCulture));
                    }
                    if (tree.Parent == null)
                    {
                        tbr.Append(";");
                    }
                    return tbr.ToString();
                }
                else
                {
                    StringBuilder tbr = new StringBuilder("(");

                    for (int i = 0; i < tree.Children.Count; i++)
                    {
                        tbr.Append(WriteTree(tree.Children[i], false, singleQuoted));
                        if (i < tree.Children.Count - 1)
                        {
                            tbr.Append(",");
                        }
                    }
                    tbr.Append(")");
                    if (!string.IsNullOrEmpty(tree.Name) && (singleQuoted || double.IsNaN(tree.Support)))
                    {
                        if (singleQuoted)
                        {
                            tbr.Append("'");
                            tbr.Append(tree.Name.Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                            tbr.Append("'");
                        }
                        else
                        {
                            tbr.Append(tree.Name);
                        }
                    }
                    if (!double.IsNaN(tree.Support))
                    {
                        tbr.Append(tree.Support.ToString(CultureInfo.InvariantCulture));
                    }
                    if (!double.IsNaN(tree.Length))
                    {
                        tbr.Append(":");
                        tbr.Append(tree.Length.ToString(CultureInfo.InvariantCulture));
                    }
                    if (tree.Parent == null)
                    {
                        tbr.Append(";");
                    }
                    return tbr.ToString();
                }
            }
            else
            {
                if (tree.Children.Count == 0)
                {
                    StringBuilder tbr = new StringBuilder();

                    if (!string.IsNullOrEmpty(tree.Name))
                    {
                        tbr.Append("'");
                        tbr.Append(tree.Name.Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                        tbr.Append("'");
                    }

                    if (!double.IsNaN(tree.Length))
                    {
                        tbr.Append(":");
                        tbr.Append(tree.Length.ToString(CultureInfo.InvariantCulture));
                    }

                    if (tree.Attributes.Count > 3)
                    {
                        tbr.Append("[");
                        bool first = true;
                        foreach (KeyValuePair<string, object> attribute in tree.Attributes)
                        {
                            if (!attribute.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) && !attribute.Key.Equals("Length", StringComparison.OrdinalIgnoreCase))
                            {
                                if (attribute.Value is double)
                                {
                                    tbr.Append((!first ? "," : ""));
                                    tbr.Append(attribute.Key);
                                    tbr.Append("=");
                                    tbr.Append(((double)attribute.Value).ToString(CultureInfo.InvariantCulture));
                                }
                                else
                                {
                                    if (!attribute.Value.ToString().Contains('\'', StringComparison.OrdinalIgnoreCase))
                                    {
                                        tbr.Append(!first ? "," : "");
                                        tbr.Append(attribute.Key);
                                        tbr.Append("='");
                                        tbr.Append(attribute.Value.ToString().Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                                        tbr.Append("'");
                                    }
                                    else
                                    {
                                        tbr.Append(!first ? "," : "");
                                        tbr.Append(attribute.Key);
                                        tbr.Append("=\"");
                                        tbr.Append(attribute.Value.ToString().Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                                        tbr.Append("\"");
                                    }

                                }
                                first = false;
                            }
                        }
                        tbr.Append("]");
                    }

                    if (tree.Parent == null)
                    {
                        tbr.Append(";");
                    }
                    return tbr.ToString();
                }
                else
                {
                    StringBuilder tbr = new StringBuilder("(");

                    for (int i = 0; i < tree.Children.Count; i++)
                    {
                        tbr.Append(WriteTree(tree.Children[i], true, true));
                        if (i < tree.Children.Count - 1)
                        {
                            tbr.Append(",");
                        }
                    }
                    tbr.Append(")");

                    if (!string.IsNullOrEmpty(tree.Name))
                    {
                        tbr.Append("'");
                        tbr.Append(tree.Name.Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                        tbr.Append("'");
                    }
                    if (tree.Support >= 0)
                    {
                        tbr.Append(tree.Support.ToString(CultureInfo.InvariantCulture));
                    }
                    if (!double.IsNaN(tree.Length))
                    {
                        tbr.Append(":");
                        tbr.Append(tree.Length.ToString(CultureInfo.InvariantCulture));
                    }

                    if (tree.Attributes.Count > 3)
                    {
                        tbr.Append("[");
                        bool first = true;
                        foreach (KeyValuePair<string, object> attribute in tree.Attributes)
                        {
                            if (!attribute.Key.Equals("Name", StringComparison.OrdinalIgnoreCase) && !attribute.Key.Equals("Support", StringComparison.OrdinalIgnoreCase) && !attribute.Key.Equals("Length", StringComparison.OrdinalIgnoreCase))
                            {
                                if (attribute.Value is double)
                                {
                                    tbr.Append(!first ? "," : "");
                                    tbr.Append(attribute.Key);
                                    tbr.Append("=");
                                    tbr.Append(((double)attribute.Value).ToString(CultureInfo.InvariantCulture));
                                }
                                else
                                {
                                    if (!attribute.Value.ToString().Contains('\'', StringComparison.OrdinalIgnoreCase))
                                    {
                                        tbr.Append(!first ? "," : "");
                                        tbr.Append(attribute.Key);
                                        tbr.Append("='");
                                        tbr.Append(attribute.Value.ToString().Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                                        tbr.Append("'");
                                    }
                                    else
                                    {
                                        tbr.Append(!first ? "," : "");
                                        tbr.Append(attribute.Key);
                                        tbr.Append("=\"");
                                        tbr.Append(attribute.Value.ToString().Replace("\\", "\\\\", StringComparison.OrdinalIgnoreCase).Replace("'", "\\'", StringComparison.OrdinalIgnoreCase).Replace("\"", "\\\"", StringComparison.OrdinalIgnoreCase));
                                        tbr.Append("\"");
                                    }

                                }
                                first = false;
                            }
                        }
                        tbr.Append("]");
                    }

                    if (tree.Parent == null)
                    {
                        tbr.Append(";");
                    }
                    return tbr.ToString();
                }
            }
        }

        /// <summary>
        /// Writes a single tree in Newick o Newick-with-Attributes format.
        /// </summary>
        /// <param name="tree">The tree to be written.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the tree should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="nwka">If this is false, a Newick-compliant string is produced for each tree, only including the <see cref="TreeNode.Name"/>, <see cref="TreeNode.Length"/> and <see cref="TreeNode.Support"/> attributes of each branch.
        /// Otherwise, a Newick-with-Attributes string is produced, including all attributes.</param>
        /// <param name="singleQuoted">If <paramref name="nwka"/> is false, this determines whether the names of the nodes are placed between single quotes.</param>
        public static void WriteTree(TreeNode tree, Stream outputStream, bool keepOpen = false, bool nwka = true, bool singleQuoted = false)
        {
            WriteAllTrees(new List<TreeNode> { tree }, outputStream, keepOpen, null, nwka, singleQuoted);
        }

        /// <summary>
        /// Writes a single tree in Newick o Newick-with-Attributes format.
        /// </summary>
        /// <param name="tree">The tree to be written.</param>
        /// <param name="outputFile">The file on which the tree should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="nwka">If this is false, a Newick-compliant string is produced for each tree, only including the <see cref="TreeNode.Name"/>, <see cref="TreeNode.Length"/> and <see cref="TreeNode.Support"/> attributes of each branch.
        /// Otherwise, a Newick-with-Attributes string is produced, including all attributes.</param>
        /// <param name="singleQuoted">If <paramref name="nwka"/> is false, this determines whether the names of the nodes are placed between single quotes.</param>
        public static void WriteTree(TreeNode tree, string outputFile, bool append = false, bool nwka = true, bool singleQuoted = false)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(new List<TreeNode>() { tree }, outputStream, false, null, nwka, singleQuoted);
        }

        /// <summary>
        /// Writes trees in Newick o Newick-with-Attributes format.
        /// </summary>
        /// <param name="trees">An <see cref="IEnumerable{T}"/> containing the trees to be written. It will ony be enumerated once.</param>
        /// <param name="outputFile">The file on which the trees should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with the number of trees written so far.</param>
        /// <param name="nwka">If this is false, a Newick-compliant string is produced for each tree, only including the <see cref="TreeNode.Name"/>, <see cref="TreeNode.Length"/> and <see cref="TreeNode.Support"/> attributes of each branch.
        /// Otherwise, a Newick-with-Attributes string is produced, including all attributes.</param>
        /// <param name="singleQuoted">If <paramref name="nwka"/> is false, this determines whether the names of the nodes are placed between single quotes.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, string outputFile, bool append = false, Action<int> progressAction = null, bool nwka = true, bool singleQuoted = false)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(trees, outputStream, false, progressAction, nwka, singleQuoted);
        }

        /// <summary>
        /// Writes trees in Newick o Newick-with-Attributes format.
        /// </summary>
        /// <param name="trees">An <see cref="IEnumerable{T}"/> containing the trees to be written. It will ony be enumerated once.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the trees should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with the number of trees written so far.</param>
        /// <param name="nwka">If this is false, a Newick-compliant string is produced for each tree, only including the <see cref="TreeNode.Name"/>, <see cref="TreeNode.Length"/> and <see cref="TreeNode.Support"/> attributes of each branch.
        /// Otherwise, a Newick-with-Attributes string is produced, including all attributes.</param>
        /// <param name="singleQuoted">If <paramref name="nwka"/> is false, this determines whether the names of the nodes are placed between single quotes.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, Stream outputStream, bool keepOpen = false, Action<int> progressAction = null, bool nwka = true, bool singleQuoted = false)
        {
            Contract.Requires(trees != null);
            using StreamWriter sw = new StreamWriter(outputStream, Encoding.UTF8, 1024, keepOpen);
            int count = 0;
            foreach (TreeNode tree in trees)
            {
                sw.WriteLine(WriteTree(tree, nwka, singleQuoted));
                count++;
                progressAction?.Invoke(count);
            }
        }

        /// <summary>
        /// Writes trees in Newick o Newick-with-Attributes format.
        /// </summary>
        /// <param name="trees">A collection of trees to be written. Each tree will only be accessed once.</param>
        /// <param name="outputFile">The file on which the trees should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with a value between 0 and 1 depending on how many trees have been written so far.</param>
        /// <param name="nwka">If this is false, a Newick-compliant string is produced for each tree, only including the <see cref="TreeNode.Name"/>, <see cref="TreeNode.Length"/> and <see cref="TreeNode.Support"/> attributes of each branch.
        /// Otherwise, a Newick-with-Attributes string is produced, including all attributes.</param>
        /// <param name="singleQuoted">If <paramref name="nwka"/> is false, this determines whether the names of the nodes are placed between single quotes.</param>
        public static void WriteAllTrees(IList<TreeNode> trees, string outputFile, bool append = false, Action<double> progressAction = null, bool nwka = true, bool singleQuoted = false)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(trees, outputStream, false, progressAction, nwka, singleQuoted);
        }

        /// <summary>
        /// Writes trees in Newick o Newick-with-Attributes format.
        /// </summary>
        /// <param name="trees">A collection of trees to be written. Each tree will only be accessed once.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the trees should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with a value between 0 and 1 depending on how many trees have been written so far.</param>
        /// <param name="nwka">If this is false, a Newick-compliant string is produced for each tree, only including the <see cref="TreeNode.Name"/>, <see cref="TreeNode.Length"/> and <see cref="TreeNode.Support"/> attributes of each branch.
        /// Otherwise, a Newick-with-Attributes string is produced, including all attributes.</param>
        /// <param name="singleQuoted">If <paramref name="nwka"/> is false, this determines whether the names of the nodes are placed between single quotes.</param>
        public static void WriteAllTrees(IList<TreeNode> trees, Stream outputStream, bool keepOpen = false, Action<double> progressAction = null, bool nwka = true, bool singleQuoted = false)
        {
            using StreamWriter sw = new StreamWriter(outputStream, Encoding.UTF8, 1024, keepOpen);
            for (int i = 0; i < trees.Count; i++)
            {
                sw.WriteLine(WriteTree(trees[i], nwka, singleQuoted));
                progressAction?.Invoke((double)i / trees.Count);
            }
        }

    }
}
