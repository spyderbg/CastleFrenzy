# Original texture variants

These PNGs preserve original authoring images that differed from the same-name
PNGs already used by the game. The active PNGs keep their existing pixels and
sizes, including the exported font alpha and effect texture dimensions.

This directory is outside the runtime asset packs and is not staged or packaged.
Use these images as lossless source art when intentionally updating a runtime
texture. Repeated copies with identical decoded pixels share one source image.
`migration.json` records the old paths, new paths, sizes and RGBA pixel hashes for
all migrated project-owned images. Third-party library samples were not changed.
