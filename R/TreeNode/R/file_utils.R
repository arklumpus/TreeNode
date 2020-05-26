########################################################################
#  file_utils.R    2020-05-20
#  by Giorgio Bianchini
#  This file is part of the R package TreeNode, licensed under GPLv3
#
#  Functions to read data from binary tree format files.
########################################################################


#Check whether a binary tree format file has a valid trailer.
has_valid_trailer <- function(file)
{
  file <- check_file(file)

  position <- seek(file, rw="read", where=-4, origin="end")

  trailer <- read_bytes(file, 4)

  seek(file, rw="read", where=position, origin="start")

  return(all(trailer == c(0x45, 0x4e, 0x44, 0xff)))
}

#Check whether an object is a valid file connection or a file name. In
#the latter case, open a connection to the file.
check_file <- function(file)
{
  if (length(class(file)) == 1 && class(file) == "character")
  {
    file <- file(file, "rb")
  }

  if (!isOpen(file) || !isSeekable(file))
  {
    stop("Invalid file object!")
  }

  return(file)
}

#Read multiple bytes from a file connection.
read_bytes <- function(file, n=1L)
{
  return(readBin(file, "raw", n, endian="little"))
}

#Read a single byte from a file connection.
read_byte <- function(file)
{
  return(readBin(file, "raw", 1, endian="little")[[1]])
}

#Read a 64-bit wide integer from a file connection. R integers are
#32-bit wide, so this may cause issues. This should only happen with
#files larger than 2GB.
read_int64 <- function(file)
{
  bytes <- read_bytes(file, 8)
  num <- 0
  for (i in seq(0, 7))
  {
    num <- num + as.integer(bytes[[i + 1]]) * 256^i
  }

  return(num)
}

#Read a 32-bit wide integer from a file connection.
read_int32 <- function(file)
{
  bytes <- read_bytes(file, 4)

  num <- 0
  for (i in seq(0, 3))
  {
    num <- num + as.integer(bytes[[i + 1]]) * 256^i
  }

  return(num)
}

#Read a variable-width integer from a file connection. If the integer
#is < 254, it is only 1-byte wide. Otherwise, it is 40-bit (5-byte) wide.
read_int <- function(file)
{
  b <- as.integer(read_byte(file))

  if (b < 254)
  {
    return(b);
  }
  else
  {
    return(read_int32(file))
  }
}

#Read a string from the stream. The string is stored as an integer n
#representing its length followed by n integers that constitute the
#UTF-16 representation of the string.
read_my_string <- function(file)
{
  length <- read_int(file)

  chars <- vector("integer", length)

  for (i in seq(1, length))
  {
    chars[[i]] <- read_int(file)
  }

  return(intToUtf8(chars, allow_surrogate_pairs=TRUE))
}
