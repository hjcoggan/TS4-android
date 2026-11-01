# Changelog

All notable changes to this project are recorded here, in the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

## [Unreleased]

### Added

- The Launcher's Achievements page shows which achievements you have earned: earned ones come first with their unlock date and an accent edge, locked ones are greyed with a padlock and show how to earn them. The header gives earned and total achievements and gamerscore, with a progress bar.
- An "Achievement unlocked" notice in the manner of the Xbox 360 dashboard's (a capsule with a green trophy badge at the bottom of the screen) replaces the small corner notice.
- An unlock sound plays with each notice; a `userdata\achievement.wav` you supply replaces it.
- Scene scripts can show any achievement's notice without unlocking it: `set achievement_toast <id>`.

### Changed

- The art folder's achievement list keeps each achievement's ID and its "how to earn it" text. An art folder written by an earlier Launcher is read again from the prepared disc on the next start.
