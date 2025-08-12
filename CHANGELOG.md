# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.9.0] - 2025-08-13

### Added

- Ability to like online items
- Ability to delete online items (locally, current run only)
- Config values to control whether clients can like or delete
- Added Scout Cannon to shareable items pool (Check the configuration; you may need to add it to the allow list manually)

### Fixed
- Invisible ropes on the client side (thanks to Harmony for the feedback!)
- Improved network handling

### Changed
- Updated visuals for online item overlay

### Removed
- Сode has been cleaned of warnings

## [0.8.3] - 2025-08-09

### Changed
- Fixed the magic bean breaking the game

## [0.8.2] - 2025-08-09 - **BROKEN, DO NOT USE**

### Added

- Structures are now spawning and despawning by map segment (optimization!)
- Configurable allow list for structures
- Rope Optimizer©️ (experimental but enabled by default, mitigates the HUGE performance hit caused by large number of ropes, disable it in config if ropes become unusable)

### Changed
- Fixed a bug that caused many structures from a several previous runs to appear for reconnecting clients.
- Default limit for online structures bumped to 40
- Server-side limit for online structures will be bumped to 300 following this mod update
- Slightly better steam ticket handling so the client can survive the server restart

## [0.8.1] - 2025-08-03

### Added

- Changelog updated - oops!
- README fixes

## [0.8.0] - 2025-08-03

### Added

- Async multiplayer (prototype!)
- Config
- License XD

### Changed
- `Load locally saved structures` turned off by default

## [0.5.1] - 2025-07-31

### Added

- Initial public beta release
