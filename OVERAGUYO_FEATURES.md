# OVERAGUYO Palworld Breeding Assistant

## Confirmed workflow

1. Detect the Steam Palworld save and monitor it for changes.
2. Parse all owned Pals, genders, passives, active skills, and IVs from the save.
3. Capture only the user-selected Palworld window; never inject into or modify the game process.
4. When the Pal details screen is visible, recognize its species, gender, and four passive slots.
5. Add a screen-recognized Pal as a provisional entry immediately.
6. On the next save-file change, reconcile provisional entries with parsed save entries.
7. Let the user choose a target species and up to four desired passives.
8. Rank breeding paths using owned Pals, inheritance probability, generations, and expected cake use.

## First recognition profile

- Language: Korean
- Resolution: 1920 x 1080
- Window mode: borderless
- Reference screens: Palbox grid and Pal details supplied by the user on 2026-09-13

## Reconciliation rules

- Save-file data is authoritative after a successful parse.
- Screen recognition is immediate but provisional.
- Match candidates by species, gender, passives, and observation time.
- Never delete an unmatched provisional entry automatically; show it for user review.
- Never write to the Palworld save.

## Safety boundary

- Read-only save parsing.
- Read-only window capture.
- No game-memory access, DLL injection, input automation, or save modification.

## Implementation status

- Done: Palworld process/window discovery and read-only window capture.
- Done: Korean 1920 x 1080 borderless coordinate profile.
- Done: Pal name, gender, and four-passive region extraction.
- Done: Windows Korean OCR adapter and fuzzy matching against PalCalc's official data.
- Done: confidence gate and in-memory provisional observation list.
- Done: toolbar start/stop command and live status display.
- Done: suppress duplicate observations within five seconds while allowing identical owned Pals later.
- Done: add regression tests for the supplied Korean Anubis details screen.
- Done: connect provisional observations to the opened save and reconcile after autosave.
- Done: debounce Palworld's multi-file save burst and reload the active Steam save automatically.
- Done: validate all six detail regions against the supplied Anubis screenshot.
- Next: add a review panel for low-confidence or unmatched observations.
- Pending verification: Windows build and in-game recognition calibration.
