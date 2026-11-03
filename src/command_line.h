// Quoting one argument of a Windows command line so that CommandLineToArgvW and the C runtime read
// it back unchanged. A path that ends in a backslash must not be written as "D:\Saves\": the
// backslash would escape the closing quote and swallow the rest of the line.

#pragma once

#include <cstddef>
#include <string>
#include <string_view>

namespace topspin4::command_line {

inline std::wstring QuoteArgument(std::wstring_view argument) {
  std::wstring quoted = L"\"";
  size_t backslashes = 0;
  for (const wchar_t c : argument) {
    if (c == L'\\') {
      ++backslashes;
      continue;
    }
    if (c == L'"') {
      // The backslashes before a quote are doubled, and one more escapes the quote itself.
      quoted.append(backslashes * 2 + 1, L'\\');
    } else {
      quoted.append(backslashes, L'\\');
    }
    backslashes = 0;
    quoted.push_back(c);
  }
  // Before the closing quote they are doubled, so the quote is not escaped.
  quoted.append(backslashes * 2, L'\\');
  quoted.push_back(L'"');
  return quoted;
}

}  // namespace topspin4::command_line
