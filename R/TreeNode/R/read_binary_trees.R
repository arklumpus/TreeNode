########################################################################
#  read_binary_trees.R    2020-05-20
#  by Giorgio Bianchini
#  This file is part of the R package TreeNode, licensed under GPLv3
#
#  Functions to read tree files in binary format.
########################################################################



#' Read Tree File in Binary Format
#'
#' This function reads a file containing one or more trees in binary format.
#'
#' @param file A file name.
#' @param tree.names A vector of mode character containing names for the trees that are read from the file;
#'        if \code{NULL} (the default), the trees will be named according to the names in the tree file or,
#'        if these are missing, as \code{"tree1"}, \code{"tree2"}, ...
#' @param keep.multi If \code{TRUE}, this function will return an object of class \code{"multiPhylo"} even
#'        when the tree file contains only a single tree. Defaults to \code{FALSE}, which means that if the
#'        file contains a single tree, an object of class \code{"phylo"} is returned.
#'
#' @return An object of class \code{"phylo"} or \code{"multiPhylo"}, compatible with the \code{\link[ape]{ape}}
#'         package.
#'
#'         In addition to the elements described in the documentation for the \code{\link[ape]{read.tree}}
#'         function of the \code{\link[ape]{ape}} package, a \code{"phylo"} object produced by this function
#'         will also have the following components:
#'         \item{\code{tip.attributes}}{A named list of attributes for the tips of the tree. Each element of
#'                                      this list is a vector of mode character or numeric (depending on the attribute).}
#'         \item{\code{node.attributes}}{A named list of attributes for the internal nodes of the tree. Each element of
#'                                       this list is a vector of mode character or numeric (depending on the attribute).}
#'
#' @details This function reads the whole file in memory at once. If you wish to process the file tree-by-tree, you
#'          should use the \code{\link{read_one_binary_tree}} function.
#'
#'          Node attributes (e.g. support values, rates, ages...) are parsed by this function and returned in the
#'          \code{tip.attributes} and \code{node.attributes} elements of the returned \code{"phylo"} objects.
#'
#'          Attribute names may appear in any kind of casing (e.g. \code{Name}, \code{name} or \code{NAME}), but they
#'          should be treated using case-insensitive comparisons.
#'
#'          If the file has an invalid trailer (e.g. because it is incomplete), the function will print a warning and
#'          attempt anyways to extract as many trees as possible.
#'
#' @author Giorgio Bianchini
#'
#' @family functions to read trees
#'
#' @seealso \code{\link{read_one_binary_tree}}, \code{\link[ape]{ape}}, \code{\link[ape]{read.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md}
#'
#' @examples
#' # Tree file (replace with your own)
#' treeFile <- system.file("extdata", "oneTree.tbi", package="TreeNode")
#'
#' # Read the tree file
#' tree <- read_binary_trees(treeFile)
#'
#' # Use support values as node labels
#' tree$node.label = tree$node.attributes$Support
#'
#' # Plot the tree with support values at the nodes
#' ape::plot.phylo(tree, show.node.label = TRUE)
#'
#' @export
read_binary_trees <- function(file, tree.names = NULL, keep.multi = FALSE)
{
  trees <- Rcpp_read_binary_trees(file)

  names(trees) = tree.names

  if (length(trees) == 1 && !keep.multi)
  {
    trees = trees[[1]]
  }

  return(trees)
}



#' Read Tree in Binary Format
#'
#' This function reads one tree from a file in binary format.
#'
#' @param file A file name.
#' @param index The index of the tree that should be read (starting from 1).
#' @param address The address (i.e. byte offset from the start of the file) of the tree that should be read.
#' @param metadata An object of class \code{"BinaryTreeMetadata"} containing the metadata extracted from the
#'                 tree file. If this is not provided, it will be read from the file (see details).
#'
#' @return An object of class \code{"phylo"}, compatible with the \code{\link[ape]{ape}}
#'         package.
#'
#'         In addition to the elements described in the documentation for the \code{\link[ape]{read.tree}}
#'         function of the \code{\link[ape]{ape}} package, a \code{"phylo"} object produced by this function
#'         will also have the following components:
#'         \item{\code{tip.attributes}}{A named list of attributes for the tips of the tree. Each element of
#'                                      this list is a vector of mode character or numeric (depending on the attribute).}
#'         \item{\code{node.attributes}}{A named list of attributes for the internal nodes of the tree. Each element of
#'                                       this list is a vector of mode character or numeric (depending on the attribute).}
#'
#' @details This function extracts only one tree from the file. The information provided by \code{metadata} is used to
#'          determine where in the file the requested tree starts. If this is not provided, this function will read the
#'          metadata from the file (using the \code{\link{read_binary_tree_metadata}} function).
#'
#'          Reusing the metadata is efficient when multiple trees need to be read from the same file (so that the metadata
#'          only needs to be read once).
#'
#'          Node attributes (e.g. support values, rates, ages...) are parsed by this function and returned in the
#'          \code{tip.attributes} and \code{node.attributes} elements of the returned \code{"phylo"} objects.
#'
#'          Attribute names may appear in any kind of casing (e.g. \code{Name}, \code{name} or \code{NAME}), but they
#'          should be treated using case-insensitive comparisons.
#'
#'          Due to limitations with R's integral types, this function may have issues with files larger than 2GB.
#'
#' @author Giorgio Bianchini
#'
#' @family functions to read trees
#'
#' @seealso \code{\link{read_binary_trees}}, \code{\link{read_binary_tree_metadata}}, \code{\link[ape]{ape}}, \code{\link[ape]{read.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md}
#'
#' @examples
#' # Tree file (replace with your own)
#' treeFile <- system.file("extdata", "manyTrees.tbi", package="TreeNode")
#'
#' # Read the 5th tree in the file
#' tree <- read_one_binary_tree(treeFile, 5)
#' #Do something with the tree
#'
#'
#' # Read the binary tree metadata
#' meta <- read_binary_tree_metadata(treeFile)
#'
#' # Process every tree in the file
#' for (add in meta$TreeAddresses)
#' {
#'     tree <- read_one_binary_tree(treeFile, address = add, metadata = meta)
#'     #Do something with the tree
#' }
#'
#'
#' @export
read_one_binary_tree <- function(file, index = 1, address = NA, metadata = NA)
{
  if (any(is.na(metadata)))
  {
    metadata = read_binary_tree_metadata(file)
  }

  if (is.na(address))
  {
    address <- metadata$TreeAddresses[[index]]
  }

  return(Rcpp_read_binary_tree(file, address, metadata$GlobalNames, metadata$Names, metadata$Attributes$AttributeName, metadata$Attributes$IsNumeric))
}


#' Read Tree Metadata in Binary Format
#'
#' This function reads the metadata from a file containing trees in binary format.
#'
#' @param file A file name.
#' @param invalid_trailer If this is set to \code{"scan"} (the default), if the tree file has an invalid trailer, the
#'        function will print a warning and then read the whole file, attempting to parse as many trees as possible and
#'        storing the addresses of those trees. If this is set to \code{"fail"}, an error will be generated if the tree
#'        file has an invalid trailer. If this is set to \code{"ignore"} and the tree file has an invalid trailer, a
#'        warning will be printed and the returned object will be missing the \code{TreeAddresses} element.
#'
#' @return An object of class \code{"BinaryTreeMetadata"} with the following components:
#'         \item{\code{GlobalNames}}{A logical value indicating whether the tree file contains a list of names in the
#'                                   header.}
#'         \item{\code{Names}}{Only present if \code{GlobalNames} is \code{TRUE}. A vector of mode character containing
#'                             the names specified in the file header.}
#'         \item{\code{GlobalAttributes}}{A logical value indicating whether the tree file contains a list of attributes
#'                                        in the header.}
#'         \item{\code{Attributes}}{Only present if \code{GlobalAttributes} is \code{TRUE}. A list of attributes. Each
#'                                  attribute is itself a list of two elements: \code{AttributeName} is a character
#'                                  object describing the attribute's name (e.g. "Length"), and \code{IsNumeric} describes
#'                                  whether the attribute represents a numeric value (e.g. a branch's length) or not.}
#'         \item{\code{TreeAddresses}}{A vector of mode integer containing the addresses (i.e. byte offsets from the start
#'                                     of the file) of the trees. If \code{invalid_trailer} is \code{"ignore"} and the
#'                                     file has an invalid trailer, this element will be missing.}
#'
#' @details This function reads the metadata information from the header and trailer of a file containing trees in binary
#'          format. This information consists in the addresses of the trees (i.e. byte offsets at which the data stream
#'          describing each tree starts) and in any names or attributes that are stored in the tree header.
#'
#'          If there are such names or attributes in the header, it \emph{usually} means that every tree in the file should
#'          have the same names and attributes. However, this is not required by the file format; some (or all) of the trees
#'          in the file may have additional/missing taxa, or additional/missing attributes.
#'
#'          If the file's trailer is invalid (e.g. because the file is incomplete), the default behaviour is to read the whole
#'          file, attempting to parse as many trees as possible. The trees themselves are discarded, while their addresses
#'          are stored. This is desirable when the concern preventing all the trees in the file from being read at once (i.e.,
#'          the use of \code{\link{read_binary_trees}}) is memory.
#'          If this is not the case, changing the value of \code{invalid_trailer} provides alternative ways to deal with this
#'          situation, either by generating an error, or by returning a valid object which is however missing the \code{TreeAddresses}
#'          attribute.
#'
#'          Due to limitations with R's integral types, this function may have issues with files larger than 2GB.
#'
#' @author Giorgio Bianchini
#'
#' @seealso \code{\link{read_binary_trees}}, \code{\link{read_one_binary_tree}}, \code{\link[ape]{ape}}, \code{\link[ape]{read.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md}
#'
#' @examples
#' # Tree file (replace with your own)
#' treeFile <- system.file("extdata", "manyTrees.tbi", package="TreeNode")
#'
#' # Read the binary tree metadata
#' meta <- read_binary_tree_metadata(treeFile)
#'
#' #Print a list of the names defined in the file's header
#' meta$Names
#'
#' #Print a list of the attributes defined in the file's header
#' meta$Attributes
#'
#'
#' @export
read_binary_tree_metadata <- function(file, invalid_trailer = c("scan", "fail", "ignore"))
{
  file <- check_file(file)

  on.exit(close(file))

  header <- read_bytes(file, 4)

  if (!all(header == c(0x23, 0x54, 0x52, 0x45)))
  {
    stop("Invalid file header!")
  }

  headerByte <- read_bytes(file, 1)

  if (bitwAnd(as.numeric(headerByte), 0xfc) != 0)
  {
    stop("Invalid file header!")
  }

  tbr <- list()
  class(tbr) <- "BinaryTreeMetadata"

  globalNames <- bitwAnd(as.numeric(headerByte), 0x01) != 0
  globalAttributes <- bitwAnd(as.numeric(headerByte), 0x02) != 0

  tbr$GlobalNames <- globalNames
  tbr$GlobalAttributes <- globalAttributes

  validTrailer <- has_valid_trailer(file)


  if (validTrailer)
  {
    seek(file, rw="read", where=-12, origin="end")

    labelAddress <- read_int64(file)

    seek(file, rw="read", where=labelAddress, origin="start")

    numOfTrees <- read_int(file)

    tbr$TreeAddresses <- vector("integer", numOfTrees)

    for (i in seq(1, numOfTrees))
    {
      tbr$TreeAddresses[[i]] <- read_int64(file)
    }
  }

  seek(file, rw="read", where=5, origin="start")

  allNames <- NULL

  if (globalNames)
  {
    allNames <- vector("character", read_int(file))

    for (i in seq(1, length(allNames)))
    {
      allNames[[i]] <- read_my_string(file)
    }

    tbr$Names <- allNames;
  }

  allAttributes <- NULL

  if (globalAttributes)
  {
    allAttributes <- list()
    numAttributes <- read_int(file)
    attributeNames <- vector("character", numAttributes)
    attributesAreNumeric <- vector("logical", numAttributes)

    for (i in seq(1, numAttributes))
    {
      attributeNames[[i]] <- read_my_string(file)
      attributesAreNumeric[[i]] <- read_int(file) == 2
    }

    allAttributes$AttributeName <- attributeNames
    allAttributes$IsNumeric <- attributesAreNumeric

    tbr$Attributes <- allAttributes
  }

  if (!validTrailer)
  {
    if (invalid_trailer == "fail")
    {
      stop("Invalid file trailer!")
    }
    else if (invalid_trailer == "ignore")
    {
      warning("Invalid file trailer!")
      return(tbr)
    }

    warning("Invalid file trailer!")

    treeAddresses <- vector("integer", 0)

    error = FALSE

    while (!error)
    {
      currPos <- seek(file, rw="read", origin="start")
      tree <- tryCatch(read_one_binary_tree(file, globalNames, allNames, allAttributes), error=function(err) { return(FALSE) })

      if (class(tree) == "logical")
      {
        error = TRUE
      }
      else
      {
        treeAddresses[[length(treeAddresses) + 1]] <- currPos
      }
    }

    tbr$TreeAddresses <- treeAddresses
  }

  return(tbr);
}
