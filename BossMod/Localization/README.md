# Localization

The interface can be shown in Simplified Chinese (Settings → Interface → Language; "Follow Dalamud" picks Chinese when Dalamud's own language is Chinese).

## How it works
- English source strings are the keys. Translations live in `zh_CN/*.json` as flat `{"English": "中文"}` objects and are embedded into the assembly; all files are merged at load time.
- Strings are translated when they are drawn (`Loc.Tr`), so configs, presets and replays always keep english text.
- Keys containing `{0}`, `{1}`... are templates: strings built with interpolation (`$"Stack with {name}!"`) are matched against them at runtime, so module code doesn't need to change.
- A string ending with a counter (`"Tower 2"`, `"Raidwide x3"`) falls back to the translation of its base text.

Already translated centrally, no code change needed:
- config attributes (`[PropertyDisplay]`, `[SectionStart]`, `[ConfigDisplay]`, `[PropertyCombo]`) and enum display names;
- autorotation track and option names (`StrategyConfig.UIName`, `StrategyOption.UIName`);
- boss module hints (`hints.Add(...)`, component hint strings) and state names in the timeline.

## Adding UI text
- Text: `ImGui.TextUnformatted(Loc.Tr("Foo"))`.
- Widget labels used as ImGui ids: `ImGui.Button(Loc.Id("Save"))` (keeps the id derived from the english text).
- Interpolated text: `Loc.Format("Pull in {0:f1}s", t)`; the dictionary key must match the format string exactly.
- Add the english key and its translation to one of the `zh_CN/*.json` files (module strings go to the `modules_*` files, everything else to the matching `ui_*`/`ar_*` file).

## Finding untranslated strings
`/bmr loc collect` starts recording strings that are drawn without a translation, `/bmr loc dump` writes them to `missing-translations.json` in the plugin config folder.
