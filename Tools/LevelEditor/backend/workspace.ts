import path from "node:path";
import { createHash, randomBytes } from "node:crypto";
import {
  readFile,
  writeFile,
  readdir,
  mkdir,
  rename,
  unlink,
  realpath,
  stat,
} from "node:fs/promises";
import {
  validate,
  type Project,
  type Unit,
  type Model,
  type OpenProject,
  newLevel,
} from "../shared/level";
import {
  configEntries,
  decode,
  readDay,
  readInfo,
  readLoadout,
  dayBytes,
  infoBytes,
  loadoutBytes,
} from "./xml";
const same = (a: unknown, b: unknown) =>
  JSON.stringify(a) === JSON.stringify(b);
const conflict = () =>
  Object.assign(
    new Error("Level files changed on disk. Reload before saving."),
    { statusCode: 409 },
  );
export async function atomicWrite(file: string, data: Buffer | string) {
  const temporary = `${file}.${randomBytes(6).toString("hex")}.tmp`;
  try {
    await writeFile(temporary, data, { flag: "wx" });
    await rename(temporary, file);
  } finally {
    await unlink(temporary).catch(() => {});
  }
}
export class Workspace {
  saving = false;
  constructor(
    public root: string,
    public defaultAssets = "Build/bin",
  ) {}
  async contained(relative: string): Promise<string> {
    if (typeof relative !== "string" || path.isAbsolute(relative))
      throw new Error("Expected a project-relative path");
    const root = await realpath(this.root),
      file = await realpath(path.resolve(root, relative));
    if (file !== root && !file.startsWith(root + path.sep))
      throw new Error("Path is outside the project");
    return file;
  }
  async assetRoots(): Promise<string[]> {
    const candidates = [
      ...new Set([
        this.defaultAssets,
        "Build/bin",
        "build/linux-debug/assets",
        "build/linux-release/assets",
      ]),
    ];
    const found: string[] = [];
    for (const p of candidates)
      try {
        await this.contained(`${p}/Levels/info.xml`);
        found.push(p);
      } catch {}
    return found;
  }
  async assets(relative: string): Promise<string> {
    if (!(await this.assetRoots()).includes(relative))
      throw new Error("Unknown asset directory");
    return this.contained(relative);
  }
  async files(assets: string): Promise<Map<string, Buffer>> {
    const dir = await this.contained(`${assets}/Levels`),
      files = new Map<string, Buffer>();
    for (const name of (await readdir(dir))
      .filter(
        (n) => n === "info.xml" || /^level\d+-((day\d+)|info)\.xml$/.test(n),
      )
      .sort()) {
      files.set(
        name,
        await readFile(await this.contained(`${assets}/Levels/${name}`)),
      );
    }
    return files;
  }
  revision(files: Map<string, Buffer>): string {
    const hash = createHash("sha256");
    for (const [name, bytes] of files) {
      hash.update(name + "\0");
      hash.update(bytes);
    }
    return hash.digest("hex");
  }
  async catalog(assets: string): Promise<{ units: Unit[]; models: Model[] }> {
    const config = async (name: string) =>
      readFile(await this.contained(`${assets}/config/${name}.xml`));
    const [u, m] = await Promise.all([config("units"), config("models")]);
    return {
      units: [...configEntries(u, "unit")].map(([name, a]) => ({
        name,
        hidden: a.HideInEditor === "true",
        icon: (a.IconRect || "0,0,20,20").split(",").map(Number),
        model: a.Model || "",
      })),
      models: [...configEntries(m, "model")].map(([name, attributes]) => ({
        name,
        attributes,
        file: attributes.File || "",
        scale: Number(attributes.Scale || 1),
      })),
    };
  }
  project(files: Map<string, Buffer>): Project {
    const info = files.get("info.xml");
    if (!info) throw new Error("Missing Levels/info.xml");
    const counts = readInfo(info);
    if (
      counts.length > 99 ||
      counts.some((c) => !Number.isInteger(c) || c < 0 || c > 99)
    )
      throw new Error("Invalid level/day counts");
    return {
      levels: counts.map((count, l) => {
        const bytes = files.get(levelFile(l));
        return {
          loadout: bytes ? readLoadout(bytes) : newLevel().loadout,
          days: Array.from({ length: count }, (_, d) => {
            const b = files.get(dayFile(l, d));
            if (!b) throw new Error(`Missing ${dayFile(l, d)}`);
            return readDay(b);
          }),
        };
      }),
    };
  }
  async open(assets = this.defaultAssets): Promise<OpenProject> {
    await this.assets(assets);
    const [files, catalog, assetRoots] = await Promise.all([
      this.files(assets),
      this.catalog(assets),
      this.assetRoots(),
    ]);
    const project = this.project(files);
    validate(project, catalog.units);
    return {
      assets,
      assetRoots,
      project,
      revision: this.revision(files),
      ...catalog,
    };
  }
  async save(assets: string, project: Project, revision: string) {
    if (this.saving)
      throw Object.assign(new Error("A save is already running"), {
        statusCode: 409,
      });
    this.saving = true;
    try {
      await this.assets(assets);
      validate(project, (await this.catalog(assets)).units);
      const before = await this.files(assets);
      if (revision !== this.revision(before)) throw conflict();
      const old = this.project(before),
        changes = new Map<string, Buffer | null>();
      const desired = new Map<string, { data: unknown; bytes: Buffer }>();
      desired.set("info.xml", {
        data: project.levels.map((l) => l.days.length),
        bytes: infoBytes(project),
      });
      project.levels.forEach((l, i) => {
        desired.set(levelFile(i), {
          data: l.loadout,
          bytes: loadoutBytes(l.loadout),
        });
        l.days.forEach((d, j) =>
          desired.set(dayFile(i, j), { data: d, bytes: dayBytes(d) }),
        );
      });
      for (const [name, item] of desired) {
        const previous = before.get(name);
        const data = !previous
          ? undefined
          : name === "info.xml"
            ? old.levels.map((l) => l.days.length)
            : name.endsWith("-info.xml")
              ? readLoadout(previous)
              : readDay(previous);
        if (!same(data, item.data)) changes.set(name, item.bytes);
      }
      // Retire obsolete indexed files when levels or days are removed.
      for (const name of before.keys())
        if (!desired.has(name)) changes.set(name, null);
      if (!changes.size) return { revision, changed: [], backup: null };
      const backup = path.join(
        ".level-editor",
        "backups",
        `${Date.now()}-${randomBytes(4).toString("hex")}`,
      );
      const backupDir = path.join(this.root, backup);
      await mkdir(backupDir, { recursive: true });
      for (const name of changes.keys())
        if (before.has(name))
          await writeFile(path.join(backupDir, name), before.get(name)!);
      await writeFile(
        path.join(backupDir, "manifest.json"),
        JSON.stringify(
          {
            assets,
            changed: [...changes.keys()],
            created: [...changes.keys()].filter((n) => !before.has(n)),
          },
          null,
          2,
        ),
      );
      if (this.revision(await this.files(assets)) !== revision)
        throw conflict();
      const directory = await this.contained(`${assets}/Levels`),
        written: string[] = [];
      try {
        // Publish the index last so newly indexed days exist before readers see it.
        for (const [name, bytes] of [...changes].sort(
          ([a], [b]) => Number(a === "info.xml") - Number(b === "info.xml"),
        )) {
          if (bytes) await atomicWrite(path.join(directory, name), bytes);
          else await unlink(path.join(directory, name));
          written.push(name);
        }
      } catch (error) {
        for (const name of written.reverse()) {
          const bytes = before.get(name);
          if (bytes) await atomicWrite(path.join(directory, name), bytes);
          else await unlink(path.join(directory, name)).catch(() => {});
        }
        throw error;
      }
      return {
        revision: this.revision(await this.files(assets)),
        changed: [...changes.keys()],
        backup,
      };
    } finally {
      this.saving = false;
    }
  }
  async model(assets: string, name: string) {
    await this.assets(assets);
    const model = (await this.catalog(assets)).models.find(
      (m) => m.name === name,
    );
    if (!model) throw new Error("Unknown model");
    let relative = path.posix.normalize(
      `Data/${model.file.replace(/\\/g, "/").replace(/\.bdae$/i, ".fbx")}`,
    );
    // Windows-authored config can differ only in basename case on Linux.
    const folder = path.posix.dirname(relative),
      basename = path.posix.basename(relative);
    const actual = (await readdir(await this.contained(folder))).find(
      (n) => n.toLowerCase() === basename.toLowerCase(),
    );
    if (actual) relative = `${folder}/${actual}`;
    await this.contained(relative);
    const textures = Object.entries(model.attributes)
      .filter(([key]) => /^TextureFile\d+$/.test(key))
      .map(([key, file]) => ({
        file,
        object:
          model.attributes[key.replace("TextureFile", "TextureObject")] || "",
      }));
    return {
      ...model,
      fbx: relative,
      textures,
      directory: path.posix.dirname(relative),
    };
  }
  async modelFile(relative: string): Promise<Buffer> {
    if (!relative.startsWith("Data/") || !/\.fbx$/i.test(relative))
      throw new Error("Expected a Data/ FBX model");
    const file = await this.contained(relative);
    if ((await stat(file)).size > 128 * 1024 * 1024)
      throw new Error("Model too large");
    return readFile(file);
  }
  async texture(relative: string): Promise<{ file: string; bytes: Buffer }> {
    if (
      !relative.startsWith("Data/") &&
      !relative.startsWith("Build/bin/") &&
      !relative.startsWith("build/")
    )
      throw new Error("Invalid texture path");
    if (!/\.(png|tga|bmp)$/i.test(relative))
      throw new Error("Expected PNG/TGA/BMP");
    for (const extension of [".png", path.extname(relative), ".tga", ".bmp"]) {
      try {
        const file = await this.contained(
          relative.replace(/\.[^.]+$/, extension),
        );
        if ((await stat(file)).size > 64 * 1024 * 1024)
          throw new Error("Image too large");
        return { file, bytes: await readFile(file) };
      } catch (error) {
        if ((error as NodeJS.ErrnoException).code !== "ENOENT") throw error;
      }
    }
    throw new Error(`Missing texture: ${relative}`);
  }
}
export const levelFile = (l: number) =>
  `level${String(l + 1).padStart(2, "0")}-info.xml`;
export const dayFile = (l: number, d: number) =>
  `level${String(l + 1).padStart(2, "0")}-day${String(d + 1).padStart(2, "0")}.xml`;
