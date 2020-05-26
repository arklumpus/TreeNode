/***********************************************************************
 *  write_nwka.cpp    2020-05-20
 *  by Giorgio Bianchini
 *  This file is part of the R package TreeNode, licensed under GPLv3
 *
 *  Methods to write trees provided by R to a file in
 *  Newick-with-Attributes (NWKA) format.
 ***********************************************************************/

// [[Rcpp::plugins(cpp17)]]

#include "common.h"

using namespace Rcpp;

//Appends a node in Newick format to the stringstream. In addition to the tree,
//this method requires a series of vectors detailing parent-children
//relationships in the tree (these can be produced using the addChildren
//method from common.h).
static void appendNodeSimpleNewick(std::stringstream* builder, phylo* tree, size_t nodeInd, std::vector<int32_t>* sortedParents, std::vector<std::vector<int32_t>>* sortedChildren, std::vector<int32_t>* sortedNodes, bool singleQuoted)
{
  if ((*sortedChildren)[nodeInd].size() == 0)
  {
    if (singleQuoted)
    {
      std::string name = (tree->tipLabel)[(*sortedNodes)[nodeInd] - 1];
      (*builder) << "'" << name << "'";
    }
    else
    {
      std::string name = (tree->tipLabel)[(*sortedNodes)[nodeInd] - 1];
      (*builder) << name;
    }

    Attribute lengthAttr;
    lengthAttr.AttributeName = "Length";
    lengthAttr.IsNumeric = true;
    int lengthAttributeIndex = attributeIndex(&(tree->attributes), &lengthAttr);

    double edgeLength = std::nan("");

    if (lengthAttributeIndex >= 0)
    {
      edgeLength = std::get<std::vector<double>>(tree->tipAttributes[lengthAttributeIndex])[(*sortedNodes)[nodeInd] - 1];
    }

    if (!std::isnan(edgeLength))
    {
      (*builder) << ":";
      (*builder) << std::to_string(edgeLength);
    }

    if ((*sortedParents)[nodeInd] < 0)
    {
      (*builder) << ";";
    }
  }
  else
  {
    *builder << "(";
    for (size_t i = 0; i < (*sortedChildren)[nodeInd].size(); i++)
    {
      appendNodeSimpleNewick(builder, tree, (*sortedChildren)[nodeInd][i], sortedParents, sortedChildren, sortedNodes, singleQuoted);

      if (i < (*sortedChildren)[nodeInd].size() - 1)
      {
        *builder << ",";
      }
    }

    *builder << ")";

    Attribute nameAttr;
    nameAttr.AttributeName = "Name";
    nameAttr.IsNumeric = false;
    int nameAttributeIndex = attributeIndex(&(tree->attributes), &nameAttr);

    std::string myName;

    if (nameAttributeIndex >= 0)
    {
      myName = std::get<std::vector<std::string>>(tree->nodeAttributes[nameAttributeIndex])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
    }

    Attribute supportAttr;
    supportAttr.AttributeName = "Support";
    supportAttr.IsNumeric = true;
    int supportAttributeIndex = attributeIndex(&(tree->attributes), &supportAttr);

    double mySupport = std::nan("");

    if (supportAttributeIndex >= 0)
    {
      mySupport = std::get<std::vector<double>>(tree->nodeAttributes[supportAttributeIndex])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
    }

    Attribute lengthAttr;
    lengthAttr.AttributeName = "Length";
    lengthAttr.IsNumeric = true;
    int lengthAttributeIndex = attributeIndex(&(tree->attributes), &lengthAttr);

    double edgeLength = std::nan("");

    if (lengthAttributeIndex >= 0)
    {
      edgeLength = std::get<std::vector<double>>(tree->nodeAttributes[lengthAttributeIndex])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
    }

    if (!myName.empty() && std::isnan(mySupport))
    {
      if (singleQuoted)
      {
        *builder << "'" << myName << "'";
      }
      else
      {
        *builder << myName;
      }
    }

    if (!std::isnan(mySupport))
    {
      *builder << std::to_string(mySupport);
    }

    if (!std::isnan(edgeLength))
    {
      *builder << ":" << std::to_string(edgeLength);
    }

    if ((*sortedParents)[nodeInd] < 0)
    {
      (*builder) << ";";
    }
  }
}

//Appends a node in NWKA format to the stringstream. In addition to the tree,
//this method requires a series of vectors detailing parent-children
//relationships in the tree (these can be produced using the addChildren
//method from common.h).
static void appendNodeNWKA(std::stringstream* builder, phylo* tree, size_t nodeInd, std::vector<int32_t>* sortedParents, std::vector<std::vector<int32_t>>* sortedChildren, std::vector<int32_t>* sortedNodes)
{
  if ((*sortedChildren)[nodeInd].size() == 0)
  {
    std::string name = (tree->tipLabel)[(*sortedNodes)[nodeInd] - 1];
    (*builder) << "'" << name << "'";

    Attribute lengthAttr;
    lengthAttr.AttributeName = "Length";
    lengthAttr.IsNumeric = true;
    int lengthAttributeIndex = attributeIndex(&(tree->attributes), &lengthAttr);

    double edgeLength = std::nan("");

    if (lengthAttributeIndex >= 0)
    {
      edgeLength = std::get<std::vector<double>>(tree->tipAttributes[lengthAttributeIndex])[(*sortedNodes)[nodeInd] - 1];
    }

    if (!std::isnan(edgeLength))
    {
      (*builder) << ":";
      (*builder) << std::to_string(edgeLength);
    }

    int currAttributeCount = 0;

    for (size_t j = 0; j < tree->attributes.size(); j++)
    {
      if (!equalCI(tree->attributes[j].AttributeName, NAMEATTRIBUTE) && !equalCI(tree->attributes[j].AttributeName, LENGTHATTRIBUTE))
      {
        if (tree->attributes[j].IsNumeric)
        {
          double value = std::get<std::vector<double>>(tree->tipAttributes[j])[(*sortedNodes)[nodeInd] - 1];
          if (!std::isnan(value))
          {
            currAttributeCount++;
          }
        }
        else
        {
          std::string value = std::get<std::vector<std::string>>(tree->tipAttributes[j])[(*sortedNodes)[nodeInd] - 1];
          if (!value.empty())
          {
            currAttributeCount++;
          }
        }
      }
    }

    if (currAttributeCount > 0)
    {
      (*builder) << "[";
      bool first = true;

      for (size_t j = 0; j < tree->attributes.size(); j++)
      {
        if (!equalCI(tree->attributes[j].AttributeName, NAMEATTRIBUTE) && !equalCI(tree->attributes[j].AttributeName, LENGTHATTRIBUTE))
        {
          if (tree->attributes[j].IsNumeric)
          {
            double value = std::get<std::vector<double>>(tree->tipAttributes[j])[(*sortedNodes)[nodeInd] - 1];
            if (!std::isnan(value))
            {
              (*builder) << (!first ? "," : "");
              (*builder) << tree->attributes[j].AttributeName;
              (*builder) << "=";
              (*builder) << std::to_string(value);
              first = false;
            }
          }
          else
          {
            std::string value = std::get<std::vector<std::string>>(tree->tipAttributes[j])[(*sortedNodes)[nodeInd] - 1];
            if (!value.empty())
            {
              if (value.find('\'') == std::string::npos)
              {
                (*builder) << (!first ? "," : "");
                (*builder) << tree->attributes[j].AttributeName;
                (*builder) << "='";
                (*builder) << value;
                (*builder) << "'";
              }
              else
              {
                (*builder) << (!first ? "," : "");
                (*builder) << tree->attributes[j].AttributeName;
                (*builder) << "=\"";
                (*builder) << value;
                (*builder) << "\"";
              }

              first = false;
            }
          }
        }
      }


      (*builder) << "]";
    }

    if ((*sortedParents)[nodeInd] < 0)
    {
      (*builder) << ";";
    }
  }
  else
  {
    *builder << "(";
    for (size_t i = 0; i < (*sortedChildren)[nodeInd].size(); i++)
    {
      appendNodeNWKA(builder, tree, (*sortedChildren)[nodeInd][i], sortedParents, sortedChildren, sortedNodes);

      if (i < (*sortedChildren)[nodeInd].size() - 1)
      {
        *builder << ",";
      }
    }

    *builder << ")";

    Attribute nameAttr;
    nameAttr.AttributeName = "Name";
    nameAttr.IsNumeric = false;
    int nameAttributeIndex = attributeIndex(&(tree->attributes), &nameAttr);

    std::string myName;

    if (nameAttributeIndex >= 0)
    {
      myName = std::get<std::vector<std::string>>(tree->nodeAttributes[nameAttributeIndex])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
    }

    Attribute supportAttr;
    supportAttr.AttributeName = "Support";
    supportAttr.IsNumeric = true;
    int supportAttributeIndex = attributeIndex(&(tree->attributes), &supportAttr);

    double mySupport = std::nan("");

    if (supportAttributeIndex >= 0)
    {
      mySupport = std::get<std::vector<double>>(tree->nodeAttributes[supportAttributeIndex])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
    }

    Attribute lengthAttr;
    lengthAttr.AttributeName = "Length";
    lengthAttr.IsNumeric = true;
    int lengthAttributeIndex = attributeIndex(&(tree->attributes), &lengthAttr);

    double edgeLength = std::nan("");

    if (lengthAttributeIndex >= 0)
    {
      edgeLength = std::get<std::vector<double>>(tree->nodeAttributes[lengthAttributeIndex])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
    }

    if (!myName.empty() && std::isnan(mySupport))
    {
      *builder << "'" << myName << "'";
    }

    if (!std::isnan(mySupport))
    {
      *builder << std::to_string(mySupport);
    }

    if (!std::isnan(edgeLength))
    {
      *builder << ":" << std::to_string(edgeLength);
    }

    int currAttributeCount = 0;

    for (size_t j = 0; j < tree->attributes.size(); j++)
    {
      if ((!equalCI(tree->attributes[j].AttributeName, NAMEATTRIBUTE) || !std::isnan(mySupport)) && !equalCI(tree->attributes[j].AttributeName, LENGTHATTRIBUTE) && !equalCI(tree->attributes[j].AttributeName, SUPPORTATTRIBUTE))
      {
        if (tree->attributes[j].IsNumeric)
        {
          double value = std::get<std::vector<double>>(tree->nodeAttributes[j])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
          if (!std::isnan(value))
          {
            currAttributeCount++;
          }
        }
        else
        {
          std::string value = std::get<std::vector<std::string>>(tree->nodeAttributes[j])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
          if (!value.empty())
          {
            currAttributeCount++;
          }
        }
      }
    }


    if (currAttributeCount > 0)
    {
      (*builder) << "[";
      bool first = true;

      for (size_t j = 0; j < tree->attributes.size(); j++)
      {
        if ((!equalCI(tree->attributes[j].AttributeName, NAMEATTRIBUTE) || !std::isnan(mySupport)) && !equalCI(tree->attributes[j].AttributeName, LENGTHATTRIBUTE) && !equalCI(tree->attributes[j].AttributeName, SUPPORTATTRIBUTE))
        {
          if (tree->attributes[j].IsNumeric)
          {
            double value = std::get<std::vector<double>>(tree->nodeAttributes[j])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
            if (!std::isnan(value))
            {
              (*builder) << (!first ? "," : "");
              (*builder) << tree->attributes[j].AttributeName;
              (*builder) << "=";
              (*builder) << std::to_string(value);
              first = false;
            }
          }
          else
          {
            std::string value = std::get<std::vector<std::string>>(tree->nodeAttributes[j])[(*sortedNodes)[nodeInd] - tree->tipLabel.size() - 1];
            if (!value.empty())
            {
              if (value.find('\'') == std::string::npos)
              {
                (*builder) << (!first ? "," : "");
                (*builder) << tree->attributes[j].AttributeName;
                (*builder) << "='";
                (*builder) << value;
                (*builder) << "'";
              }
              else
              {
                (*builder) << (!first ? "," : "");
                (*builder) << tree->attributes[j].AttributeName;
                (*builder) << "=\"";
                (*builder) << value;
                (*builder) << "\"";
              }

              first = false;
            }
          }
        }
      }


      (*builder) << "]";
    }

    if ((*sortedParents)[nodeInd] < 0)
    {
      (*builder) << ";";
    }
  }
}

//Convert a phylo object representing a tree to the Newick or NWKA representation
//of the tree.
static std::string toString(phylo* tree, bool nwka, bool singleQuoted = false)
{
  std::vector<int32_t> parents(tree->Nnode + tree->tipLabel.size() + 1);
  std::vector<std::vector<int32_t>> children(tree->Nnode + tree->tipLabel.size() + 1);

  std::vector<int32_t> sortedParents(tree->Nnode + tree->tipLabel.size());
  std::vector<std::vector<int32_t>> sortedChildren(tree->Nnode + tree->tipLabel.size());

  std::vector<int32_t> sortedNodes(tree->Nnode + tree->tipLabel.size());

  for (size_t i = 0; i < tree->edge.size(); i++)
  {
    children[tree->edge[i][0]].push_back(tree->edge[i][1]);
    parents[tree->edge[i][1]] = tree->edge[i][0];
  }

  int32_t rootNode = -1;

  for (size_t i = 1; i < parents.size(); i++)
  {
    if (parents[i] == 0)
    {
      rootNode = i;
      break;
    }
  }

  int32_t currIndex = 0;

  addChildren(&children, &sortedParents, &sortedChildren, &sortedNodes, &currIndex, rootNode, -1);

  std::stringstream tbr;

  if (!nwka)
  {
    appendNodeSimpleNewick(&tbr, tree, 0, &sortedParents, &sortedChildren, &sortedNodes, singleQuoted);
  }
  else
  {
    appendNodeNWKA(&tbr, tree, 0, &sortedParents, &sortedChildren, &sortedNodes);
  }

  return tbr.str();
}

//Determine whether a map contains a certain key.
static bool containsKey(std::map<std::string, int>* map, std::string key)
{
  return map->count(key) > 0;
}

//Translate the tip labels of a tree to numbers. This is used while building
//the "Translate" table of a NEXUS file.
static void translateNames(phylo* tree, std::map<std::string, int>* translation)
{
  for (size_t i = 0; i < tree->tipLabel.size(); i++)
  {
    tree->tipLabel[i] = std::to_string((*translation)[tree->tipLabel[i]] + 1);
  }
}

//Convert the tree(s) provided by R to their Newick/NWKA representation and pass them
//back to R.
//[[Rcpp::export]]
std::string Rcpp_multiPhylo_to_string(Rcpp::List trees, bool nwka, bool singleQuoted)
{
  multiPhylo convertedTrees = convertTrees(&trees);

  std::stringstream tbr;

  for (size_t i = 0; i < convertedTrees.trees.size(); i++)
  {
    tbr << toString(&(convertedTrees.trees[i]), nwka, singleQuoted) << "\n";
  }

  return tbr.str();
}

//Write the tree(s) provided by R to a file in Newick/NWKA format.
//[[Rcpp::export]]
void Rcpp_multiPhylo_to_file(Rcpp::List trees, std::string fileName, bool nwka, bool singleQuoted, bool append)
{
  multiPhylo convertedTrees = convertTrees(&trees);

  std::fstream file;

  if (append)
  {
    file.open(fileName, std::fstream::app | std::fstream::out);
  }
  else
  {
    file.open(fileName, std::fstream::trunc | std::fstream::out);
  }

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for writing.");
  }

  for (size_t i = 0; i < convertedTrees.trees.size(); i++)
  {
    file << toString(&(convertedTrees.trees[i]), nwka, singleQuoted) << "\n";
  }

  file.close();
}

//Write the tree(s) provided by R to a file in NEXUS format (using NWKA in the "Trees" block).
//[[Rcpp::export]]
void Rcpp_multiPhylo_to_nexus(Rcpp::List trees, std::string fileName, bool translate, bool translateQuotes)
{
  multiPhylo convertedTrees = convertTrees(&trees);

  std::fstream file(fileName, std::fstream::trunc | std::fstream::out);

  if (!file.is_open())
  {
    Rcpp::stop("ERROR! Could not open the file for writing.");
  }

  file << "#NEXUS\n\n";

  std::map<std::string, int> tipLabels;

  if (translate)
  {
    int index = 0;

    for (size_t i = 0; i < convertedTrees.trees.size(); i++)
    {
      for (size_t j = 0; j < convertedTrees.trees[i].tipLabel.size(); j++)
      {
        std::string label = convertedTrees.trees[i].tipLabel[j];

        if (!containsKey(&tipLabels, label))
        {
          tipLabels[label] = index;
          index++;
        }
      }
    }

    file << "Begin Taxa;\n\tDimensions ntax=" << std::to_string(index) << ";\n\tTaxLabels\n";

    std::map<std::string, int>::iterator it;

    if (!translateQuotes)
    {
      for (it = tipLabels.begin(); it != tipLabels.end(); it++)
      {
        file << "\t\t" << it->first << "\n";
      }
    }
    else
    {
      for (it = tipLabels.begin(); it != tipLabels.end(); it++)
      {
        file << "\t\t'" << it->first << "'\n";
      }
    }


    file << "\t\t;\nEnd;\n\nBegin Trees;\n\tTranslate\n";

    int count = 0;

    if (!translateQuotes)
    {
      for (it = tipLabels.begin(); it != tipLabels.end(); it++)
      {
        file << "\t\t" << std::to_string(it->second + 1) << " " << it->first;
        count++;
        if (count < index)
        {
          file << ",\n";
        }
        else
        {
          file << "\n";
        }
      }
    }
    else
    {
      for (it = tipLabels.begin(); it != tipLabels.end(); it++)
      {
        file << "\t\t" << std::to_string(it->second + 1) << " '" << it->first << "'";
        count++;
        if (count < index)
        {
          file << ",\n";
        }
        else
        {
          file << "\n";
        }
      }
    }

    file << "\t\t;\n";
  }
  else
  {
    file << "Begin Trees;\n";
  }

  for (size_t i = 0; i < convertedTrees.trees.size(); i++)
  {
    translateNames(&(convertedTrees.trees[i]), &tipLabels);
    file << "\tTree " << convertedTrees.treeNames[i] << " = " << toString(&(convertedTrees.trees[i]), true, true) << "\n";
  }

  file << "End;\n";

  file.close();
}
