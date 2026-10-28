// src/command_line.h: quoting one argument of a Windows command line, as CommandLineToArgvW and
// the C runtime read it back. Used to start the Launcher with paths (src/not_set_up.cpp).

#include <catch_amalgamated.hpp>

#include "command_line.h"

using topspin4::command_line::QuoteArgument;

TEST_CASE("A plain path is wrapped in quotes") {
  CHECK(QuoteArgument(LR"(C:\Games\Top Spin 4)") == LR"("C:\Games\Top Spin 4")");
}

TEST_CASE("A path that ends in a backslash does not escape the closing quote") {
  // "D:\Saves\" would read as D:\Saves" followed by the rest of the command line.
  CHECK(QuoteArgument(LR"(D:\Saves\)") == LR"("D:\Saves\\")");
  CHECK(QuoteArgument(LR"(D:\)") == LR"("D:\\")");
  CHECK(QuoteArgument(LR"(D:\Saves\\)") == LR"("D:\Saves\\\\")");
}

TEST_CASE("Backslashes before a quote are doubled and the quote is escaped") {
  CHECK(QuoteArgument(LR"(a"b)") == LR"("a\"b")");
  CHECK(QuoteArgument(LR"(a\"b)") == LR"("a\\\"b")");
}

TEST_CASE("Backslashes in the middle of a path are left alone") {
  CHECK(QuoteArgument(LR"(C:\a\b\c.txt)") == LR"("C:\a\b\c.txt")");
  CHECK(QuoteArgument(L"") == LR"("")");
}
