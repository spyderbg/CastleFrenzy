import { test } from "node:test";
import assert from "node:assert/strict";
import path from "node:path";
import os from "node:os";
import {
  mkdtemp,
  cp,
  mkdir,
  readFile,
  writeFile,
  rm,
  symlink,
} from "node:fs/promises";
import { Workspace, dayFile } from "../backend/workspace";
import { dayBytes, readDay, decode, readInfo, infoBytes } from "../backend/xml";
import {
  clone,
  newDay,
  newLevel,
  newWave,
  challenges,
  validate,
} from "../shared/level";
import { createServer } from "../backend/server";
const root = path.resolve("../..");
async function fixture(t: { after: (f: () => Promise<void>) => void }) {
  const temp = await mkdtemp(path.join(os.tmpdir(), "castle-level-test-"));
  t.after(() => rm(temp, { recursive: true, force: true }));
  await mkdir(path.join(temp, "Build/bin"), { recursive: true });
  await Promise.all(
    ["Levels", "config"].map((n) =>
      cp(path.join(root, "Build/bin", n), path.join(temp, "Build/bin", n), {
        recursive: true,
      }),
    ),
  );
  return new Workspace(temp);
}
test("reads every shipping day and model; UTF-8/UTF-16 round-trip agrees with runtime fields", async () => {
  const w = new Workspace(root),
    opened = await w.open();
  assert.equal(opened.project.levels.length, 11);
  assert.equal(opened.project.levels.flatMap((l) => l.days).length, 105);
  for (const level of opened.project.levels)
    for (const day of level.days) {
      assert.deepEqual(readDay(dayBytes(day)), day);
      const utf16 = Buffer.concat([
        Buffer.from([255, 254]),
        Buffer.from(decode(dayBytes(day)), "utf16le"),
      ]);
      assert.deepEqual(readDay(utf16), day);
    }
  assert.deepEqual(
    readInfo(infoBytes(opened.project)),
    opened.project.levels.map((l) => l.days.length),
  );
  assert(opened.units.some((u) => u.hidden));
  assert.equal(opened.models.length, 21);
  for (const m of opened.models) {
    const model = await w.model(opened.assets, m.name);
    assert.match(model.fbx, /^Data\/Scenes\/.+\.fbx$/i);
    assert.equal(
      (await w.modelFile(model.fbx)).subarray(0, 20).toString("latin1"),
      "Kaydara FBX Binary  ",
    );
  }
  await assert.rejects(w.modelFile("Data/Scenes/Goblin/goblin.bdae"), /FBX/);
  await assert.rejects(w.modelFile("Build/bin/Scenes/x.fbx"), /FBX/);
});
test("unchanged saves preserve original bytes; edits back up only changed XML and reject stale revisions", async (t) => {
  const w = await fixture(t),
    opened = await w.open();
  const before = await w.files(opened.assets),
    noChange = await w.save(opened.assets, opened.project, opened.revision);
  assert.deepEqual(noChange.changed, []);
  assert.equal(noChange.revision, opened.revision);
  assert.deepEqual(await w.files(opened.assets), before);
  const day = opened.project.levels[0].days[0];
  day.waves[0].name = 'Test & "name"';
  day.waves[0].variants[0].enemies[0].max = 5;
  const result = await w.save(opened.assets, opened.project, opened.revision);
  assert.deepEqual(result.changed, [dayFile(0, 0)]);
  assert(result.backup);
  assert.deepEqual(
    await readFile(path.join(w.root, result.backup!, dayFile(0, 0))),
    before.get(dayFile(0, 0)),
  );
  assert.deepEqual((await w.open()).project, opened.project);
  for (const [name, bytes] of await w.files(opened.assets))
    if (name !== dayFile(0, 0)) assert.deepEqual(bytes, before.get(name));
  await assert.rejects(
    w.save(opened.assets, opened.project, opened.revision),
    /changed on disk/,
  );
});
test("level/day insert, reorder, removal and copy preserve the indexed game project", async (t) => {
  const w = await fixture(t),
    opened = await w.open(),
    p = opened.project;
  p.levels.unshift(newLevel());
  p.levels[0].days.push(newDay(), clone(p.levels[1].days[0]));
  p.levels[0].days[0].waves.push(newWave(0));
  p.levels[1].days.reverse();
  let result = await w.save(opened.assets, p, opened.revision);
  assert.deepEqual((await w.open()).project, p);
  p.levels.splice(1, 2);
  p.levels[0].days.splice(0, 1);
  result = await w.save(opened.assets, p, result.revision);
  assert.deepEqual((await w.open()).project, p);
  assert(!(await w.files(opened.assets)).has("level12-info.xml"));
});
test("validates range, challenge and unit references before touching files", async (t) => {
  const w = await fixture(t),
    opened = await w.open(),
    p = clone(opened.project);
  p.levels[0].days[0].waves[0].repeatCount = 0;
  await assert.rejects(w.save(opened.assets, p, opened.revision), /integer/);
  assert.equal((await w.open()).revision, opened.revision);
  for (const challenge of challenges) {
    const p = clone(opened.project);
    p.levels[0].days[0].challenge = challenge;
    validate(p, opened.units);
  }
  p.levels[0].days[0].waves[0].repeatCount = 1;
  p.levels[0].days[0].waves[0].variants[0].enemies[0].type = "missing";
  assert.throws(() => validate(p, opened.units), /Unknown unit/);
});
test("API session/token, host, path traversal, stale revisions and unknown API protection", async (t) => {
  const w = await fixture(t),
    app = createServer(w);
  t.after(() => app.close());
  const session = (await app.inject("/api/session")).json(),
    opened = (await app.inject("/api/project")).json();
  assert.equal(opened.project.levels.length, 11);
  assert.equal(
    (
      await app.inject({
        method: "POST",
        url: "/api/save",
        payload: {
          assets: opened.assets,
          project: opened.project,
          revision: opened.revision,
        },
      })
    ).statusCode,
    403,
  );
  assert.equal(
    (
      await app.inject({
        method: "POST",
        url: "/api/save",
        headers: { "x-level-editor-token": session.token },
        payload: {
          assets: opened.assets,
          project: opened.project,
          revision: opened.revision,
        },
      })
    ).statusCode,
    200,
  );
  assert.equal(
    (
      await app.inject({
        url: "/api/project",
        headers: { host: "foreign.test" },
      })
    ).statusCode,
    403,
  );
  assert.equal(
    (
      await app.inject({
        url: "/api/session",
        headers: { "sec-fetch-site": "cross-site" },
      })
    ).statusCode,
    403,
  );
  assert.equal(
    (await app.inject("/api/texture?path=../../etc/passwd.png")).statusCode,
    400,
  );
  assert.equal((await app.inject("/api/missing")).statusCode, 404);
  const link = path.join(w.root, "escape");
  await symlink("/etc", link);
  await assert.rejects(w.contained("escape/passwd"), /outside/);
  await writeFile(
    path.join(w.root, "Build/bin/Levels/level01-day01.xml"),
    Buffer.from("external edit"),
  );
  const stale = await app.inject({
    method: "POST",
    url: "/api/save",
    headers: { "x-level-editor-token": session.token },
    payload: {
      assets: opened.assets,
      project: opened.project,
      revision: opened.revision,
    },
  });
  assert.equal(stale.statusCode, 409);
});
