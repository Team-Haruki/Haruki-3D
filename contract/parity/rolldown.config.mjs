// Bundles the engine's PRODUCTION runtimePackageLoader.ts (in place, never a
// copy) to a Node-importable module for contract/parity/parity.test.mjs, using
// the engine's own bundler (rolldown, vite's bundler). Run from engine/ so the
// CLI resolves like the engine's own builds:
//   (cd engine && node_modules/.bin/rolldown -c ../contract/parity/rolldown.config.mjs)
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const engineRoot = path.resolve(here, "../../engine");

export default {
  input: path.join(engineRoot, "src/runtime/runtimePackageLoader.ts"),
  output: {
    file: path.join(here, "out/engine-runtime-package-loader.mjs"),
    format: "esm",
    codeSplitting: false,
  },
  plugins: [
    {
      // Vite-only asset imports ("...?url") cannot resolve outside vite; the
      // parity tests never execute the code paths that consume them.
      name: "stub-vite-url-assets",
      resolveId(source) {
        return source.includes("?url") ? "\0vite-url-asset-stub" : null;
      },
      load(id) {
        return id === "\0vite-url-asset-stub"
          ? 'export default "about:blank#vite-url-asset-stub";'
          : null;
      },
    },
  ],
};
