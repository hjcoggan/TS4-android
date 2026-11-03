// The achievement toasts waiting to show: one at a time, each for `duration` from the frame it
// first shows. Thread-safe (the runtime pushes from guest threads, the UI thread draws), and free
// of the SDK so the host tests can drive it with plain values and their own clock.

#pragma once

#include <chrono>
#include <deque>
#include <mutex>
#include <optional>
#include <utility>

namespace topspin4::achievement_toast {

template <typename Event, typename Clock = std::chrono::steady_clock>
class Queue {
 public:
  explicit Queue(float duration_seconds) : duration_(duration_seconds) {}

  void Push(Event event) {
    std::lock_guard lock(mutex_);
    pending_.push_back({std::move(event), std::nullopt});
  }

  struct Showing {
    Event event;
    float age = 0;         // seconds since it first showed
    bool started = false;  // true on the frame it first shows (when its sound plays)
  };

  // The toast to draw at `now`, dropping the ones whose time is up; nullopt once none is left.
  std::optional<Showing> At(typename Clock::time_point now) {
    std::lock_guard lock(mutex_);
    while (!pending_.empty()) {
      auto& front = pending_.front();
      const bool started = !front.shown_since;
      if (started) front.shown_since = now;
      const float age = std::chrono::duration<float>(now - *front.shown_since).count();
      if (age < duration_) return Showing{front.event, age, started};
      pending_.pop_front();
    }
    return std::nullopt;
  }

 private:
  struct Pending {
    Event event;
    std::optional<typename Clock::time_point> shown_since;
  };

  const float duration_;
  std::mutex mutex_;
  std::deque<Pending> pending_;
};

}  // namespace topspin4::achievement_toast
