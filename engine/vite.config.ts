import { defineConfig } from "vite";
import { fileURLToPath } from "node:url";
import { copyBasisTranscoder, externalizeBrotliWasm } from "./vite.basis.plugin";

export default defineConfig({
  base: "./",
  plugins: [externalizeBrotliWasm(), copyBasisTranscoder()],
  worker: {
    plugins: () => [externalizeBrotliWasm()],
  },
  build: {
    lib: {
      entry: {
        index: fileURLToPath(new URL("./src/index.ts", import.meta.url)),
        internal: fileURLToPath(new URL("./src/internal.ts", import.meta.url)),
        base: fileURLToPath(new URL("./src/base/index.ts", import.meta.url)),
        costume_shop: fileURLToPath(new URL("./src/costume_shop/index.ts", import.meta.url)),
        mv: fileURLToPath(new URL("./src/mv/index.ts", import.meta.url)),
      },
      formats: ["es"],
      fileName: (_format, entryName) => ({
        index: "haruki-3d-engine.js",
        internal: "haruki-3d-engine-internal.js",
        base: "haruki-3d-engine-base.js",
        costume_shop: "haruki-3d-engine-costume-shop.js",
        mv: "haruki-3d-engine-mv.js",
      })[entryName] ?? `${entryName}.js`,
    },
    rollupOptions: {
      external: ["three", "@pixiv/three-vrm"],
      output: {
        assetFileNames: "assets/[name]-[hash][extname]",
        globals: {
          three: "THREE",
          "@pixiv/three-vrm": "THREE_VRM",
        },
      },
    },
  },
});
