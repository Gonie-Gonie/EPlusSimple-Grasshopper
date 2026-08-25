# Dragon icon assets

- `source/` contains the full product artwork used for assembly, package, and Yak icons.
- `illustrated/` contains transparent 4-by-4 functional illustration atlases. These are the
  high-resolution visual masters for Grasshopper component icons.
- `generated/<product>/components/` contains the exact 24-by-24 embedded component resources.
  Every public component has a type-named PNG, a two-pixel transparent border, and a unique
  content hash.
- `generated/<product>/*-component-contact-sheet.png` provides a review sheet for all current
  component resources.

Run `dev.cmd icons` from the repository root after changing an icon master or the component
catalog. The deterministic generator trims atlas noise, composes functional geometry overlays,
checks dimensions/borders/uniqueness, refreshes the contact sheets, and synchronizes package
icons. Do not hand-edit generated PNG files.

