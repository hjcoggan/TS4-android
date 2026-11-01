// The achievement toast's motion, in the manner of the Xbox 360 dashboard's: a round badge pops
// in at the screen's lower middle, the capsule opens out of it both ways, the text fades in;
// at the end the same in reverse. Pure, so the host tests hold the shape of it.

#pragma once

#include <algorithm>

namespace topspin4::achievement_toast {

// Seconds from when the toast first shows.
inline constexpr float kBadgeIn = 0.25f;
inline constexpr float kOpenStart = 0.20f, kOpenEnd = 0.55f;
inline constexpr float kTextInStart = 0.45f, kTextInEnd = 0.75f;
inline constexpr float kTextOutStart = 5.15f, kTextOutEnd = 5.35f;
inline constexpr float kCloseStart = 5.30f, kCloseEnd = 5.65f;
inline constexpr float kBadgeOutEnd = 6.00f;
inline constexpr float kDuration = kBadgeOutEnd;

struct Frame {
  float badge_scale = 0;  // 0..about 1.1 (the pop overshoots a little)
  float alpha = 0;        // the whole toast's opacity
  float open = 0;         // 0: only the badge; 1: the capsule at full width
  float text_alpha = 0;
};

inline float Clamp01(float x) { return std::clamp(x, 0.0f, 1.0f); }
inline float Progress(float t, float start, float end) { return Clamp01((t - start) / (end - start)); }
inline float EaseOutCubic(float x) { return 1 - (1 - x) * (1 - x) * (1 - x); }
inline float EaseInCubic(float x) { return x * x * x; }
inline float EaseOutBack(float x) {
  constexpr float c1 = 1.70158f, c3 = c1 + 1;
  const float y = x - 1;
  return 1 + c3 * y * y * y + c1 * y * y;
}

inline Frame At(float t) {
  Frame f;
  if (t < 0 || t >= kDuration) return f;
  if (t < kCloseEnd) {
    const float in = Progress(t, 0, kBadgeIn);
    f.badge_scale = EaseOutBack(in);
    f.alpha = Clamp01(in * 2);
  } else {
    const float out = Progress(t, kCloseEnd, kBadgeOutEnd);
    f.badge_scale = 1 - EaseInCubic(out);
    f.alpha = 1 - out;
  }
  f.open = t < kCloseStart ? EaseOutCubic(Progress(t, kOpenStart, kOpenEnd))
                           : 1 - EaseInCubic(Progress(t, kCloseStart, kCloseEnd));
  f.text_alpha = t < kTextOutStart ? Progress(t, kTextInStart, kTextInEnd)
                                   : 1 - Progress(t, kTextOutStart, kTextOutEnd);
  return f;
}

}  // namespace topspin4::achievement_toast
