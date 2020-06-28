/***********************************************************************
 *  read_binary_tree.cpp    2020-05-20
 *  by Giorgio Bianchini
 *  This file is part of the R package TreeNode, licensed under GPLv3
 *
 *  Methods to read trees from a file in binary tree format and pass
 *  them to R.
 ***********************************************************************/

// [[Rcpp::plugins(cpp17)]]

#include "common.h"

//Read a single byte from the stream.
static byte readByte(std::fstream* stream)
{
  byte buffer[1];
  stream->read((char*)buffer, 1);

  return buffer[0];
}

//Read multiple bytes from the stream.
static std::vector<byte> readBytes(std::fstream* stream, int count)
{
  std::vector<byte> buffer(count);
  stream->read((char*)buffer.data(), count);

  return buffer;
}

//Read a double-precision floating-point number from the stream. The
//numbers are stored in 64-bit IEEE754 format, hopefully this
//corresponds to the internal format of double on the current platform.
static double readDouble(std::fstream* stream)
{
  double buffer[1];

  stream->read((char*)&buffer, 8);

  return buffer[0];
}

//Read a 32-bit (4-byte) wide integer from the stream (little-endian).
static int32_t readInt32(std::fstream* stream)
{
  std::vector<byte> bytes = readBytes(stream, 4);

  int32_t num = 0;
  for (int i = 0; i < 4; i++)
  {
    num += bytes[i] << (8 * i);
  }

  return num;
}

//Read a 64-bit (8-byte) wide integer from the stream (little-endian).
static int64_t readInt64(std::fstream* stream)
{
  std::vector<byte> bytes = readBytes(stream, 8);

  int64_t num = 0;
  for (int i = 0; i < 8; i++)
  {
    num += (int64_t)bytes[i] << (8 * i);
  }

  return num;
}

//Read an integer from the stream. If the integer is smaller than 254,
//it is only 1-byte wide, otherwise it is 40-bit (5-byte) wide.
static int32_t readInt(std::fstream* stream)
{
  unsigned char b = readByte(stream);

  if (b < 254)
  {
    return(b);
  }
  else
  {
    return readInt32(stream);
  }
}

//Read a string from the stream. The string is stored as an integer n
//representing its length followed by n integers that constitute the
//UTF-16 representation of the string. Since codecvt_utf8 does not
//apparently work, we are stuck with a straight int->char conversion,
//which will probably only work for ASCII characters.
static std::string readMyString(std::fstream* stream)
{
  int32_t length = readInt(stream);

  std::vector<char> chars(length);

  for (int i = 0; i < length; i++)
  {
    chars[i] = (char)readInt(stream);
  }

  /*std::wstring_convert<std::codecvt_utf8<uint16_t>, uint16_t> conversion;
   uint16_t* p = chars.data();
   std::string tbr = conversion.to_bytes(p, p + length);*/

  std::string tbrStd(chars.data(), length);

  return tbrStd;
}

//Read a variable-width integer from the stream. If the integer is equal
//to 0, 2 or 3, it is 2-bit wide; if it is 1, 4 or 5, it is 4-bit wide;
//if it is greater than 5, the current byte is padded and the integer is
//represented as an integer of the format read by readInt in the
//following byte(s). The initial value of *currByte should be read
//using readByte and the initial value of *currIndex should be 0.
//Successive reads should use the same variables, which will have been
//updated by this method. After the last read, if *currIndex is equal to
//0, it means that *currByte has not been processed (thus you should
//seek back by 1).
static int32_t readShortInt(std::fstream* stream, byte* currByte, int* currIndex)
{
  if (*currIndex == 0)
  {
    int32_t twoBits = *currByte & 0b00000011;

    *currIndex = 2;

    if (twoBits == 0b00)
    {
      return 0;
    }
    else if (twoBits == 0b01)
    {
      return 2;
    }
    else if (twoBits == 0b10)
    {
      return 3;
    }
    else// if (twoBits == 0b11)
    {
      int32_t fourBits = *currByte & 0b00001111;
      *currIndex = 4;

      if (fourBits == 0b0011)
      {
        return 1;
      }
      else if (fourBits == 0b0111)
      {
        return 4;
      }
      else if (fourBits == 0b1011)
      {
        return 5;
      }
      else// if (fourBits == 0b1111)
      {
        int32_t tbr = readInt(stream);
        *currByte = readByte(stream);
        *currIndex = 0;
        return tbr;
      }
    }
  }
  else if (*currIndex == 2)
  {
    int32_t twoBits = (*currByte & 0b00001100) >> 2;

    *currIndex = 4;

    if (twoBits == 0b00)
    {
      return 0;
    }
    else if (twoBits == 0b01)
    {
      return 2;
    }
    else if (twoBits == 0b10)
    {
      return 3;
    }
    else// if (twoBits == 0b11)
    {
      int32_t fourBits = (*currByte & 0b00111100) >> 2;
      *currIndex = 6;

      if (fourBits == 0b0011)
      {
        return 1;
      }
      else if (fourBits == 0b0111)
      {
        return 4;
      }
      else if (fourBits == 0b1011)
      {
        return 5;
      }
      else// if (fourBits == 0b1111)
      {
        int32_t tbr = readInt(stream);
        *currByte = readByte(stream);
        *currIndex = 0;
        return tbr;
      }
    }
  }
  else if (*currIndex == 4)
  {
    int32_t twoBits = (*currByte & 0b00110000) >> 4;

    *currIndex = 6;

    if (twoBits == 0b00)
    {
      return 0;
    }
    else if (twoBits == 0b01)
    {
      return 2;
    }
    else if (twoBits == 0b10)
    {
      return 3;
    }
    else// if (twoBits == 0b11)
    {
      int32_t fourBits = (*currByte & 0b11110000) >> 4;
      *currIndex = 0;

      if (fourBits == 0b0011)
      {
        *currByte = readByte(stream);
        return 1;
      }
      else if (fourBits == 0b0111)
      {
        *currByte = readByte(stream);
        return 4;
      }
      else if (fourBits == 0b1011)
      {
        *currByte = readByte(stream);
        return 5;
      }
      else// if (fourBits == 0b1111)
      {
        int32_t tbr = readInt(stream);
        *currByte = readByte(stream);
        *currIndex = 0;
        return tbr;
      }
    }
  }
  else //if (*currIndex == 6)
  {
    int32_t twoBits = (*currByte & 0b11000000) >> 6;

    *currIndex = 0;
    *currByte = readByte(stream);

    if (twoBits == 0b00)
    {
      return 0;
    }
    else if (twoBits == 0b01)
    {
      return 2;
    }
    else if (twoBits == 0b10)
    {
      return 3;
    }
    else// if (twoBits == 0b11)
    {
      int32_t fourBits = twoBits | ((*currByte & 0b00000011) << 2);
      *currIndex = 2;

      if (fourBits == 0b0011)
      {
        return 1;
      }
      else if (fourBits == 0b0111)
      {
        return 4;
      }
      else if (fourBits == 0b1011)
      {
        return 5;
      }
      else// if (fourBits == 0b1111)
      {
        int32_t tbr = readInt(stream);
        *currByte = readByte(stream);
        *currIndex = 0;
        return tbr;
      }
    }
  }
}

//Read a single tree in binary format from the file stream.
static phylo readBinaryTree(std::fstream* file, bool globalNames = false, std::vector<std::string> names = std::vector<std::string>(), std::vector<Attribute> attributes = std::vector<Attribute>())
{
  int32_t numAttributes = readInt(file);

  if (numAttributes > 0)
  {
    attributes = std::vector<Attribute>(numAttributes);

    for (int i = 0; i < numAttributes; i++)
    {
      attributes[i].AttributeName = readMyString(file);
      attributes[i].IsNumeric = readInt(file) == 2;
    }
  }

  std::vector<int32_t> parents;
  std::vector<std::vector<int32_t>> children;
  std::vector<int32_t> addedChildren;

  int32_t currParent = 0;

  parents.push_back(-1);
  addedChildren.push_back(0);

  int32_t tipCount = 0;

  byte currByte = readByte(file);
  int32_t currIndex = 0;

  while (currParent >= 0)
  {
    int32_t currCount = readShortInt(file, &currByte, &currIndex);

    children.push_back(std::vector<int32_t>(currCount));

    if (currCount == 0)
    {
      tipCount++;
    }

    while (currParent >= 0 && children[currParent].size() == (size_t)addedChildren[currParent])
    {
      currParent = parents[currParent];
    }

    if (currParent >= 0)
    {
      int newNode = parents.size();
      children[currParent][addedChildren[currParent]] = newNode;
      addedChildren[currParent]++;
      parents.push_back(currParent);
      addedChildren.push_back(0);
      currParent = newNode;
    }
  }

  if (currIndex == 0)
  {
    file->seekg(-1, std::ios_base::cur);
  }

  int32_t nodeCount = parents.size();
  std::vector<double> edgeLengths(nodeCount, std::nan(""));
  std::vector<double> nodeSupport(nodeCount, std::nan(""));
  std::vector<std::string> nodeNames(nodeCount);

  std::vector<std::vector<std::variant<std::string, double>>> nodeAttributes(attributes.size());

  int32_t nameAttributeIndex = -1;
  int32_t supportAttributeIndex = -1;

  for (size_t i = 0; i < attributes.size(); i++)
  {
    nodeAttributes[i] = std::vector<std::variant<std::string, double>>(nodeCount);

    if (attributes[i].IsNumeric)
    {
      for (int32_t j = 0; j < nodeCount; j++)
      {
        nodeAttributes[i][j] = std::nan("");
      }
    }
    else
    {
      for (int32_t j = 0; j < nodeCount; j++)
      {
        nodeAttributes[i][j] = "";
      }
    }

    if (equalCI(attributes[i].AttributeName, NAMEATTRIBUTE) && !attributes[i].IsNumeric)
    {
      nameAttributeIndex = i;
    }
    else if (equalCI(attributes[i].AttributeName, SUPPORTATTRIBUTE) && attributes[i].IsNumeric)
    {
      supportAttributeIndex = i;
    }
  }

  for (int i = 0; i < nodeCount; i++)
  {
    int32_t attributeCount = readInt(file);

    for (int j = 0; j < attributeCount; j++)
    {
      int32_t attributeIndex = readInt(file);

      std::string attributeName = attributes[attributeIndex].AttributeName;

      bool isNumeric = attributes[attributeIndex].IsNumeric;

      if (isNumeric)
      {
        if (equalCI(attributeName, LENGTHATTRIBUTE))
        {
          edgeLengths[i] = readDouble(file);
          nodeAttributes[attributeIndex][i] = edgeLengths[i];
        }
        else if (equalCI(attributeName, SUPPORTATTRIBUTE))
        {
          nodeSupport[i] = readDouble(file);
          nodeAttributes[attributeIndex][i] = nodeSupport[i];
        }
        else
        {
          nodeAttributes[attributeIndex][i] = readDouble(file);
        }
      }
      else if (!equalCI(attributeName, NAMEATTRIBUTE))
      {
        nodeAttributes[attributeIndex][i] = readMyString(file);
      }
      else
      {
        if (!globalNames)
        {
          nodeNames[i] = readMyString(file);
        }
        else
        {
          byte b = readByte(file);

          if (b == 0)
          {
            nodeNames[i] = "";
          }
          else if (b <= 254)
          {
            file->seekg(-1, std::ios::cur);
            int32_t index = readInt(file);
            nodeNames[i] = names[index - 1];
          }
          else //if (b == 255)
          {
            nodeNames[i] = readMyString(file);
          }
        }

        nodeAttributes[attributeIndex][i] = nodeNames[i];
      }
    }
  }

  std::vector<int32_t> correspondences(nodeCount);
  std::vector<std::string> tipLabels(tipCount);
  std::vector<std::string> nodeLabels(nodeCount - tipCount);

  std::vector<std::variant<std::vector<std::string>, std::vector<double>>> internalNodeAttributes(attributes.size());
  std::vector<std::variant<std::vector<std::string>, std::vector<double>>> tipAttributes(attributes.size());

  for (size_t i = 0; i < attributes.size(); i++)
  {
    if (attributes[i].IsNumeric)
    {
      internalNodeAttributes[i] = std::vector<double>(nodeCount - tipCount);
      tipAttributes[i] = std::vector<double>(tipCount);
    }
    else
    {
      internalNodeAttributes[i] = std::vector<std::string>(nodeCount - tipCount);
      tipAttributes[i] = std::vector<std::string>(tipCount);
    }
  }

  int32_t tipIndex = -1;
  int32_t nonTipIndex = -1;

  for (int i = 0; i < nodeCount; i++)
  {
    if (children[i].size() == 0)
    {
      tipIndex++;
      correspondences[i] = tipIndex;
      tipLabels[tipIndex] = nodeNames[i];

      for (size_t j = 0; j < attributes.size(); j++)
      {
        if (attributes[j].IsNumeric)
        {
          std::get<std::vector<double>>(tipAttributes[j])[tipIndex] = std::get<double>(nodeAttributes[j][i]);
        }
        else
        {
          std::get<std::vector<std::string>>(tipAttributes[j])[tipIndex] = std::get<std::string>(nodeAttributes[j][i]);
        }
      }
    }
    else
    {
      nonTipIndex++;
      correspondences[i] = nonTipIndex + tipCount;

      for (size_t j = 0; j < attributes.size(); j++)
      {
        if (attributes[j].IsNumeric)
        {
          std::get<std::vector<double>>(internalNodeAttributes[j])[nonTipIndex] = std::get<double>(nodeAttributes[j][i]);
        }
        else
        {
          std::get<std::vector<std::string>>(internalNodeAttributes[j])[nonTipIndex] = std::get<std::string>(nodeAttributes[j][i]);
        }
      }
    }
  }

  std::vector<std::vector<int32_t>> edges(nodeCount - 1);
  std::vector<double> correspEdgeLengths(nodeCount - 1);

  for (int i = 1; i < nodeCount; i++)
  {
    edges[i - 1] = std::vector<int32_t>(2);

    edges[i - 1][0] = correspondences[parents[i]];
    edges[i - 1][1] = correspondences[i];
    correspEdgeLengths[i - 1] = edgeLengths[i];
  }

  phylo tbr;

  tbr.Nnode = nodeCount - tipCount;
  tbr.edge = edges;
  tbr.tipLabel = tipLabels;
  tbr.edgeLength = correspEdgeLengths;

  for (int i = 0; i < nodeCount - 1; i++)
  {
    if (!std::isnan(correspEdgeLengths[i]))
    {
      tbr.hasEdgeLength = true;
      break;
    }
  }

  tbr.tipAttributes = tipAttributes;
  tbr.nodeAttributes = internalNodeAttributes;
  tbr.attributes = attributes;

  tbr.rootEdge = edgeLengths[0];

  bool found = false;

  if (nameAttributeIndex >= 0)
  {
    for (size_t i = 0; i < std::get<std::vector<std::string>>(internalNodeAttributes[nameAttributeIndex]).size(); i++)
    {
      if (std::get<std::vector<std::string>>(internalNodeAttributes[nameAttributeIndex])[i] != "")
      {
        found = true;
        break;
      }
    }
  }

  if (found)
  {
    tbr.nodeLabel = std::get<std::vector<std::string>>(internalNodeAttributes[nameAttributeIndex]);
    tbr.hasNodeLabel = true;
  }
  else if (supportAttributeIndex >= 0)
  {
    for (size_t i = 0; i < std::get<std::vector<double>>(internalNodeAttributes[supportAttributeIndex]).size(); i++)
    {
      if (std::get<std::vector<double>>(internalNodeAttributes[supportAttributeIndex])[i] > 0)
      {
        found = true;
        break;
      }
    }

    if (found)
    {
      tbr.nodeLabel = std::vector<std::string>(std::get<std::vector<double>>(internalNodeAttributes[supportAttributeIndex]).size());

      for (size_t i = 0; i < std::get<std::vector<double>>(internalNodeAttributes[supportAttributeIndex]).size(); i++)
      {
        tbr.nodeLabel[i] = std::to_string(std::get<std::vector<double>>(internalNodeAttributes[supportAttributeIndex])[i]);
      }

      tbr.hasNodeLabel = true;
    }
  }

  return tbr;
}

//Check whether the tree has a valid trailer.
static bool hasValidTrailer(std::fstream* file)
{
  std::streampos currPos = file->tellg();
  file->seekg(-4, std::ios::end);

  std::vector<byte> trailer = readBytes(file, 4);

  file->seekg(currPos, std::ios::beg);

  return trailer[0] == 0x45 && trailer[1] == 0x4e && trailer[2] == 0x44 && trailer[3] == 0xff;
}

//Read multiple trees in binary format from a file stream.
static multiPhylo readBinaryTrees(std::fstream* file)
{
  std::vector<byte> header = readBytes(file, 4);

  if (header[0] != 0x23 || header[1] != 0x54 || header[2] != 0x52 || header[3] != 0x45)
  {
    Rcpp::stop("Invalid file header!");
  }

  byte headerByte = readByte(file);

  if ((headerByte & 0xfc) != 0)
  {
    Rcpp::stop("Invalid file header!");
  }
  bool globalNames = (headerByte & 0x01) != 0;

  bool globalAttributes = (headerByte & 0x02) != 0;

  bool validTrailer = hasValidTrailer(file);

  std::vector<int64_t> treeAddresses;

  if (validTrailer)
  {
    file->seekg(-12, std::ios::end);

    int64_t labelAddress = readInt64(file);

    file->seekg(labelAddress, std::ios::beg);

    int32_t numOfTrees = readInt(file);

    treeAddresses = std::vector<int64_t>(numOfTrees);

    for (int i = 0; i < numOfTrees; i++)
    {
      treeAddresses[i] = readInt64(file);
    }
  }

  file->seekg(5, std::ios::beg);

  std::vector<std::string> allNames;

  if (globalNames)
  {
    int32_t numNames = readInt(file);
    allNames = std::vector<std::string>(numNames);

    for (int i = 0; i < numNames; i++)
    {
      allNames[i] = readMyString(file);
    }
  }

  std::vector<Attribute> allAttributes;

  if (globalAttributes)
  {
    int32_t numAttributes = readInt(file);
    allAttributes = std::vector<Attribute>(numAttributes);

    for (int i = 0; i < numAttributes; i++)
    {
      allAttributes[i].AttributeName = readMyString(file);
      allAttributes[i].IsNumeric = readInt(file) == 2;
    }
  }

  if (validTrailer)
  {
    std::vector<phylo> parsedTrees;

    std::vector<std::string> treeNames;

    for (size_t i = 0; i < treeAddresses.size(); i++)
    {
      file->seekg(treeAddresses[i], std::ios::beg);
      phylo tree = readBinaryTree(file, globalNames, allNames, allAttributes);

      std::string treeName = "tree" + std::to_string(i + 1);

      int treeNameIndex = -1;

      for (size_t j = 0; j < tree.attributes.size(); j++)
      {
        if (equalCI(tree.attributes[j].AttributeName, TREENAMEATTRIBUTE))
        {
          treeNameIndex = j;
          break;
        }
      }

      if (treeNameIndex >= 0 && tree.nodeAttributes.size() > 0 && std::get<std::vector<std::string>>(tree.nodeAttributes[treeNameIndex])[0] != "")
      {
        treeName = std::get<std::vector<std::string>>(tree.nodeAttributes[treeNameIndex])[0];
      }

      parsedTrees.push_back(tree);
      treeNames.push_back(treeName);
    }

    multiPhylo tbr;

    tbr.trees = parsedTrees;
    tbr.treeNames = treeNames;

    return tbr;
  }
  else
  {
    Rcpp::warning("Invalid file trailer!");

    bool error = false;

    std::vector<phylo> parsedTrees;

    std::vector<std::string> treeNames;

    int i = 0;

    while (!error)
    {
      phylo tree;

      try
      {
        tree = readBinaryTree(file, globalNames, allNames, allAttributes);
      }
      catch ( ... )
      {
        error = true;
      }

      if (!error)
      {
        i++;

        std::string treeName = "tree" + std::to_string(i);

        int treeNameIndex = -1;

        for (size_t j = 0; j < tree.attributes.size(); j++)
        {
          if (equalCI(tree.attributes[j].AttributeName, TREENAMEATTRIBUTE))
          {
            treeNameIndex = j;
            break;
          }
        }

        if (treeNameIndex >= 0 && tree.nodeAttributes.size() > 0 && std::get<std::vector<std::string>>(tree.nodeAttributes[treeNameIndex])[0] != "")
        {
          treeName = std::get<std::vector<std::string>>(tree.nodeAttributes[treeNameIndex])[0];
        }

        parsedTrees.push_back(tree);
        treeNames.push_back(treeName);
      }
    }

    multiPhylo tbr;

    tbr.trees = parsedTrees;
    tbr.treeNames = treeNames;

    return tbr;
  }
}

//Read a single tree in binary format from a file and pass it back to R.
// [[Rcpp::export]]
SEXP Rcpp_read_binary_tree(std::string fileName, long offset, bool globalNames, std::vector<std::string> names, std::vector<std::string> attributeNames, std::vector<bool> attributesAreNumeric)
{
 std::vector<Attribute> attributes(attributeNames.size());

 for (size_t i = 0; i < attributeNames.size(); i++)
 {
   attributes[i].AttributeName = attributeNames[i];
   attributes[i].IsNumeric = attributesAreNumeric[i];
 }

 std::fstream file(fileName, std::ios::in | std::ios::binary);

 if (!file.is_open())
 {
   Rcpp::stop("ERROR! Could not open the file for reading.");
 }

 file.seekg(offset);

 phylo tree = readBinaryTree(&file, globalNames, names, attributes);

 file.close();

 return Rcpp::wrap(convertPhylo(tree));
}

//Read multiple trees in binary format from a file and pass them back to R.
// [[Rcpp::export]]
SEXP Rcpp_read_binary_trees(std::string fileName)
{
  std::fstream file(fileName, std::ios::in | std::ios::binary);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for reading.");
  }

  multiPhylo trees = readBinaryTrees(&file);

  file.close();

  return Rcpp::wrap(convertMultiPhylo(&trees));
}
