// The game relaunches itself to go back to the title screen: Boot calls
// XamLoaderLaunchTitle("default.xex") after XamLoaderSetLaunchData, and reads the data back at
// the next boot (it marks the boot as not fresh). The SDK ends the process instead, so the port
// starts a new copy (src/relaunch.cpp). This is the pure part: whether a launch is the game's
// own, and the new copy's arguments.

#pragma once

#include <algorithm>
#include <cwctype>
#include <string>
#include <string_view>
#include <vector>

namespace topspin4::relaunch {

inline constexpr std::wstring_view kLaunchDataFlag = L"--ts4_launch_data";

// A launch of the game's own executable ("game:\default.xex", or the bare name).
inline bool IsOwnTitle(std::string_view launch_path) {
  constexpr std::string_view kName = "default.xex";
  if (launch_path.size() < kName.size()) return false;
  const std::string_view tail = launch_path.substr(launch_path.size() - kName.size());
  if (!std::equal(tail.begin(), tail.end(), kName.begin(),
                  [](char a, char b) { return std::tolower(uint8_t(a)) == b; })) {
    return false;
  }
  if (launch_path.size() == kName.size()) return true;
  const char before = launch_path[launch_path.size() - kName.size() - 1];
  return before == '\\' || before == '/' || before == ':';
}

inline bool HasFlag(std::wstring_view argument, std::wstring_view flag) {
  return argument.size() >= flag.size() && argument.substr(0, flag.size()) == flag &&
         (argument.size() == flag.size() || argument[flag.size()] == L'=');
}

// The new copy's arguments (without the exe): this run's, minus an earlier launch data file and
// a scripted pad (a script drives one run; replayed it would press through the title again),
// plus the launch data file.
inline std::vector<std::wstring> ChildArguments(const std::vector<std::wstring>& arguments,
                                                const std::wstring& launch_data_file) {
  std::vector<std::wstring> child;
  for (const std::wstring& argument : arguments) {
    if (HasFlag(argument, kLaunchDataFlag) || HasFlag(argument, L"--ts4_input_script")) continue;
    child.push_back(argument);
  }
  child.push_back(std::wstring(kLaunchDataFlag) + L"=" + launch_data_file);
  return child;
}

}  // namespace topspin4::relaunch
