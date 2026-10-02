# Costume runtime module boundaries

Status: Accepted

`base` owns package loading, character assembly, animation, constraints and spring simulation. It must not depend on `costume_shop`, including through type-only imports.

`costume_shop` owns the single-character preview kernel, camera, height and lighting policy. The default entry exports this kernel. The internal entry supports capture and diagnostics.

Product integrations own controls and layout. The capture service owns its persistent browser lifecycle and PNG delivery. Module-boundary tests enforce these dependencies.
