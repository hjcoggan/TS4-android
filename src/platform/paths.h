// Host paths for the port (docs/windows-api-audit.md, "Suggested platform shim").
//
// Shared code never asks the OS where it lives: it asks here. On Windows that is the folder
// holding topspin4.exe (the portable install, ADR-0009). On Android there is no writable exe
// folder (the process is /system/bin/app_process64), so the base is the app's data folder, given
// by the Java activity through REX_APP_FOLDER / REX_EXECUTABLE_FOLDER before SDL loads us.
//
// Everything is UTF-8 std::string / std::filesystem::path; there is no std::wstring here.

#pragma once

#include <cstdio>
#include <filesystem>
#include <string>
#include <string_view>

namespace topspin4::platform {

// The folder the portable install is rooted at: the exe folder on Windows, the app data folder
// (gamedata/, updatedata/, userdata/, cache/) on Android.
std::filesystem::path BaseFolder();

// An explicit base, before anything reads it (the command line's --user_data_root or a script).
void SetBaseFolder(const std::filesystem::path& folder);

// BaseFolder()/userdata: saves, the Settings file, achievements.
std::filesystem::path UserDataFolder();

// BaseFolder(): where gamedata/ and updatedata/ sit.
std::filesystem::path PreparedDataRoot();

// BaseFolder()/cache: shader and pipeline caches.
std::filesystem::path CacheFolder();

// BaseFolder()/logs: crash reports and session logs.
std::filesystem::path LogsFolder();

// The system temporary folder (Android: the app cache dir, via TMPDIR).
std::filesystem::path TempFolder();

// UTF-8 <-> path. PathToUtf8 is for logs and APIs, never for opening files.
std::filesystem::path PathFromUtf8(std::string_view text);
std::string PathToUtf8(const std::filesystem::path& path);

// fopen for a path whose name may be outside the ANSI code page. On Windows this widens and
// calls _wfopen; everywhere else the narrow path is already UTF-8.
FILE* OpenFile(const std::filesystem::path& path, const char* mode);

}  // namespace topspin4::platform
