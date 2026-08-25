# Dragon icon assets

- `source/` contains the full product artwork used for assembly, package, and Yak icons.
- `illustrated/` contains transparent 4-by-4 functional illustration atlases. These are the
  high-resolution product signatures and the visual masters for components whose atlas subject
  remains legible at toolbar scale.
- `generated/<product>/components/` contains the exact 24-by-24 embedded component resources.
  Every public component has a type-named PNG, a two-pixel transparent border, and a unique
  content hash.
- `generated/<product>/*-component-contact-sheet.png` provides a review sheet for all current
  component resources.

Run `dev.cmd icons` from the repository root after changing an icon master, palette, silhouette,
or the component catalog. At 24-by-24, HVAC equipment and file operations use deterministic
System.Drawing silhouettes across roughly 65-80 percent of the working canvas. The illustrated
atlas remains visible as the product signature behind a spectral InvisibleDragon backplate or an
origami SimpleDragon backplate; it is not reused as the primary shape for a family of equipment.

The generator trims atlas noise, checks dimensions and the two-pixel border, rejects byte
collisions, and compares every pair on both light and dark Grasshopper-like backgrounds. No pair
may share more than 72 percent of its pixels or have normalized composited RMS distance below
0.10. These gates reject the former nine-pixel corner-badge scheme, whose confusing families
shared 82-91 percent of their pixels. The command also refreshes both contact sheets and
synchronizes package icons. Do not hand-edit generated PNG files.
