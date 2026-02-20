# Generic Mod Config Menu — Android Controller Fork

**This is a fork of [spacechase0's Generic Mod Config Menu](https://github.com/spacechase0/StardewValleyMods/tree/develop/GenericModConfigMenu)** with added controller/gamepad snap navigation. All original credit goes to spacechase0 for creating and maintaining GMCM.

An upstream PR has been proposed: [spacechase0/StardewValleyMods#560](https://github.com/spacechase0/StardewValleyMods/issues/560)

## What This Fork Adds

GMCM's existing mouse/keyboard interface works great, but navigating with a controller's free cursor can feel slow — especially scrolling through a long mod list or moving between settings. This fork adds **snap navigation** so the d-pad and thumbstick jump directly between interactive elements, matching how the rest of Stardew Valley handles controller input.

- **D-pad / left stick snap navigation** between interactive rows (with hold-to-repeat)
- **Element-specific interactions**: dropdowns, sliders, checkboxes, keybind editors
- **B button back-navigation**: mod config → mod list → close (with scroll position and selection preserved)
- **Only activates when `Game1.options.gamepadControls` is true** — mouse and keyboard behavior is completely unaffected

Since it keys off `gamepadControls` and not platform, it works for Android controllers, Steam Deck, and any PC gamepad setup.

## Installation

Download the latest release and extract it to your `Mods/` folder, replacing the standard GenericModConfigMenu. The UniqueID is the same (`spacechase0.GenericModConfigMenu`), so all mods that register with GMCM will work without changes.

## Compatibility

- Stardew Valley 1.6+ (Android or PC)
- SMAPI 4.0+
- Drop-in replacement for standard GMCM — same UniqueID, same API

## License

MIT — see [LICENSE](LICENSE). Original work by spacechase0, controller navigation by sonofskywalker3.
