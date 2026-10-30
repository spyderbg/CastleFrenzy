import { readFile, readdir, realpath, copyFile, mkdir } from "node:fs/promises";
import path from "node:path";
import { createHash } from "node:crypto";
import {
  parseSpriteAny,
  serializeSpriteFor,
  isSpritePath,
  decodeText,
} from "./format";
import { validateSprite, type SpriteDocument } from "../shared/sprite";
import { atomicWrite } from "./batch";

export class Workspace {
  private pending = new Map<string, Promise<unknown>>();
  constructor(public root: string) {
    this.root = path.resolve(root);
  }
  async file(relative: string, existing = true) {
    if (typeof relative !== "string" || relative.includes("\0"))
      throw new Error("Invalid path");
    const file = path.resolve(this.root, relative.replace(/\\/g, "/"));
    if (file !== this.root && !file.startsWith(this.root + path.sep))
      throw new Error("File is outside the workspace");
    let ancestor = existing ? file : path.dirname(file);
    if (!existing)
      while (true) {
        try {
          await realpath(ancestor);
          break;
        } catch (error) {
          if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
          const parent = path.dirname(ancestor);
          if (parent === ancestor) throw error;
          ancestor = parent;
        }
      }
    const check = await realpath(ancestor);
    if (
      check !== (await realpath(this.root)) &&
      !check.startsWith((await realpath(this.root)) + path.sep)
    )
      throw new Error("File is outside the workspace");
    return file;
  }
  async list(directory: string) {
    const folder = await this.file(directory || ".");
    const entries = await readdir(folder, { withFileTypes: true });
    return entries
      .filter(
        (e) =>
          !e.name.startsWith(".") &&
          !["node_modules", "build", "bin", "dist"].includes(e.name),
      )
      .filter(
        (e) =>
          !e.isSymbolicLink() &&
          (e.isDirectory() ||
            /\.(sprite|ya?ml|sprcmd|png|tga|bmp|fft)$/i.test(e.name)),
      )
      .map((e) => ({
        name: e.name,
        path: path
          .relative(this.root, path.join(folder, e.name))
          .split(path.sep)
          .join("/"),
        directory: e.isDirectory(),
      }))
      .sort(
        (a, b) =>
          Number(b.directory) - Number(a.directory) ||
          a.name.localeCompare(b.name),
      );
  }
  async open(relative: string) {
    if (!isSpritePath(relative))
      throw new Error("Choose a .sprite or .yaml file");
    const file = await this.file(relative);
    const bytes = await readFile(file);
    const source = decodeText(bytes);
    return {
      path: path.relative(this.root, file).split(path.sep).join("/"),
      document: parseSpriteAny(source),
      revision: hash(bytes),
      source,
    };
  }
  async save(
    relative: string,
    document: SpriteDocument,
    revision: string,
    saveAs = false,
    sourcePath?: string,
    rawSource?: string,
  ) {
    const key = path.resolve(this.root, relative);
    const result = (this.pending.get(key) || Promise.resolve())
      .catch(() => {})
      .then(() =>
        this.saveUnlocked(
          relative,
          document,
          revision,
          saveAs,
          sourcePath,
          rawSource,
        ),
      );
    this.pending.set(key, result);
    try {
      return await result;
    } finally {
      if (this.pending.get(key) === result) this.pending.delete(key);
    }
  }
  private async saveUnlocked(
    relative: string,
    document: SpriteDocument,
    revision: string,
    saveAs = false,
    sourcePath?: string,
    rawSource?: string,
  ) {
    const errors = validateSprite(document);
    if (errors.length) throw new Error(errors.join("\n"));
    if (
      rawSource !== undefined &&
      JSON.stringify(parseSpriteAny(rawSource)) !== JSON.stringify(document)
    )
      throw new Error(
        "Source and document differ. Apply the source before saving.",
      );
    if (!isSpritePath(relative))
      throw new Error("Save as a .sprite or .yaml file");
    const file = await this.file(relative, !saveAs);
    if (
      saveAs &&
      sourcePath &&
      path.dirname(sourcePath) !== path.dirname(relative)
    ) {
      const source = await this.file(sourcePath, false);
      document = structuredClone(document);
      rawSource = undefined;
      for (const image of document.images)
        for (const key of ["file", "alphaFile"] as const)
          if (image[key])
            image[key] = path
              .relative(
                path.dirname(file),
                path.resolve(
                  path.dirname(source),
                  image[key]!.replace(/\\/g, "/"),
                ),
              )
              .split(path.sep)
              .join("/");
    }
    let previous: Buffer | undefined;
    try {
      previous = await readFile(file);
    } catch (e) {
      if ((e as NodeJS.ErrnoException).code !== "ENOENT") throw e;
    }
    if (previous) {
      if (saveAs)
        throw new Error(
          "Save As will not overwrite an existing file. Choose another name.",
        );
      if (hash(previous) !== revision)
        throw new Error("The sprite changed on disk. Reload before saving.");
      if (
        JSON.stringify(parseSpriteAny(decodeText(previous))) ===
          JSON.stringify(document) &&
        (rawSource === undefined || rawSource === decodeText(previous))
      )
        return this.open(relative);
      const backup = path.join(
        this.root,
        ".auroramx/backups",
        new Date().toISOString().replace(/[:.]/g, "-") +
          "-" +
          revision.slice(0, 8),
        relative,
      );
      await mkdir(path.dirname(backup), { recursive: true });
      await copyFile(file, backup);
    }
    await atomicWrite(file, rawSource ?? serializeSpriteFor(file, document));
    return this.open(relative);
  }
  async imagePath(spritePath: string, file: string) {
    const relative = path.join(
      path.dirname(spritePath),
      file.replace(/\\/g, "/"),
    );
    // Immutable legacy sprites retain their old image names. Prefer the
    // migrated PNG, including case-only filename differences on Linux.
    for (const candidate of new Set([
      relative.replace(/\.(?:tga|pvr|bmp)$/i, ".png"), relative,
    ])) {
      try {
        return await this.file(candidate);
      } catch {
        const folder = await this.file(path.dirname(candidate));
        const match = (await readdir(folder)).find(
          (name) => name.toLowerCase() === path.basename(candidate).toLowerCase(),
        );
        if (match) return this.file(path.join(path.dirname(candidate), match));
      }
    }
    throw new Error(`Image not found: ${file}`);
  }
}
const hash = (bytes: Buffer) =>
  createHash("sha256").update(bytes).digest("hex");
