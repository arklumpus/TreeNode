########################################################################
#  write_binary_trees.R    2020-05-20
#  by Giorgio Bianchini
#  This file is part of the R package TreeNode, licensed under GPLv3
#
#  Functions to write tree files in binary format.
########################################################################



#' Write Tree File in Binary Format
#'
#' This function writes one or more trees to a file in binary format.
#'
#'
#' @param trees An object of class \code{"phylo"} or \code{"multiPhylo"}.
#' @param file A file name.
#' @param additional_data A vector of mode raw containg additional binary data that will be included within
#'                        the tree file.
#'
#'
#' @details This function writes all the trees at once. If you wish to write the trees one at a time, you
#'          should use the \code{\link{keep_writing_binary_trees}} function.
#'
#'          This function will analyse all the trees to determine whether it is appropriate to include any
#'          names or attributes in the file header. It will then write the header, the trees and conclude the
#'          file with an appropriate trailer.
#'
#'          The tip names can be specified either as a \code{Name} element in the tree's \code{tip.attributes}
#'          element, or as the \code{tip.label} element of the tree. If both are specified, the values stored in
#'          the \code{Name} attribute take precedence (this allows backward compatibility for trees created
#'          using \code{\link[ape]{ape}}).
#'
#'          The node names and support values can similarly be specified either with a \code{Name} or \code{Support}
#'          element in the tree's \code{node.attributes}, or as the tree's \code{node.label}. If all the node labels
#'          can be parsed as numbers, they will be assumed to contain support values; otherwise, they will be
#'          assumed to contain node names. If the \code{node.attributes} already contain a \code{Name} or \code{Support}
#'          element, the node labels will be ignored.
#'
#'          The additional binary data (if any) will be written in the file after the trees and before the trailer.
#'
#' @author Giorgio Bianchini
#'
#' @family functions to write trees
#'
#' @seealso \code{\link{keep_writing_binary_trees}}, \code{\link[ape]{ape}}, \code{\link[ape]{write.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md}
#'
#'
#' @export
write_binary_trees <- function(trees, file, additional_data = vector("raw", 0))
{
  if (!inherits(trees, c("phylo", "multiPhylo")))
  {
    stop("Expecting a \"phylo\" or \"multiPhylo\" object!");
  }

  if (!inherits(trees, "multiPhylo"))
  {
    realTrees <- list()
    realTrees[["tree"]] <- trees
    trees <- realTrees
  }

  Rcpp_write_binary_trees(trees, file, additional_data)
}

#' Write Tree File Header in Binary Format
#'
#' This function initializes a file that will be used to store trees in binary format.
#'
#'
#' @param file A file name.
#'
#' @return A vector of mode integer, which should be used to keep track of the addresses of trees that will be
#'         added to the file.
#'
#' @details This function will create an empty header for the binary format file (without writing any trees).
#'          Trees should written after the header using the \code{\link{keep_writing_binary_trees}}
#'          function. The file should be finalised using the \code{\link{finish_writing_binary_trees}}
#'          function.
#'
#'          Note that, since node names are not stored in the header, files produced with this workflow may be
#'          much larger than files produced using the \code{\link{write_binary_trees}} function (e.g. if
#'          the file contains many trees which all have the same tip labels). The advantage of this approach is
#'          that the trees do not need to be all available/stored in memory at the same time.
#'
#'          The vector that is returned by this function contains the position at which the first tree will be
#'          appended in the file. This vector should be provided to subsequent calls to \code{\link{keep_writing_binary_trees}}.
#'
#' @author Giorgio Bianchini
#'
#' @seealso \code{\link{keep_writing_binary_trees}}, \code{\link{finish_writing_binary_trees}}, \code{\link[ape]{ape}}, \code{\link[ape]{write.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md}
#'
#'
#'
#' @examples
#' #A simple tree
#' tree1 <- ape::read.tree(text = "((A,B),(C,D));")
#'
#' # Initialise the output file
#' addresses <- begin_writing_binary_trees("outputFile.tbi")
#'
#' # Append a tree to the output file
#' addresses <- keep_writing_binary_trees(tree1, "outputFile.tbi", addresses)
#'
#' # Some more trees (note that we are overwriting tree1)
#' tree1 <- ape::read.tree(text = "(((A,B),C),D);")
#' tree2 <- ape::read.tree(text = "((D,(A,B)),C);")
#'
#' # Append them to the file
#' addresses <- keep_writing_binary_trees(tree1, "outputFile.tbi", addresses)
#' addresses <- keep_writing_binary_trees(tree2, "outputFile.tbi", addresses)
#'
#' #Some raw data
#' raw_data <- as.raw(seq(1, 5))
#'
#' # Finalise the output file
#' finish_writing_binary_trees("outputFile.tbi", addresses, raw_data)
#'
#' @export
begin_writing_binary_trees <- function(file)
{
  return(Rcpp_begin_writing_binary_trees(file))
}



#' Add Tree to File in Binary Format
#'
#' This function adds trees to a file in binary format.
#'
#' @param trees An object of class \code{"phylo"} or \code{"multiPhylo"}.
#' @param file A file name.
#' @param addresses A vector of mode integer containing the addresses of previous trees that have been
#'                  added to the file.
#'
#' @return A vector of mode integer containing the addresses of the trees that have been added to the file,
#'         which should be used to keep track of the addresses of subsequent trees.
#'
#' @details This function will append trees in binary format to a file. The file should have been already
#'          initialised by the \code{\link{begin_writing_binary_trees}} function and may already
#'          contain some trees. It should be finalised using the \code{\link{finish_writing_binary_trees}}
#'          function.
#'
#'          Note that, since node names are not stored in the header, files produced with this workflow may be
#'          much larger than files produced using the \code{\link{write_binary_trees}} function (e.g. if
#'          the file contains many trees which all have the same tip labels). The advantage of this approach is
#'          that the trees do not need to be all available/stored in memory at the same time.
#'
#'          The vector that is returned by this function contains the position at which the trees have been appendend
#'          to the file, as well as the position at which the next tree will be appended. This vector should be provided
#'          to subsequent calls to \code{\link{keep_writing_binary_trees}} and to \code{\link{finish_writing_binary_trees}}.
#'
#' @author Giorgio Bianchini
#'
#' @family functions to write trees
#'
#' @seealso \code{\link{write_binary_trees}}, \code{\link{begin_writing_binary_trees}}, \code{\link{finish_writing_binary_trees}}, \code{\link[ape]{ape}}, \code{\link[ape]{write.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md}
#'
#' @examples
#' #A simple tree
#' tree1 <- ape::read.tree(text = "((A,B),(C,D));")
#'
#' # Initialise the output file
#' addresses <- begin_writing_binary_trees("outputFile.tbi")
#'
#' # Append a tree to the output file
#' addresses <- keep_writing_binary_trees(tree1, "outputFile.tbi", addresses)
#'
#' # Some more trees (note that we are overwriting tree1)
#' tree1 <- ape::read.tree(text = "(((A,B),C),D);")
#' tree2 <- ape::read.tree(text = "((D,(A,B)),C);")
#'
#' # Append them to the file
#' addresses <- keep_writing_binary_trees(tree1, "outputFile.tbi", addresses)
#' addresses <- keep_writing_binary_trees(tree2, "outputFile.tbi", addresses)
#'
#' #Some raw data
#' raw_data <- as.raw(seq(1, 5))
#'
#' # Finalise the output file
#' finish_writing_binary_trees("outputFile.tbi", addresses, raw_data)
#'
#' @export
keep_writing_binary_trees <- function(trees, file, addresses)
{
  if (!inherits(trees, c("phylo", "multiPhylo")))
  {
    stop("Expecting a \"phylo\" or \"multiPhylo\" object!");
  }

  if (!inherits(trees, "multiPhylo"))
  {
    return(Rcpp_write_binary_tree(trees, file, addresses))
  }
  else
  {
    for (tree in trees)
    {
      addresses = Rcpp_write_binary_tree(tree, file, addresses)
    }
    return(addresses)
  }
}


#' Finalise Tree File in Binary Format
#'
#' This function finalises a tree file in binary format.
#'
#' @param file A file name.
#' @param addresses A vector of mode integer containing the addresses of previous trees that have been
#'                  added to the file.
#' @param additional_data A vector of mode raw containg additional binary data that will be included within
#'                        the tree file.
#'
#' @details This function will finalise a tree file in binary format, by writing the file trailer containing the
#'          addresses of the trees stored in the file.
#'
#'          Note that, since node names are not stored in the header, files produced with this workflow may be
#'          much larger than files produced using the \code{\link{write_binary_trees}} function (e.g. if
#'          the file contains many trees which all have the same tip labels). The advantage of this approach is
#'          that the trees do not need to be all available/stored in memory at the same time.
#'
#'          Finalising the file is not \emph{strictly} necessary, in the sense that a file with a missing or
#'          incomplete trailer can still be parsed. However, parsing such a file requires scanning through the
#'          whole file to determine tree addresses (which is not necessary if they are stored in a proper trailer).
#'
#'          The additional binary data (if any) will be written in the file after the trees and before the trailer.
#'
#'
#' @author Giorgio Bianchini
#'
#' @seealso \code{\link{begin_writing_binary_trees}}, \code{\link{keep_writing_binary_trees}}, \code{\link[ape]{ape}}, \code{\link[ape]{write.tree}}
#'
#' @references
#' \url{https://github.com/arklumpus/TreeNode/blob/master/BinaryTree.md}
#'
#'
#' @examples
#' #A simple tree
#' tree1 <- ape::read.tree(text = "((A,B),(C,D));")
#'
#' # Initialise the output file
#' addresses <- begin_writing_binary_trees("outputFile.tbi")
#'
#' # Append a tree to the output file
#' addresses <- keep_writing_binary_trees(tree1, "outputFile.tbi", addresses)
#'
#' # Some more trees (note that we are overwriting tree1)
#' tree1 <- ape::read.tree(text = "(((A,B),C),D);")
#' tree2 <- ape::read.tree(text = "((D,(A,B)),C);")
#'
#' # Append them to the file
#' addresses <- keep_writing_binary_trees(tree1, "outputFile.tbi", addresses)
#' addresses <- keep_writing_binary_trees(tree2, "outputFile.tbi", addresses)
#'
#' #Some raw data
#' raw_data <- as.raw(seq(1, 5))
#'
#' # Finalise the output file
#' finish_writing_binary_trees("outputFile.tbi", addresses, raw_data)
#'
#' @export
finish_writing_binary_trees <- function(file, addresses, additional_data = vector("raw", 0))
{
  Rcpp_finish_writing_binary_trees(file, addresses, additional_data);
}
