using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using PhyloTree.Extensions;

namespace PhyloTree.Formats
{
    /// <summary>
    /// Contains methods to read and write trees in NEXUS format.
    /// </summary>
    public static class NEXUS
    {
        /// <summary>
        /// Possible states while reading a NEXUS file
        /// </summary>
        private enum NEXUSStatus
        {
            /// <summary>
            /// At the root of the NEXUS structure
            /// </summary>
            Root,

            /// <summary>
            /// Inside a comment at the root of the NEXUS structure
            /// </summary>
            InCommentInRoot,

            /// <summary>
            /// Inside a block that is not a "Trees" block
            /// </summary>
            InOtherBlock,

            /// <summary>
            /// Inside a comment inside a block that is not a "Trees" block.
            /// </summary>
            InCommentInOtherBlock,

            /// <summary>
            /// Inside a "Trees" block
            /// </summary>
            InTreeBlock,

            /// <summary>
            /// Inside a "Translate" statement inside a "Trees" block.
            /// </summary>
            InTranslateStatement,

            /// <summary>
            /// Inside a "Tree" statement inside a "Trees" block.
            /// </summary>
            InTreeStatement,

            /// <summary>
            /// Inside a comment inside a "Trees" block.
            /// </summary>
            InCommentInTreeBlock,

            /// <summary>
            /// Inside a comment inside a "Translate" statement inside a "Trees" block
            /// </summary>
            InCommentInTranslateStatement,

            /// <summary>
            /// Inside a comment before the equal sign inside a "Tree" statement inside a "Trees" block
            /// </summary>
            InCommentInTreeStatementName
        }

        /// <summary>
        /// Parses a NEXUS file and completely loads it into memory. Can be used to parse a string or a file.
        /// </summary>
        /// <param name="sourceString">The NEXUS file content. If this parameter is specified, <paramref name="sourceStream"/> is ignored.</param>
        /// <param name="sourceStream">The stream to parse.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that might be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the "Trees" blocks of the NEXUS file.</returns>
        public static List<TreeNode> ParseAllTrees(string sourceString = null, Stream sourceStream = null, bool keepOpen = false, Action<double> progressAction = null)
        {
            return ParseTrees(sourceString, sourceStream, keepOpen, progressAction).ToList();
        }


        /// <summary>
        /// Lazily parses a NEXUS file. Each tree in the NEXUS file is not read and parsed until it is requested. Can be used to parse a <see cref="string"/> or a <see cref="Stream"/>.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <param name="progressAction">An <see cref="Action" /> that might be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the "Trees" blocks of the NEXUS file.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000")]
        public static IEnumerable<TreeNode> ParseTrees(string inputFile, Action<double> progressAction = null)
        {
            FileStream inputStream = File.OpenRead(inputFile);
            return ParseTrees(sourceStream: inputStream, keepOpen: false, progressAction: progressAction);
        }

        /// <summary>
        /// Lazily parses a NEXUS file. Each tree in the NEXUS file is not read and parsed until it is requested. Can be used to parse a <see cref="string"/> or a <see cref="Stream"/>.
        /// </summary>
        /// <param name="sourceString">The NEXUS file content. If this parameter is specified, <paramref name="sourceStream"/> is ignored.</param>
        /// <param name="sourceStream">The stream to parse.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that might be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the "Trees" blocks of the NEXUS file.</returns>
        public static IEnumerable<TreeNode> ParseTrees(string sourceString = null, Stream sourceStream = null, bool keepOpen = false, Action<double> progressAction = null)
        {
            bool isUsingSourceString = !string.IsNullOrEmpty(sourceString);

            using TextReader reader = isUsingSourceString ? (TextReader)(new StringReader(sourceString)) : (TextReader)(new StreamReader(sourceStream, Encoding.UTF8, true, 1024, keepOpen));

            double totalLength = isUsingSourceString ? sourceString.Length : ((StreamReader)reader).BaseStream.Length;

            Func<long> currentPos;

            if (isUsingSourceString)
            {
                System.Reflection.FieldInfo fi = typeof(StringReader).GetField("_pos", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                currentPos = () =>
                {
                    return (int)fi.GetValue(reader);
                };
            }
            else
            {
                currentPos = () =>
                {
                    return ((StreamReader)reader).BaseStream.Position;
                };
            }

            NEXUSStatus status = NEXUSStatus.Root;

            string word = reader.NextWord(out bool eof);

            Dictionary<string, string> translateDictionary = new Dictionary<string, string>();

            string treeName;

            while (!eof)
            {
                switch (status)
                {
                    case NEXUSStatus.Root:
                        if (word.Equals("begin", StringComparison.OrdinalIgnoreCase))
                        {
                            word = reader.NextWord(out _);

                            if (word.Equals("trees", StringComparison.OrdinalIgnoreCase))
                            {
                                status = NEXUSStatus.InTreeBlock;
                            }
                            else
                            {
                                status = NEXUSStatus.InOtherBlock;
                            }
                        }
                        else if (word.Equals("[", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InCommentInRoot;
                        }
                        break;
                    case NEXUSStatus.InCommentInRoot:
                        if (word.Equals("]", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.Root;
                        }
                        break;
                    case NEXUSStatus.InOtherBlock:
                        if (word.Equals("end", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.Root;
                        }
                        else if (word.Equals("[", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InCommentInOtherBlock;
                        }
                        break;
                    case NEXUSStatus.InCommentInOtherBlock:
                        if (word.Equals("]", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InOtherBlock;
                        }
                        break;
                    case NEXUSStatus.InTreeBlock:
                        if (word.Equals("translate", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InTranslateStatement;
                        }
                        else if (word.Equals("tree", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InTreeStatement;
                        }
                        else if (word.Equals("end", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.Root;
                        }
                        else if (word.Equals("[", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InCommentInTreeBlock;
                        }
                        break;
                    case NEXUSStatus.InCommentInTreeBlock:
                        if (word.Equals("]", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InTreeBlock;
                        }
                        break;
                    case NEXUSStatus.InTranslateStatement:
                        if (word.Equals("[", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InCommentInTranslateStatement;
                        }
                        else if (word.Equals(";", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InTreeBlock;
                        }
                        else if (word.Equals(",", StringComparison.OrdinalIgnoreCase))
                        { }
                        else
                        {
                            string name = word;
                            word = reader.NextWord(out _);

                            if ((name.StartsWith("'", StringComparison.OrdinalIgnoreCase) && name.EndsWith("'", StringComparison.OrdinalIgnoreCase)) || (name.StartsWith("\"", StringComparison.OrdinalIgnoreCase) && name.EndsWith("\"", StringComparison.OrdinalIgnoreCase)))
                            {
                                name = name[1..^1];
                            }

                            if ((word.StartsWith("'", StringComparison.OrdinalIgnoreCase) && word.EndsWith("'", StringComparison.OrdinalIgnoreCase)) || (word.StartsWith("\"", StringComparison.OrdinalIgnoreCase) && word.EndsWith("\"", StringComparison.OrdinalIgnoreCase)))
                            {
                                word = word[1..^1];
                            }

                            translateDictionary.Add(name, word);
                        }
                        break;
                    case NEXUSStatus.InCommentInTranslateStatement:
                        if (word.Equals("]", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InTranslateStatement;
                        }
                        break;
                    case NEXUSStatus.InCommentInTreeStatementName:
                        if (word.Equals("]", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InTreeStatement;
                        }
                        break;
                    case NEXUSStatus.InTreeStatement:
                        if (word.Equals("[", StringComparison.OrdinalIgnoreCase))
                        {
                            status = NEXUSStatus.InCommentInTreeStatementName;
                        }
                        else
                        {
                            treeName = word;
                            bool escaping = false;
                            bool openQuotes = false;
                            bool openApostrophe = false;
                            bool openComment = false;

                            char c = reader.NextToken(ref escaping, out bool escaped, ref openQuotes, ref openApostrophe, out eof);

                            while (!eof && c != '=')
                            {
                                if (c == '[')
                                {
                                    openComment = true;
                                }

                                if (c == ']')
                                {
                                    openComment = false;
                                }

                                c = reader.NextToken(ref escaping, out escaped, ref openQuotes, ref openApostrophe, out eof);
                            }

                            StringBuilder preComments = new StringBuilder();
                            StringBuilder tree = new StringBuilder();

                            c = reader.NextToken(ref escaping, out escaped, ref openQuotes, ref openApostrophe, out eof);

                            while (!(c == '(' && !openComment) && !eof)
                            {
                                preComments.Append(c);

                                if (c == '[')
                                {
                                    openComment = true;
                                }

                                if (c == ']')
                                {
                                    openComment = false;
                                }

                                c = reader.NextToken(ref escaping, out escaped, ref openQuotes, ref openApostrophe, out eof);
                            }



                            while (!(c == ';' && !openComment && !escaped && !openQuotes && !openApostrophe) && !eof)
                            {
                                tree.Append(c);

                                if (c == '[')
                                {
                                    openComment = true;
                                }

                                if (c == ']')
                                {
                                    openComment = false;
                                }


                                c = reader.NextToken(ref escaping, out escaped, ref openQuotes, ref openApostrophe, out eof);
                            }


                            TreeNode parsedTree = NWKA.ParseTree(tree.ToString());

                            if (!parsedTree.Attributes.ContainsKey("TreeName"))
                            {
                                parsedTree.Attributes.Add("TreeName", treeName);
                            }

                            List<TreeNode> nodes = parsedTree.GetChildrenRecursive();

                            foreach (TreeNode node in nodes)
                            {
                                if (!string.IsNullOrEmpty(node.Name) && translateDictionary.TryGetValue(node.Name, out string newName))
                                {
                                    node.Name = newName;
                                }
                            }

                            bool tempEof = false;

                            string tempGuid = Guid.NewGuid().ToString();

                            parsedTree.Name = tempGuid;

                            string preCommentsString = preComments.ToString();

                            if (preCommentsString != "[&R]" && preCommentsString != "[&U]")
                            {
                                using StringReader sr = new StringReader(preCommentsString);
                                NWKA.ParseAttributes(sr, ref tempEof, parsedTree, parsedTree.Children.Count);
                            }

                            if (parsedTree.Name == tempGuid)
                            {
                                parsedTree.Name = null;
                            }

                            yield return parsedTree;

                            double progress = Math.Max(0, Math.Min(1, currentPos() / totalLength));

                            progressAction?.Invoke(progress);

                            status = NEXUSStatus.InTreeBlock;
                        }
                        break;
                }

                word = reader.NextWord(out eof);
            }
        }

        /// <summary>
        /// Lazily parses trees from a file in NEXUS format. Each tree in the file is not read and parsed until it is requested.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that will be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A lazy <see cref="IEnumerable{T}"/> containing the trees defined in the file.</returns>
        public static IEnumerable<TreeNode> ParseTrees(Stream inputStream, bool keepOpen = false, Action<double> progressAction = null)
        {
            return ParseTrees(null, inputStream, keepOpen, progressAction);
        }

        /// <summary>
        /// Parses trees from a file in NEXUS format and completely loads them in memory.
        /// </summary>
        /// <param name="inputFile">The path to the input file.</param>
        /// <param name="progressAction">An <see cref="Action" /> that will be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the file.</returns>
        public static List<TreeNode> ParseAllTrees(string inputFile, Action<double> progressAction = null)
        {
            using FileStream inputStream = File.OpenRead(inputFile);
            return ParseAllTrees(inputStream, false, progressAction);
        }

        /// <summary>
        /// Parses trees from a file in NEXUS format and completely loads them in memory.
        /// </summary>
        /// <param name="inputStream">The <see cref="Stream"/> from which the file should be read.</param>
        /// <param name="keepOpen">Determines whether the stream should be disposed at the end of this method or not.</param>
        /// <param name="progressAction">An <see cref="Action" /> that will be called after each tree is parsed, with the approximate progress (as determined by the position in the stream), ranging from 0 to 1.</param>
        /// <returns>A <see cref="List{T}"/> containing the trees defined in the file.</returns>
        public static List<TreeNode> ParseAllTrees(Stream inputStream, bool keepOpen = false, Action<double> progressAction = null)
        {
            return ParseTrees(inputStream, keepOpen, progressAction).ToList();
        }

        /// <summary>
        /// Writes a single tree in NEXUS format.
        /// </summary>
        /// <param name="tree">The tree to be written.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the tree should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="translate">If this is <c>true</c>, a <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are added to the NEXUS file.</param>
        /// <param name="translateQuotes">If this is <c>true</c>, entries in the <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are placed between single quotes. Otherwise, they are not. This has no effect if <paramref name="translate"/> is <c>false</c>.</param>
        /// <param name="additionalNexusBlocks">A <see cref="TextReader"/> that can read additional NEXUS blocks that will be placed at the end of the file.</param>
        public static void WriteTree(TreeNode tree, Stream outputStream, bool keepOpen = false, bool translate = true, bool translateQuotes = true, TextReader additionalNexusBlocks = null)
        {
            WriteAllTrees(new List<TreeNode>() { tree }, outputStream, keepOpen, null, translate, translateQuotes, additionalNexusBlocks);
        }

        /// <summary>
        /// Writes a single tree in NEXUS format.
        /// </summary>
        /// <param name="tree">The tree to be written.</param>
        /// <param name="outputFile">The file on which the tree should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="translate">If this is <c>true</c>, a <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are added to the NEXUS file.</param>
        /// <param name="translateQuotes">If this is <c>true</c>, entries in the <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are placed between single quotes. Otherwise, they are not. This has no effect if <paramref name="translate"/> is <c>false</c>.</param>
        /// <param name="additionalNexusBlocks">A <see cref="TextReader"/> that can read additional NEXUS blocks that will be placed at the end of the file.</param>
        public static void WriteTree(TreeNode tree, string outputFile, bool append = false, bool translate = true, bool translateQuotes = true, TextReader additionalNexusBlocks = null)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(new List<TreeNode>() { tree }, outputStream, false, null, translate, translateQuotes, additionalNexusBlocks);
        }

        /// <summary>
        /// Writes trees in NEXUS format.
        /// </summary>
        /// <param name="trees">A collection of trees to be written. If <paramref name="translate"/> is <c>true</c>, each tree will be accessed twice. Otherwise, each tree will be accessed once.</param>
        /// <param name="outputFile">The file on which the trees should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with a value between 0 and 1 depending on how many trees have been written so far.</param>
        /// <param name="translate">If this is <c>true</c>, a <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are added to the NEXUS file.</param>
        /// <param name="translateQuotes">If this is <c>true</c>, entries in the <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are placed between single quotes. Otherwise, they are not. This has no effect if <paramref name="translate"/> is <c>false</c>.</param>
        /// <param name="additionalNexusBlocks">A <see cref="TextReader"/> that can read additional NEXUS blocks that will be placed at the end of the file.</param>
        public static void WriteAllTrees(IList<TreeNode> trees, string outputFile, bool append = false, Action<double> progressAction = null, bool translate = true, bool translateQuotes = true, TextReader additionalNexusBlocks = null)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(trees, outputStream, false, progressAction, translate, translateQuotes, additionalNexusBlocks);
        }

        /// <summary>
        /// Writes trees in NEXUS format.
        /// </summary>
        /// <param name="trees">A collection of trees to be written. If <paramref name="translate"/> is <c>true</c>, each tree will be accessed twice. Otherwise, each tree will be accessed once.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the trees should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with a value between 0 and 1 depending on how many trees have been written so far.</param>
        /// <param name="translate">If this is <c>true</c>, a <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are added to the NEXUS file.</param>
        /// <param name="translateQuotes">If this is <c>true</c>, entries in the <c>Taxa</c> block and a <c>Translate</c> statement in the <c>Trees</c> block are placed between single quotes. Otherwise, they are not. This has no effect if <paramref name="translate"/> is <c>false</c>.</param>
        /// <param name="additionalNexusBlocks">A <see cref="TextReader"/> that can read additional NEXUS blocks that will be placed at the end of the file.</param>
        public static void WriteAllTrees(IList<TreeNode> trees, Stream outputStream, bool keepOpen = false, Action<double> progressAction = null, bool translate = true, bool translateQuotes = true, TextReader additionalNexusBlocks = null)
        {
            Contract.Requires(trees != null);

            using StreamWriter sw = new StreamWriter(outputStream, Encoding.UTF8, 8192, keepOpen);

            sw.WriteLine("#NEXUS");
            sw.WriteLine();

            Dictionary<string, int> translationLabels = new Dictionary<string, int>();

            if (translate)
            {
                int index = 0;

                for (int i = 0; i < trees.Count; i++)
                {
                    foreach (string label in trees[i].GetLeafNames())
                    {
                        if (!translationLabels.ContainsKey(label))
                        {
                            translationLabels[label] = index;
                            index++;
                        }
                    }
                }

                sw.WriteLine("Begin Taxa;");
                sw.WriteLine("\tDimensions ntax=" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ";");
                sw.WriteLine("\tTaxLabels");

                if (!translateQuotes)
                {
                    foreach (KeyValuePair<string, int> kvp in translationLabels)
                    {
                        sw.WriteLine("\t\t" + kvp.Key);
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, int> kvp in translationLabels)
                    {
                        sw.WriteLine("\t\t'" + kvp.Key + "'");
                    }
                }

                sw.WriteLine("\t\t;");
                sw.WriteLine("End;");
                sw.WriteLine();
                sw.WriteLine("Begin Trees;");
                sw.WriteLine("\tTranslate\n");

                int count = 0;

                if (!translateQuotes)
                {
                    foreach (KeyValuePair<string, int> kvp in translationLabels)
                    {
                        count++;
                        sw.WriteLine("\t\t" + (kvp.Value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + kvp.Key + (count < index ? "," : ""));
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, int> kvp in translationLabels)
                    {
                        count++;
                        sw.WriteLine("\t\t" + (kvp.Value + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " '" + kvp.Key + "'" + (count < index ? "," : ""));
                    }
                }

                sw.WriteLine("\t\t;");
            }
            else
            {
                sw.WriteLine("Begin Trees;");
            }

            for (int i = 0; i < trees.Count; i++)
            {
                TreeNode tree = trees[i].Clone();

                foreach (TreeNode leaf in tree.GetLeaves())
                {
                    if (translationLabels.TryGetValue(leaf.Name, out int translation))
                    {
                        leaf.Name = (translation + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                }

                string treeName = "";

                if (tree.Attributes.TryGetValue("TreeName", out object value))
                {
                    treeName = value.ToString();
                }
                else
                {
                    treeName = "tree" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                sw.WriteLine("\tTree " + treeName + " = " + NWKA.WriteTree(tree, true, true));
                progressAction?.Invoke((double)(i + 1) / trees.Count);
            }

            sw.WriteLine("End;");

            if (additionalNexusBlocks != null)
            {
                sw.WriteLine();

                char[] buffer = new char[1024];

                int bytesRead;

                while ((bytesRead = additionalNexusBlocks.Read(buffer, 0, 1024)) > 0)
                {
                    sw.Write(buffer, 0, bytesRead);
                }
            }
        }

        /// <summary>
        /// Writes trees in NEXUS format.
        /// </summary>
        /// <param name="trees">An <see cref="IEnumerable{T}"/> containing the trees to be written. It will only be enumerated once.</param>
        /// <param name="outputFile">The file on which the trees should be written.</param>
        /// <param name="append">Specifies whether the file should be overwritten or appended to.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with the number of trees written so far.</param>
        /// <param name="additionalNexusBlocks">A <see cref="TextReader"/> that can read additional NEXUS blocks that will be placed at the end of the file.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, string outputFile, bool append = false, Action<int> progressAction = null, TextReader additionalNexusBlocks = null)
        {
            using FileStream outputStream = append ? new FileStream(outputFile, FileMode.Append) : File.Create(outputFile);
            WriteAllTrees(trees, outputStream, false, progressAction, additionalNexusBlocks);
        }

        /// <summary>
        /// Writes trees in NEXUS format.
        /// </summary>
        /// <param name="trees">An <see cref="IEnumerable{T}"/> containing the trees to be written. It will only be enumerated once.</param>
        /// <param name="outputStream">The <see cref="Stream"/> on which the trees should be written.</param>
        /// <param name="keepOpen">Determines whether the <paramref name="outputStream"/> should be kept open after the end of this method.</param>
        /// <param name="progressAction">An <see cref="Action"/> that will be invoked after each tree is written, with the number of trees written so far.</param>
        /// <param name="additionalNexusBlocks">A <see cref="TextReader"/> that can read additional NEXUS blocks that will be placed at the end of the file.</param>
        public static void WriteAllTrees(IEnumerable<TreeNode> trees, Stream outputStream, bool keepOpen = false, Action<int> progressAction = null, TextReader additionalNexusBlocks = null)
        {
            Contract.Requires(trees != null);

            using StreamWriter sw = new StreamWriter(outputStream, Encoding.UTF8, 8192, keepOpen);

            sw.WriteLine("#NEXUS");
            sw.WriteLine();
            sw.WriteLine("Begin Trees;");

            int treeIndex = 0;
            foreach (TreeNode tree in trees)
            {
                string treeName = "";

                if (tree.Attributes.TryGetValue("TreeName", out object value))
                {
                    treeName = value.ToString();
                }
                else
                {
                    treeName = "tree" + (treeIndex + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                sw.WriteLine("\tTree " + treeName + " = " + NWKA.WriteTree(tree, true, true));

                treeIndex++;
                progressAction?.Invoke(treeIndex);
            }

            sw.WriteLine("End;");

            if (additionalNexusBlocks != null)
            {
                sw.WriteLine();

                char[] buffer = new char[1024];

                int bytesRead;

                while ((bytesRead = additionalNexusBlocks.Read(buffer, 0, 1024)) > 0)
                {
                    sw.Write(buffer, 0, bytesRead);
                }
            }
        }
    }
}