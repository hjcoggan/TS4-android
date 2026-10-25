// Crash reports (ticket 11), adapted from WWE All Stars' (ADR-0004): what goes into the zip, the
// zip itself, and the request a crashing game hands the second copy of itself that writes the
// dump.

#include <catch_amalgamated.hpp>

#include <cstdint>
#include <ctime>
#include <filesystem>
#include <fstream>
#include <random>
#include <string>
#include <vector>

#include "crash_dump_request.h"
#include "crash_report_plan.h"
#include "crash_zip.h"

namespace fs = std::filesystem;
using namespace topspin4::crash;

namespace {

CrashFacts Crash() {
  CrashFacts facts;
  facts.version = "0.1.0";
  facts.commit = "f9c8b3e";
  facts.renderer = "classic";
  facts.gpu_name = "AMD Radeon RX 7900 XT";
  facts.driver_version = "0x80018B";
  facts.exception_status = 0xC0000005;
  facts.stamp = "20261025-110000";
  facts.logs_folder = "C:/ts4/logs";
  facts.session_log = "C:/ts4/logs/topspin4_007.log";
  facts.settings_file = "C:/Users/p/AppData/Roaming/TopSpin4Recomp/topspin4.settings.toml";
  facts.settings_exists = true;
  return facts;
}

uint32_t U16(const std::vector<uint8_t>& v, size_t at) { return v[at] | v[at + 1] << 8; }

std::tm Time() {
  std::tm time{};
  time.tm_year = 2026 - 1900;
  time.tm_mon = 10 - 1;
  time.tm_mday = 25;
  time.tm_hour = 14;
  time.tm_min = 5;
  time.tm_sec = 31;
  return time;
}

struct TempFolder {
  TempFolder() {
    std::random_device random;
    path = fs::temp_directory_path() / ("ts4-crash-" + std::to_string(random()));
    fs::create_directories(path);
  }
  ~TempFolder() {
    std::error_code error;
    fs::remove_all(path, error);
  }
  fs::path path;
};

}  // namespace

TEST_CASE("A crash report holds the dump, the session log, the settings file and a manifest") {
  const auto plan = PlanCrashReport(Crash());

  CHECK(plan.zip_path == fs::path("C:/ts4/logs/crash-20261025-110000.zip"));
  CHECK(plan.dump_name == "crash.dmp");
  REQUIRE(plan.files.size() == 2);
  CHECK(plan.files[0].name == "topspin4_007.log");
  CHECK(plan.files[0].source == fs::path("C:/ts4/logs/topspin4_007.log"));
  CHECK(plan.files[1].name == "topspin4.settings.toml");
  CHECK(plan.manifest_name == "manifest.txt");
  CHECK(plan.manifest ==
        "Top Spin 4 Recomp crash report\n"
        "version: 0.1.0\n"
        "commit: f9c8b3e\n"
        "renderer: classic\n"
        "gpu: AMD Radeon RX 7900 XT\n"
        "driver: 0x80018B\n"
        "reason: exception 0xC0000005\n"
        "session log: topspin4_007.log\n"
        "settings file: topspin4.settings.toml\n"
        "launcher log: none\n");
}

TEST_CASE("A crash report without a settings file, session log or GPU says so") {
  CrashFacts facts = Crash();
  facts.settings_exists = false;
  facts.session_log.clear();
  facts.gpu_name.clear();
  facts.driver_version.clear();

  const auto plan = PlanCrashReport(facts);

  CHECK(plan.files.empty());
  CHECK(plan.manifest.find("gpu: unknown\ndriver: unknown\n") != std::string::npos);
  CHECK(plan.manifest.find("session log: none\nsettings file: none\n") != std::string::npos);
}

TEST_CASE("A crash report holds the launcher log when the launcher wrote one") {
  CrashFacts facts = Crash();
  facts.launcher_log = "C:/ts4/logs/launcher.log";

  const auto plan = PlanCrashReport(facts);

  REQUIRE(plan.files.size() == 3);
  CHECK(plan.files[2].name == "launcher.log");
  CHECK(plan.manifest.find("launcher log: launcher.log\n") != std::string::npos);
}

TEST_CASE("A crash report carries no game data: only logs, settings and the manifest") {
  CrashFacts facts = Crash();
  facts.launcher_log = "C:/ts4/logs/launcher.log";
  for (const ReportFile& file : PlanCrashReport(facts).files) {
    INFO(file.name);
    const std::string extension = file.source.extension().string();
    CHECK((extension == ".log" || extension == ".toml"));
  }
}

TEST_CASE("A crash report names its files in UTF-8, whatever the code page") {
  CrashFacts facts = Crash();
  facts.settings_file = fs::path(u8"C:/hra/nastaven\u00ed \u5168.toml");

  const auto plan = PlanCrashReport(facts);

  REQUIRE(plan.files.size() == 2);
  CHECK(plan.files[1].name == "nastaven\xC3\xAD \xE5\x85\xA8.toml");
}

TEST_CASE("The logs in a crash report are capped, keeping their start and end") {
  const auto plan = PlanCrashReport(Crash());
  CHECK(plan.files[0].max_bytes == kMaxLogBytes);
  CHECK(plan.files[1].max_bytes == 0);

  std::vector<uint8_t> log;
  for (int i = 0; i < 1000; ++i) log.push_back(uint8_t('a' + i % 26));
  CHECK(CapLog(log, 0) == log);
  CHECK(CapLog(log, 1000) == log);
  const std::vector<uint8_t> capped = CapLog(log, 400);
  const std::string text(capped.begin(), capped.end());
  const std::string original(log.begin(), log.end());
  CHECK(text.starts_with(original.substr(0, 100)));
  CHECK(text.ends_with(original.substr(700)));
  CHECK(text.find("\n[crash report: 600 bytes left out]\n") == 100);
}

TEST_CASE("A report from abort, a pure call or a bad CRT parameter names it") {
  CrashFacts facts = Crash();
  facts.exception_status = kAbortCode;
  CHECK(PlanCrashReport(facts).manifest.find("reason: abort (exception 0xE0545301)\n") !=
        std::string::npos);
  facts.exception_status = kPureCallCode;
  CHECK(PlanCrashReport(facts).manifest.find(
            "reason: pure virtual call (exception 0xE0545302)\n") != std::string::npos);
  facts.exception_status = kInvalidParameterCode;
  CHECK(PlanCrashReport(facts).manifest.find(
            "reason: invalid CRT parameter (exception 0xE0545303)\n") != std::string::npos);
}

TEST_CASE("The session log is the newest of the runtime's rotating logs") {
  TempFolder folder;
  CHECK(NewestSessionLog(folder.path).empty());
  std::ofstream(folder.path / "topspin4_001.log") << "a";
  std::ofstream(folder.path / "topspin4_010.log") << "b";
  std::ofstream(folder.path / "topspin4_002.log") << "c";
  std::ofstream(folder.path / "launcher.log") << "d";
  CHECK(NewestSessionLog(folder.path) == folder.path / "topspin4_010.log");
}

TEST_CASE("A crash report zip flags its names as UTF-8 and dates them with the report's time") {
  std::vector<uint8_t> out;
  StoredZipWriter zip(
      [&](const uint8_t* data, size_t size) {
        out.insert(out.end(), data, data + size);
        return true;
      },
      Time());

  REQUIRE(zip.Add("nastaven\xC3\xAD.toml", {'a', 'b'}));
  REQUIRE(zip.Add("manifest.txt", {'c'}));
  REQUIRE(zip.Finish());

  CHECK(U16(out, 0) == 0x4B50);
  CHECK(U16(out, 6) == 0x0800);
  CHECK(U16(out, 10) == (14u << 11 | 5u << 5 | 31u / 2));
  CHECK(U16(out, 12) == ((2026u - 1980u) << 9 | 10u << 5 | 25u));
  CHECK(std::string(out.begin() + 30, out.begin() + 30 + 15) == "nastaven\xC3\xAD.toml");
  const size_t end = out.size() - 22;
  CHECK(U16(out, end) == 0x4B50);
  CHECK(U16(out, end + 10) == 2);
}

TEST_CASE("A crash report zip reports a write that failed, and every write after it") {
  int writes = 0;
  StoredZipWriter zip([&](const uint8_t*, size_t) { return ++writes != 2; }, Time());

  CHECK_FALSE(zip.Add("crash.dmp", std::vector<uint8_t>(64, 0x5A)));
  CHECK_FALSE(zip.Add("manifest.txt", {'c'}));
  CHECK_FALSE(zip.Finish());
}

TEST_CASE("A dump request survives the helper's command line, guest views included") {
  DumpRequest request;
  request.process_id = 1234;
  request.thread_id = 5678;
  request.exception_pointers = 0x7FF612345678;
  request.guest_views[0] = 0x100000000;
  request.guest_views[1] = 0x200000000;
  request.dump_path = fs::path(u8"C:/hra/logs/crash-20261025-120000 \u00e9.dmp");

  std::vector<std::string> args = {"topspin4.exe"};
  for (const std::string& arg : DumpRequestArgs(request)) args.push_back(arg);
  const auto parsed = ParseDumpRequest(args);

  CHECK(args[1] == "--ts4-write-crash-dump");
  REQUIRE(parsed);
  CHECK(parsed->process_id == 1234);
  CHECK(parsed->thread_id == 5678);
  CHECK(parsed->exception_pointers == 0x7FF612345678);
  CHECK(parsed->guest_views[0] == 0x100000000);
  CHECK(parsed->guest_views[1] == 0x200000000);
  CHECK(parsed->dump_path == request.dump_path);
}

TEST_CASE("No dump request without the switch, or with a malformed one") {
  CHECK_FALSE(ParseDumpRequest({"topspin4.exe", "--headless"}));
  CHECK_FALSE(ParseDumpRequest({"topspin4.exe", "--ts4-write-crash-dump", "12", "34"}));
  CHECK_FALSE(ParseDumpRequest(
      {"topspin4.exe", "--ts4-write-crash-dump", "x", "1", "0", "0", "0", "C:/a.dmp"}));
}
