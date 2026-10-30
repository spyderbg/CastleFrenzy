import test from "node:test";
import assert from "node:assert/strict";
import {
  mkdtemp,
  mkdir,
  writeFile,
  readFile,
  readdir,
  rm,
  symlink,
} from "node:fs/promises";
import os from "node:os";
import path from "node:path";
import { Workspace } from "../backend/workspace";
import { decodeImage, previewImage } from "../backend/image";
import { PNG } from "pngjs";
import { createServer } from "../backend/server";

test("legacy sprite image names resolve migrated PNGs, including filename case", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "auroramx-png-"));
  try {
    const workspace = new Workspace(root);
    await mkdir(path.join(root, "Sprites"));
    const png = path.join(root, "Sprites", "Atlas.PNG");
    await writeFile(png, "PNG fixture");
    await writeFile(path.join(root, "Sprites", "atlas.tga"), "legacy fixture");
    assert.equal(await workspace.imagePath("Sprites/old.sprite", "atlas.tga"), png);
    assert.equal(await workspace.imagePath("Sprites/old.sprite", "atlas.PNG"), png);
    await writeFile(path.join(root, "Sprites", "other.tga"), "import fixture");
    assert.equal(await workspace.imagePath("Sprites/old.sprite", "other.tga"),
      path.join(root, "Sprites", "other.tga"));
    await assert.rejects(workspace.imagePath("Sprites/old.sprite", "missing.tga"), /Image not found/);
    await assert.rejects(workspace.imagePath("Sprites/old.sprite", "../../escape.tga"), /outside the workspace/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("BMP previews preserve RGB row order, color keys and 32-bit alpha", () => {
  const header = Buffer.alloc(54);
  header.write("BM");
  header.writeUInt32LE(54, 10);
  header.writeUInt32LE(40, 14);
  header.writeInt32LE(2, 18);
  header.writeInt32LE(1, 22);
  header.writeUInt16LE(1, 26);
  header.writeUInt16LE(24, 28);
  const rgb = Buffer.concat([
    header,
    Buffer.from([255, 0, 255, 30, 20, 10, 0, 0]),
  ]);
  assert.deepEqual(
    [...PNG.sync.read(previewImage(rgb, 0xff00ff)).data],
    [255, 0, 255, 0, 10, 20, 30, 255],
  );
  header.writeUInt16LE(32, 28);
  const rgba = decodeImage(
    Buffer.concat([header, Buffer.from([3, 2, 1, 128, 6, 5, 4, 0])]),
  );
  assert.deepEqual([...rgba.data], [1, 2, 3, 128, 4, 5, 6, 0]);
});

test("source comments are saved and concurrent stale revisions cannot overwrite each other", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "auroramx-source-"));
  const workspace = new Workspace(root);
  try {
    const source =
      '{ VERSION 0001\nIMAGE 0 "a.png"\nMODULES { MD 0x1000 MD_IMAGE 0 0 0 2 2 }\nSPRITE_END }\n';
    await writeFile(path.join(root, "a.sprite"), source);
    const opened = await workspace.open("a.sprite");
    const raw = "// annotation in source editor\n" + source;
    const saved = await workspace.save(
      "a.sprite",
      opened.document,
      opened.revision,
      false,
      undefined,
      raw,
    );
    assert.equal(await readFile(path.join(root, "a.sprite"), "utf8"), raw);
    const first = structuredClone(saved.document),
      second = structuredClone(saved.document);
    first.modules[0].values[1] = 1;
    second.modules[0].values[1] = 2;
    const results = await Promise.allSettled([
      workspace.save("a.sprite", first, saved.revision),
      workspace.save("a.sprite", second, saved.revision),
    ]);
    assert.equal(results.filter((r) => r.status === "fulfilled").length, 1);
    assert.equal(results.filter((r) => r.status === "rejected").length, 1);
    assert.equal(
      (await workspace.open("a.sprite")).document.modules[0].values[1],
      1,
    );
    const copy = await workspace.save(
      "nested/b.sprite",
      first,
      "",
      true,
      "a.sprite",
    );
    assert.equal(copy.document.images[0].file, "../a.png");
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});

test("save preserves untouched source bytes, detects external edits, and backs up changed files", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "auroramx-save-"));
  const workspace = new Workspace(root);
  try {
    const source =
      '// original comments\r\n{ VERSION 0001\r\nIMAGE 0 "a.png"\r\nMODULES { MD 0x1000 MD_IMAGE 0 0 0 2 2 }\r\nSPRITE_END }\r\n';
    await writeFile(path.join(root, "a.sprite"), source);
    let opened = await workspace.open("a.sprite");
    await workspace.save("a.sprite", opened.document, opened.revision);
    assert.equal(await readFile(path.join(root, "a.sprite"), "utf8"), source);
    opened.document.modules[0].values[1] = 1;
    const saved = await workspace.save(
      "a.sprite",
      opened.document,
      opened.revision,
    );
    assert.equal(saved.document.modules[0].values[1], 1);
    const dates = await readdir(path.join(root, ".auroramx/backups"));
    assert.equal(
      await readFile(
        path.join(root, ".auroramx/backups", dates[0], "a.sprite"),
        "utf8",
      ),
      source,
    );
    await writeFile(path.join(root, "a.sprite"), source + "// external edit");
    await assert.rejects(
      workspace.save("a.sprite", saved.document, saved.revision),
      /changed on disk/,
    );
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
test("workspace refuses path traversal, symlink escapes and overwriting Save As", async () => {
  const root = await mkdtemp(path.join(os.tmpdir(), "auroramx-path-"));
  const workspace = new Workspace(root);
  try {
    await assert.rejects(workspace.file("../escape.sprite", false), /outside/);
    await symlink(os.tmpdir(), path.join(root, "outside"));
    await assert.rejects(workspace.file("outside/x.sprite", false), /outside/);
    await writeFile(
      path.join(root, "a.sprite"),
      "{ VERSION 0001 MODULES {} SPRITE_END }",
    );
    const opened = await workspace.open("a.sprite");
    await assert.rejects(
      workspace.save("a.sprite", opened.document, "", true),
      /overwrite/,
    );
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
test("TGA preview decodes row origin, alpha, RLE and transparent color without changing source pixels", () => {
  const header = Buffer.alloc(18);
  header[2] = 2;
  header.writeUInt16LE(2, 12);
  header.writeUInt16LE(1, 14);
  header[16] = 32;
  header[17] = 40;
  const tga = Buffer.concat([
    header,
    Buffer.from([255, 0, 255, 255, 30, 20, 10, 128]),
  ]);
  const before = Buffer.from(tga);
  const p = PNG.sync.read(previewImage(tga, 0xff00ff));
  assert.equal(p.data[3], 0);
  assert.deepEqual([...p.data.subarray(4, 8)], [10, 20, 30, 128]);
  assert.deepEqual(tga, before);
  const rle = Buffer.from(header);
  rle[2] = 10;
  const image = decodeImage(
    Buffer.concat([rle, Buffer.from([129, 3, 2, 1, 255])]),
  );
  assert.deepEqual([...image.data], [1, 2, 3, 255, 1, 2, 3, 255]);
});
test("HTTP API exposes working sprite parsing and requires a local session for writes", async () => {
  const app = createServer(new Workspace(path.resolve("../..")));
  try {
    const session = (await app.inject("/api/session")).json();
    assert.equal(session.app, "AuroraMX");
    const opened = (
      await app.inject("/api/sprite?path=Data/Sprites/Menu/menu_bg.sprite")
    ).json();
    assert.equal(opened.document.frames.length, 4);
    assert.equal(
      (
        await app.inject({
          method: "POST",
          url: "/api/parse",
          payload: { source: opened.source },
        })
      ).statusCode,
      403,
    );
    const parsed = await app.inject({
      method: "POST",
      url: "/api/parse",
      headers: { "x-auroramx-token": session.token },
      payload: { source: opened.source },
    });
    assert.equal(parsed.statusCode, 200);
    assert.equal(parsed.json().document.modules.length, 6);
    assert.equal(
      (
        await app.inject({
          url: "/api/session",
          headers: { host: "foreign.example" },
        })
      ).statusCode,
      403,
    );
    const image = await app.inject(
      "/api/image?sprite=Data/Sprites/Menu/menu_bg.sprite&file=.%5Cmenu_bg.tga",
    );
    assert.equal(image.statusCode, 200);
    assert.equal(image.headers["content-type"], "image/png");
  } finally {
    await app.close();
  }
});
