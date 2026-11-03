// The game relaunches itself to go back to the title screen: Boot calls
// XamLoaderLaunchTitle("default.xex") after XamLoaderSetLaunchData, and reads the data back at
// the next boot (it marks the boot as not fresh). Xenia relaunches inside its window; the SDK
// cannot, so the port hands over to a new copy (src/relaunch.cpp). This is the pure part:
// whether a launch is the game's own, and the new copy's arguments.

#pragma once

#include <algorithm>
#include <cctype>
#include <cstdint>
#include <string>
#include <string_view>
#include <vector>

namespace topspin4::relaunch {

inline constexpr std::wstring_view kLaunchDataFlag = L"--ts4_launch_data";
inline constexpr std::wstring_view kReadyFlag = L"--ts4_relaunch_ready";
inline constexpr std::wstring_view kRectFlag = L"--ts4_relaunch_rect";
// Tests: the scripted pad of the copy a relaunch starts (it becomes that copy's --ts4_input_script).
inline constexpr std::wstring_view kRelaunchScriptFlag = L"--ts4_relaunch_input_script";

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

// "--flag" of "--flag=value" (the whole argument when it has no value).
inline std::wstring_view FlagOf(std::wstring_view argument) {
  return argument.substr(0, argument.find(L'='));
}

// The new copy's arguments (without the exe): this run's, minus a scripted pad (a script drives
// one run; replayed it would press through the title again) and minus any flag `settings`
// sets, then `settings` ("--flag=value" each). A --ts4_relaunch_input_script becomes the new
// copy's --ts4_input_script.
inline std::vector<std::wstring> ChildArguments(const std::vector<std::wstring>& arguments,
                                                const std::vector<std::wstring>& settings) {
  std::vector<std::wstring> child;
  for (const std::wstring& argument : arguments) {
    const std::wstring_view flag = FlagOf(argument);
    if (flag == L"--ts4_input_script") continue;
    if (flag == kRelaunchScriptFlag) {
      child.push_back(L"--ts4_input_script" + argument.substr(flag.size()));
      continue;
    }
    if (std::any_of(settings.begin(), settings.end(),
                    [&](const std::wstring& setting) { return FlagOf(setting) == flag; })) {
      continue;
    }
    child.push_back(argument);
  }
  child.insert(child.end(), settings.begin(), settings.end());
  return child;
}

// "x,y,width,height" (the window's outer rectangle) as the new copy reads it; false if malformed.
struct Rect {
  int32_t x = 0, y = 0, width = 0, height = 0;
};

inline bool ParseRect(std::string_view text, Rect& rect) {
  int32_t values[4] = {};
  size_t at = 0;
  for (int i = 0; i < 4; ++i) {
    bool negative = false;
    if (at < text.size() && text[at] == '-') {
      negative = true;
      ++at;
    }
    const size_t start = at;
    int64_t value = 0;
    while (at < text.size() && text[at] >= '0' && text[at] <= '9' && value < (1 << 24)) {
      value = value * 10 + (text[at++] - '0');
    }
    if (at == start) return false;
    values[i] = int32_t(negative ? -value : value);
    if (i < 3) {
      if (at >= text.size() || text[at] != ',') return false;
      ++at;
    }
  }
  if (at != text.size() || values[2] <= 0 || values[3] <= 0) return false;
  rect = {values[0], values[1], values[2], values[3]};
  return true;
}

}  // namespace topspin4::relaunch
