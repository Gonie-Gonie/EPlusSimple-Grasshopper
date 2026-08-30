# Dragon icon assets

- `source/` contains the full product artwork used for assembly, package, and Yak icons.
- `illustrated/` contains transparent 4-by-4 functional illustration atlases. These are the
  high-resolution product signatures and the visual masters for components whose atlas subject
  remains legible at toolbar scale. It can also contain a type-specific transparent master when
  an added component needs a silhouette that the existing atlas does not provide.
- `generated/<product>/components/` contains the exact 24-by-24 embedded component resources.
  Every public component has a type-named PNG, a two-pixel transparent border, and a unique
  content hash.
- `generated/<product>/parameters/` contains the corresponding type-named 24-by-24 resources
  for every public persistent parameter. Parameter icons retain the illustrated domain subject
  and add a large corner-and-socket data frame so they cannot collapse to Grasshopper's shared
  default parameter glyph or be confused with an operation component.
- `generated/<product>/*-component-contact-sheet.png` provides a review sheet for all current
  component resources.
- `generated/<product>/*-parameter-contact-sheet.png` provides the matching parameter review
  sheet.

Run `dev.cmd icons` from the repository root after changing an icon master, palette, silhouette,
or the component catalog. At 24-by-24, HVAC equipment and file operations use deterministic
System.Drawing silhouettes across roughly 65-80 percent of the working canvas. The illustrated
atlas remains visible as the product signature behind a spectral InvisibleDragon backplate or an
origami SimpleDragon backplate; it is not reused as the primary shape for a family of equipment.

The generator trims atlas noise, checks dimensions and the two-pixel border, rejects byte
collisions, and compares all component and parameter resources on both light and dark
Grasshopper-like backgrounds. No pair may share more than 72 percent of its pixels or have
normalized composited RMS distance below 0.10. These gates reject the former nine-pixel
corner-badge scheme, whose confusing families shared 82-91 percent of their pixels. The command
also refreshes all contact sheets. Packaging copies each product's canonical generated 256-pixel
icon directly, so there is no second package-icon source to synchronize. Do not hand-edit
generated PNG files.
