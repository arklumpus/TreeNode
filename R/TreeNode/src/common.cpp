/***********************************************************************
 *  common.cpp    2020-05-20
 *  by Giorgio Bianchini
 *  This file is part of the R package TreeNode, licensed under GPLv3
 *
 *  Common definitions used by multiple files in the package.
 ***********************************************************************/

// [[Rcpp::plugins(cpp17)]]

#include "common.h"

using namespace Rcpp;

//Compare two strings case-insensitively
//From https://thispointer.com/c-case-insensitive-string-comparison-using-stl-c11-boost-library/
bool equalCI(std::string& str1, std::string& str2)
{
    return ((str1.size() == str2.size()) && std::equal(str1.begin(), str1.end(), str2.begin(), [](char& c1, char& c2) {
        return (c1 == c2 || std::toupper(c1) == std::toupper(c2));
    }));
}

//Case-insensitive comparator for less operator (used in std::map) - Part 1
bool ci_less::nocase_compare::operator() (const unsigned char& c1, const unsigned char& c2) const {
    return std::tolower(c1) < std::tolower(c2);
}

//Case-insensitive comparator for less operator (used in std::map) - Part 2
bool ci_less::operator() (const std::string& s1, const std::string& s2) const {
    return std::lexicographical_compare
    (s1.begin(), s1.end(),   // source range
     s2.begin(), s2.end(),   // dest range
     nocase_compare());  // comparison
}

//Convert a C++ phylo object into an object of class "phylo" that can be passed back to R
Rcpp::List convertPhylo(phylo tree)
{
    std::string* tipLabelP = tree.tipLabel.data();
    Rcpp::StringVector tipLabel(tipLabelP, tipLabelP + tree.tipLabel.size());

    double* edgeLengthP = tree.edgeLength.data();
    Rcpp::NumericVector edgeLength(edgeLengthP, edgeLengthP + tree.edgeLength.size());

    std::string* nodeLabelP = tree.nodeLabel.data();
    Rcpp::StringVector nodeLabel(nodeLabelP, nodeLabelP + tree.nodeLabel.size());

    Rcpp::List tipAttributes = Rcpp::List::create();
    Rcpp::List nodeAttributes = Rcpp::List::create();

    std::vector<Attribute> attributes = tree.attributes;

    for (size_t i = 0; i < attributes.size(); i++)
    {
        if (attributes[i].IsNumeric)
        {
            double* tipAttributeP = std::get<std::vector<double>>(tree.tipAttributes[i]).data();
            Rcpp::NumericVector tipAttributeV(tipAttributeP, tipAttributeP + std::get<std::vector<double>>(tree.tipAttributes[i]).size());
            tipAttributes.push_back(tipAttributeV, attributes[i].AttributeName);

            double* nodeAttributeP = std::get<std::vector<double>>(tree.nodeAttributes[i]).data();
            Rcpp::NumericVector nodeAttributeV(nodeAttributeP, nodeAttributeP + std::get<std::vector<double>>(tree.nodeAttributes[i]).size());
            nodeAttributes.push_back(nodeAttributeV, attributes[i].AttributeName);
        }
        else
        {
            std::string* tipAttributeP = std::get<std::vector<std::string>>(tree.tipAttributes[i]).data();
            Rcpp::CharacterVector tipAttributeV(tipAttributeP, tipAttributeP + std::get<std::vector<std::string>>(tree.tipAttributes[i]).size());
            tipAttributes.push_back(tipAttributeV, attributes[i].AttributeName);

            std::string* nodeAttributeP = std::get<std::vector<std::string>>(tree.nodeAttributes[i]).data();
            Rcpp::CharacterVector nodeAttributeV(nodeAttributeP, nodeAttributeP + std::get<std::vector<std::string>>(tree.nodeAttributes[i]).size());
            nodeAttributes.push_back(nodeAttributeV, attributes[i].AttributeName);
        }
    }

    Rcpp::IntegerMatrix edge(tree.edge.size(), 2);

    for (size_t i = 0; i < tree.edge.size(); i++)
    {
        edge(i, 0) = tree.edge[i][0] + 1;
        edge(i, 1) = tree.edge[i][1] + 1;
    }

    Rcpp::List tbr = Rcpp::List::create(
        Rcpp::Named("Nnode") = tree.Nnode,
        Rcpp::Named("tip.label") = tipLabel,
        Rcpp::Named("tip.attributes") = tipAttributes,
        Rcpp::Named("node.attributes") = nodeAttributes,
        Rcpp::Named("edge") = edge
    );


    if (!std::isnan(tree.rootEdge))
    {
        tbr["rootEdge"] = tree.rootEdge;
    }

    if (tree.hasEdgeLength)
    {
        tbr["edge.length"] = edgeLength;
    }

    if (tree.hasNodeLabel)
    {
        tbr["node.label"] = nodeLabel;
    }

    tbr.attr("class") = "phylo";
    tbr.attr("order") = "cladewise";

    return tbr;
}

//Convert a C++ multiPhylo object (list of trees) into an object of class "multiPhylo" that can be passed back to R
Rcpp::List convertMultiPhylo(multiPhylo* trees)
{
    Rcpp::List treeList = Rcpp::List::create();

    for (size_t i = 0; i < trees->trees.size(); i++)
    {
        treeList.push_back(convertPhylo(trees->trees[i]), trees->treeNames[i]);
    }

    treeList.attr("class") = "multiPhylo";

    return treeList;
}

//Determine whether a string can be parsed into a double (and optionally return the parsed value)
bool tryParse(std::string val, double* output)
{
    size_t p;

    bool error = true;
    double parsed = 0;

    if (val.length() > 0)
    {
        try
        {
            parsed = std::stod(val, &p);
            error = false;
        }
        catch (...)
        {
            error = true;
        }
    }

    if (!error && p == val.length())
    {
        if (output != NULL)
        {
            *output = parsed;
        }
        return true;
    }
    else
    {
        if (output != NULL)
        {
            *output = std::nan("");
        }
        return false;
    }
}

//Get the index of an attribute within a vector of attributes
int attributeIndex(std::vector<Attribute>* attributes, Attribute* attribute)
{
    for (size_t i = 0; i < attributes->size(); i++)
    {
        if (equalCI((*attributes)[i].AttributeName, attribute->AttributeName) && (*attributes)[i].IsNumeric == attribute->IsNumeric)
        {
            return (int)i;
        }
    }

    return -1;
}

//Extract information about node parent-child relationships from a phylo object.
//The *children vector should be properly initialised with the information from the edges of the phylo object.
//The *sortedParents, *sortedChildren and *sortedNodes vectors should be initialised with a length equal to
//the number of nodes in the tree (they will be filled by this method).
//The initial value of *currIndex should be 0 when this method is called by external code. The initial value of
//currNonSortedIndex should be the index of the root node in the phylo representation. The initial value of
//The initial value of currSortedParent should be -1 when this method is called by external code.
//A typical invocation of this method should be something like:
//  addChildren(&children, &sortedParents, &sortedChildren, &sortedNodes, &currIndex, rootNode, -1);
int32_t addChildren(std::vector<std::vector<int32_t>>* children, std::vector<int32_t>* sortedParents, std::vector<std::vector<int32_t>>* sortedChildren, std::vector<int32_t>* sortedNodes, int32_t* currIndex, int32_t currNonSortedIndex, int32_t currSortedParent)
{
    (*sortedParents)[*currIndex] = currSortedParent;
    int32_t myIndex = *currIndex;
    (*sortedNodes)[myIndex] = currNonSortedIndex;
    (*currIndex)++;
    for (size_t i = 0; i < (*children)[currNonSortedIndex].size(); i++)
    {
        int32_t childInd = addChildren(children, sortedParents, sortedChildren, sortedNodes, currIndex, (*children)[currNonSortedIndex][i], myIndex);
        (*sortedChildren)[myIndex].push_back(childInd);
    }

    return myIndex;
}

//Sets the tipAttributes and nodeAttributes members of a phylo object, to provide compatibility with trees
//produced by the ape library (which does not use these members).
void setAttributes(phylo* tree)
{
    int nameIndex = -1;
    int lengthIndex = -1;
    int supportIndex = -1;

    for (size_t j = 0; j < tree->attributes.size(); j++)
    {
        if (equalCI(tree->attributes[j].AttributeName, NAMEATTRIBUTE) && !tree->attributes[j].IsNumeric)
        {
            nameIndex = j;
        }
        if (equalCI(tree->attributes[j].AttributeName, LENGTHATTRIBUTE) && tree->attributes[j].IsNumeric)
        {
            lengthIndex = j;
        }
        if (equalCI(tree->attributes[j].AttributeName, SUPPORTATTRIBUTE) && tree->attributes[j].IsNumeric)
        {
            supportIndex = j;
        }
    }

    bool areNodeLabelsNames = false;
    bool areNodeLabelsSupport = false;
    if (tree->nodeLabel.size() > 0)
    {
        areNodeLabelsSupport = true;
        for (size_t i = 0; i < tree->nodeLabel.size(); i++)
        {
            if (tree->nodeLabel[i].length() > 0 && !tryParse(tree->nodeLabel[i]))
            {
                areNodeLabelsNames = true;
                areNodeLabelsSupport = false;
                break;
            }
        }
    }

    if (nameIndex < 0)
    {
        Attribute name;
        name.AttributeName = "Name";
        name.IsNumeric = false;
        tree->attributes.push_back(name);

        std::vector<std::string> tipNames(tree->tipLabel);
        std::vector<std::string> nodeNames(tree->Nnode);

        if (areNodeLabelsNames)
        {
            nodeNames = std::vector<std::string>(tree->nodeLabel);
        }

        tree->tipAttributes.push_back(tipNames);
        tree->nodeAttributes.push_back(nodeNames);
    }
    else
    {
        std::vector<std::string> tipNames = std::get<std::vector<std::string>>(tree->tipAttributes[nameIndex]);
        tree->tipLabel =  tipNames;
    }

    if (lengthIndex < 0)
    {
        Attribute length;
        length.AttributeName = "Length";
        length.IsNumeric = true;
        tree->attributes.push_back(length);

        std::vector<double> tipLengths(tree->tipLabel.size());
        std::vector<double> nodeLengths(tree->Nnode);

        for (size_t i = 0; i < tipLengths.size(); i++)
        {
            tipLengths[i] = std::nan("");
        }

        for (size_t i = 0; i < nodeLengths.size(); i++)
        {
            nodeLengths[i] = std::nan("");
        }

        int32_t tipCount = (int32_t)(tree->tipLabel.size());

        for (size_t i = 0; i < tree->edge.size(); i++)
        {
            if (tree->edge[i][1] <= tipCount)
            {
                tipLengths[tree->edge[i][1] - 1] = tree->edgeLength[i];
            }
            else
            {
                nodeLengths[tree->edge[i][1] - tipCount - 1] = tree->edgeLength[i];
            }
        }

        tree->tipAttributes.push_back(tipLengths);
        tree->nodeAttributes.push_back(nodeLengths);
    }

    if (supportIndex < 0)
    {
        Attribute support;
        support.AttributeName = "Support";
        support.IsNumeric = true;
        tree->attributes.push_back(support);

        std::vector<double> tipSupport(tree->tipLabel.size());
        std::vector<double> nodeSupport(tree->Nnode);


        if (areNodeLabelsSupport)
        {
            for (size_t i = 0; i < tipSupport.size(); i++)
            {
                tipSupport[i] = std::nan("");
            }

            for (size_t i = 0; i < tree->nodeLabel.size(); i++)
            {
                if (tree->nodeLabel[i].length() > 0)
                {
                    double parsed = std::nan("");
                    if (tryParse(tree->nodeLabel[i], &parsed))
                    {
                        nodeSupport[i] = parsed;
                    }
                    else
                    {
                        nodeSupport[i] = std::nan("");
                    }
                }
                else
                {
                    nodeSupport[i] = std::nan("");
                }
            }
        }
        else
        {
            for (size_t i = 0; i < tipSupport.size(); i++)
            {
                tipSupport[i] = std::nan("");
            }

            for (size_t i = 0; i < nodeSupport.size(); i++)
            {
                nodeSupport[i] = std::nan("");
            }
        }

        tree->tipAttributes.push_back(tipSupport);
        tree->nodeAttributes.push_back(nodeSupport);
    }
}

//Convert attribute information passed by R into a vector more easily accessible by C++
std::vector<std::variant<std::vector<std::string>, std::vector<double>>> convertAttributeList(Rcpp::List* attributes, std::vector<Attribute>* allAttributes)
{
    std::vector<std::variant<std::vector<std::string>, std::vector<double>>> tbr;

    std::vector<std::string> attributeNames = as<std::vector<std::string>>(attributes->names());

    for (R_xlen_t i = 0; i < attributes->size(); i++)
    {
        Rcpp::GenericVector attribute = (*attributes)[i];

        if (attribute.size() > 0)
        {
            bool isNumeric = Rf_isNumeric(attribute[0]);

            bool found = false;
            for (size_t j = 0; j < allAttributes->size(); j++)
            {
                if (equalCI((*allAttributes)[j].AttributeName, attributeNames[i]) && (*allAttributes)[j].IsNumeric == isNumeric)
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Attribute currAttr;
                currAttr.AttributeName = attributeNames[i];
                currAttr.IsNumeric = isNumeric;
                allAttributes->push_back(currAttr);
            }

            if (isNumeric)
            {
                Rcpp::NumericVector numericAttribute = (*attributes)[i];
                tbr.push_back(as<std::vector<double>>(numericAttribute));
            }
            else
            {
                Rcpp::StringVector stringAttribute = (*attributes)[i];
                tbr.push_back(as<std::vector<std::string>>(stringAttribute));
            }
        }
    }

    return tbr;
}

//Convert a tree passed by R into a phylo object with members easily accessible by C++
phylo convertTree(Rcpp::List* tree)
{
    phylo tbr;
    tbr.Nnode = as<int32_t>((*tree)["Nnode"]);

    if (tree->containsElementNamed("root.edge"))
    {
        tbr.rootEdge = as<double>((*tree)["root.edge"]);
    }
    else
    {
        tbr.rootEdge = std::nan("");
    }

    tbr.tipLabel = as<std::vector<std::string>>((*tree)["tip.label"]);

    if (tree->containsElementNamed("node.label"))
    {
        tbr.nodeLabel = as<std::vector<std::string>>((*tree)["node.label"]);
    }

    Rcpp::IntegerMatrix edge = (*tree)["edge"];

    for (R_xlen_t i = 0; i < edge.nrow(); i++)
    {
        std::vector<int32_t> row(2);
        row[0] = edge(i, 0);
        row[1] = edge(i, 1);
        tbr.edge.push_back(row);
    }

    if (tree->containsElementNamed("edge.length"))
    {
        tbr.edgeLength = as<std::vector<double>>((*tree)["edge.length"]);
    }
    else
    {
        for (size_t i = 0; i < tbr.edge.size(); i++)
        {
            tbr.edgeLength.push_back(std::nan(""));
        }
    }

    if (tree->containsElementNamed("tip.attributes"))
    {
        Rcpp::List tipAttributes = (*tree)["tip.attributes"];
        tbr.tipAttributes = convertAttributeList(&tipAttributes, &(tbr.attributes));
    }

    if (tree->containsElementNamed("node.attributes"))
    {
        Rcpp::List nodeAttributes = (*tree)["node.attributes"];
        tbr.nodeAttributes = convertAttributeList(&nodeAttributes, &(tbr.attributes));
    }

    setAttributes(&tbr);

    return tbr;
}

//If not already present, add a TreeName attribute to a tree, representing the tree's name
void setTreeName(phylo* tree, std::string name)
{
    int treeNameIndex = -1;

    for (size_t j = 0; j < tree->attributes.size(); j++)
    {
        if (equalCI(tree->attributes[j].AttributeName, TREENAMEATTRIBUTE) && !tree->attributes[j].IsNumeric)
        {
            treeNameIndex = j;
        }
    }

    if (treeNameIndex < 0)
    {
        Attribute treeName;
        treeName.AttributeName = "TreeName";
        treeName.IsNumeric = false;
        tree->attributes.push_back(treeName);

        std::vector<std::string> tipNames(tree->tipLabel.size());
        std::vector<std::string> nodeNames(tree->Nnode);

        nodeNames[0] = name;

        tree->tipAttributes.push_back(tipNames);
        tree->nodeAttributes.push_back(nodeNames);
    }
}

//Convert a list of trees passed by R into a multiPhylo object with members easily accessible by C++
multiPhylo convertTrees(Rcpp::List* trees)
{
    multiPhylo tbr;

    tbr.treeNames = as<std::vector<std::string>>(trees->names());

    for (R_xlen_t i = 0; i < trees->size(); i++)
    {
        Rcpp::List tree = (*trees)[i];

        phylo convertedTree = convertTree(&tree);

        setTreeName(&convertedTree, tbr.treeNames[i]);

        tbr.trees.push_back(convertedTree);
    }

    return tbr;
}
